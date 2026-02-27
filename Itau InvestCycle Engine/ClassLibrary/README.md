# ClassLibrary - Documentacao

## Visao Geral
A `ClassLibrary` concentra os contratos e modelos de dominio compartilhados pelo projeto:
- Tipos base (`Entity`, `AuditableEntity`)
- Contratos de entrada/saida (DTOs e paginacao)
- Entidades de negocio
- Enums de dominio
- Value objects

Target framework: `net9.0`.

## Estrutura

```text
ClassLibrary/
  Common/
    Auditing/
    BaseTypes/
  Contracts/
    Common/
    DTOs/
  Domain/
    Entities/
      CestasRecomendacao/
      Clientes/
      CompraDistribuicao/
      RebalanceamentoIR/
    Enums/
    ValueObjects/
  Class1.cs
```

## Common

### `Entity<TId>`
Arquivo: `Common/BaseTypes/Entity.cs`
- `Id`
- `Equals` e `GetHashCode` baseados em `Id`

### `AuditableEntity<TId>`
Arquivo: `Common/Auditing/AuditableEntity.cs`
- Herda de `Entity<TId>`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- Metodo `Touch()` para atualizar `UpdatedAtUtc`

## Contracts

### Paginacao
Arquivo: `Contracts/Common/Paging.cs`
- `PagedRequest(int Page = 1, int PageSize = 20)`
- `PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)`

### DTOs
Arquivo: `Contracts/DTOs/CotacaoIngestDto.cs`
- `DataPregao`
- `Ticker`
- `PrecoAbertura`
- `PrecoFechamento`
- `PrecoMaximo`
- `PrecoMinimo`

Arquivo: `Contracts/DTOs/CotahistPriceRecord.cs`
- `Symbol`
- `TradeDate`
- `Open`
- `High`
- `Low`
- `Close`
- `Volume`

## Domain - Enums
Arquivo: `Domain/Enums/DomainEnums.cs`
- `AssetType`: `Stock`, `Etf`, `Crypto`, `Fund`, `Bdr`
- `PlanStatus`: `Active`, `Paused`, `Cancelled`
- `FrequencyType`: `Daily`, `Weekly`, `Monthly`
- `ExecutionStatus`: `Executed`, `Failed`, `Skipped`
- `CurrencyCode`: `BRL`, `USD`, `EUR`
- `TipoConta`: `Master`, `Filhote`
- `TipoMercado`: `LOTE`, `FRACIONARIO`
- `TipoRebalanceamento`: `MUDANCA_CESTA`, `DESVIO`
- `TipoIR`: `DEDO_DURO`, `IR_Venda`

## Domain - Value Objects

### `Money`
Arquivo: `Domain/ValueObjects/Money.cs`
- `record struct` imutavel
- Campos:
  - `Amount`
  - `Currency` (`CurrencyCode`)

## Domain - Entities

### Nucleo InvestCycle

#### `Asset`
Arquivo: `Domain/Entities/Asset.cs`
- Herda de `AuditableEntity<Guid>`
- Campos:
  - `Id`
  - `Symbol`
  - `Type` (`AssetType`)
  - `Currency` (`CurrencyCode`)

#### `InvestmentAccount`
Arquivo: `Domain/Entities/InvestmentAccount.cs`
- Herda de `AuditableEntity<Guid>`
- Campos:
  - `Id`
  - `OwnerUserId`
  - `BrokerName`
  - `AccountCode`

#### `PlanExecution`
Arquivo: `Domain/Entities/PlanExecution.cs`
- Herda de `AuditableEntity<Guid>`
- Campos:
  - `Id`
  - `PlanId`
  - `RunAtUtc`
  - `Status` (`ExecutionStatus`)
  - `ErrorMessage` (opcional)

#### `PlanSchedule`
Arquivo: `Domain/Entities/PlanSchedule.cs`
- Tipo de agenda de execucao
- Campos:
  - `Frequency` (`FrequencyType`)
  - `Interval`
  - `DayOfWeek` (opcional)
  - `DayOfMonth` (opcional)
  - `RunAtLocalTime`

#### `ProgrammedPurchasePlan`
Arquivo: `Domain/Entities/ProgrammedPurchasePlan.cs`
- Herda de `AuditableEntity<Guid>`
- Campos:
  - `Id`
  - `AccountId`
  - `AssetId`
  - `AmountPerRun` (`Money`)
  - `Schedule` (`PlanSchedule`)
  - `Status` (`PlanStatus`)
  - `NextRunAtUtc`
