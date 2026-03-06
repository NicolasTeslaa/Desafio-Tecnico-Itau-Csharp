# Mapeamento dos Requisitos Tecnicos para a Implementacao Atual

Este documento relaciona os requisitos tecnicos solicitados na documentacao do desafio com a implementacao atual do repositório.

Fonte principal dos requisitos:
- `docs/desafio-tecnico-compra-programada.md`
- `docs/exemplos-contratos-api.md`
- `docs/regras-negocio-detalhadas.md`

Observacao:
- Este mapeamento foi feito por leitura da base atual.
- Os percentuais de cobertura citados abaixo foram obtidos do `README.md` do projeto e nao foram reexecutados nesta verificacao.

## 1. Stack obrigatoria

| Requisito solicitado | Implementacao no projeto atual | Evidencias no repositorio | Status |
|---|---|---|---|
| Backend em .NET Core (C#) | O backend foi implementado em C# com dois servicos ASP.NET Core: `ScheduledPurchaseEngineService` e `MarketDataIngestionService`, ambos em `net9.0`. | `ScheduledPurchaseEngineService/ScheduledPurchaseEngineService.csproj`, `MarketDataIngestionService/MarketDataIngestionService.csproj`, `ClassLibrary/ClassLibrary.csproj` | Implementado |
| Banco de dados MySQL | O projeto usa EF Core com o provider `Pomelo.EntityFrameworkCore.MySql`, com MySQL definido tanto no Docker Compose quanto nas configuracoes de runtime. | `ScheduledPurchaseEngineService/ScheduledPurchaseEngineService.csproj`, `MarketDataIngestionService/MarketDataIngestionService.csproj`, `docker-compose.yml`, `README.md` | Implementado |
| Mensageria com Apache Kafka em instancia real via Docker | O ambiente local sobe Kafka real via Docker Compose, e o servico de compra programada publica eventos fiscais via `KafkaFinanceEventsPublisher`. | `docker-compose.yml`, `ScheduledPurchaseEngineService/Services/KafkaFinanceEventsPublisher.cs`, `ScheduledPurchaseEngineService/Program.cs`, `README.md` | Implementado |
| API REST com documentacao Swagger/OpenAPI | Os dois servicos registram controllers REST e habilitam Swagger em `Development`. O servico principal tambem registra OpenAPI. | `ScheduledPurchaseEngineService/Program.cs`, `MarketDataIngestionService/Program.cs`, `ScheduledPurchaseEngineService/Controllers/`, `MarketDataIngestionService/Controllers/`, `README.md` | Implementado |
| Leitura de cotacoes via arquivo COTAHIST da B3 (TXT) | O `MarketDataIngestionService` recebe upload do arquivo, persiste o TXT em `cotacoes/`, enfileira ingestao e usa `CotahistParser` para interpretar o layout da B3. | `MarketDataIngestionService/Controllers/CotacoesController.cs`, `MarketDataIngestionService/Parser/CotahistParser.cs`, `README.md`, `docs/layout-cotahist-b3.md` | Implementado |

## 2. Requisitos de qualidade

| Requisito solicitado | Implementacao no projeto atual | Evidencias no repositorio | Status |
|---|---|---|---|
| Cobertura minima de 70% em testes unitarios e/ou integracao | O projeto possui duas suites xUnit, com cobertura coletada por `coverlet.collector`. O `README.md` registra ultimo consolidado de `77,23%`, acima do minimo solicitado. | `tests/ScheduledPurchaseEngineService.Tests/`, `tests/MarketDataIngestionService.Tests/`, `README.md`, `coverage.runsettings` | Implementado |
| Codigo limpo, SOLID e design patterns adequados | A base segue uma separacao clara entre `Controllers`, `Services`, `Repositories`, `Data`, `Interfaces` e `ClassLibrary`, com uso de `Repository` e `UnitOfWork`. Isso atende bem ao requisito arquitetural, embora a avaliacao de Clean Code e SOLID sempre tenha componente qualitativo. | `ScheduledPurchaseEngineService/`, `MarketDataIngestionService/`, `ClassLibrary/`, `README.md` | Implementado com ressalvas qualitativas |
| README completo com instrucoes de execucao, arquitetura e decisoes tecnicas | O `README.md` atual descreve stack, arquitetura, execucao local/Docker, endpoints, Swagger, testes, cobertura e decisoes tecnicas. | `README.md` | Implementado |

## 3. Diferenciais valorizados

| Diferencial solicitado | Implementacao no projeto atual | Evidencias no repositorio | Status |
|---|---|---|---|
| Interface web para cliente e/ou painel administrativo | Existe um frontend em React/Vite/TypeScript na pasta `spa`, tambem previsto no Docker Compose. | `spa/package.json`, `spa/src/`, `docker-compose.yml`, `README.md` | Implementado |
| Uso de CQRS, Event Sourcing ou DDD | Ha sinais de organizacao inspirada em DDD, especialmente pela existencia de `ClassLibrary.Domain`, contratos separados e entidades de dominio compartilhadas. Nao foram encontrados CQRS ou Event Sourcing explicitos na implementacao atual. | `ClassLibrary/Domain/`, `ClassLibrary/Contracts/`, `ScheduledPurchaseEngineService/Services/`, `MarketDataIngestionService/Services/` | Parcial |
| Observabilidade (logs estruturados, metricas) | O projeto possui logging com `ILogger`, tratamento sanitizado de excecoes, `X-Request-Id` nas respostas e mensagens operacionais relevantes. Nao foram encontrados indicadores de metricas, tracing distribuido ou dashboards. | `ScheduledPurchaseEngineService/Program.cs`, `MarketDataIngestionService/Program.cs`, `ScheduledPurchaseEngineService/Services/`, `MarketDataIngestionService/Services/` | Parcial |
| CI/CD configurado no repositorio | Existe a pasta `.github/workflows`, mas nao foram encontrados arquivos de pipeline nela durante esta verificacao. | `.github/workflows/` | Nao identificado como implementado |

## 4. Ligacao direta entre requisitos funcionais tecnicos e a implementacao

Embora a secao 5 do desafio trate da base tecnica, alguns pontos funcionais tecnicos dos documentos tambem estao refletidos diretamente no projeto:

| Requisito documentado | Implementacao no projeto atual | Evidencias no repositorio | Status |
|---|---|---|---|
| Motor de compra programada com execucao manual para testes | O endpoint `POST /api/motor/executar-compra` existe no servico principal e delega a execucao para `IScheduledPurchaseEngine`. | `ScheduledPurchaseEngineService/Controllers/MotorController.cs`, `ClassLibrary/Contracts/DTOs/Motor/MotorContracts.cs`, `docs/exemplos-contratos-api.md` | Implementado |
| Execucao automatica do motor em dias 5, 15 e 25 ou proximo dia util | O `TradingCalendar` resolve dias uteis, e o `ScheduledPurchaseHostedService` tenta executar automaticamente o motor de forma recorrente. | `ScheduledPurchaseEngineService/Services/TradingCalendar.cs`, `ScheduledPurchaseEngineService/Services/ScheduledPurchaseHostedService.cs`, `docs/regras-negocio-detalhadas.md` | Implementado |
| Publicacao de IR em Kafka | O motor registra eventos de IR e usa `IFinanceEventsPublisher` para publicar `ir-dedo-duro` e `ir-venda`. | `ScheduledPurchaseEngineService/Services/ScheduledPurchaseEngine.cs`, `ScheduledPurchaseEngineService/Services/KafkaFinanceEventsPublisher.cs`, `README.md` | Implementado |
| Rastreabilidade de execucao do motor | O projeto persiste execucao corrente e historico do motor em entidades dedicadas. | `ClassLibrary/Domain/Entities/MotorExecucao.cs`, `ClassLibrary/Domain/Entities/MotorExecucaoHistorico.cs`, `ScheduledPurchaseEngineService/Controllers/MotorController.cs` | Implementado |

## 5. Conclusao

O projeto atende aos requisitos tecnicos obrigatorios do desafio e implementa os principais diferenciais praticos, com destaque para:
- backend em .NET/C#
- MySQL
- Kafka real via Docker
- APIs REST com Swagger
- ingestao de COTAHIST
- cobertura documentada acima de 70%
- frontend web

Os pontos que aparecem como parciais ou nao identificados sao:
- uso explicito de CQRS/Event Sourcing
- observabilidade com metricas/tracing
- pipeline de CI/CD versionado no repositorio
