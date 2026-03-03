import { useState } from "react";
import { scheduledApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import type {
  AdesaoRequest, Cliente, AlterarValorMensalRequest,
  CarteiraResponse, RentabilidadeResponse, AtivoCarteira,
} from "@/lib/types";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Loader2, UserPlus, Settings } from "lucide-react";
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from "recharts";

export default function ClientesPage() {
  const [tab, setTab] = useState<"adesao" | "gerenciar">("adesao");

  return (
    <div className="max-w-5xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Clientes</h1>
      <div className="flex gap-2">
        {[
          { key: "adesao" as const, label: "Adesão", icon: UserPlus },
          { key: "gerenciar" as const, label: "Gerenciar", icon: Settings },
        ].map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm rounded-lg flex items-center gap-2 transition-colors ${
              tab === t.key ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground hover:text-foreground"
            }`}
          >
            <t.icon className="h-4 w-4" />
            {t.label}
          </button>
        ))}
      </div>

      {tab === "adesao" ? <AdesaoTab /> : <GerenciarTab />}
    </div>
  );
}

function isValidCpf(cpf: string): boolean {
  if (cpf.length !== 11 || /^(\d)\1{10}$/.test(cpf)) return false;
  return /^\d{11}$/.test(cpf);
}

function AdesaoTab() {
  const [form, setForm] = useState({ nome: "", cpf: "", email: "", valorMensal: "" });
  const [loading, setLoading] = useState(false);
  const [cliente, setCliente] = useState<Cliente | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const cpf = form.cpf.replace(/\D/g, "");
    if (!isValidCpf(cpf)) { toast.error("CPF inválido. Deve ter 11 dígitos e não pode ser repetido."); return; }
    const valorMensal = parseFloat(form.valorMensal);
    if (isNaN(valorMensal) || valorMensal < 100) { toast.error("Valor mensal mínimo: R$ 100,00."); return; }

    setLoading(true);
    setCliente(null);
    try {
      const payload: AdesaoRequest = { nome: form.nome, cpf, email: form.email, valorMensal };
      const res = await scheduledApi.post<Cliente>("/api/clientes/adesao", payload);
      setCliente(res);
      toast.success("Cliente cadastrado com sucesso!");
    } catch (err) {
      apiError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="metric-card">
      <form onSubmit={handleSubmit} className="space-y-4 max-w-md">
        <div>
          <label className="text-xs text-muted-foreground">Nome</label>
          <Input value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} required />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">CPF (11 dígitos)</label>
          <Input value={form.cpf} onChange={(e) => setForm({ ...form, cpf: e.target.value })} placeholder="00000000000" maxLength={14} required />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">Email</label>
          <Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">Valor Mensal (R$)</label>
          <Input type="number" step="0.01" min="100" value={form.valorMensal} onChange={(e) => setForm({ ...form, valorMensal: e.target.value })} required />
        </div>
        <Button type="submit" disabled={loading}>
          {loading && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
          Cadastrar Adesão
        </Button>
      </form>

      {cliente && (
        <div className="mt-6 bg-muted rounded-lg p-4 text-sm space-y-1">
          <h3 className="font-semibold mb-2">Cliente Cadastrado</h3>
          <p><span className="text-muted-foreground">ID:</span> <span className="font-mono">{cliente.clienteId}</span></p>
          <p><span className="text-muted-foreground">Nome:</span> {cliente.nome}</p>
          <p><span className="text-muted-foreground">CPF:</span> {cliente.cpf}</p>
          <p><span className="text-muted-foreground">Valor Mensal:</span> R$ {cliente.valorMensal?.toFixed(2)}</p>
        </div>
      )}
    </div>
  );
}

function GerenciarTab() {
  const [clienteId, setClienteId] = useState("");
  const [novoValor, setNovoValor] = useState("");
  const [loading, setLoading] = useState<string | null>(null);
  const [carteira, setCarteira] = useState<CarteiraResponse | null>(null);
  const [rentabilidade, setRentabilidade] = useState<RentabilidadeResponse | null>(null);

  async function action(key: string, fn: () => Promise<void>) {
    if (!clienteId.trim()) { toast.error("Informe o ID do cliente."); return; }
    setLoading(key);
    try {
      await fn();
    } catch (err) {
      apiError(err);
    } finally {
      setLoading(null);
    }
  }

  return (
    <div className="space-y-4">
      <div className="metric-card">
        <div className="flex flex-col sm:flex-row gap-3 items-end">
          <div className="flex-1">
            <label className="text-xs text-muted-foreground">ID do Cliente</label>
            <Input value={clienteId} onChange={(e) => setClienteId(e.target.value)} placeholder="Ex: 1" className="font-mono" />
          </div>
        </div>

        <div className="flex flex-wrap gap-2 mt-4">
          <Button
            variant="outline" size="sm"
            disabled={loading !== null}
            onClick={() => action("saida", async () => {
              await scheduledApi.post(`/api/clientes/${clienteId}/saida`);
              toast.success("Saída registrada.");
            })}
          >
            {loading === "saida" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
            Saída
          </Button>
          <Button
            variant="outline" size="sm"
            disabled={loading !== null}
            onClick={() => action("excluir", async () => {
              await scheduledApi.delete(`/api/clientes/${clienteId}`);
              toast.success("Cliente excluído.");
            })}
          >
            {loading === "excluir" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
            Excluir
          </Button>
          <div className="flex gap-2 items-end">
            <Input
              value={novoValor}
              onChange={(e) => setNovoValor(e.target.value)}
              placeholder="Novo valor"
              type="number"
              step="0.01"
              className="w-32"
            />
            <Button
              variant="outline" size="sm"
              disabled={loading !== null}
              onClick={() => action("valor", async () => {
                const v = parseFloat(novoValor);
                if (isNaN(v) || v < 100) { toast.error("Valor mínimo: R$ 100,00."); return; }
                const body: AlterarValorMensalRequest = { novoValorMensal: v };
                await scheduledApi.put(`/api/clientes/${clienteId}/valor-mensal`, body);
                toast.success("Valor mensal alterado.");
              })}
            >
              {loading === "valor" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Alterar Valor
            </Button>
          </div>
          <Button
            variant="outline" size="sm"
            disabled={loading !== null}
            onClick={() => action("carteira", async () => {
              const res = await scheduledApi.get<CarteiraResponse>(`/api/clientes/${clienteId}/carteira`);
              setCarteira(res);
              setRentabilidade(null);
            })}
          >
            {loading === "carteira" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
            Carteira
          </Button>
          <Button
            variant="outline" size="sm"
            disabled={loading !== null}
            onClick={() => action("rentabilidade", async () => {
              const res = await scheduledApi.get<RentabilidadeResponse>(`/api/clientes/${clienteId}/rentabilidade`);
              setRentabilidade(res);
              setCarteira(null);
            })}
          >
            {loading === "rentabilidade" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
            Rentabilidade
          </Button>
        </div>
      </div>

      {/* Carteira */}
      {carteira && (
        <div className="metric-card overflow-x-auto">
          <h3 className="font-semibold mb-4">Carteira</h3>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-4">
            <Metric label="Valor Investido" value={`R$ ${carteira.resumo.valorTotalInvestido.toFixed(2)}`} />
            <Metric label="Valor Atual" value={`R$ ${carteira.resumo.valorAtualCarteira.toFixed(2)}`} />
            <Metric label="P/L Total" value={`R$ ${carteira.resumo.plTotal.toFixed(2)}`} positive={carteira.resumo.plTotal >= 0} />
            <Metric label="Rentabilidade" value={`${carteira.resumo.rentabilidadePercentual.toFixed(2)}%`} positive={carteira.resumo.rentabilidadePercentual >= 0} />
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>Ticker</th><th>Qtd</th><th>PM</th><th>Cotação Atual</th><th>Valor Atual</th><th>P/L</th><th>% Composição</th>
              </tr>
            </thead>
            <tbody>
              {carteira.ativos.map((a: AtivoCarteira, i: number) => (
                <tr key={i}>
                  <td className="font-mono font-medium">{a.ticker}</td>
                  <td>{a.quantidade}</td>
                  <td>R$ {a.precoMedio.toFixed(2)}</td>
                  <td>R$ {a.cotacaoAtual.toFixed(2)}</td>
                  <td>R$ {a.valorAtual.toFixed(2)}</td>
                  <td className={a.pl >= 0 ? "text-primary" : "text-destructive"}>R$ {a.pl.toFixed(2)}</td>
                  <td>{a.composicaoCarteira.toFixed(2)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Rentabilidade */}
      {rentabilidade && (
        <div className="metric-card">
          <h3 className="font-semibold mb-4">Rentabilidade</h3>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-4">
            <Metric label="Valor Investido" value={`R$ ${rentabilidade.rentabilidade.valorTotalInvestido.toFixed(2)}`} />
            <Metric label="Valor Atual" value={`R$ ${rentabilidade.rentabilidade.valorAtualCarteira.toFixed(2)}`} />
            <Metric label="P/L Total" value={`R$ ${rentabilidade.rentabilidade.plTotal.toFixed(2)}`} positive={rentabilidade.rentabilidade.plTotal >= 0} />
            <Metric label="Rentabilidade" value={`${rentabilidade.rentabilidade.rentabilidadePercentual.toFixed(2)}%`} positive={rentabilidade.rentabilidade.rentabilidadePercentual >= 0} />
          </div>
          {rentabilidade.evolucaoCarteira && rentabilidade.evolucaoCarteira.length > 0 && (
            <div className="h-64 mt-4">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={rentabilidade.evolucaoCarteira}>
                  <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
                  <XAxis dataKey="data" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Line type="monotone" dataKey="valorCarteira" stroke="hsl(152, 60%, 42%)" strokeWidth={2} dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Metric({ label, value, positive }: { label: string; value: string; positive?: boolean }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={`text-lg font-semibold ${positive === true ? "text-primary" : positive === false ? "text-destructive" : ""}`}>
        {value}
      </div>
    </div>
  );
}

function apiError(err: unknown) {
  if (err instanceof ApiClientError && err.apiError) {
    toast.error(friendlyError(err.apiError.codigo), { description: err.apiError.codigo });
  } else {
    toast.error(err instanceof Error ? err.message : "Erro inesperado.");
  }
}
