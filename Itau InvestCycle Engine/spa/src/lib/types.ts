// API Error from backend
export interface ApiError {
  erro: string;
  codigo: string;
}

// Paged response
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages?: number;
}

// Market Data types
export interface Cotacao {
  id: number;
  ticker: string;
  dataPregao: string;
  precoAbertura: number;
  precoMaximo: number;
  precoMinimo: number;
  precoFechamento: number;
  precoMedio: number;
  volume: number;
}

export interface IngestResponse {
  file: string;
  saved: number;
}

export interface IngestStartResponse {
  jobId: string;
  file: string;
  status: "QUEUED" | "PROCESSING" | "COMPLETED" | "FAILED";
  createdAtUtc: string;
}

export interface IngestJobStatusResponse {
  jobId: string;
  file: string;
  status: "QUEUED" | "PROCESSING" | "COMPLETED" | "FAILED";
  createdAtUtc: string;
  startedAtUtc?: string | null;
  finishedAtUtc?: string | null;
  saved: number;
  error?: string | null;
}

export interface IngestOverviewResponse {
  hasProcessing: boolean;
  processingCount: number;
  lastJob?: IngestJobStatusResponse | null;
}

// Cesta types
export interface CestaItem {
  ticker: string;
  percentual: number;
  cotacaoAtual?: number;
}

export interface Cesta {
  cestaId?: number;
  nome: string;
  ativa?: boolean;
  itens: CestaItem[];
  dataCriacao?: string;
  dataDesativacao?: string | null;
}

export interface HistoricoCestasResponse {
  cestas: Cesta[];
}

export interface CriarCestaRequest {
  nome: string;
  itens: { ticker: string; percentual: number }[];
}

// Cliente types
export interface AdesaoRequest {
  nome: string;
  cpf: string;
  email: string;
  valorMensal: number;
}

export interface Cliente {
  clienteId: number;
  nome: string;
  cpf: string;
  email: string;
  valorMensal: number;
  ativo: boolean;
  dataAdesao?: string;
  contaGrafica?: {
    id: number;
    numeroConta: string;
    tipo: string;
    dataCriacao: string;
  };
}

export interface AlterarValorMensalRequest {
  novoValorMensal: number;
}

// Carteira
export interface AtivoCarteira {
  ticker: string;
  quantidade: number;
  precoMedio: number;
  cotacaoAtual: number;
  valorAtual: number;
  pl: number;
  plPercentual: number;
  composicaoCarteira: number;
}

export interface ResumoCarteira {
  valorTotalInvestido: number;
  valorAtualCarteira: number;
  plTotal: number;
  rentabilidadePercentual: number;
}

export interface CarteiraResponse {
  clienteId: number;
  nome: string;
  contaGrafica: string;
  dataConsulta: string;
  resumo: ResumoCarteira;
  ativos: AtivoCarteira[];
}

// Rentabilidade
export interface PontoEvolucao {
  data: string;
  valorCarteira: number;
  valorInvestido: number;
  rentabilidade: number;
}

export interface RentabilidadeResumo {
  valorTotalInvestido: number;
  valorAtualCarteira: number;
  plTotal: number;
  rentabilidadePercentual: number;
}

export interface RentabilidadeResponse {
  clienteId: number;
  nome: string;
  dataConsulta: string;
  rentabilidade: RentabilidadeResumo;
  evolucaoCarteira?: PontoEvolucao[];
}

// Motor
export interface ExecutarCompraRequest {
  dataReferencia: string;
}

export interface DetalheLote {
  tipo: string;
  ticker: string;
  quantidade: number;
}

export interface OrdemCompra {
  ticker: string;
  quantidadeTotal: number;
  detalhes: DetalheLote[];
  precoUnitario: number;
  valorTotal: number;
}

export interface DistribuicaoCliente {
  clienteId: number;
  nome: string;
  valorAporte: number;
  ativos: Array<{
    ticker: string;
    quantidade: number;
  }>;
}

export interface ResiduoCustMaster {
  ticker: string;
  quantidade: number;
}

export interface ExecutarCompraResponse {
  totalClientes: number;
  totalConsolidado: number;
  ordensCompra: OrdemCompra[];
  distribuicoes: DistribuicaoCliente[];
  residuosCustMaster: ResiduoCustMaster[];
  eventosIrPublicados: number;
}
