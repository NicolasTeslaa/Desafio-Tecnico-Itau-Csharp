import { useState, useEffect } from "react";
import { scheduledApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import type { Cesta, CriarCestaRequest, HistoricoCestasResponse } from "@/lib/types";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Loader2, Plus, History, Eye } from "lucide-react";
import { fmtDateBR } from "@/lib/dateUtil";

export default function CestaPage() {
  const [tab, setTab] = useState<"atual" | "historico" | "cadastrar">("atual");

  return (
    <div className="max-w-4xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Cesta Top Five</h1>
      <div className="flex gap-2 flex-wrap">
        {[
          { key: "atual" as const, label: "Cesta Atual", icon: Eye },
          { key: "historico" as const, label: "Histórico", icon: History },
          { key: "cadastrar" as const, label: "Cadastrar/Alterar", icon: Plus },
        ].map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm rounded-lg flex items-center gap-2 transition-colors ${tab === t.key ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground hover:text-foreground"
              }`}
          >
            <t.icon className="h-4 w-4" />
            {t.label}
          </button>
        ))}
      </div>

      {tab === "atual" && <CestaAtual />}
      {tab === "historico" && <CestaHistorico />}
      {tab === "cadastrar" && <CestaCadastrar />}
    </div>
  );
}

function CestaAtual() {
  const [cesta, setCesta] = useState<Cesta | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    scheduledApi.get<Cesta>("/api/admin/cesta/atual")
      .then(setCesta)
      .catch((err) => {
        if (err instanceof ApiClientError && err.status === 404) setError("Nenhuma cesta ativa configurada.");
        else setError(err.message);
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>;
  if (error) return <div className="metric-card text-center text-muted-foreground py-8">{error}</div>;
  if (!cesta) return null;

  return (
    <div className="metric-card">
      <h2 className="font-semibold mb-1">{cesta.nome}</h2>
      {cesta.dataCriacao && <p className="text-xs text-muted-foreground mb-4">Criada em: {fmtDateBR(cesta.dataCriacao)}</p>}
      <table className="data-table">
        <thead><tr><th>Ticker</th><th>Percentual</th><th>Cotação Atual</th></tr></thead>
        <tbody>
          {cesta.itens.map((item, i) => (
            <tr key={i}>
              <td className="font-mono font-medium">{item.ticker}</td>
              <td>{item.percentual.toFixed(2)}%</td>
              <td>{item.cotacaoAtual != null ? `R$ ${item.cotacaoAtual.toFixed(2)}` : "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function CestaHistorico() {
  const [cestas, setCestas] = useState<Cesta[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    scheduledApi.get<HistoricoCestasResponse>("/api/admin/cesta/historico")
      .then((res) => setCestas(res.cestas ?? []))
      .catch((err) => { toast.error(err.message); })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>;
  if (cestas.length === 0) return <div className="metric-card text-center text-muted-foreground py-8">Nenhuma cesta no histórico.</div>;

  return (
    <div className="space-y-4">
      {cestas.map((c, idx) => (
        <div key={idx} className="metric-card">
          <div className="flex justify-between items-start mb-2">
            <h3 className="font-semibold text-sm">{c.nome}</h3>
            
            {c.dataCriacao && <span className="text-xs text-muted-foreground">{fmtDateBR(c.dataCriacao)}</span>}
          </div>
          <div className="flex flex-wrap gap-2">
            {c.itens.map((item, j) => (
              <span key={j} className="status-badge-success font-mono">
                {item.ticker} {item.percentual.toFixed(1)}%
              </span>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function CestaCadastrar() {
  const [nome, setNome] = useState("");
  const [itens, setItens] = useState(
    Array.from({ length: 5 }, () => ({ ticker: "", percentual: "" }))
  );
  const [loading, setLoading] = useState(false);

  function updateItem(i: number, field: "ticker" | "percentual", value: string) {
    setItens((prev) => prev.map((item, idx) => (idx === i ? { ...item, [field]: value } : item)));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    // Validations
    for (const item of itens) {
      if (!item.ticker.trim()) {
        toast.error("Todos os 5 tickers devem ser preenchidos.");
        return;
      }
      const pct = parseFloat(item.percentual);
      if (isNaN(pct) || pct <= 0) {
        toast.error(`Percentual de ${item.ticker || "ativo"} deve ser maior que 0.`);
        return;
      }
    }

    const soma = itens.reduce((s, it) => s + parseFloat(it.percentual), 0);
    if (Math.abs(soma - 100) > 0.0001) {
      toast.error(`Soma dos percentuais deve ser 100%. Atual: ${soma.toFixed(2)}%`);
      return;
    }

    const payload: CriarCestaRequest = {
      nome,
      itens: itens.map((it) => ({ ticker: it.ticker.toUpperCase().trim(), percentual: parseFloat(it.percentual) })),
    };

    setLoading(true);
    try {
      await scheduledApi.post("/api/admin/cesta", payload);
      toast.success("Cesta cadastrada com sucesso!");
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
    <div className="metric-card">
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="text-xs text-muted-foreground">Nome da Cesta</label>
          <Input value={nome} onChange={(e) => setNome(e.target.value)} placeholder="Top Five - Março 2026" required />
        </div>
        <div className="space-y-2">
          <label className="text-xs text-muted-foreground">Ativos (exatamente 5)</label>
          {itens.map((item, i) => (
            <div key={i} className="flex gap-3">
              <Input
                value={item.ticker}
                onChange={(e) => updateItem(i, "ticker", e.target.value)}
                placeholder={`Ticker ${i + 1}`}
                className="flex-1 font-mono"
              />
              <Input
                value={item.percentual}
                onChange={(e) => updateItem(i, "percentual", e.target.value)}
                placeholder="%"
                type="number"
                step="0.01"
                min="0.01"
                className="w-28"
              />
            </div>
          ))}
          <p className="text-xs text-muted-foreground">
            Soma: {itens.reduce((s, it) => s + (parseFloat(it.percentual) || 0), 0).toFixed(2)}%
          </p>
        </div>
        <Button type="submit" disabled={loading || !nome.trim()}>
          {loading && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
          Salvar Cesta
        </Button>
      </form>
    </div>
  );
}
