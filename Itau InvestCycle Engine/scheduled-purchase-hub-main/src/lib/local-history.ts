export interface IngestaoRecord {
  file: string;
  saved: number;
  dataHora: string;
}

export interface CompraRecord {
  dataReferencia: string;
  totalClientes: number;
  totalConsolidado: number;
  dataHora: string;
}

const INGESTAO_KEY = 'cp_historico_ingestoes';
const COMPRA_KEY = 'cp_historico_compras';

export function getIngestoes(): IngestaoRecord[] {
  try {
    return JSON.parse(localStorage.getItem(INGESTAO_KEY) || '[]');
  } catch { return []; }
}

export function addIngestao(r: Omit<IngestaoRecord, 'dataHora'>) {
  const list = getIngestoes();
  list.unshift({ ...r, dataHora: new Date().toISOString() });
  localStorage.setItem(INGESTAO_KEY, JSON.stringify(list.slice(0, 20)));
}

export function getCompras(): CompraRecord[] {
  try {
    return JSON.parse(localStorage.getItem(COMPRA_KEY) || '[]');
  } catch { return []; }
}

export function addCompra(r: Omit<CompraRecord, 'dataHora'>) {
  const list = getCompras();
  list.unshift({ ...r, dataHora: new Date().toISOString() });
  localStorage.setItem(COMPRA_KEY, JSON.stringify(list.slice(0, 20)));
}
