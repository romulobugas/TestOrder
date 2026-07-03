# Data Model — Módulo 001

**Data**: 2026-07-03  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Diagrama relacional

```text
products (1) ──────< order_items (N)
                         │
                         │ N
                         │
                         v
                      orders (1)
```

## Tabela `products`

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `id` | `INT` AUTO_INCREMENT | PK | Identificador |
| `name` | `VARCHAR(200)` | NOT NULL | Nome do produto |
| `unit_price` | `DECIMAL(18,2)` | NOT NULL | Preço de catálogo |
| `stock_quantity` | `INT` | NOT NULL, DEFAULT 0 | Estoque disponível (módulo 002) |

**Regras**:
- `unit_price` ≥ 0
- `stock_quantity` ≥ 0 no seed (valores altos, ex. 1.000–10.000, para não esgotar antes do módulo 002)

## Tabela `orders`

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `id` | `BIGINT` AUTO_INCREMENT | PK | Identificador |
| `created_at` | `DATETIME(6)` | NOT NULL | Data/hora UTC do pedido |
| `status` | `VARCHAR(32)` | NOT NULL | Ex.: `created`, `processed` |

**Regras**:
- Sem coluna `total` persistida — total é derivado dos itens
- `status` no seed: majoritariamente `created`; ~10% `processed` para variedade na demo
- Sem dados de cliente neste módulo (YAGNI)

## Tabela `order_items`

| Coluna | Tipo MySQL | Nulo | Descrição |
| --- | --- | --- | --- |
| `id` | `BIGINT` AUTO_INCREMENT | PK | Identificador da linha |
| `order_id` | `BIGINT` | NOT NULL, FK → `orders.id` | Pedido pai |
| `product_id` | `INT` | NOT NULL, FK → `products.id` | Produto |
| `quantity` | `INT` | NOT NULL | Quantidade comprada |
| `unit_price` | `DECIMAL(18,2)` | NOT NULL | Snapshot do preço na linha |

**Regras**:
- `quantity` > 0
- `unit_price` copiado do produto no seed (pode divergir do catálogo atual em pedidos futuros)
- Índice único composto **não** exigido neste módulo (mesmo produto pode aparecer em linhas separadas se necessário no futuro; seed evita duplicata por pedido)

## Índices mínimos

| Índice | Tabela | Colunas | Motivo |
| --- | --- | --- | --- |
| PK | `products` | `id` | Já implícito |
| PK | `orders` | `id` | Já implícito |
| `IX_orders_created_at` | `orders` | `created_at DESC` | Paginação ordenada |
| PK | `order_items` | `id` | Já implícito |
| `IX_order_items_order_id` | `order_items` | `order_id` | Busca de itens por pedido |
| `IX_order_items_product_id` | `order_items` | `product_id` | Suporte futuro a relatórios |

FK `order_items.order_id` → `orders.id` com `ON DELETE CASCADE` (facilita reset em testes).  
FK `order_items.product_id` → `products.id` com `ON DELETE RESTRICT`.

## Entidades EF (C#)

Mapeamento 1:1 com tabelas; navigation properties opcionais (`Order.Items`, `OrderItem.Product`) apenas se simplificarem seed — **não obrigatórias para leitura Dapper**.

```text
Product   { int Id, string Name, decimal UnitPrice, int StockQuantity }
Order     { long Id, DateTime CreatedAt, string Status }
OrderItem { long Id, long OrderId, int ProductId, int Quantity, decimal UnitPrice }
```

## Estado e transições

Neste módulo não há máquina de estados. `status` é valor informativo no seed e na resposta JSON. Transições reais entram no módulo 005 (Node/outbox).

## Preparação para módulo 002 (sem implementar agora)

- `products.stock_quantity` já populado
- Tipos numéricos compatíveis com decremento transacional
- Nenhuma tabela de reserva/outbox ainda
