# Data Model — Módulo 002

**Data**: 2026-07-03  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Evolução do modelo do módulo 001. Diagrama completo:

```text
products (1) ──< inventory_units (N)
    │                    │
    │                    │ reserva (status available → reserved)
    │                    v
    │            order_reservation_units (N) >── (1) orders
    │                                              │
    └──< order_items (N) >─────────────────────────┤
                                                   │
orders (1) ──< order_processing_events             │
```

## Alterações em tabelas existentes

### `orders` — nova coluna

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `customer_name` | `VARCHAR(200)` | NULL | Nome opcional do cliente |

Demais colunas inalteradas (`id`, `created_at`, `status`). Status inicial em `POST`: **`created`**.

### `products` — sem alteração de schema

| Coluna | Nota módulo 002 |
| --- | --- |
| `stock_quantity` | **Legado/indicador**; não usado na reserva concorrente. Pode permanecer desatualizado após vendas via `POST` (aceitável) ou sincronizado opcionalmente no futuro — **fora de escopo** sincronizar neste módulo. |

### `order_items` — inalterada

Continua snapshot de `unit_price` e `quantity` por linha de produto no pedido.

---

## Nova tabela `inventory_units`

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `id` | `BIGINT` AUTO_INCREMENT | PK | Identificador da unidade |
| `product_id` | `INT` | NOT NULL, FK → `products.id` | Produto |
| `status` | `VARCHAR(16)` | NOT NULL | `available` ou `reserved` |

**Regras**:
- Uma linha = uma unidade física/lógica vendável.
- Reserva: transição `available` → `reserved` dentro da transação de criação.
- Fonte de verdade para “tem estoque?” na criação de pedidos.

**Índices**:

| Nome | Colunas | Motivo |
| --- | --- | --- |
| `IX_inventory_units_product_status_id` | `(product_id, status, id)` | `SKIP LOCKED` + `ORDER BY id` + `LIMIT n` |

FK `product_id` → `products.id` **`ON DELETE RESTRICT`**.

---

## Nova tabela `order_reservation_units`

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `id` | `BIGINT` AUTO_INCREMENT | PK | Identificador |
| `order_id` | `BIGINT` | NOT NULL, FK → `orders.id` | Pedido |
| `inventory_unit_id` | `BIGINT` | NOT NULL, FK → `inventory_units.id` | Unidade reservada |

**Regras**:
- Uma linha por unidade reservada (quantidade 2 ⇒ 2 linhas).
- `inventory_unit_id` **UNIQUE** — mesma unidade não pode pertencer a dois pedidos.

**Índices**:

| Nome | Colunas |
| --- | --- |
| `IX_order_reservation_units_order_id` | `order_id` |
| `UQ_order_reservation_units_inventory_unit_id` | `inventory_unit_id` UNIQUE |

FK `order_id` → `orders.id` **`ON DELETE CASCADE`** (facilita testes).  
FK `inventory_unit_id` → `inventory_units.id` **`ON DELETE RESTRICT`**.

---

## Nova tabela `order_processing_events` (outbox)

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `id` | `BIGINT` AUTO_INCREMENT | PK | Identificador |
| `order_id` | `BIGINT` | NOT NULL, FK → `orders.id` | Pedido |
| `event_type` | `VARCHAR(64)` | NOT NULL | Ex.: `OrderCreated` |
| `status` | `VARCHAR(16)` | NOT NULL | `pending` neste módulo |
| `payload` | `JSON` | NOT NULL | Ex.: `{"orderId":42}` |
| `created_at` | `DATETIME(6)` | NOT NULL | UTC |

**Regras**:
- Inserido na mesma transação do pedido.
- Nenhuma transição `pending` → `processed` neste módulo.

**Índices**:

| Nome | Colunas | Motivo |
| --- | --- | --- |
| `IX_order_processing_events_status_created` | `(status, created_at)` | Consumo futuro pelo Node |
| `IX_order_processing_events_order_id` | `order_id` | Lookup por pedido |

---

## Entidades EF (C#) — adições

```text
InventoryUnit       { long Id, int ProductId, string Status }
OrderReservationUnit { long Id, long OrderId, long InventoryUnitId }
OrderProcessingEvent { long Id, long OrderId, string EventType, string Status, string Payload, DateTime CreatedAt }
Order               { + string? CustomerName }
```

Navigation properties opcionais; **não** usadas na transação Dapper.

---

## Backfill (dados iniciais)

| Momento | Ação |
| --- | --- |
| Startup pós-migration | Se `inventory_units` vazia, inserir `products.stock_quantity` linhas `available` por produto |
| Seed histórico (5k pedidos) | **Não** altera `inventory_units` retroativamente |
| Novos produtos (futuro) | Fora de escopo; módulo 002 assume produtos do seed 001 |

---

## Transições de estado

### `inventory_units.status`

```text
available ──(POST /api/orders reserva)──> reserved
```

Sem transição reversa neste módulo (cancelamento/devolução = módulo futuro).

### `order_processing_events.status`

```text
(persistido como) pending ──(módulo Node futuro)──> processed / failed
```

### `orders.status`

Novos pedidos via `POST`: **`created`** (mesmo valor do seed majoritário).

---

## Volume esperado pós-backfill (dev)

| Entidade | Ordem de grandeza |
| --- | --- |
| `inventory_units` | ~Σ `stock_quantity` ≈ 50 × ~5500 ≈ **275k linhas** |
| `order_reservation_units` | cresce com cada `POST` (1 linha por unidade vendida) |
| `order_processing_events` | 1 linha `pending` por pedido criado via `POST` |

Testes de concorrência usam produtos/estoque controlados inseridos no próprio teste para evitar dependência do volume total.
