# /dotnet-check — Verifica convenzioni .NET 8

## Input
File corrente. `@.claude/libs/stacks/dotnet.md`, `snippets/dotnet-patterns.md`, `arch/clean-arch.md`, `@.claude/memory/conventions.md`. Context7 per NuGet.

## Regole
- Checklist per tipo: Entity (factory method, setter privati), Command/Query (record, IRequest), Handler (IRequestHandler, no EF Core), Repository (async/await), Controller (ISender, ProducesResponseType)
- Max ~40 righe per Handler
- ✅/⚠️/❌ per criterio

## Output
Checklist per tipo, punteggio X/Y, azioni prioritarie