- Metodos:
  - `Pause()`
  - `Resume(DateTime nextRunAtUtc)`
  - `Cancel()`
  - `SetNextRun(DateTime nextRunAtUtc)`

### Cotacoes

#### `Cotacoes`
Arquivo: `Domain/Entities/Cotacoes.cs`
- Campos:
  - `Id`
  - `DataPregao`
  - `Ticker` (`[MaxLength(12)]`)
  - `PrecoAbertura`
  - `PrecoFechamento`
  - `PrecoMaximo`
  - `PrecoMinimo`

### Cestas de Recomendacao

#### `CestasRecomendacao`
Arquivo: `Domain/Entities/CestasRecomendacao/CestasRecomendacao.cs`
- Campos:
  - `Id`
  - `Nome` (`[MaxLength(100)]`)
  - `Ativa`
  - `DataCriacao`
  - `DataDesativacao` (opcional)

#### `ItensCesta`
Arquivo: `Domain/Entities/CestasRecomendacao/ItensCesta.cs`
- Campos:
  - `Id`
  - `CestaId` (FK)
  - `Cesta` (navegacao)
  - `Ticker` (`[MaxLength(10)]`)
  - `Percentual`

### Clientes

#### `Clientes`
Arquivo: `Domain/Entities/Clientes/Clientes.cs`
- Campos:
  - `Id`
  - `Nome` (`[MaxLength(200)]`)
  - `CPF` (`[MaxLength(11)]`)
  - `Email` (`[MaxLength(200)]`)
  - `ValorMensal`
  - `Ativo`
  - `DataAdesao`

#### `ContasGraficas`
Arquivo: `Domain/Entities/Clientes/ContasGraficas.cs`
- Campos:
  - `Id`
  - `Cliente` (navegacao)
  - `ClienteId` (FK)
  - `NumeroConta` (`[MaxLength(20)]`)
  - `Tipo` (`TipoConta`)
  - `DataCriacao`

### Compra e Distribuicao

#### `Custodias`
Arquivo: `Domain/Entities/CompraDistribuicao/Custodias.cs`
- Campos:
  - `Id`
  - `ContasGraficas` (navegacao)
  - `ContasGraficasId` (FK)
  - `Ticker` (`[MaxLength(10)]`)
  - `Quantidade`
  - `PrecoMedio`
  - `DataUltimaAtualizacao`

#### `Distribuicoes`
Arquivo: `Domain/Entities/CompraDistribuicao/Distribuicoes.cs`
- Campos:
  - `Id`
  - `Ticker` (`[MaxLength(10)]`)
  - `Valor`
  - `Data`

#### `OrdensCompra`
Arquivo: `Domain/Entities/CompraDistribuicao/OrdensCompra.cs`
- Campos:
  - `Id`
  - `ContaGrafica` (navegacao)
  - `ContaMasterId` (FK)
  - `Ticker` (`[MaxLength(10)]`)
  - `Quantidade`
  - `PrecoUnitario`
  - `TipoMercado` (`TipoMercado`)
  - `DataExecucao`

### Rebalanceamento e IR

#### `EventosIR`
Arquivo: `Domain/Entities/RebalanceamentoIR/EventosIR.cs`
- Campos:
  - `Id`
  - `Cliente` (navegacao)
  - `ClienteId` (FK)
  - `Tipo` (`TipoIR`)
  - `ValorBase`
  - `ValorIR`
  - `PublicadoKafka`
  - `DataEvento`

#### `Rebalanceamentos`
Arquivo: `Domain/Entities/RebalanceamentoIR/Rebalanceamentos.cs`
- Campos:
  - `Id`
  - `Cliente` (navegacao)
  - `ClienteId` (FK)
  - `TickerVendido` (`[MaxLength(10)]`)
  - `TickerComprado` (`[MaxLength(10)]`)
  - `ValorVenda`
  - `DataRebalanceamento`

## Observacoes
- Existem namespaces misturados entre `ClassLibrary.*` e `Itau.InvestCycleEngine.*`.
- `Class1.cs` e um placeholder sem uso funcional.
- Ha propriedades nao anulaveis sem valor inicial em algumas entidades (gera warning de nulabilidade em build).
