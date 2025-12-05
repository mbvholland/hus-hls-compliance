# HlsCompliance — Project Overzicht

**Gegenereerd:** 2025-12-04 23:12

## Solution


```powershell
# Alle tests uitvoeren
dotnet test
```

## Opmerkingen

- Mappen zoals `bin`, `obj`, `.git`, `.vs` en `node_modules` zijn weggelaten in de boomweergave.
- Target frameworks worden uit de `.csproj` gelezen (indien aanwezig).
- NuGet packages zijn inclusief transitieve afhankelijkheden; parsing is best-effort o.b.v. `dotnet list package`.
