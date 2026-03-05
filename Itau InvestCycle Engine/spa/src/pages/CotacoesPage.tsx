import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { marketDataApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import type { Cotacao, IngestJobStatusResponse, IngestOverviewResponse, IngestStartResponse, PagedResponse } from "@/lib/types";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import {
  Loader2,
  Upload,
  Search,
  X,
  FileText,
  Database,
  Filter,
  RefreshCcw,
} from "lucide-react";
import { fmtDateBR, fmtDateTimeBR } from "@/lib/dateUtil";

export default function CotacoesPage() {
  const [tab, setTab] = useState<"ingestao" | "consulta">("ingestao");

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold tracking-tight">Cotações</h1>
          <p className="text-sm text-muted-foreground">
            Ingestão COTAHIST e consulta paginada de cotações no banco.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <TabButton
            active={tab === "ingestao"}
            onClick={() => setTab("ingestao")}
            icon={<Upload className="h-4 w-4" />}
            label="Ingestão"
          />
          <TabButton
            active={tab === "consulta"}
            onClick={() => setTab("consulta")}
            icon={<Search className="h-4 w-4" />}
            label="Consulta"
          />
        </div>
      </div>

      {/* Content */}
      {tab === "ingestao" ? <IngestaoTab /> : <ConsultaTab />}
    </div>
  );
}

function TabButton({
  active,
  onClick,
  icon,
  label,
}: {
  active: boolean;
  onClick: () => void;
  icon: React.ReactNode;
  label: string;
}) {
  return (
    <button
      onClick={onClick}
      className={[
        "inline-flex items-center gap-2 rounded-xl px-3 sm:px-4 py-2 text-sm transition-colors",
        "border bg-card hover:bg-accent hover:text-accent-foreground",
        active
          ? "border-primary/30 bg-primary text-primary-foreground hover:bg-primary"
          : "text-muted-foreground",
      ].join(" ")}
    >
      {icon}
      {label}
    </button>
  );
}

function Card({
  title,
  subtitle,
  icon,
  right,
  children,
  className,
}: {
  title: string;
  subtitle?: string;
  icon?: React.ReactNode;
  right?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={[
        "rounded-2xl border bg-card shadow-sm",
        "p-5 sm:p-6",
        className ?? "",
      ].join(" ")}
    >
      <div className="mb-4 flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          {icon ? (
            <div className="mt-0.5 rounded-xl border bg-muted p-2 text-muted-foreground">
              {icon}
            </div>
          ) : null}
          <div className="space-y-1">
            <div className="text-base font-semibold leading-tight">{title}</div>
            {subtitle ? (
              <div className="text-sm text-muted-foreground">{subtitle}</div>
            ) : null}
          </div>
        </div>
        {right}
      </div>

      {children}
    </div>
  );
}

function Badge({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center rounded-full border bg-muted px-2.5 py-1 text-xs text-muted-foreground">
      {children}
    </span>
  );
}

