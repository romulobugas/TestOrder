# API Contracts — Módulo 003

**Base URL**: `/api`
**Content-Type**: `application/json`
**Spec**: [spec.md](../spec.md) | **Módulos anteriores**: [../../001-base-listagem-pedidos/contracts/api.md](../../001-base-listagem-pedidos/contracts/api.md), [../../002-criacao-pedido-reservas/contracts/api.md](../../002-criacao-pedido-reservas/contracts/api.md)

## Convenções (herdadas dos módulos anteriores)

- Propriedades JSON em **camelCase**.
- Valores monetários como número decimal JSON.
- Sem autenticação.
- Erros: `{ "error": "mensagem" }`.
- **Exceção de data**: campos de dia civil (`startDate`, `endDate`, `days[].date`) são strings `yyyy-MM-dd` **sem** hora nem sufixo `Z` — diferente de `createdAt` (instante UTC) usado nos módulos 001/002.

> **Nota de evolução (follow-up módulo 007):** `startDate` e `endDate` deixaram de ser obrigatórios. Parâmetro ausente = sem filtro naquele lado; ambos ausentes = agrega todos os dias com pedido (sem zero-fill). A tabela de validação 400 abaixo reflete o contrato **original**; ver `specs/007-tela-faturamento-periodo/` para o contrato atual.

---

## Endpoints preservados (módulos 001 e 002)

Sem alteração de contrato:

- `GET /api/products`
- `GET /api/orders`
- `GET /api/orders/{id}`
- `POST /api/orders`

Ver contratos completos em [módulo 001](../../001-base-listagem-pedidos/contracts/api.md) e [módulo 002](../../002-criacao-pedido-reservas/contracts/api.md).

---

## GET /api/revenue/daily

Retorna o faturamento bruto agregado por dia dentro de um intervalo de datas informado.

### Query string

| Parâmetro | Tipo | Obrigatório | Regras |
| --- | --- | --- | --- |
| `startDate` | string | sim | Formato estrito `yyyy-MM-dd`; data válida |
| `endDate` | string | sim | Formato estrito `yyyy-MM-dd`; data válida; `>= startDate` |

**Regras de validação (400)**:

- `startDate` ausente ou vazio
- `endDate` ausente ou vazio
- `startDate` ou `endDate` fora do formato `yyyy-MM-dd` ou data inexistente (ex.: `2026-13-40`, `2026-02-30`)
- `startDate` posterior a `endDate`
- Intervalo maior que **366 dias** (contando ambas as extremidades)

### Request (exemplo)

```text
GET /api/revenue/daily?startDate=2026-06-01&endDate=2026-06-07
```

### Response 200 OK

```json
{
  "startDate": "2026-06-01",
  "endDate": "2026-06-07",
  "totalRevenue": 1250.50,
  "totalOrders": 18,
  "days": [
    { "date": "2026-06-01", "revenue": 320.00, "orderCount": 5 },
    { "date": "2026-06-02", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-03", "revenue": 930.50, "orderCount": 13 },
    { "date": "2026-06-04", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-05", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-06", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-07", "revenue": 0, "orderCount": 0 }
  ]
}
```

| Campo | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `startDate` | string (`yyyy-MM-dd`) | sim | Eco do parâmetro validado |
| `endDate` | string (`yyyy-MM-dd`) | sim | Eco do parâmetro validado |
| `totalRevenue` | number | sim | Soma de `days[].revenue` |
| `totalOrders` | number | sim | Soma de `days[].orderCount` |
| `days` | array | sim | Um item por **cada dia** do intervalo (inclusive), mesmo sem pedido |
| `days[].date` | string (`yyyy-MM-dd`) | sim | Dia civil UTC |
| `days[].revenue` | number | sim | `SUM(quantity * unitPrice)` dos pedidos `created` daquele dia; `0` se nenhum |
| `days[].orderCount` | number | sim | Contagem de pedidos `created` distintos daquele dia; `0` se nenhum |

**Regras de cálculo**:

- Considera apenas pedidos com `status = 'created'`.
- Considera pedidos do seed (módulo 001) e pedidos criados via `POST /api/orders` (módulo 002).
- Intervalo **inclusivo**: `startDate` e `endDate` participam do resultado.
- Dia civil interpretado em **UTC**, consistente com `orders.created_at`.

**Efeitos colaterais**: nenhum — leitura pura, não altera pedidos, reservas, estoque ou outbox.

### Response 400 — validação

```json
{
  "error": "startDate is required."
}
```

Exemplos de mensagens (implementação pode variar texto, semântica fixa):

| Situação | Exemplo de mensagem |
| --- | --- |
| `startDate` ausente | `startDate is required.` |
| `endDate` ausente | `endDate is required.` |
| Data inválida | `startDate must be a valid date in yyyy-MM-dd format.` |
| `startDate > endDate` | `startDate must not be after endDate.` |
| Intervalo > 366 dias | `date range must not exceed 366 days.` |

---

## Erros gerais (módulo 003)

| Status | Quando |
| --- | --- |
| 200 | Consulta válida (com ou sem pedidos no intervalo) |
| 400 | Parâmetro ausente, inválido, invertido ou intervalo excessivo |
| 500 | Falha de banco ou erro não tratado |
