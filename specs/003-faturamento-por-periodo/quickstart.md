# Quickstart — Módulo 003

**Objetivo**: Validar `GET /api/revenue/daily` — agregação de faturamento por dia, validação de parâmetros e regressão dos módulos 001/002.
**Contratos**: [contracts/api.md](./contracts/api.md) | **Modelo**: [data-model.md](./data-model.md)

> **Nota de evolução (follow-up módulo 007):** datas opcionais em `GET /api/revenue/daily` — ausência de `startDate`/`endDate` não retorna mais `400`. Os testes `GetDailyRevenue_MissingStartDate_Returns400` / `MissingEndDate` foram substituídos por casos de limite aberto. Comportamento atual: `specs/007-tela-faturamento-periodo/` e `AI_NOTES.md`.

## Pré-requisitos

- Módulos 001 e 002 operacionais (Docker, `dev-up.ps1`, `POST /api/orders` funcionando)
- .NET 10 SDK + Docker
- **Nenhuma migration nova** — módulo 003 não altera schema

```powershell
# Parar API antes de build/test no Windows (evita lock do .exe)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Subir ambiente

```powershell
.\scripts\dev-up.ps1
```

API em **`http://localhost:5069`**.

## Validar faturamento — intervalo com pedidos (200)

Ajustar as datas para cobrir o período do seed (pedidos distribuídos nos últimos ~365 dias a partir da data de referência do seed):

```powershell
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-01-01&endDate=2026-01-31"
```

**Esperado**: HTTP 200, `days` com 31 entradas, `totalRevenue` = soma de `days[].revenue`, `totalOrders` = soma de `days[].orderCount`.

Conferir via SQL (ajustar datas):

```sql
SELECT DATE(o.created_at) AS day, SUM(oi.quantity * oi.unit_price) AS revenue, COUNT(DISTINCT o.id) AS order_count
FROM orders o
INNER JOIN order_items oi ON oi.order_id = o.id
WHERE o.status = 'created'
  AND o.created_at >= '2026-01-01'
  AND o.created_at < '2026-02-01'
GROUP BY DATE(o.created_at);
```

## Validar intervalo sem pedidos (200 zerado)

```powershell
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2099-01-01&endDate=2099-01-07"
```

**Esperado**: HTTP 200, `totalRevenue: 0`, `totalOrders: 0`, todos os 7 dias em `days` com `revenue: 0` e `orderCount: 0`.

## Validar novo pedido aparece no dia correto

```powershell
# Criar pedido agora
curl -s -X POST http://localhost:5069/api/orders `
  -H "Content-Type: application/json" `
  -d '{"items":[{"productId":1,"quantity":1}]}'

# Consultar faturamento do dia de hoje (ajustar data)
curl -s "http://localhost:5069/api/revenue/daily?startDate=2026-07-03&endDate=2026-07-03"
```

**Esperado**: `days[0].orderCount >= 1` refletindo o pedido recém-criado.

## Validar erros (400)

```powershell
# startDate ausente
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?endDate=2026-01-31"

# endDate ausente
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-01-01"

# Data inválida
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-13-40&endDate=2026-01-31"

# startDate > endDate
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-02-01&endDate=2026-01-01"

# Intervalo > 366 dias
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2025-01-01&endDate=2026-12-31"
```

**Esperado**: HTTP 400 + `{ "error": "..." }` em todos os casos acima.

## Regressão módulos 001 e 002

```powershell
curl -s http://localhost:5069/api/products
curl -s "http://localhost:5069/api/orders?page=1&pageSize=5"
curl -s http://localhost:5069/api/orders/1
curl -s -X POST http://localhost:5069/api/orders -H "Content-Type: application/json" -d '{"items":[{"productId":1,"quantity":1}]}'
```

**Esperado**: HTTP 200/200/200/201 como antes — módulo 003 não altera esses contratos.

## Testes automatizados

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

**Esperado**: todos os testes dos módulos 001 + 002 + módulo 003 passando — **46/46** (32 dos módulos 001/002 + 14 casos novos em 10 métodos de teste do módulo 003).

| Teste | Filtro sugerido |
| --- | --- |
| `GetDailyRevenue_ValidRange_ReturnsAggregatedDays` | `ValidRange` |
| `GetDailyRevenue_SingleDayRange_ReturnsExactlyOneDay` | `SingleDayRange` |
| `GetDailyRevenue_TotalRevenue_MatchesSumOfDays` | `TotalRevenue` |
| `GetDailyRevenue_EmptyRange_ReturnsZeroedDays` | `EmptyRange` |
| `GetDailyRevenue_MissingStartDate_Returns400` | `MissingStartDate` |
| `GetDailyRevenue_MissingEndDate_Returns400` | `MissingEndDate` |
| `GetDailyRevenue_InvalidDate_Returns400` (Theory, 5 casos) | `InvalidDate` |
| `GetDailyRevenue_StartAfterEnd_Returns400` | `StartAfterEnd` |
| `GetDailyRevenue_RangeBoundary_AcceptsUpTo366AndRejectsOver` | `RangeBoundary` |
| `Regression_Modules001And002_StillWork` (inclui checagem do seed) | `Regression` |

## Parar ambiente

- API: Ctrl+C no terminal do `dev-up.ps1`
- MySQL: `docker compose down` ou `docker compose down -v`

## Documentação pós-implementação

Atualizar após implementação:

- `AI_NOTES.md` — decisão de preenchimento de dias em C#, escolha de intervalo semiaberto UTC
- `docs/PRESENTATION_GUIDE.md` — roteiro demo GET faturamento + SQL de conferência
