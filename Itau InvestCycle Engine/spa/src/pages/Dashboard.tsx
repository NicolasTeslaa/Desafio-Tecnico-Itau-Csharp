import { useEffect, useState } from "react";
import { pingScheduled, pingMarketData } from "@/lib/api-client";
import { getIngestoes, getCompras } from "@/lib/local-history";
import { CheckCircle2, XCircle, Loader2, ArrowRight } from "lucide-react";
import { Link } from "react-router-dom";
import { fmtDateBR } from "@/lib/dateUtil";

const STEPS = [
  { label: "Ingerir Cotações (COTAHIST)", to: "/cotacoes", done: false },
  { label: "Configurar Cesta Top Five", to: "/cesta", done: false },
  { label: "Cadastrar Clientes", to: "/clientes", done: false },
  { label: "Executar Compra", to: "/motor", done: false },
  { label: "Consultar Carteira", to: "/clientes", done: false },
];

export default function Dashboard() {
  const [scheduledOk, setScheduledOk] = useState<boolean | null>(null);
  const [marketOk, setMarketOk] = useState<boolean | null>(null);

  useEffect(() => {
    pingScheduled().then(setScheduledOk);
    pingMarketData().then(setMarketOk);
  }, []);

  const ingestoes = getIngestoes();
  const compras = getCompras();

  const StatusIcon = ({ ok }: { ok: boolean | null }) => {
    if (ok === null) return <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />;
    if (ok) return <CheckCircle2 className="h-5 w-5 text-primary" />;
    return <XCircle className="h-5 w-5 text-destructive" />;
  };

  return (
    <div className="max-w-4xl space-y-8">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground mt-1">Painel de Compra Programada — Top Five</p>
      </div>

      {/* Status */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="metric-card flex items-center gap-4">
          <StatusIcon ok={scheduledOk} />
          <div>
            <div className="metric-label">Motor / Clientes</div>
            <div className="text-sm font-medium">
              {scheduledOk === null ? "Verificando..." : scheduledOk ? "Online" : "Offline"}
            </div>
          </div>
        </div>
        <div className="metric-card flex items-center gap-4">
          <StatusIcon ok={marketOk} />
          <div>
            <div className="metric-label">Market Data</div>
            <div className="text-sm font-medium">
              {marketOk === null ? "Verificando..." : marketOk ? "Online" : "Offline"}
            </div>
          </div>
        </div>
      </div>

      {/* Checklist */}
      <div className="metric-card">
        <h2 className="font-semibold mb-4">Fluxo Recomendado</h2>
        <div className="space-y-3">
          {STEPS.map((step, i) => (
            <Link
              key={i}
              to={step.to}
              className="flex items-center gap-3 group text-sm hover:text-primary transition-colors"
            >
              <span className="h-6 w-6 rounded-full border flex items-center justify-center text-xs font-mono text-muted-foreground group-hover:border-primary group-hover:text-primary">
                {i + 1}
              </span>
              <span className="flex-1">{step.label}</span>
              <ArrowRight className="h-4 w-4 opacity-0 group-hover:opacity-100 transition-opacity" />
            </Link>
          ))}
        </div>
      </div>

      {/* Recent history */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="metric-card">
          <h3 className="font-semibold text-sm mb-3">Últimas Ingestões</h3>
          {ingestoes.length === 0 ? (
            <p className="text-xs text-muted-foreground">Nenhuma ingestão registrada.</p>
          ) : (
            <div className="space-y-2 text-xs">
              {ingestoes.slice(0, 5).map((r, i) => (
                <div key={i} className="flex justify-between">
                  <span className="font-mono">{r.file}</span>
                  <span className="text-muted-foreground">{r.saved} registros</span>
                </div>
              ))}
            </div>
          )}
        </div>
        <div className="metric-card">
          <h3 className="font-semibold text-sm mb-3">Últimas Compras</h3>
          {compras.length === 0 ? (
            <p className="text-xs text-muted-foreground">Nenhuma compra registrada.</p>
          ) : (
            <div className="space-y-2 text-xs">
              {compras.slice(0, 5).map((r, i) => (
                <div key={i} className="flex justify-between">
                  <span className="font-mono">{fmtDateBR(r.dataReferencia)}</span>
                  <span className="text-muted-foreground">{r.totalClientes} clientes</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}