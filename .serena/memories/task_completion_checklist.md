# Checklist de conclusão de tarefa

Ao concluir uma alteração, validar o mínimo necessário para a área tocada.

## Se alterar backend C#
- Rodar build do projeto afetado ou da solução: `dotnet build`
- Rodar os testes do serviço impactado em `tests/`
- Se a mudança tocar regras críticas, considerar cobertura com `--collect:"XPlat Code Coverage"`
- Verificar se a alteração mantém startup saudável, especialmente registros de DI, migrations e configurações

## Se alterar frontend
- Na pasta `spa/`, rodar `npm run lint`
- Rodar `npm run test`
- Se necessário, validar `npm run build`

## Se alterar integração/infra
- Validar `docker compose up --build` ou ao menos os serviços afetados
- Confirmar portas/URLs esperadas descritas no `README.md`
- Se a mudança depender de MySQL/Kafka, confirmar que as configurações locais permanecem compatíveis

## Regras práticas
- Preservar a separação entre `Controllers`, `Services`, `Repositories` e `Data`
- Evitar lógica de negócio extensa em controllers
- Propagar `CancellationToken` quando o fluxo já usa async
- Não introduzir quebras em contratos compartilhados de `ClassLibrary` sem revisar impacto nos dois serviços e testes
