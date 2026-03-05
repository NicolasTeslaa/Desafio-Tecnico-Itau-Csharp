import { useEffect, useMemo, useState } from "react";
import { scheduledApi, ApiClientError } from "@/lib/api-client";
import { friendlyError } from "@/lib/error-dictionary";
import type {
  AdesaoRequest,
  Cliente,
  AlterarValorMensalRequest,
  CarteiraResponse,
  RentabilidadeResponse,
  AtivoCarteira,
  ClienteListaItem,
  ListarClientesResponse,
} from "@/lib/types";
import { fmtDateBR } from "@/lib/dateUtil";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Loader2, UserPlus, Settings, Users, RefreshCcw } from "lucide-react";
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from "recharts";

type ClienteTab = "adesao" | "gerenciar" | "lista";

export default function ClientesPage() {
  const [tab, setTab] = useState<ClienteTab>("adesao");
  const [clienteSelecionadoId, setClienteSelecionadoId] = useState("");

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Clientes</h1>

      <div className="flex flex-wrap gap-2">
        {[
          { key: "adesao" as const, label: "Adesao", icon: UserPlus },
          { key: "gerenciar" as const, label: "Gerenciar", icon: Settings },
          { key: "lista" as const, label: "Clientes atuais", icon: Users },
        ].map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-3 sm:px-4 py-2 text-sm rounded-lg flex items-center gap-2 transition-colors ${tab === t.key
              ? "bg-primary text-primary-foreground"
              : "bg-muted text-muted-foreground hover:text-foreground"
              }`}
          >
            <t.icon className="h-4 w-4" />
            {t.label}
          </button>
        ))}
      </div>

      {tab === "adesao" && <AdesaoTab />}
      {tab === "gerenciar" && (
        <GerenciarTab
          clienteIdInicial={clienteSelecionadoId}
          onClienteIdChange={setClienteSelecionadoId}
        />
      )}
      {tab === "lista" && (
        <ListaClientesTab
          onGerenciar={(clienteId) => {
            setClienteSelecionadoId(String(clienteId));
            setTab("gerenciar");
          }}
        />
      )}
    </div>
  );
}

function isValidCpf(cpf: string): boolean {
  if (cpf.length !== 11 || !/^\d{11}$/.test(cpf)) return false;
  if (/^(\d)\1{10}$/.test(cpf)) return false;

  const calculateDigit = (length: number) => {
    let sum = 0;
    let weight = length + 1;

    for (let i = 0; i < length; i++) {
      sum += Number(cpf[i]) * weight;
      weight -= 1;
    }

    const mod = sum % 11;
    return mod < 2 ? 0 : 11 - mod;
  };

  const firstDigit = calculateDigit(9);
  if (Number(cpf[9]) !== firstDigit) return false;

  const secondDigit = calculateDigit(10);
  return Number(cpf[10]) === secondDigit;
}

function AdesaoTab() {
  const [form, setForm] = useState({ nome: "", cpf: "", email: "", valorMensal: "" });
  const [loading, setLoading] = useState(false);
  const [cliente, setCliente] = useState<Cliente | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const cpf = form.cpf.replace(/\D/g, "");
    if (!isValidCpf(cpf)) {
      toast.error("CPF invalido. Informe 11 digitos validos.");
      return;
    }

    const valorMensal = parseFloat(form.valorMensal);
    if (Number.isNaN(valorMensal) || valorMensal < 100) {
      toast.error("Valor mensal minimo: R$ 100,00.");
      return;
    }

    setLoading(true);
    setCliente(null);

    try {
      const payload: AdesaoRequest = {
        nome: form.nome,
        cpf,
        email: form.email,
        valorMensal,
      };
      const res = await scheduledApi.post<Cliente>("/api/clientes/adesao", payload);
      setCliente(res);
      setForm({ nome: "", cpf: "", email: "", valorMensal: "" });
      toast.success("Cliente cadastrado com sucesso.");
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
          <Input
            value={form.nome}
            onChange={(e) => setForm({ ...form, nome: e.target.value })}
            required
          />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">CPF (11 digitos)</label>
          <Input
            value={form.cpf}
            onChange={(e) =>
              setForm({ ...form, cpf: e.target.value.replace(/\D/g, "").slice(0, 11) })
            }
            placeholder="00000000000"
            maxLength={11}
            required
            type="text"
          />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">Email</label>
          <Input
            type="email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            required
          />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">Valor mensal (R$)</label>
          <Input
            type="number"
            step="0.01"
            min="100"
            value={form.valorMensal}
            onChange={(e) => setForm({ ...form, valorMensal: e.target.value })}
            required
          />
        </div>
        <Button type="submit" disabled={loading}>
          {loading && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
          Cadastrar adesao
        </Button>
      </form>

      {cliente && (
        <div className="mt-6 bg-muted rounded-lg p-4 text-sm space-y-1">
          <h3 className="font-semibold mb-2">Cliente cadastrado</h3>
          <p>
            <span className="text-muted-foreground">ID:</span>{" "}
            <span className="font-mono">{cliente.clienteId}</span>
          </p>
          <p>
            <span className="text-muted-foreground">Nome:</span> {cliente.nome}
          </p>
          <p>
            <span className="text-muted-foreground">CPF:</span>{" "}
            {cliente.cpf?.slice(0, -2) + "XX"}
          </p>
          <p>
            <span className="text-muted-foreground">Valor mensal:</span> R${" "}
            {cliente.valorMensal?.toFixed(2)}
          </p>
        </div>
      )}
    </div>
  );
}

function GerenciarTab({
  clienteIdInicial,
  onClienteIdChange,
}: {
  clienteIdInicial: string;
  onClienteIdChange: (value: string) => void;
}) {
  const [clienteId, setClienteId] = useState(clienteIdInicial);
  const [novoValor, setNovoValor] = useState("");
  const [loading, setLoading] = useState<string | null>(null);
  const [carteira, setCarteira] = useState<CarteiraResponse | null>(null);
  const [rentabilidade, setRentabilidade] = useState<RentabilidadeResponse | null>(null);

  useEffect(() => {
    setClienteId(clienteIdInicial);
  }, [clienteIdInicial]);

  function updateClienteId(raw: string) {
    const onlyDigits = raw.replace(/\D/g, "");
    setClienteId(onlyDigits);
    onClienteIdChange(onlyDigits);
    setCarteira(null);
    setRentabilidade(null);
  }

  async function action(key: string, fn: () => Promise<void>) {
    if (!clienteId.trim()) {
      toast.error("Informe o ID do cliente.");
      return;
    }

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
      <div className="metric-card space-y-4">
        <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
          <div>
            <label className="text-xs text-muted-foreground">ID do cliente</label>
            <Input
              value={clienteId}
              onChange={(e) => updateClienteId(e.target.value)}
              placeholder="Ex: 1"
              className="font-mono"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Informe o ID para executar as operacoes de gestao.
            </p>
          </div>
          <Button
            variant="outline"
            disabled={loading !== null}
            onClick={() => {
              setCarteira(null);
              setRentabilidade(null);
              setNovoValor("");
            }}
          >
            Limpar painel
          </Button>
        </div>

        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <div className="rounded-lg border p-3 space-y-2">
            <p className="text-xs font-semibold uppercase text-muted-foreground">Cadastro</p>
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              disabled={loading !== null}
              onClick={() =>
                action("saida", async () => {
                  await scheduledApi.post(`/api/clientes/${clienteId}/saida`);
                  toast.success("Saida registrada.");
                })
              }
            >
              {loading === "saida" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Registrar saida
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              disabled={loading !== null}
              onClick={() =>
                action("excluir", async () => {
                  const ok = window.confirm("Confirma a exclusao permanente do cliente?");
                  if (!ok) return;
                  await scheduledApi.delete(`/api/clientes/${clienteId}`);
                  toast.success("Cliente excluido.");
                  setCarteira(null);
                  setRentabilidade(null);
                })
              }
            >
              {loading === "excluir" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Excluir cliente
            </Button>
          </div>

          <div className="rounded-lg border p-3 space-y-2">
            <p className="text-xs font-semibold uppercase text-muted-foreground">Aporte mensal</p>
            <Input
              value={novoValor}
              onChange={(e) => setNovoValor(e.target.value)}
              placeholder="Novo valor"
              type="number"
              step="0.01"
            />
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              disabled={loading !== null}
              onClick={() =>
                action("valor", async () => {
                  const v = parseFloat(novoValor);
                  if (Number.isNaN(v) || v < 100) {
                    toast.error("Valor minimo: R$ 100,00.");
                    return;
                  }
                  const body: AlterarValorMensalRequest = { novoValorMensal: v };
                  await scheduledApi.put(`/api/clientes/${clienteId}/valor-mensal`, body);
                  toast.success("Valor mensal alterado.");
                })
              }
            >
              {loading === "valor" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Alterar valor
            </Button>
          </div>

          <div className="rounded-lg border p-3 space-y-2">
            <p className="text-xs font-semibold uppercase text-muted-foreground">Consultas</p>
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              disabled={loading !== null}
              onClick={() =>
                action("carteira", async () => {
                  const res = await scheduledApi.get<CarteiraResponse>(`/api/clientes/${clienteId}/carteira`);
                  setCarteira(res);
                  setRentabilidade(null);
                })
              }
            >
              {loading === "carteira" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Consultar carteira
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              disabled={loading !== null}
              onClick={() =>
                action("rentabilidade", async () => {
                  const res = await scheduledApi.get<RentabilidadeResponse>(`/api/clientes/${clienteId}/rentabilidade`);
                  setRentabilidade(res);
                  setCarteira(null);
                })
              }
            >
              {loading === "rentabilidade" && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Consultar rentabilidade
            </Button>
          </div>
        </div>
      </div>

      {carteira && (
        <div className="metric-card">
          <h3 className="font-semibold mb-4">Carteira</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-4">
            <Metric label="Valor investido" value={`R$ ${carteira.resumo.valorTotalInvestido.toFixed(2)}`} />
            <Metric label="Valor atual" value={`R$ ${carteira.resumo.valorAtualCarteira.toFixed(2)}`} />
            <Metric
              label="P/L total"
              value={`R$ ${carteira.resumo.plTotal.toFixed(2)}`}
              positive={carteira.resumo.plTotal >= 0}
            />
            <Metric
              label="Rentabilidade"
              value={`${carteira.resumo.rentabilidadePercentual.toFixed(2)}%`}
              positive={carteira.resumo.rentabilidadePercentual >= 0}
            />
          </div>
          <div className="overflow-x-auto">
            <table className="data-table min-w-[760px]">
              <thead>
                <tr>
                  <th>Ticker</th>
                  <th>Qtd</th>
                  <th>PM</th>
                  <th>Cotacao atual</th>
                  <th>Valor atual</th>
                  <th>P/L</th>
                  <th>% composicao</th>
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
        </div>
      )}

      {rentabilidade && (
        <div className="metric-card">
          <h3 className="font-semibold mb-4">Rentabilidade</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-4">
            <Metric
              label="Valor investido"
              value={`R$ ${rentabilidade.rentabilidade.valorTotalInvestido.toFixed(2)}`}
            />
            <Metric
              label="Valor atual"
              value={`R$ ${rentabilidade.rentabilidade.valorAtualCarteira.toFixed(2)}`}
            />
            <Metric
              label="P/L total"
              value={`R$ ${rentabilidade.rentabilidade.plTotal.toFixed(2)}`}
              positive={rentabilidade.rentabilidade.plTotal >= 0}
            />
            <Metric
              label="Rentabilidade"
              value={`${rentabilidade.rentabilidade.rentabilidadePercentual.toFixed(2)}%`}
              positive={rentabilidade.rentabilidade.rentabilidadePercentual >= 0}
            />
          </div>

          {rentabilidade.evolucaoCarteira && rentabilidade.evolucaoCarteira.length > 0 && (
            <div className="h-64 mt-4">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={rentabilidade.evolucaoCarteira}>
                  <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
                  <XAxis dataKey="data" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Line
                    type="monotone"
                    dataKey="valorCarteira"
                    stroke="hsl(152, 60%, 42%)"
                    strokeWidth={2}
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ListaClientesTab({ onGerenciar }: { onGerenciar: (clienteId: number) => void }) {
  const [filtro, setFiltro] = useState<"ativos" | "todos">("ativos");
  const [busca, setBusca] = useState("");
  const [loading, setLoading] = useState(false);
  const [clientes, setClientes] = useState<ClienteListaItem[]>([]);
  const [clienteParaExcluir, setClienteParaExcluir] = useState<ClienteListaItem | null>(null);
  const [excluindoId, setExcluindoId] = useState<number | null>(null);

  async function carregar() {
    setLoading(true);
    try {
      const path = filtro === "ativos" ? "/api/clientes?ativo=true" : "/api/clientes";
      const res = await scheduledApi.get<ListarClientesResponse>(path);
      setClientes(res.clientes ?? []);
    } catch (err) {
      apiError(err);
      setClientes([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void carregar();
  }, [filtro]);

  const clientesFiltrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    if (!termo) return clientes;

    return clientes.filter((c) => {
      return (
        c.nome.toLowerCase().includes(termo) ||
        c.email.toLowerCase().includes(termo) ||
        c.cpf.includes(termo) ||
        String(c.clienteId).includes(termo)
      );
    });
  }, [busca, clientes]);

  async function excluirClienteSelecionado() {
    if (!clienteParaExcluir) return;

    setExcluindoId(clienteParaExcluir.clienteId);
    try {
      await scheduledApi.delete(`/api/clientes/${clienteParaExcluir.clienteId}`);
      setClientes((prev) => prev.filter((c) => c.clienteId !== clienteParaExcluir.clienteId));
      toast.success(`Cliente #${clienteParaExcluir.clienteId} excluido com sucesso.`);
      setClienteParaExcluir(null);
    } catch (err) {
      apiError(err);
    } finally {
      setExcluindoId(null);
    }
  }

  return (
    <div className="metric-card space-y-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-1">
          <h3 className="font-semibold">Lista de clientes</h3>
          <p className="text-xs text-muted-foreground">
            Consulte clientes ativos ou todos e acesse o gerenciamento em um clique.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={carregar} disabled={loading}>
          {loading ? <Loader2 className="h-3 w-3 animate-spin mr-1" /> : <RefreshCcw className="h-3 w-3 mr-1" />}
          Atualizar
        </Button>
      </div>

      <div className="grid gap-3 md:grid-cols-[1fr_auto]">
        <Input
          value={busca}
          onChange={(e) => setBusca(e.target.value)}
          placeholder="Buscar por nome, email, CPF ou ID"
        />
        <div className="flex gap-2">
          <Button
            size="sm"
            variant={filtro === "ativos" ? "default" : "outline"}
            onClick={() => setFiltro("ativos")}
            disabled={loading}
          >
            Ativos
          </Button>
          <Button
            size="sm"
            variant={filtro === "todos" ? "default" : "outline"}
            onClick={() => setFiltro("todos")}
            disabled={loading}
          >
            Todos
          </Button>
        </div>
      </div>

      <div className="text-xs text-muted-foreground">
        {clientesFiltrados.length} cliente(s) listado(s)
      </div>

      <div className="overflow-x-auto">
        <table className="data-table min-w-[980px]">
          <thead>
            <tr>
              <th>ID</th>
              <th>Nome</th>
              <th>CPF</th>
              <th>Email</th>
              <th>Valor mensal</th>
              <th>Status</th>
              <th>Adesao</th>
              <th>Conta</th>
              <th>Acoes</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={9} className="text-center py-6 text-muted-foreground">
                  <span className="inline-flex items-center gap-2">
                    <Loader2 className="h-4 w-4 animate-spin" /> Carregando...
                  </span>
                </td>
              </tr>
            ) : clientesFiltrados.length === 0 ? (
              <tr>
                <td colSpan={9} className="text-center py-6 text-muted-foreground">
                  Nenhum cliente encontrado.
                </td>
              </tr>
            ) : (
              clientesFiltrados.map((c) => (
                <tr key={c.clienteId}>
                  <td className="font-mono">{c.clienteId}</td>
                  <td>{c.nome}</td>
                  <td className="font-mono">
                    {c.cpf?.slice(0, -2) + "-xx"}
                  </td>
                  <td>{c.email}</td>
                  <td>R$ {c.valorMensal.toFixed(2)}</td>
                  <td>
                    <span
                      className={`inline-flex rounded-full px-2 py-0.5 text-xs ${c.ativo
                        ? "bg-primary/15 text-primary"
                        : "bg-destructive/15 text-destructive"
                        }`}
                    >
                      {c.ativo ? "Ativo" : "Inativo"}
                    </span>
                  </td>
                  <td>{fmtDateBR(c.dataAdesao)}</td>
                  <td className="font-mono">{c.contaGrafica ?? "-"}</td>
                  <td>
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="outline" onClick={() => onGerenciar(c.clienteId)}>
                        Gerenciar
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="border-destructive/30 text-destructive hover:bg-destructive/10"
                        onClick={() => setClienteParaExcluir(c)}
                      >
                        Excluir
                      </Button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <AlertDialog
        open={!!clienteParaExcluir}
        onOpenChange={(open) => {
          if (!open && excluindoId == null) {
            setClienteParaExcluir(null);
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Confirmar exclusao de cliente</AlertDialogTitle>
            <AlertDialogDescription>
              {clienteParaExcluir ? (
                <>
                  Deseja realmente excluir o cliente <strong>#{clienteParaExcluir.clienteId}</strong> ({clienteParaExcluir.nome})?
                  Esta acao e permanente.
                </>
              ) : (
                "Deseja realmente excluir este cliente?"
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={excluindoId != null}>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              disabled={excluindoId != null}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={(event) => {
                event.preventDefault();
                void excluirClienteSelecionado();
              }}
            >
              {excluindoId != null ? "Excluindo..." : "Confirmar exclusao"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function Metric({ label, value, positive }: { label: string; value: string; positive?: boolean }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div
        className={`text-lg font-semibold ${positive === true ? "text-primary" : positive === false ? "text-destructive" : ""
          }`}
      >
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
