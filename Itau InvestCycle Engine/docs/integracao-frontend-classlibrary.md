# Integracao Frontend + ClassLibrary + Controllers

## Objetivo
Este documento descreve:
- o que existe na `ClassLibrary` que impacta a API;
- quais endpoints o frontend precisa chamar;
- quais pre-condicoes/regras das controllers devem ser respeitadas para o fluxo funcionar.

## Servicos e Base URLs (ambiente local)
- `ScheduledPurchaseEngineService`: `http://localhost:5115` e `https://localhost:7298`
- `MarketDataIngestionService`: `http://localhost:5136` e `https://localhost:7241`

Arquivos de referencia:
- `ScheduledPurchaseEngineService/Properties/launchSettings.json`
- `MarketDataIngestionService/Properties/launchSettings.json`

## ClassLibrary: o que interessa para o frontend

### 1. Contratos de erro e resultado
- `ClassLibrary/Contracts/DTOs/ApiError.cs`
  - `erro` (mensagem)
  - `codigo` (codigo funcional)
- `ClassLibrary/Contracts/DTOs/Result.cs`
  - padrao interno usado pelos services para sucesso/erro.

### 2. Contratos de paginacao
- `ClassLibrary/Contracts/Common/Paging.cs`
  - `PagedRequest(page, pageSize)`
  - `PagedResponse<T>(items, page, pageSize, totalItems)`

### 3. Contratos por modulo (request/response)
- Clientes: `ClassLibrary/Contracts/DTOs/Clientes/ClientesDTO.cs`
  - adesao, saida, exclusao, alteracao de valor mensal, carteira e rentabilidade.
- Admin: `ClassLibrary/Contracts/DTOs/Admin/AdminContracts.cs`
  - cadastro/consulta de cesta e custodia master.
- Motor: `ClassLibrary/Contracts/DTOs/Motor/MotorContracts.cs`
  - execucao manual de compra e detalhamento de ordens/distribuicoes/residuos.
- Cotacoes: `ClassLibrary/Contracts/DTOs/CotacaoIngestDto.cs` e `CotahistPriceRecord.cs`
  - retorno de cotacoes e ingestao de arquivo COTAHIST.

### 4. Entidades de dominio usadas indiretamente na API
Namespace `ClassLibrary.Domain.Entities`:
- Clientes/contas: `Clientes`, `ContasGraficas`
- Cestas: `CestasRecomendacao`, `ItensCesta`
- Cotacoes: `Cotacoes`
- Distribuicao: `Custodias`, `OrdensCompra`, `Distribuicoes`

Essas entidades nao sao chamadas diretamente pelo frontend, mas sao a base das regras das controllers/services.

## Endpoints que o frontend precisa chamar

## 1. Clientes (`ScheduledPurchaseEngineService`)
Controller: `ScheduledPurchaseEngineService/Controllers/ClientController.cs`

### `POST /api/clientes/adesao`
- Body:
```json
{
  "nome": "Joao da Silva",
  "cpf": "12345678901",
  "email": "joao@email.com",
  "valorMensal": 3000.00
}
```
- Retornos:
  - `201`: `AdesaoClienteResponse`
  - `400`: `ApiError`

Regras:
- `valorMensal >= 100`
- CPF precisa ser valido (11 digitos, nao repetido)
- CPF nao pode estar duplicado.

### `POST /api/clientes/{clienteId}/saida`
- Retornos:
  - `200`: `SaidaClienteResponse`
  - `400` ou `404`: `ApiError`

### `DELETE /api/clientes/{clienteId}`
- Retornos:
  - `200`: `ExcluirClienteResponse`
  - `404`: `ApiError`

### `PUT /api/clientes/{clienteId}/valor-mensal`
- Body:
```json
{
  "novoValorMensal": 6000.00
}
```
- Retornos:
  - `200`: `AlterarValorMensalResponse`
  - `400` ou `404`: `ApiError`

### `GET /api/clientes/{clienteId}/carteira`
- Retornos:
  - `200`: `ConsultarCarteiraResponse`
  - `404`: `ApiError`

### `GET /api/clientes/{clienteId}/rentabilidade`
- Retornos:
  - `200`: `ConsultarRentabilidadeResponse`
  - `404`: `ApiError`

## 2. Admin (`ScheduledPurchaseEngineService`)
Controller: `ScheduledPurchaseEngineService/Controllers/AdminController.cs`

