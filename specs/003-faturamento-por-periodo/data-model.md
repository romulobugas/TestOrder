# Data Model — Módulo 003

**Data**: 2026-07-03
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Sem novas entidades, tabelas ou migrations

Este módulo é **leitura pura** sobre o schema existente dos módulos 001/002. Nenhuma migration EF, entidade nova ou alteração de coluna é necessária.

Tabelas consultadas (inalteradas):

```text
orders (status = 'created')
  │ id, created_at, status
  │
  └──< order_items
         order_id, product_id, quantity, unit_price
```

- **`orders.created_at`**: já `DATETIME(6)` em UTC (módulo 001) — base do agrupamento por dia.
- **`orders.status`**: já `VARCHAR` (módulo 001) — filtro fixo `= 'created'` (único status existente até este módulo; pedidos do módulo 002 também nascem com `created`).
- **`order_items.quantity`**, **`order_items.unit_price`**: já existentes (módulo 001) — base do cálculo `quantity * unit_price`.

Nenhuma tabela de `inventory_units`, `order_reservation_units` ou `order_processing_events` (módulo 002) é referenciada — faturamento não tem relação com reserva de estoque ou outbox.

---

## Modelos de leitura (não persistidos, apenas C#)

Usados internamente na consulta Dapper e na resposta HTTP — não são entidades EF, apenas `record`s.

### Linha de agregação SQL (`RevenueQueries.cs`)

```text
RevenueDayRow { DateTime Date, decimal Revenue, int OrderCount }
```

Uma linha por **dia com pelo menos um pedido** no intervalo — dias sem pedido não aparecem no resultado da query (preenchidos depois em C#, ver [research.md](./research.md) R3).

### Resposta HTTP (`Models/Responses/ApiResponses.cs`)

```text
RevenueDayResponse   { string Date, decimal Revenue, int OrderCount }
DailyRevenueResponse { string StartDate, string EndDate, decimal TotalRevenue, int TotalOrders, IReadOnlyList<RevenueDayResponse> Days }
```

`TotalRevenue` e `TotalOrders` são calculados somando os itens de `Days` (já preenchido com zeros), não uma segunda agregação SQL.

---

## Validação de entrada (não persistida)

| Campo | Regra | Erro |
| --- | --- | --- |
| `startDate` | obrigatório, `yyyy-MM-dd` estrito | 400 |
| `endDate` | obrigatório, `yyyy-MM-dd` estrito | 400 |
| `startDate` vs `endDate` | `startDate <= endDate` | 400 se invertido |
| Intervalo | `(endDate - startDate).Days <= 365` (≤ 366 dias no total) | 400 se maior |

Nenhum dado de validação é persistido — mesmo padrão do módulo 001/002 (`ErrorResponse` retornado diretamente).
