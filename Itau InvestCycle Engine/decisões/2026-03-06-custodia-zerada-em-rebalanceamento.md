# Decisao: Manter custodia zerada em rebalanceamentos

## Contexto

Os requisitos tecnicos em `docs/` exigem:

- manutencao de posicoes e historico de operacoes
- rastreabilidade das distribuicoes
- atualizacao da custodia filhote no rebalanceamento

Referencias principais:

- `docs/desafio-tecnico-compra-programada.md`
  - conceito de `Custodia Filhote`
  - fluxo de rebalanceamento atualizando a custodia filhote
  - acompanhamento com historico e rentabilidade
- `docs/regras-negocio-detalhadas.md`
  - RN-008: posicao existente e mantida
  - RN-019: alteracao de cesta dispara rebalanceamento
  - RN-038: preco medio da custodia filhote deve ser atualizado
  - checklist final: historico completo de operacoes

O modelo atual tambem possui `Distribuicoes` apontando para `Custodias` com restricao referencial. Isso significa que apagar uma custodia que ja participou de distribuicoes quebra a rastreabilidade historica e pode falhar no banco.

## Decisao

Quando um rebalanceamento zerar a posicao de um ativo, a linha de `Custodias` nao deve ser removida fisicamente.

Em vez disso:

- `Quantidade` deve ser ajustada para `0`
- `DataUltimaAtualizacao` deve ser atualizada
- o registro de `PrecoMedio` associado deve ser mantido consistente

## Justificativa

Essa abordagem preserva:

- integridade referencial com o historico de `Distribuicoes`
- rastreabilidade das operacoes passadas
- aderencia ao requisito de historico completo

Tambem evita que uma nova compra futura do mesmo ativo precise recriar artificialmente a identidade da custodia para o mesmo cliente.

## Consequencia

Consultas de carteira e rentabilidade devem continuar considerando apenas posicoes com `Quantidade > 0`, enquanto a base preserva o historico completo da custodia.
