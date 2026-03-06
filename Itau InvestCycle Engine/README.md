# Itau InvestCycle Engine

Implementacao do desafio tecnico de compra programada de acoes, com ingestao de cotacoes B3, motor de compra consolidada, distribuicao para custodias filhote, rebalanceamento e publicacao de eventos de IR em Kafka.

## Visao geral

O projeto esta organizado em tres blocos principais:

- `ScheduledPurchaseEngineService`: API principal do dominio de compra programada, clientes, carteira, cestas e rebalanceamento.
- `MarketDataIngestionService`: API de ingestao assincorna de arquivos COTAHIST e consulta de cotacoes.
- `spa`: frontend React/Vite para operacao e visualizacao.

A solucao tambem possui:

- `ClassLibrary`: contratos, entidades e tipos compartilhados.
- `tests`: testes unitarios e de integracao leve dos servicos.
- `cotacoes/`: pasta raiz para armazenamento dos arquivos COTAHIST, conforme o enunciado.
- `docker-compose.yml`: sobe MySQL, Kafka, Kafka UI, frontend e as duas APIs.

## Arquitetura

### Backend

Os dois servicos seguem uma estrutura parecida:

- `Controllers`: endpoints HTTP.
- `Services`: regras de negocio e orchestration.
- `Repositories` + `UnitOfWork`: acesso a dados com Entity Framework Core.
- `Data`: `DbContext`, configuracoes e migrations.

Principais fluxos de negocio implementados:

- adesao, saida e alteracao de valor mensal do cliente
- consulta de carteira e rentabilidade detalhada
- cadastro/versionamento de cesta Top Five
- motor programado 5/15/25 com separacao entre lote padrao e fracionario
- uso de saldo residual na conta master
- distribuicao rastreavel por ordem e custodia filhote
- rebalanceamento por mudanca de cesta
- rebalanceamento por desvio de proporcao
- publicacao Kafka para `ir-dedo-duro` e `ir-venda`

### Market data

O `MarketDataIngestionService` recebe um arquivo COTAHIST via upload, salva o arquivo em `cotacoes/` na raiz do projeto e enfileira um job de ingestao. O acompanhamento do processamento e feito por endpoint separado de status.

### Rentabilidade

O endpoint de rentabilidade reconstrui a evolucao historica da carteira usando:

- historico de aportes
- distribuicoes efetuadas para a custodia filhote
- movimentacoes de rebalanceamento
- cotacoes historicas armazenadas

## Stack

- .NET 9
- ASP.NET Core
- Entity Framework Core
- MySQL 8
- Apache Kafka
- React 18 + Vite
- xUnit
- Docker Compose

## Requisitos

Antes de rodar localmente, tenha instalado:

- .NET SDK 9
- Docker Desktop
- Node.js 20+
- npm

## Como executar com Docker

Na raiz do repositorio:

```powershell
docker compose up --build
```

Servicos disponiveis:

- frontend: `http://localhost:8081`
- ScheduledPurchaseEngineService: `http://localhost:5115`
- MarketDataIngestionService: `http://localhost:5136`
- Kafka UI: `http://localhost:8080`
- MySQL: `localhost:3307`

## Como executar localmente

### Infraestrutura

Suba ao menos MySQL e Kafka:

```powershell
docker compose up mysql kafka kafka-ui
```

Se quiser subir tambem o frontend:

```powershell
docker compose up frontend
```

### ScheduledPurchaseEngineService

```powershell
dotnet run --project .\ScheduledPurchaseEngineService\ScheduledPurchaseEngineService.csproj
```

URL local:

- `http://localhost:5115`

### MarketDataIngestionService

```powershell
dotnet run --project .\MarketDataIngestionService\MarketDataIngestionService.csproj
```

URL local:

- `http://localhost:5136`

### Frontend

```powershell
cd .\spa
npm ci
npm run dev
```

URL local:

- `http://localhost:8081`

## Banco e mensageria

Configuracoes padrao de desenvolvimento:

- MySQL database: `investCycle`
- MySQL user: `root`
- MySQL password: `12345678`
- Kafka bootstrap local: `localhost:29092`
- topico IR dedo-duro: `ir-dedo-duro`
- topico IR venda: `ir-venda`

