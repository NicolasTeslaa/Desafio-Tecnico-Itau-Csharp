const ERROR_MESSAGES: Record<string, string> = {
  PERCENTUAIS_INVALIDOS: 'A soma dos percentuais deve ser exatamente 100%.',
  QUANTIDADE_ATIVOS_INVALIDA: 'A cesta deve conter exatamente 5 ativos.',
  TICKER_INVALIDO: 'Um ou mais tickers informados nao existem na base de cotacoes.',
  VALOR_MENSAL_INVALIDO: 'O valor mensal deve ser no minimo R$ 100,00.',
  CPF_INVALIDO: 'O CPF informado e invalido.',
  CLIENTE_CPF_DUPLICADO: 'Ja existe um cliente cadastrado com este CPF.',
  CLIENTE_NAO_ENCONTRADO: 'Cliente nao encontrado com o ID informado.',
  CLIENTE_JA_INATIVO: 'Este cliente ja esta inativo.',
  COTACAO_NAO_ENCONTRADA: 'Cotacao nao encontrada para a data/ativo informado.',
  CESTA_NAO_ENCONTRADA: 'Nenhuma cesta ativa configurada. Configure a cesta antes de executar.',
  DATA_EXECUCAO_INVALIDA: 'Data invalida para execucao. Use 5, 15, 25 ou o proximo dia util.',
  COMPRA_JA_EXECUTADA: 'Ja existe execucao para esta data de referencia.',
};

export function friendlyError(codigo: string): string {
  return ERROR_MESSAGES[codigo] || codigo;
}
