# Estilo e convenções

## Backend C#
Convenções inferidas a partir dos projetos e arquivos inspecionados:
- `TargetFramework` = `net9.0`
- `Nullable` habilitado
- `ImplicitUsings` habilitado
- Uso de namespaces com sintaxe de arquivo (`namespace X;`)
- Classes principais frequentemente `sealed`
- Async/await em serviços e controllers com sufixo `Async`
- Injeção de dependência por construtor ou registro direto em `Program.cs`
- Controllers finos delegando regras de negócio para services
- Repositories + `IUnitOfWork` como padrão de acesso a dados
- EF Core com migrations aplicadas automaticamente no startup
- `CancellationToken` exposto nos endpoints e propagado para serviços/repositórios
- Erros de API modelados como DTO (`ApiError`) e traduzidos para HTTP 400/404/503/500
- Logging com `ILogger<T>` e mensagens objetivas
- `X-Request-Id` adicionado às respostas para rastreabilidade

## Nomenclatura
- Tipos e membros públicos em PascalCase
- Parâmetros/locais em camelCase
- DTOs e respostas explicitamente nomeados por caso de uso (`AdesaoClienteRequest`, `ConsultarCarteiraResponse` etc.)
- Entidades de domínio compartilhadas em `ClassLibrary.Domain.*`

## Frontend
A pasta `spa/` usa:
- TypeScript
- Vite
- ESLint (`eslint.config.js`)
- Vitest (`vitest.config.ts`)
- Tailwind + componentes Radix

## Observações de consistência
- Não foi encontrado `.editorconfig` na raiz
- Há mistura de nomenclatura em português de domínio com termos técnicos em inglês, então preservar o padrão existente por módulo é mais seguro do que tentar normalizar manualmente
- Há alguns nomes legados/inconsistentes já presentes no código, por exemplo `IClentService`; evitar renomeações amplas sem necessidade explícita
