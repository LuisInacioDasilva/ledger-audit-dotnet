<div align="center">

![Ledger Audit](assets/hero.svg)

# Ledger Audit

Ledger de dupla entrada em C# / ASP.NET Core 10. Um projeto de engenharia para demonstrar invariantes financeiros, escrita durável, idempotência e replay de eventos — com uma console web para inspecionar lançamentos e saldos.

[API](#api) · [Rodar localmente](#rodar-localmente) · [Decisões](#decisões-de-projeto) · [Testes](#testes-e-deploy)

</div>

## O que tem aqui

- Cada lançamento precisa de pelo menos duas linhas, sem valor zero, com soma exatamente **zero**. Valores são `long` em unidades mínimas (centavos), nunca `float`.
- `POST /api/entries` exige bearer token. O mesmo `id` com o mesmo payload retorna duplicado sem nova gravação; payload diferente retorna conflito.
- Gravação append-only em NDJSON, serializada por lock e sincronizada em disco antes da resposta. Na inicialização, o arquivo é revalidado; dado corrompido impede a API de subir.
- Dashboard responsivo com trilha recente, posições por conta e conferência da soma dos saldos. Conteúdo vindo da API entra no DOM via `textContent`.
- Testes para equilíbrio, overflow, replay, conflito de idempotência, corrupção e concorrência.

## Rodar localmente

Requer [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0). Em PowerShell:

```powershell
$env:LEDGER_API_KEY = "uma-chave-local-com-mais-de-24-caracteres"
$env:LEDGER_DATA_FILE = "./data/entries.ndjson"
dotnet run --project LedgerAudit.Api --urls http://localhost:5080
```

Abra `http://localhost:5080`. O painel começa vazio; para criar um lançamento de exemplo:

```powershell
$body = @{
  id = "pedido-42-pagamento"
  occurredAt = "2026-10-19T12:00:00Z"
  description = "Pagamento do pedido 42"
  lines = @(
    @{ account = "caixa"; amountMinor = 12500 },
    @{ account = "recebiveis"; amountMinor = -12500 }
  )
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/entries -Headers @{ Authorization = "Bearer $env:LEDGER_API_KEY" } -ContentType 'application/json' -Body $body
```

Variáveis: `LEDGER_API_KEY` (obrigatória, mínimo 24 caracteres) e `LEDGER_DATA_FILE` (opcional, padrão `./data/entries.ndjson`). Consulte [.env.example](.env.example); nunca versione chaves reais.

## API

| Rota | Resultado |
| --- | --- |
| `GET /api/health` | Status e quantidade de eventos |
| `GET /api/entries` | Eventos em ordem decrescente |
| `GET /api/entries/{id}` | Evento por ID |
| `GET /api/balances` | Soma por conta, em unidades mínimas |
| `POST /api/entries` | Grava lançamento validado e autenticado (`201`, `200` duplicado, `409` conflito, `422` inválido) |

## Decisões de projeto

Este é um **laboratório com dados simulados**, não um razão contábil pronto para uso financeiro real. A simplicidade do arquivo NDJSON deixa as regras e o replay visíveis, mas limita a operação a **uma instância escritora** e não oferece snapshot, retenção, transação distribuída ou recuperação automática de gravação parcial. Para produção, migraria a persistência para PostgreSQL com `UNIQUE(id)`, transação para cabeçalho + linhas, índice por conta e trilha imutável. A API pública de consulta também precisaria de autenticação e autorização por escopo.

O arquivo é a fonte da verdade. Não edite nem apague linhas manualmente. Backup/rollback do experimento: pare a aplicação, copie `entries.ndjson` para um local seguro e restaure essa cópia com a aplicação parada. Se o replay acusar corrupção, preserve o arquivo original para análise; não ignore a linha inválida.

## Testes e deploy

```powershell
dotnet test LedgerAudit.Tests/LedgerAudit.Tests.csproj --configuration Release
dotnet publish LedgerAudit.Api/LedgerAudit.Api.csproj --configuration Release --output ./publish
```

O GitHub Actions executa os dois comandos em cada PR. Para uma implantação de demonstração, publique a saída de `publish/` em um host .NET 10 com HTTPS, defina as variáveis de ambiente e monte armazenamento persistente para o NDJSON. Mantenha **uma instância**. Não exponha esta implementação a dados financeiros reais.

---

<div align="center"><a href="https://github.com/LuisInacioDasilva"><img src="https://github.com/LuisInacioDasilva.png" width="64" alt="Luis da Silva" /></a><br/>Desenvolvido por <a href="https://github.com/LuisInacioDasilva">Luis da Silva</a>.</div>