As migrations sao aplicadas automaticamente no startup das APIs.

## Principais endpoints

### ScheduledPurchaseEngineService

- `POST /api/clientes/adesao`
- `POST /api/clientes/{clienteId}/saida`
- `PUT /api/clientes/{clienteId}/valor-mensal`
- `GET /api/clientes/{clienteId}/carteira`
- `GET /api/clientes/{clienteId}/rentabilidade`
- `POST /api/admin/cesta`
- `PUT /api/admin/cesta/{id}`
- `GET /api/admin/cesta/atual`
- `GET /api/admin/cesta/historico`
- `POST /api/admin/rebalancear/desvio`

### MarketDataIngestionService

- `POST /api/cotacoes/ingest`
- `GET /api/cotacoes/ingest/{jobId}`
- `GET /api/cotacoes/ingest/overview`
- `GET /api/cotacoes/ingest/history`
- `GET /api/cotacoes`
- `GET /api/cotacoes/{id}`
- `GET /api/cotacoes/tickers`

## Swagger

Em ambiente `Development`, os servicos expõem Swagger:

- ScheduledPurchaseEngineService: `http://localhost:5115/swagger`
- MarketDataIngestionService: `http://localhost:5136/swagger`

## Arquivos COTAHIST

Os arquivos devem ficar na pasta raiz `cotacoes/`, com nome original do pregao, por exemplo:

```text
cotacoes/COTAHIST_D20260225.TXT
```

O sistema utiliza a cotacao de fechamento do ultimo pregao disponivel para calculos de compra e para consultas historicas.

## Testes

Projetos de teste disponiveis:

- `tests/ScheduledPurchaseEngineService.Tests`
- `tests/MarketDataIngestionService.Tests`

Executar:

```powershell
dotnet test .\tests\ScheduledPurchaseEngineService.Tests\ScheduledPurchaseEngineService.Tests.csproj
dotnet test .\tests\MarketDataIngestionService.Tests\MarketDataIngestionService.Tests.csproj
```

Coletar cobertura:

```powershell
dotnet test .\tests\ScheduledPurchaseEngineService.Tests\ScheduledPurchaseEngineService.Tests.csproj --collect:"XPlat Code Coverage"
dotnet test .\tests\MarketDataIngestionService.Tests\MarketDataIngestionService.Tests.csproj --collect:"XPlat Code Coverage"
```

Os arquivos gerados ficam em:

- `tests/ScheduledPurchaseEngineService.Tests/TestResults/<guid>/coverage.cobertura.xml`
- `tests/MarketDataIngestionService.Tests/TestResults/<guid>/coverage.cobertura.xml`

Para localizar o XML mais recente de cada projeto:

```powershell
Get-ChildItem .\tests\ScheduledPurchaseEngineService.Tests\TestResults -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
Get-ChildItem .\tests\MarketDataIngestionService.Tests\TestResults -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
```

Ultimo resultado gerado:

- consolidado: `77,23%`
- `ScheduledPurchaseEngineService.Tests`: `76,23%`
- `MarketDataIngestionService.Tests`: `81,79%`

Arquivos do ultimo resultado:

- `tests/ScheduledPurchaseEngineService.Tests/TestResults/d01035b1-2095-4a8d-b867-5ae5bcc3e48a/coverage.cobertura.xml`
- `tests/MarketDataIngestionService.Tests/TestResults/303ebd6c-0c55-4d80-aea0-02ddd0889e8d/coverage.cobertura.xml`

## Decisoes tecnicas

- separacao de responsabilidades em duas APIs: uma para dominio de investimento e outra para ingestao de mercado
- Kafka usado apenas como producer, conforme o escopo do desafio
- persistencia de historico suficiente para rastrear distribuicoes e rebalanceamentos
- upload de COTAHIST assincorno, com endpoint dedicado de status
- `X-Request-Id` emitido nas respostas para rastreabilidade
- versionamento de cesta mantendo apenas uma cesta ativa por vez

## Estrutura resumida

```text
.
|-- cotacoes/
|-- docs/
|-- ClassLibrary/
|-- MarketDataIngestionService/
|-- ScheduledPurchaseEngineService/
|-- spa/
|-- tests/
|-- docker-compose.yml
|-- README.md
```
