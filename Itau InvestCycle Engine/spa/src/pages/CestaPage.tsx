import { useState, useEffect } from "react";
import { scheduledApi, marketDataApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import type {
  CadastrarOuAlterarCestaResponse,
  Cesta,
  CriarCestaRequest,
  HistoricoCestasResponse,
  TickersDisponiveisResponse,
} from "@/lib/types";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Loader2, Plus, History, Eye, Pencil, Trash2 } from "lucide-react";
import { fmtDateBR } from "@/lib/dateUtil";

type FormItem = { ticker: string; percentual: string };

const emptyItems = () => Array.from({ length: 5 }, () => ({ ticker: "", percentual: "" }));
const normalizeTicker = (value: string) => value.trim().toUpperCase();

export default function CestaPage() {
  const [tab, setTab] = useState<"atual" | "historico" | "cadastrar">("atual");
  const [editingCesta, setEditingCesta] = useState<Cesta | null>(null);

  function handleEdit(cesta: Cesta) {
    setEditingCesta(cesta);
    setTab("cadastrar");
  }

  function handleSaved() {
    setEditingCesta(null);
    setTab("historico");
  }

  function handleCancelEdit() {
    setEditingCesta(null);
  }

  return (
    <div className="max-w-5xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Cesta Top Five</h1>
      <div className="flex gap-2 flex-wrap">
        {[
          { key: "atual" as const, label: "Cesta Atual", icon: Eye },
          { key: "historico" as const, label: "Historico", icon: History },
          { key: "cadastrar" as const, label: "Cadastrar/Alterar", icon: Plus },
        ].map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-3 sm:px-4 py-2 text-sm rounded-lg flex items-center gap-2 transition-colors ${
              tab === t.key
                ? "bg-primary text-primary-foreground"
                : "bg-muted text-muted-foreground hover:text-foreground"
            }`}
          >
            <t.icon className="h-4 w-4" />
            {t.label}
          </button>
        ))}
      </div>

      {tab === "atual" && <CestaAtual />}
      {tab === "historico" && <CestaHistorico onEdit={handleEdit} />}
      {tab === "cadastrar" && (
        <CestaCadastrar
          editingCesta={editingCesta}
          onSaved={handleSaved}
          onCancelEdit={handleCancelEdit}
        />
      )}
    </div>
  );
}

function CestaAtual() {
  const [cesta, setCesta] = useState<Cesta | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    scheduledApi
      .get<Cesta>("/api/admin/cesta/atual")
      .then(setCesta)
      .catch((err) => {
        if (err instanceof ApiClientError && err.status === 404) {
          setError("Nenhuma cesta ativa configurada.");
        } else {
          setError(err.message);
        }
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading)
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );

  if (error)
    return <div className="metric-card text-center text-muted-foreground py-8">{error}</div>;

  if (!cesta) return null;

  return (
    <div className="metric-card">
      <h2 className="font-semibold mb-1">{cesta.nome}</h2>
      {cesta.dataCriacao && (
        <p className="text-xs text-muted-foreground mb-4">Criada em: {fmtDateBR(cesta.dataCriacao)}</p>
      )}
      <div className="overflow-x-auto">
        <table className="data-table min-w-[520px]">
          <thead>
            <tr>
              <th>Ticker</th>
              <th>Percentual</th>
              <th>Cotacao Atual</th>
            </tr>
          </thead>
          <tbody>
            {cesta.itens.map((item, i) => (
              <tr key={i}>
                <td className="font-mono font-medium">{item.ticker}</td>
                <td>{item.percentual.toFixed(2)}%</td>
                <td>{item.cotacaoAtual != null ? `R$ ${item.cotacaoAtual.toFixed(2)}` : "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function CestaHistorico({ onEdit }: { onEdit: (cesta: Cesta) => void }) {
  const [cestas, setCestas] = useState<Cesta[]>([]);
  const [loading, setLoading] = useState(true);
  const [deletingId, setDeletingId] = useState<number | null>(null);

  useEffect(() => {
    scheduledApi
      .get<HistoricoCestasResponse>("/api/admin/cesta/historico")
      .then((res) => setCestas(res.cestas ?? []))
      .catch((err) => {
        toast.error(err.message);
      })
      .finally(() => setLoading(false));
  }, []);

  async function handleDelete(cesta: Cesta) {
    const cestaId = cesta.cestaId;
    if (!cestaId) {
      toast.error("Nao foi possivel excluir: cesta sem id.");
      return;
    }

    if (!window.confirm(`Deseja excluir a cesta "${cesta.nome}"?`)) {
      return;
    }

    setDeletingId(cestaId);
    try {
      await scheduledApi.delete<void>(`/api/admin/cesta/${cestaId}`);
      setCestas((prev) => prev.filter((c) => c.cestaId !== cestaId));
      toast.success("Cesta excluida com sucesso.");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Erro ao excluir cesta.");
    } finally {
      setDeletingId(null);
    }
  }

  if (loading)
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );

  if (cestas.length === 0)
    return <div className="metric-card text-center text-muted-foreground py-8">Nenhuma cesta no historico.</div>;

  return (
    <div className="space-y-4">
      {cestas.map((c, idx) => (
        <div key={`${c.cestaId ?? idx}`} className="metric-card">
          <div className="flex flex-col sm:flex-row justify-between items-start mb-2 gap-3">
            <div>
              <h3 className="font-semibold text-sm">{c.nome}</h3>
              {c.dataCriacao && (
                <span className="text-xs text-muted-foreground">{fmtDateBR(c.dataCriacao)}</span>
              )}
            </div>
            <div className="flex w-full sm:w-auto gap-2">
              <Button type="button" size="sm" variant="secondary" onClick={() => onEdit(c)}>
                <Pencil className="h-4 w-4" />
                Editar
              </Button>
              <Button
                type="button"
                size="sm"
                variant="destructive"
                onClick={() => handleDelete(c)}
                disabled={deletingId === c.cestaId}
              >
                {deletingId === c.cestaId ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Trash2 className="h-4 w-4" />
                )}
                Excluir
              </Button>
            </div>
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

function CestaCadastrar({
  editingCesta,
  onSaved,
  onCancelEdit,
}: {
  editingCesta: Cesta | null;
  onSaved: () => void;
  onCancelEdit: () => void;
}) {
  const [nome, setNome] = useState("");
  const [itens, setItens] = useState<FormItem[]>(emptyItems());
  const [loading, setLoading] = useState(false);
  const [availableTickers, setAvailableTickers] = useState<string[]>([]);
  const [tickersLoaded, setTickersLoaded] = useState(false);

  useEffect(() => {
    if (!editingCesta) {
      setNome("");
      setItens(emptyItems());
      return;
    }

    const filled = editingCesta.itens.slice(0, 5).map((item) => ({
      ticker: item.ticker,
      percentual: item.percentual.toString(),
    }));

    while (filled.length < 5) {
      filled.push({ ticker: "", percentual: "" });
    }

    setNome(editingCesta.nome ?? "");
    setItens(filled);
  }, [editingCesta]);

  useEffect(() => {
    async function loadTickers() {
      try {
        const res = await marketDataApi.get<TickersDisponiveisResponse>("/api/cotacoes/tickers?limit=2000");
        const unique = Array.from(new Set((res.tickers ?? []).map(normalizeTicker))).sort();
        setAvailableTickers(unique);
        setTickersLoaded(true);
        return;
      } catch {
        // fallback: backend de agendamento le da mesma base de cotacoes importadas
      }

      try {
        const res = await scheduledApi.get<TickersDisponiveisResponse>("/api/admin/cesta/tickers?limit=2000");
        const unique = Array.from(new Set((res.tickers ?? []).map(normalizeTicker))).sort();
        setAvailableTickers(unique);
        setTickersLoaded(true);
      } catch {
        setAvailableTickers([]);
        setTickersLoaded(false);
        toast.error("Nao foi possivel carregar tickers do historico importado do COTAHIST.");
      }
    }

    void loadTickers();
  }, []);

  function getTickerSuggestions(index: number) {
    const current = normalizeTicker(itens[index]?.ticker ?? "");
    const usedByOthers = new Set(
      itens
        .map((item, idx) => (idx === index ? "" : normalizeTicker(item.ticker)))
        .filter((item) => item.length > 0)
    );

    return availableTickers
      .filter((ticker) => !usedByOthers.has(ticker))
      .filter((ticker) => current.length === 0 || ticker.includes(current))
      .slice(0, 20);
  }

  function updateItem(i: number, field: "ticker" | "percentual", value: string) {
    setItens((prev) => prev.map((item, idx) => (idx === i ? { ...item, [field]: value } : item)));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    if (!tickersLoaded) {
      toast.error("Nao foi possivel validar os tickers. Tente novamente em instantes.");
      return;
    }

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

    const normalizedTickers = itens.map((item) => normalizeTicker(item.ticker));
    const uniqueTickers = new Set(normalizedTickers);
    if (uniqueTickers.size !== normalizedTickers.length) {
      toast.error("Nao e permitido repetir ticker na cesta.");
      return;
    }

    const validTickers = new Set(availableTickers);
    const invalidTickers = normalizedTickers.filter((ticker) => !validTickers.has(ticker));
    if (invalidTickers.length > 0) {
      toast.error(`Ticker(s) invalido(s): ${Array.from(new Set(invalidTickers)).join(", ")}`);
      return;
    }

    const soma = itens.reduce((s, it) => s + parseFloat(it.percentual), 0);
    if (Math.abs(soma - 100) > 0.0001) {
      toast.error(`Soma dos percentuais deve ser 100%. Atual: ${soma.toFixed(2)}%`);
      return;
    }

    const payload: CriarCestaRequest = {
      nome,
      itens: itens.map((it) => ({
        ticker: normalizeTicker(it.ticker),
        percentual: parseFloat(it.percentual),
      })),
    };

    setLoading(true);
    try {
      const response = await scheduledApi.post<CadastrarOuAlterarCestaResponse>("/api/admin/cesta", payload);

      if (response.rebalanceamentoDisparado) {
        const alterados = (response.ativosPercentualAlterado ?? []).map(
          (x) => `${x.ticker}: ${x.percentualAnterior.toFixed(2)}% -> ${x.percentualNovo.toFixed(2)}%`
        );

        const detalhes = [
          response.ativosRemovidos.length > 0 ? `Removidos: ${response.ativosRemovidos.join(", ")}` : null,
          response.ativosAdicionados.length > 0 ? `Adicionados: ${response.ativosAdicionados.join(", ")}` : null,
          alterados.length > 0 ? `Percentuais alterados: ${alterados.join(" | ")}` : null,
        ].filter(Boolean);

        toast.success("Cesta salva com rebalanceamento disparado.", {
          description: detalhes.length > 0 ? detalhes.join("\n") : response.mensagem,
        });
      } else {
        toast.success(editingCesta ? "Cesta alterada com sucesso!" : "Cesta cadastrada com sucesso!");
      }

      onSaved();
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
        {editingCesta && (
          <div className="rounded-md border border-border bg-muted/40 p-3 text-sm text-muted-foreground">
            Editando cesta existente. Ao salvar, uma nova versao de cesta sera criada.
          </div>
        )}

        <div>
          <label className="text-xs text-muted-foreground">Nome da Cesta</label>
          <Input
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            placeholder="Top Five - Marco 2026"
            required
          />
        </div>

        <div className="space-y-2">
          <label className="text-xs text-muted-foreground">Ativos (exatamente 5)</label>
          {itens.map((item, i) => (
            <div key={i} className="flex flex-col sm:flex-row gap-3">
              <Input
                value={item.ticker}
                onChange={(e) => updateItem(i, "ticker", e.target.value.toUpperCase())}
                onBlur={(e) => updateItem(i, "ticker", normalizeTicker(e.target.value))}
                placeholder={`Ticker ${i + 1}`}
                className="flex-1 font-mono"
                list={`ticker-options-${i}`}
              />
              <datalist id={`ticker-options-${i}`}>
                {getTickerSuggestions(i).map((ticker) => (
                  <option key={`${i}-${ticker}`} value={ticker} />
                ))}
              </datalist>
              <Input
                value={item.percentual}
                onChange={(e) => updateItem(i, "percentual", e.target.value)}
                placeholder="%"
                type="number"
                step="0.01"
                min="0.01"
                className="w-full sm:w-28"
              />
            </div>
          ))}
          <p className="text-xs text-muted-foreground">
            Soma: {itens.reduce((s, it) => s + (parseFloat(it.percentual) || 0), 0).toFixed(2)}%
          </p>
        </div>

        <div className="flex flex-col sm:flex-row gap-2">
          <Button type="submit" disabled={loading || !nome.trim() || !tickersLoaded}>
            {loading && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            {editingCesta ? "Salvar Alteracao" : "Salvar Cesta"}
          </Button>

          {editingCesta && (
            <Button type="button" variant="outline" onClick={onCancelEdit} disabled={loading}>
              Cancelar Edicao
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}