function IngestaoTab() {
  const [loading, setLoading] = useState(false);
  const [loadingOverview, setLoadingOverview] = useState(true);
  const [job, setJob] = useState<IngestJobStatusResponse | null>(null);
  const [fileName, setFileName] = useState<string>("");
  const pollRef = useRef<number | null>(null);

  function stopPolling() {
    if (pollRef.current != null) {
      window.clearInterval(pollRef.current);
      pollRef.current = null;
    }
  }

  async function fetchStatus(jobId: string): Promise<"QUEUED" | "PROCESSING" | "COMPLETED" | "FAILED"> {
    const status = await marketDataApi.get<IngestJobStatusResponse>(`/api/cotacoes/ingest/${jobId}`);
    setJob(status);

    if (status.status === "COMPLETED") {
      stopPolling();
      toast.success(`Importacao concluida: ${status.saved} registros salvos.`);
    }

    if (status.status === "FAILED") {
      stopPolling();
      toast.error("Falha na importacao.", { description: status.error ?? "Erro desconhecido." });
    }

    return status.status;
  }

  function startPolling(jobId: string) {
    stopPolling();
    pollRef.current = window.setInterval(() => {
      fetchStatus(jobId).catch((err) => {
        stopPolling();
        handleApiError(err);
      });
    }, 2000);
  }

  async function loadOverview() {
    try {
      const overview = await marketDataApi.get<IngestOverviewResponse>("/api/cotacoes/ingest/overview");
      if (!overview.lastJob) {
        return;
      }

      setJob(overview.lastJob);
      if (overview.lastJob.status === "QUEUED" || overview.lastJob.status === "PROCESSING") {
        startPolling(overview.lastJob.jobId);
      }
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoadingOverview(false);
    }
  }

  useEffect(() => {
    void loadOverview();
    return () => stopPolling();
  }, []);

  async function handleUpload(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();

    const form = e.currentTarget;
    const fileInput = form.querySelector('input[type="file"]') as HTMLInputElement;
    const file = fileInput?.files?.[0];

    if (!file) {
      toast.error("Selecione um arquivo COTAHIST.");
      return;
    }
    if (file.size === 0) {
      toast.error("Arquivo vazio.");
      return;
    }

    const fd = new FormData();
    fd.append("file", file);
    setLoading(true);
    setJob(null);

    try {
      const res = await marketDataApi.upload<IngestStartResponse>("/api/cotacoes/ingest", fd);
      const initial: IngestJobStatusResponse = {
        jobId: res.jobId,
        file: res.file,
        status: res.status,
        createdAtUtc: res.createdAtUtc,
        startedAtUtc: null,
        finishedAtUtc: null,
        saved: 0,
        error: null,
      };
      setJob(initial);
      toast.success("Arquivo recebido. Processamento em background iniciado.");
      const currentStatus = await fetchStatus(res.jobId);
      if (currentStatus === "QUEUED" || currentStatus === "PROCESSING") {
        startPolling(res.jobId);
      }
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <Card
      title="Ingestao COTAHIST"
      subtitle="Envie um .TXT ou .ZIP (COTAHIST). O processamento ocorre em background."
      icon={<FileText className="h-5 w-5" />}
      right={
        loadingOverview ? (
          <Badge>Carregando status...</Badge>
        ) : job?.status === "QUEUED" || job?.status === "PROCESSING" ? (
          <Badge>Job em processamento</Badge>
        ) : job?.status === "COMPLETED" ? (
          <Badge>Ultima execucao finalizada</Badge>
        ) : job?.status === "FAILED" ? (
          <Badge>Ultima execucao falhou</Badge>
        ) : (
          <Badge>Pronto</Badge>
        )
      }
    >
      <form onSubmit={handleUpload} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
          <div className="space-y-1.5">
            <label className="text-xs text-muted-foreground">Arquivo</label>
            <Input
              type="file"
              name="file"
              accept=".txt,.zip,.TXT,.ZIP"
              className="h-10"
              onChange={(e) => setFileName(e.target.files?.[0]?.name ?? "")}
            />
            <div className="text-xs text-muted-foreground">
              {fileName ? (
                <span>
                  Selecionado: <span className="font-mono">{fileName}</span>
                </span>
              ) : (
                <span>Nenhum arquivo selecionado.</span>
              )}
            </div>
          </div>

          <Button type="submit" disabled={loading} className="h-10">
            {loading ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Ingerir
          </Button>
        </div>

        {loading ? (
          <div className="rounded-xl border bg-muted/40 p-4 text-sm text-muted-foreground">
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              Enviando arquivo...
            </div>
          </div>
        ) : null}

        {job ? (
          <div className="rounded-xl border bg-muted/30 p-4 text-sm">
            <div className="grid gap-2 sm:grid-cols-5">
              <div className="space-y-1">
                <div className="text-xs text-muted-foreground">Arquivo</div>
                <div className="font-mono">{job.file}</div>
              </div>
              <div className="space-y-1">
                <div className="text-xs text-muted-foreground">Status</div>
                <div className="font-semibold">{job.status}</div>
              </div>
              <div className="space-y-1">
                <div className="text-xs text-muted-foreground">Registros salvos</div>
                <div className="text-lg font-semibold">{job.saved.toLocaleString("pt-BR")}</div>
              </div>
              <div className="space-y-1">
                <div className="text-xs text-muted-foreground">Inicio</div>
                <div>{fmtDateTimeBR(job.startedAtUtc)}</div>
              </div>
              <div className="space-y-1">
                <div className="text-xs text-muted-foreground">Fim</div>
                <div>{fmtDateTimeBR(job.finishedAtUtc)}</div>
              </div>
            </div>
            {job.error ? (
              <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/5 p-2 text-xs text-destructive">
                {job.error}
              </div>
            ) : null}
          </div>
        ) : (
          <div className="rounded-xl border bg-muted/20 p-4 text-sm text-muted-foreground">
            Dica: o upload retorna rapido e a importacao continua em background.
          </div>
        )}
      </form>
    </Card>
  );
}
function ConsultaTab() {
  const [ticker, setTicker] = useState("");
  const [dataPregao, setDataPregao] = useState("");
  const [pageSize, setPageSize] = useState(20);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResponse<Cotacao> | null>(null);
  const [loading, setLoading] = useState(false);

  const [detail, setDetail] = useState<Cotacao | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const tickerUpper = useMemo(() => ticker.trim().toUpperCase(), [ticker]);

  const search = useCallback(
    async (p = 1) => {
      setLoading(true);
      setDetail(null);

      const params = new URLSearchParams();
      params.set("page", String(p));
      params.set("pageSize", String(pageSize));
      if (tickerUpper) params.set("ticker", tickerUpper);
      if (dataPregao) params.set("dataPregao", dataPregao);

      try {
        const res = await marketDataApi.get<PagedResponse<Cotacao>>(`/api/cotacoes?${params}`);
        setData(res);
        setPage(p);
      } catch (err) {
        handleApiError(err);
      } finally {
        setLoading(false);
      }
    },
    [tickerUpper, dataPregao, pageSize]
  );

  async function loadDetail(id: number) {
    setDetailLoading(true);
    try {
      const res = await marketDataApi.get<Cotacao>(`/api/cotacoes/${id}`);
      setDetail(res);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 404) {
        toast.error("Cotação não encontrada.");
      } else {
        handleApiError(err);
      }
    } finally {
      setDetailLoading(false);
    }
  }

  function clearFilters() {
    setTicker("");
    setDataPregao("");
    setData(null);
    setPage(1);
    setDetail(null);
  }

  // Enter para buscar
  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Enter") {
      e.preventDefault();
      search(1);
    }
  }

  return (
    <div className="space-y-4">
      <Card
        title="Consulta"
        subtitle="Filtre por ticker e/ou data do pregão. Resultados paginados."
        icon={<Database className="h-5 w-5" />}
        right={
          <div className="flex items-center gap-2">
            <Badge>
              <Filter className="mr-1 h-3.5 w-3.5" />
              Filtros
            </Badge>
          </div>
        }
      >
        <div className="grid gap-3 lg:grid-cols-[auto_auto_auto_1fr_auto_auto] lg:items-end">
          <div className="space-y-1.5" onKeyDown={onKeyDown}>
            <label className="text-xs text-muted-foreground">Ticker</label>
            <Input
              value={ticker}
              onChange={(e) => setTicker(e.target.value)}
              placeholder="PETR4"
              className="h-10 w-full lg:w-36"
            />
          </div>

          <div className="space-y-1.5" onKeyDown={onKeyDown}>
            <label className="text-xs text-muted-foreground">Data Pregão</label>
            <Input
              type="date"
              value={dataPregao}
              onChange={(e) => setDataPregao(e.target.value)}
              className="h-10 w-full lg:w-44"
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-xs text-muted-foreground">Por página</label>
            <select
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
              className="h-10 w-full rounded-md border bg-card px-3 text-sm lg:w-36"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>

          <div className="hidden lg:block" />

          <Button onClick={() => search(1)} disabled={loading} className="h-10 w-full lg:w-auto">
            {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Search className="mr-2 h-4 w-4" />}
            Buscar
          </Button>

          <Button
            variant="outline"
            onClick={clearFilters}
            disabled={loading && !data}
            className="h-10 w-full lg:w-auto"
            title="Limpar filtros"
          >
            <RefreshCcw className="mr-2 h-4 w-4" />
            Limpar
          </Button>
        </div>
      </Card>

      {/* Result */}
      <ResultsCard
        data={data}
        page={page}
        loading={loading}
        onPrev={() => search(page - 1)}
        onNext={() => search(page + 1)}
        onDetail={loadDetail}
      />

      {/* Modal */}
      <DetailModal
        open={!!detail}
        onClose={() => setDetail(null)}
        title={detail ? `Detalhes — ${detail.ticker}` : "Detalhes"}
      >
        {detailLoading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Carregando detalhes...
          </div>
        ) : detail ? (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 text-sm">
            <Field label="Ticker" value={detail.ticker} mono />
            <Field label="Data Pregão" value={fmtDateBR(detail.dataPregao)} />
            <Field label="Abertura" value={fmt(detail.precoAbertura)} />
            <Field label="Máximo" value={fmt(detail.precoMaximo)} />
            <Field label="Mínimo" value={fmt(detail.precoMinimo)} />
            <Field label="Fechamento" value={fmt(detail.precoFechamento)} />
            <Field label="Médio" value={fmt(detail.precoMedio)} />
            <Field label="Volume" value={detail.volume?.toLocaleString("pt-BR") ?? "—"} />
            <Field label="Atualizado em" value={fmtDateBR((detail as any).updatedAtUtc ?? (detail as any).updatedAt)} />
          </div>
        ) : null}
      </DetailModal>
    </div>
  );
}

