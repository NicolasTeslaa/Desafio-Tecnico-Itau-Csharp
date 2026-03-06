# Itau InvestCycle Engine - visão geral

## Propósito
Implementação de um desafio técnico de compra programada de ações. O sistema cobre ingestão de cotações da B3 (COTAHIST), motor de compra consolidada, distribuição para custodias filhote, rebalanceamento e publicação de eventos fiscais em Kafka.

## Stack principal
- Backend: .NET 9, ASP.NET Core Web API
- Persistência: Entity Framework Core 9 + Pomelo MySQL
- Mensageria: Apache Kafka (producer no serviço de compra programada)
- Frontend: React 18 + Vite + TypeScript + Tailwind + Radix UI + React Query
- Testes backend: xUnit + coverlet
- Testes frontend: Vitest
- Infra local: Docker Compose

## Estrutura do repositório
Raiz do workspace:
- `README.md`: documentação geral e comandos principais
- `Itau InvestCycle Engine/`: solução principal

Dentro de `Itau InvestCycle Engine/`:
- `ScheduledPurchaseEngineService/`: API principal do domínio de compra programada
- `MarketDataIngestionService/`: API para upload/processamento de COTAHIST e consulta de cotações
- `ClassLibrary/`: contratos, entidades e tipos compartilhados
- `tests/`: projetos de teste xUnit dos dois serviços
- `spa/`: frontend React/Vite
- `docs/`: documentação funcional/técnica
- `cotacoes/`: armazenamento local dos arquivos COTAHIST
- `docker-compose.yml`: sobe MySQL, Kafka, Kafka UI, frontend e APIs
- `Itau.InvestCycleEngine.slnx`: solução do .NET

## Arquitetura observada
Os serviços seguem uma organização consistente com:
- `Controllers`: endpoints HTTP
- `Services`: regras de negócio/orquestração
- `Repositories` + `UnitOfWork`: acesso a dados
- `Data`: DbContext, mapeamentos e migrations
- `Interfaces`: contratos internos
- `Settings`/`Support`/`Parser`: infraestrutura específica por serviço

## Entrypoints relevantes
- `ScheduledPurchaseEngineService/Program.cs`: registra DI, CORS, Swagger/OpenAPI, migrations automáticas, hosted service e publisher Kafka
- `MarketDataIngestionService/Program.cs`: registra DI, Swagger, background ingest job, migrations automáticas e tabela de segurança para `ingestao_jobs`
- `spa/package.json`: scripts de dev/build/lint/test do frontend

## Ambiente/local
- Sistema-alvo do desenvolvimento: Windows
- SDKs .NET encontrados localmente: 9.0.305 e 10.0.101
- `dotnet format` está disponível localmente