### `POST /api/admin/cesta`
- Body:
```json
{
  "nome": "Top Five - Marco 2026",
  "itens": [
    { "ticker": "PETR4", "percentual": 25.00 },
    { "ticker": "VALE3", "percentual": 20.00 },
    { "ticker": "ITUB4", "percentual": 20.00 },
    { "ticker": "ABEV3", "percentual": 20.00 },
    { "ticker": "RENT3", "percentual": 15.00 }
  ]
}
```
- Retornos:
  - `201`: `CadastrarOuAlterarCestaResponse`
  - `400`: `ApiError`

Regras:
- cesta deve ter exatamente 5 ativos;
- soma dos percentuais deve ser exatamente `100`.

### `GET /api/admin/cesta/atual`
- `200`: `CestaAtualResponse`

### `GET /api/admin/cesta/historico`
- `200`: `HistoricoCestasResponse`

### `GET /api/admin/conta-master/custodia`
- `200`: `ContaMasterCustodiaResponse`

Observacao importante:
- essa controller hoje retorna dados mockados (nao persistidos no banco) para consultas/admin.

## 3. Motor (`ScheduledPurchaseEngineService`)
Controller: `ScheduledPurchaseEngineService/Controllers/MotorController.cs`

### `POST /api/motor/executar-compra`
- Body:
```json
{
  "dataReferencia": "2026-03-01"
}
```
- Retornos:
  - `200`: `ExecutarCompraResponse`
  - `404`: `ApiError` (`COTACAO_NAO_ENCONTRADA` ou `CESTA_NAO_ENCONTRADA`)
  - `400`: `ApiError` (`QUANTIDADE_ATIVOS_INVALIDA` quando cesta sem itens)

## 4. Cotacoes (`MarketDataIngestionService`)
Controller: `MarketDataIngestionService/Controllers/CotacoesController.cs`

### `GET /api/cotacoes?page=1&pageSize=20&ticker=PETR4&dataPregao=2026-03-01`
- `200`: `PagedResponse<CotacaoIngestDto>`

### `GET /api/cotacoes/{id}`
- `200`: `CotacaoIngestDto`
- `404`: sem body padrao

### `POST /api/cotacoes/ingest` (multipart/form-data)
- Campo esperado no form-data: `file`
- Retornos:
  - `200`: `{ "file": "nome_arquivo.txt", "saved": 123 }`
  - `400`: `"File is empty."`

## Necessidades das Controllers (analise para frontend)

## 1. Dependencias de dados para o fluxo principal
Para o fluxo de compra funcionar sem erro:
1. Deve existir cotacao para todos os tickers da cesta na data de referencia (ou data anterior).
2. Deve existir cesta ativa com itens.
3. Devem existir clientes ativos com `valorMensal > 0`.

Se faltar qualquer item acima, o frontend deve tratar os erros de `ApiError`.

## 2. Sequencia recomendada de chamadas
1. Ingerir cotacoes: `POST /api/cotacoes/ingest`
2. Criar/alterar cesta: `POST /api/admin/cesta`
3. Cadastrar clientes: `POST /api/clientes/adesao`
4. Executar compra: `POST /api/motor/executar-compra`
5. Consultar carteira/rentabilidade: endpoints de `GET /api/clientes/...`

## 3. Tratamento de erros no frontend
Padrao sugerido:
- sempre ler `codigo` de `ApiError` para decidir mensagem de negocio;
- usar `erro` para mensagem exibivel ao usuario;
- mapear por endpoint os casos comuns:
  - `VALOR_MENSAL_INVALIDO`
  - `CPF_INVALIDO`
  - `CLIENTE_CPF_DUPLICADO`
  - `CLIENTE_NAO_ENCONTRADO`
  - `CLIENTE_JA_INATIVO`
  - `PERCENTUAIS_INVALIDOS`
  - `QUANTIDADE_ATIVOS_INVALIDA`
  - `CESTA_NAO_ENCONTRADA`
  - `COTACAO_NAO_ENCONTRADA`

## 4. Pontos de infraestrutura que impactam o frontend
- Nao ha configuracao explicita de CORS em `Program.cs`.
  - Para browser app em outra origem, sera necessario configurar CORS no backend ou usar proxy.
- Os servicos de negocio e cotacoes estao separados.
  - O frontend precisa tratar duas base URLs, ou usar um BFF/API gateway.
