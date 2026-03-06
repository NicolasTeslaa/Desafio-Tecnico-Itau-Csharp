import { useState } from "react";
import { scheduledApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import type { ExecutarCompraRequest, ExecutarCompraResponse } from "@/lib/types";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Loader2, Zap } from "lucide-react";

function isBusinessDay(date: Date): boolean {
  const day = date.getDay();
  return day !== 0 && day !== 6;
}

function resolveRunDate(year: number, monthZeroBased: number, day: number): Date {
  const date = new Date(year, monthZeroBased, day);
  while (!isBusinessDay(date)) {
    date.setDate(date.getDate() + 1);
  }
  return date;
}

function formatDateIso(date: Date): string {
  const yyyy = date.getFullYear();
  const mm = String(date.getMonth() + 1).padStart(2, "0");
  const dd = String(date.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

function formatDateBr(date: Date): string {
  return date.toLocaleDateString("pt-BR");
}

function getValidRunDatesHint(referenceIso?: string): string {
  const base = referenceIso ? new Date(`${referenceIso}T00:00:00`) : new Date();
  if (Number.isNaN(base.getTime())) {
    return "Use uma data de referencia no formato AAAA-MM-DD.";
  }

  const year = base.getFullYear();
  const month = base.getMonth();
  const d5 = resolveRunDate(year, month, 5);
  const d15 = resolveRunDate(year, month, 15);
  const d25 = resolveRunDate(year, month, 25);

  return `Datas validas neste mes: (${formatDateBr(d5)}, ${formatDateBr(d15)}, ${formatDateBr(d25)}).`;
}

function getRunDateValidation(referenceIso?: string): { valid: boolean; reason?: string } {
  if (!referenceIso) {
    return { valid: false, reason: "Selecione uma data de referencia." };
  }

  const base = new Date(`${referenceIso}T00:00:00`);
  if (Number.isNaN(base.getTime())) {
    return { valid: false, reason: "Data invalida. Use o formato AAAA-MM-DD." };
  }

  const year = base.getFullYear();
  const month = base.getMonth();
  const validDates = [
    formatDateIso(resolveRunDate(year, month, 5)),
    formatDateIso(resolveRunDate(year, month, 15)),
    formatDateIso(resolveRunDate(year, month, 25)),
  ];

  if (!validDates.includes(referenceIso)) {
    return { valid: false, reason: "Data fora da janela de execucao do motor (5, 15, 25 ou proximo dia util)." };
  }

  return { valid: true };
}

export default function MotorPage() {
  const [dataRef, setDataRef] = useState("");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ExecutarCompraResponse | null>(null);
  const dateValidation = getRunDateValidation(dataRef);

  async function handleExecutar(e: React.FormEvent) {
    e.preventDefault();
    if (!dataRef) {
      toast.error("Informe a data de referencia.");
      return;
    }
    if (!dateValidation.valid) {
      toast.error(dateValidation.reason ?? "Data de referencia invalida.");
      return;
    }

    setLoading(true);
    setResult(null);
    try {
      const payload: ExecutarCompraRequest = { dataReferencia: dataRef };
      const res = await scheduledApi.post<ExecutarCompraResponse>("/api/motor/executar-compra", payload);
      setResult(res);
      toast.success("Compra executada com sucesso!");
    } catch (err) {
      if (err instanceof ApiClientError && err.apiError) {
        if (err.apiError.codigo === "DATA_EXECUCAO_INVALIDA") {
          const hint = getValidRunDatesHint(dataRef);
          toast.error(friendlyError(err.apiError.codigo), {
            description: `${err.apiError.erro}\n${hint}`,
          });
        } else {
          toast.error(friendlyError(err.apiError.codigo), { description: err.apiError.codigo });
        }
      } else {
        toast.error(err instanceof Error ? err.message : "Erro inesperado.");
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="mx-auto w-full max-w-5xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Motor de Compra</h1>

      <div className="metric-card">
        <form onSubmit={handleExecutar} className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-end">
          <div>
            <label className="text-xs text-muted-foreground">Data de Referencia</label>
            <Input type="date" value={dataRef} onChange={(e) => setDataRef(e.target.value)} required />
            <p className="mt-2 text-xs text-muted-foreground">{getValidRunDatesHint(dataRef)}</p>
            {!dateValidation.valid && (
              <p className="mt-1 text-xs text-red-600">{dateValidation.reason}</p>
            )}
          </div>
          <Button type="submit" disabled={loading || !dateValidation.valid}>
            {loading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Zap className="h-4 w-4 mr-2" />}
            Executar Compra Manual
          </Button>
        </form>
      </div>

      {result && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="metric-card">
              <div className="metric-label">Total Clientes</div>
              <div className="metric-value">{result.totalClientes}</div>
            </div>
            <div className="metric-card">
              <div className="metric-label">Total Consolidado</div>
              <div className="metric-value">R$ {result.totalConsolidado.toLocaleString("pt-BR", { minimumFractionDigits: 2 })}</div>
            </div>
          </div>

          {result.ordensCompra && result.ordensCompra.length > 0 && (
            <div className="metric-card overflow-x-auto">
              <h3 className="font-semibold mb-3">Ordens de Compra Consolidadas</h3>
              <table className="data-table min-w-[760px]">
                <thead>
                  <tr><th>Ticker</th><th>Qtd Total</th><th>Preco</th><th>Valor</th><th>Detalhes</th></tr>
                </thead>
                <tbody>
                  {result.ordensCompra.map((o, i) => (
                    <tr key={i}>
                      <td className="font-mono font-medium">{o.ticker}</td>
                      <td>{o.quantidadeTotal}</td>
                      <td>R$ {o.precoUnitario?.toFixed(2)}</td>
                      <td>R$ {o.valorTotal?.toFixed(2)}</td>
                      <td className="text-xs">
                        {o.detalhes?.map((d, j) => (
                          <span key={j} className="status-badge-success mr-1">{d.tipo} {d.ticker}: {d.quantidade}</span>
                        ))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {result.distribuicoes && result.distribuicoes.length > 0 && (
            <div className="metric-card">
              <h3 className="font-semibold mb-3">Distribuicoes por Cliente</h3>
              <div className="space-y-3">
                {result.distribuicoes.map((d, i) => (
                  <div key={i} className="bg-muted rounded-lg p-3">
                    <div className="flex flex-col sm:flex-row sm:justify-between text-sm mb-2 gap-1">
                      <span className="font-mono">{d.nome} (#{d.clienteId})</span>
                      <span>Aporte: R$ {d.valorAporte?.toFixed(2)}</span>
                    </div>
                    <div className="flex flex-wrap gap-1">
                      {d.ativos?.map((a, j) => (
                        <span key={j} className="text-xs bg-card px-2 py-1 rounded font-mono">
                          {a.ticker} {a.quantidade}x
                        </span>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {result.residuosCustMaster && result.residuosCustMaster.length > 0 && (
            <div className="metric-card">
              <h3 className="font-semibold mb-3">Residuos Cust Master</h3>
              <div className="flex flex-wrap gap-2">
                {result.residuosCustMaster.map((r, i) => (
                  <span key={i} className="status-badge-warning font-mono">{r.ticker}: {r.quantidade}</span>
                ))}
              </div>
            </div>
          )}

          <div className="metric-card">
            <h3 className="font-semibold mb-3">Eventos IR Publicados</h3>
            <div className="text-sm">
              Total de eventos: <span className="font-semibold">{result.eventosIrPublicados ?? 0}</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

