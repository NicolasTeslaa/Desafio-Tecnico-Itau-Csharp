# Comandos sugeridos

Assuma o workspace em `C:\repo\Desafio-Tecnico-Itau-Csharp`.

## Navegação/utilitários Windows
- Listar arquivos: `Get-ChildItem`
- Listar recursivamente: `Get-ChildItem -Recurse`
- Trocar diretório: `Set-Location "Itau InvestCycle Engine"`
- Ler arquivo: `Get-Content -Raw <arquivo>`
- Buscar texto: `Select-String -Path <arquivo-ou-glob> -Pattern "texto"`
- Git status: `git status --short`
- Git diff: `git diff`

## Rodar a solução com Docker
Na pasta `Itau InvestCycle Engine/`:
- Subir tudo: `docker compose up --build`
- Subir apenas infra: `docker compose up mysql kafka kafka-ui`
- Subir frontend via compose: `docker compose up frontend`

## Rodar APIs localmente
Na pasta `Itau InvestCycle Engine/`:
- ScheduledPurchaseEngineService: `dotnet run --project .\ScheduledPurchaseEngineService\ScheduledPurchaseEngineService.csproj`
- MarketDataIngestionService: `dotnet run --project .\MarketDataIngestionService\MarketDataIngestionService.csproj`

## URLs locais esperadas
- ScheduledPurchaseEngineService: `http://localhost:5115`
- MarketDataIngestionService: `http://localhost:5136`
- Swagger ScheduledPurchaseEngineService: `http://localhost:5115/swagger`
- Swagger MarketDataIngestionService: `http://localhost:5136/swagger`
- Frontend: `http://localhost:8081`
- Kafka UI: `http://localhost:8080`
- MySQL: `localhost:3307`

## Frontend
Na pasta `Itau InvestCycle Engine\spa`:
- Instalar dependências: `npm ci`
- Desenvolvimento: `npm run dev`
- Build: `npm run build`
- Lint: `npm run lint`
- Testes: `npm run test`
- Testes em watch: `npm run test:watch`

## Testes backend
Na pasta `Itau InvestCycle Engine/`:
- Testes do ScheduledPurchaseEngineService: `dotnet test .\tests\ScheduledPurchaseEngineService.Tests\ScheduledPurchaseEngineService.Tests.csproj`
- Testes do MarketDataIngestionService: `dotnet test .\tests\MarketDataIngestionService.Tests\MarketDataIngestionService.Tests.csproj`
- Cobertura do ScheduledPurchaseEngineService: `dotnet test .\tests\ScheduledPurchaseEngineService.Tests\ScheduledPurchaseEngineService.Tests.csproj --collect:"XPlat Code Coverage"`
- Cobertura do MarketDataIngestionService: `dotnet test .\tests\MarketDataIngestionService.Tests\MarketDataIngestionService.Tests.csproj --collect:"XPlat Code Coverage"`

## Build/validação .NET
Na pasta `Itau InvestCycle Engine/`:
- Build de um serviço: `dotnet build .\ScheduledPurchaseEngineService\ScheduledPurchaseEngineService.csproj`
- Build da solução: `dotnet build .\Itau.InvestCycleEngine.slnx`
- Formatação: `dotnet format .\Itau.InvestCycleEngine.slnx`

## Arquivos de cobertura
Para localizar o XML de cobertura mais recente:
- ScheduledPurchaseEngineService: `Get-ChildItem .\tests\ScheduledPurchaseEngineService.Tests\TestResults -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1`
- MarketDataIngestionService: `Get-ChildItem .\tests\MarketDataIngestionService.Tests\TestResults -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1`
