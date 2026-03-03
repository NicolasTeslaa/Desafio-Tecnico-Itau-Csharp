import { useState, useCallback } from "react";
import { marketDataApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import { addIngestao } from "@/lib/local-history";
import type { Cotacao, IngestResponse, PagedResponse } from "@/lib/types";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Loader2, Upload, Search, X } from "lucide-react";

export default function CotacoesPage() {
  const [tab, setTab] = useState<"ingestao" | "consulta">("ingestao");

  return (
    <div className="max-w-5xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Cotações</h1>
      <div className="flex gap-2">
        <button
          onClick={() => setTab("ingestao")}
          className={`px-4 py-2 text-sm rounded-lg transition-colors ${tab === "ingestao" ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground hover:text-foreground"
            }`}
        >
          Ingestão COTAHIST
        </button>
        <button
          onClick={() => setTab("consulta")}
          className={`px-4 py-2 text-sm rounded-lg transition-colors ${tab === "consulta" ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground hover:text-foreground"
            }`}
        >
          Consulta
        </button>
      </div>

      {tab === "ingestao" ? <IngestaoTab /> : <ConsultaTab />}
    </div>
  );
}

function IngestaoTab() {
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<IngestResponse | null>(null);

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
      toast.error("File is empty.");
      return;
    }

    const fd = new FormData();
    fd.append("file", file);
    setLoading(true);
    setResult(null);

    try {
      const res = await marketDataApi.upload<IngestResponse>("/api/cotacoes/ingest", fd);
      setResult(res);
      addIngestao({ file: res.file, saved: res.saved });
      toast.success(`Ingestão concluída: ${res.saved} registros salvos.`);
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="metric-card space-y-4">
      <h2 className="font-semibold">Upload COTAHIST</h2>
      <form onSubmit={handleUpload} className="flex flex-col sm:flex-row gap-3">
        <Input type="file" name="file" accept=".txt,.zip,.TXT,.ZIP" className="flex-1" />
        <Button type="submit" disabled={loading}>
          {loading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Upload className="h-4 w-4 mr-2" />}
          Ingerir
        </Button>
      </form>
      {result && (
        <div className="bg-muted rounded-lg p-4 text-sm space-y-1">
          <div><span className="text-muted-foreground">Arquivo:</span> <span className="font-mono">{result.file}</span></div>
          <div><span className="text-muted-foreground">Registros salvos:</span> <span className="font-semibold">{result.saved}</span></div>
        </div>
      )}
    </div>
  );
}
function fmtDateBR(value?: string) {
  if (!value) return "—";

  // Aceita: "2025-12-30", "2025-12-30T00:00:00", "2025-12-30T00:00:00Z"
  const isoDateOnly = value.length >= 10 ? value.slice(0, 10) : value;

  // Força interpretação como UTC pra não “voltar 1 dia” por timezone
  const d = new Date(`${isoDateOnly}T00:00:00Z`);
  if (Number.isNaN(d.getTime())) return value;

  return d.toLocaleDateString("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function fmtDateTimeBR(value?: string) {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;

  return d.toLocaleString("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
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

  const search = useCallback(async (p = 1) => {
    setLoading(true);
    setDetail(null);
    const params = new URLSearchParams();
    params.set("page", String(p));
    params.set("pageSize", String(pageSize));
    if (ticker) params.set("ticker", ticker.toUpperCase());
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
  }, [ticker, dataPregao, pageSize]);

  async function loadDetail(id: string) {
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

  return (
    <div className="space-y-4">
      <div className="metric-card">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="text-xs text-muted-foreground">Ticker</label>
            <Input value={ticker} onChange={(e) => setTicker(e.target.value)} placeholder="PETR4" className="w-32" />
          </div>
          <div>
            <label className="text-xs text-muted-foreground">Data Pregão</label>
            <Input type="date" value={dataPregao} onChange={(e) => setDataPregao(e.target.value)} className="w-40" />
          </div>
          <div>
            <label className="text-xs text-muted-foreground">Por página</label>
            <select
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
              className="h-9 rounded-md border bg-card px-3 text-sm"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>
          <Button onClick={() => search(1)} disabled={loading}>
            {loading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Search className="h-4 w-4 mr-2" />}
            Buscar
          </Button>
        </div>
      </div>

      {data && (
        <div className="metric-card overflow-x-auto">
          <table className="data-table">
            <thead>
              <tr>
                <th>Ticker</th>
                <th>Data Pregão</th>
                <th>Abertura</th>
                <th>Fechamento</th>
                <th>Volume</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 ? (
                <tr><td colSpan={6} className="text-center text-muted-foreground py-8">Nenhuma cotação encontrada.</td></tr>
              ) : (
                data.items.map((c) => (
                  <tr key={c.id}>
                    <td className="font-mono font-medium">{c.ticker}</td>
                    <td>{fmtDateBR(c.dataPregao)}</td>
                    <td>{fmt(c.precoAbertura)}</td>
                    <td>{fmt(c.precoFechamento)}</td>
                    <td>{c.volume?.toLocaleString("pt-BR")}</td>
                    <td>
                      <button onClick={() => loadDetail(c.id)} className="text-xs text-primary hover:underline">
                        Detalhar
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="flex items-center justify-between mt-4 text-sm">
            <span className="text-muted-foreground">{data.totalItems} registros</span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => search(page - 1)}>Anterior</Button>
              <span className="flex items-center px-2 text-muted-foreground">
                {page} / {data.totalPages || 1}
              </span>
              <Button variant="outline" size="sm" disabled={page >= (data.totalPages || 1)} onClick={() => search(page + 1)}>Próxima</Button>
            </div>
          </div>
        </div>
      )}

      {/* Detail modal */}
      {detail && (
        <div className="metric-card relative">
          <button onClick={() => setDetail(null)} className="absolute top-3 right-3 text-muted-foreground hover:text-foreground">
            <X className="h-4 w-4" />
          </button>
          <h3 className="font-semibold mb-3">Detalhes — {detail.ticker}</h3>
          {detailLoading ? <Loader2 className="h-5 w-5 animate-spin" /> : (
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 text-sm">
              <Field label="Data Pregão" value={fmtDateBR(detail.dataPregao)} />
              <Field label="Abertura" value={fmt(detail.precoAbertura)} />
              <Field label="Máximo" value={fmt(detail.precoMaximo)} />
              <Field label="Mínimo" value={fmt(detail.precoMinimo)} />
              <Field label="Fechamento" value={fmt(detail.precoFechamento)} />
              <Field label="Médio" value={fmt(detail.precoMedio)} />
              <Field label="Volume" value={detail.volume?.toLocaleString("pt-BR")} />
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Field({ label, value }: { label: string; value: string | undefined }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="font-mono">{value ?? "—"}</div>
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