function ResultsCard({
  data,
  page,
  loading,
  onPrev,
  onNext,
  onDetail,
}: {
  data: PagedResponse<Cotacao> | null;
  page: number;
  loading: boolean;
  onPrev: () => void;
  onNext: () => void;
  onDetail: (id: number) => void;
}) {
  const totalPages = data
    ? Math.max(
        1,
        data.totalPages ?? Math.ceil(data.totalItems / Math.max(1, data.pageSize || 1))
      )
    : 1;

  // Estado inicial (sem busca)
  if (!data && !loading) {
    return (
      <div className="rounded-2xl border bg-card p-8 text-center text-sm text-muted-foreground">
        Faça uma busca para listar as cotações.
      </div>
    );
  }

  return (
    <div className="rounded-2xl border bg-card shadow-sm">
      <div className="flex items-center justify-between gap-4 border-b p-4 sm:p-5">
        <div className="flex items-center gap-2 text-sm">
          <span className="font-semibold">Resultados</span>
          {data ? <Badge>{data.totalItems.toLocaleString("pt-BR")} registros</Badge> : null}
        </div>
        {loading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Carregando...
          </div>
        ) : null}
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-[820px] w-full text-sm">
          <thead className="sticky top-0 bg-card">
            <tr className="border-b">
              <Th>Ticker</Th>
              <Th>Data Pregão</Th>
              <Th className="text-right">Abertura</Th>
              <Th className="text-right">Fechamento</Th>
              <Th className="text-right">Volume</Th>
              <Th className="text-right">Ações</Th>
            </tr>
          </thead>

          <tbody>
            {loading && !data ? (
              <SkeletonRows />
            ) : data && data.items.length === 0 ? (
              <tr>
                <td colSpan={6} className="py-10 text-center text-muted-foreground">
                  Nenhuma cotação encontrada.
                </td>
              </tr>
            ) : (
              data?.items.map((c) => (
                <tr key={c.id} className="border-b last:border-b-0 hover:bg-muted/30">
                  <td className="px-4 py-3">
                    <span className="font-mono font-medium">{c.ticker}</span>
                  </td>
                  <td className="px-4 py-3">{fmtDateBR(c.dataPregao)}</td>
                  <td className="px-4 py-3 text-right font-mono">{fmt(c.precoAbertura)}</td>
                  <td className="px-4 py-3 text-right font-mono">{fmt(c.precoFechamento)}</td>
                  <td className="px-4 py-3 text-right">{c.volume?.toLocaleString("pt-BR") ?? "—"}</td>
                  <td className="px-4 py-3 text-right">
                    <Button variant="outline" size="sm" onClick={() => onDetail(c.id)}>
                      Detalhar
                    </Button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex flex-col gap-3 border-t p-4 text-sm sm:flex-row sm:items-center sm:justify-between">
        <div className="text-muted-foreground">
          Página <span className="font-medium text-foreground">{page}</span>
          {data ? (
            <>
              {" "}
              de <span className="font-medium text-foreground">{totalPages}</span>
            </>
          ) : null}
        </div>

        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" disabled={!data || page <= 1 || loading} onClick={onPrev}>
            Anterior
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!data || page >= totalPages || loading}
            onClick={onNext}
          >
            Próxima
          </Button>
        </div>
      </div>
    </div>
  );
}

function Th({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <th
      className={[
        "px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground",
        className ?? "",
      ].join(" ")}
    >
      {children}
    </th>
  );
}

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 8 }).map((_, i) => (
        <tr key={i} className="border-b last:border-b-0">
          <td className="px-4 py-3">
            <div className="h-4 w-20 animate-pulse rounded bg-muted" />
          </td>
          <td className="px-4 py-3">
            <div className="h-4 w-28 animate-pulse rounded bg-muted" />
          </td>
          <td className="px-4 py-3 text-right">
            <div className="ml-auto h-4 w-24 animate-pulse rounded bg-muted" />
          </td>
          <td className="px-4 py-3 text-right">
            <div className="ml-auto h-4 w-24 animate-pulse rounded bg-muted" />
          </td>
          <td className="px-4 py-3 text-right">
            <div className="ml-auto h-4 w-28 animate-pulse rounded bg-muted" />
          </td>
          <td className="px-4 py-3 text-right">
            <div className="ml-auto h-8 w-24 animate-pulse rounded bg-muted" />
          </td>
        </tr>
      ))}
    </>
  );
}

