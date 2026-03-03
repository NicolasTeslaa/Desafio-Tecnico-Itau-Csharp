import { useState } from "react";
import { scheduledApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import { addCompra } from "@/lib/local-history";
import type { ExecutarCompraRequest, ExecutarCompraResponse } from "@/lib/types";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Loader2, Zap } from "lucide-react";

export default function MotorPage() {
  const [dataRef, setDataRef] = useState("");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ExecutarCompraResponse | null>(null);

  async function handleExecutar(e: React.FormEvent) {
    e.preventDefault();
    if (!dataRef) {
      toast.error("Informe a data de referencia.");
      return;
    }

    setLoading(true);
    setResult(null);
    try {
      const payload: ExecutarCompraRequest = { dataReferencia: dataRef };
      const res = await scheduledApi.post<ExecutarCompraResponse>("/api/motor/executar-compra", payload);
      setResult(res);
      addCompra({ dataReferencia: dataRef, totalClientes: res.totalClientes, totalConsolidado: res.totalConsolidado });
      toast.success("Compra executada com sucesso!");
    } catch (err) {
      if (err instanceof ApiClientError && err.apiError) {
        toast.error(friendlyError(err.apiError.codigo), { description: err.apiError.codigo });
      } else {
        toast.error(err instanceof Error ? err.message : "Erro inesperado.");
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="max-w-5xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Motor de Compra</h1>

      <div className="metric-card">
        <form onSubmit={handleExecutar} className="flex flex-col sm:flex-row gap-3 items-end">
          <div>
            <label className="text-xs text-muted-foreground">Data de Referencia</label>
            <Input type="date" value={dataRef} onChange={(e) => setDataRef(e.target.value)} required />
          </div>
          <Button type="submit" disabled={loading}>
            {loading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Zap className="h-4 w-4 mr-2" />}
            Executar Compra Manual
          </Button>
        </form>
      </div>

      {result && (
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
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
              <table className="data-table">
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
                    <div className="flex justify-between text-sm mb-2">
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
