# Data Model: Módulo 007 — Tela de Faturamento por Período

**Input**: [spec.md](./spec.md) Key Entities | [research.md](./research.md)

Nenhuma entidade de backend, tabela ou migration é criada ou alterada. Este documento descreve o **modelo de estado do frontend** para a nova área de faturamento e como ele deriva do contrato JSON já existente (`DailyRevenueResponse`, módulo 003).

---

## Entidades vindas do backend (somente leitura)

### `RevenueDay`

Vem de `GET /api/revenue/daily` → `DailyRevenueResponse.days[]` (`RevenueDayResponse`).

| Campo | Tipo JSON | Tipo JS | Origem |
| --- | --- | --- | --- |
| `date` | string `YYYY-MM-DD` | string | `RevenueDayResponse.Date` |
| `revenue` | number (decimal) | number | `RevenueDayResponse.Revenue` |
| `orderCount` | number (int) | number | `RevenueDayResponse.OrderCount` |

**Regra de exibição**: `date` é mostrada como `DD/MM/YYYY` via `formatCalendarDate` (split de `YYYY-MM-DD`), sem `new Date()` — mesmo dia calendário do backend.

### `DailyRevenue` (resultado da consulta)

Vem de `GET /api/revenue/daily` (`DailyRevenueResponse`).

| Campo | Tipo JSON | Tipo JS | Origem |
| --- | --- | --- | --- |
| `startDate` | string `YYYY-MM-DD` | string | `DailyRevenueResponse.StartDate` |
| `endDate` | string `YYYY-MM-DD` | string | `DailyRevenueResponse.EndDate` |
| `totalRevenue` | number (decimal) | number | `DailyRevenueResponse.TotalRevenue` |
| `totalOrders` | number (int) | number | `DailyRevenueResponse.TotalOrders` |
| `days` | array | `RevenueDay[]` | `DailyRevenueResponse.Days` — inclui **todos** os dias do intervalo, zeros inclusive |

### `ApiError` (erro HTTP)

| Campo | Tipo | Origem |
| --- | --- | --- |
| `error` | string | `ErrorResponse.Error` — corpo de `400` (datas ausentes, formato inválido, `startDate > endDate`, intervalo > 366 dias) |

---

## Estado local — área de faturamento (React)

Gerenciado em `App.jsx` (valores passados como props para `RevenuePanel.jsx`).

### `RevenueQuery` (formulário)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `startDate` | string `YYYY-MM-DD` | Controlado por `<input type="date">`; default = 1º dia do mês corrente (R3) |
| `endDate` | string `YYYY-MM-DD` | Controlado por `<input type="date">`; default = hoje (local) |

### `RevenueResult` (última consulta bem-sucedida)

Espelha `DailyRevenue` sem transformação estrutural — armazenado em `revenue` (`null` antes da primeira consulta bem-sucedida).

| Campo | Tipo | Regra |
| --- | --- | --- |
| `startDate` | string | Eco da resposta |
| `endDate` | string | Eco da resposta |
| `totalRevenue` | number | Formatado na UI com `formatCurrency` |
| `totalOrders` | number | Exibido como inteiro |
| `days` | `RevenueDay[]` | Uma linha por dia na tabela |

### Flags de UI

| Estado | Tipo | Regra |
| --- | --- | --- |
| `loadingRevenue` | boolean | `true` entre clique em `Consultar` e fim da requisição (sucesso ou erro) |
| `revenueError` | string \| null | Mensagem amigável; limpa ao iniciar nova consulta; em erro de validação local ou HTTP, **não** substitui `revenue` por objeto parcial |

---

## Estado local — navegação (React)

| Estado | Tipo | Valores | Regra |
| --- | --- | --- | --- |
| `activeTab` | string | `'orders'` \| `'revenue'` | Default `'orders'`; alternância sem reload (FR-001) |

---

## Estado local — área de pedidos (inalterado em schema, preservado entre abas)

Estados existentes do módulo 004 permanecem em `App.jsx` e **não são resetados** ao trocar para `Faturamento` (R8):

| Grupo | Campos principais |
| --- | --- |
| Listagem | `orders`, `pagination`, `page`, `loadingOrders`, `ordersError` |
| Produtos / formulário | `products`, `draftOrder`, `loadingProducts`, `productsError`, `creating`, `createError`, `createSuccessMessage`, `duplicateItemMessage` |

---

## Transições de estado (faturamento)

```text
[Abrir aba Faturamento]
  → startDate/endDate defaults se ainda não tocados pelo usuário
  → revenue/revenueError inalterados (consulta anterior permanece visível se existir)

[Clicar Consultar — validação local falha]
  → revenueError = mensagem PT
  → loadingRevenue = false
  → revenue = inalterado

[Clicar Consultar — válido]
  → revenueError = null
  → loadingRevenue = true
  → fetchDailyRevenue(startDate, endDate)
  → sucesso: revenue = body, revenueError = null
  → erro: revenueError = error.message, revenue = inalterado
  → loadingRevenue = false
  → se activeTab !== 'revenue' ou cancelled: ignorar setState de resultado

[Trocar para Pedidos durante loading]
  → handler ignora resposta tardia (R7)
```

---

## Relacionamentos

```text
App.jsx (container)
├── activeTab ──► renderiza área Pedidos (inline) OU RevenuePanel
├── RevenueQuery (startDate, endDate)
├── revenue ──► DailyRevenue | null
├── loadingRevenue, revenueError
├── validação local + handleConsultRevenue + guard de race
├── estados de pedidos (módulo 004, preservados)
└── passa props para RevenuePanel

RevenuePanel.jsx (apresentação — controlado por props; sem fetch/validação HTTP)
├── props: datas, loading, error, revenue, callbacks
└── import formatCurrency, formatCalendarDate de formatters.js

api.js
└── fetchDailyRevenue ──► GET /api/revenue/daily
```

---

## Fora do modelo deste módulo

- Persistência (`localStorage`, query string na URL).
- Cache/deduplicação de consultas repetidas (cada clique em `Consultar` refaz a chamada).
- Entidades de "faturar pedido", status `invoiced`, baixa de estoque.
