const ERROR_MESSAGES: Record<string, string> = {
  PERCENTUAIS_INVALIDOS: 'A soma dos percentuais deve ser exatamente 100%.',
  QUANTIDADE_ATIVOS_INVALIDA: 'A cesta deve conter exatamente 5 ativos.',
  VALOR_MENSAL_INVALIDO: 'O valor mensal deve ser no mínimo R$ 100,00.',
  CPF_INVALIDO: 'O CPF informado é inválido.',
  CLIENTE_CPF_DUPLICADO: 'Já existe um cliente cadastrado com este CPF.',
  CLIENTE_NAO_ENCONTRADO: 'Cliente não encontrado com o ID informado.',
  CLIENTE_JA_INATIVO: 'Este cliente já está inativo.',
  COTACAO_NAO_ENCONTRADA: 'Cotação não encontrada para a data/ativo informado.',
  CESTA_NAO_ENCONTRADA: 'Nenhuma cesta ativa configurada. Configure a cesta antes de executar.',
};

export function friendlyError(codigo: string): string {
  return ERROR_MESSAGES[codigo] || codigo;
}