function DetailModal({
  open,
  onClose,
  title,
  children,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
}) {
  // fecha no ESC
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-[2px]"
        onClick={onClose}
        aria-hidden="true"
      />
      <div className="absolute inset-0 flex items-center justify-center p-4">
        <div className="w-full max-w-2xl rounded-2xl border bg-card shadow-lg max-h-[85vh] overflow-hidden">
          <div className="flex items-center justify-between border-b p-4 sm:p-5">
            <div className="text-sm font-semibold">{title}</div>
            <button
              onClick={onClose}
              className="rounded-lg p-2 text-muted-foreground hover:bg-muted hover:text-foreground"
              aria-label="Fechar"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="p-4 sm:p-5 overflow-auto">{children}</div>
        </div>
      </div>
    </div>
  );
}

function Field({
  label,
  value,
  mono,
}: {
  label: string;
  value: string | undefined;
  mono?: boolean;
}) {
  return (
    <div className="rounded-xl border bg-muted/20 p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={mono ? "font-mono" : ""}>{value ?? "—"}</div>
    </div>
  );
}

function fmt(n?: number) {
  if (n == null) return "—";
  return n.toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function handleApiError(err: unknown) {
  if (err instanceof ApiClientError) {
    const msg = err.apiError ? `${friendlyError(err.apiError.codigo)}` : err.message;
    const desc = err.apiError ? err.apiError.codigo : undefined;
    toast.error(msg, { description: desc });
  } else {
    toast.error("Erro inesperado.");
  }
}





