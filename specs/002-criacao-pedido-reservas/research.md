# Research — Módulo 002

**Data**: 2026-07-03  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## R1 — Modelo de reserva: unidades vs. contador

**Decision**: Tabela `inventory_units` com uma linha por unidade vendável (`status`: `available` | `reserved`); reserva via `SELECT ... FOR UPDATE SKIP LOCKED` + `UPDATE status`; ligação pedido↔unidade em `order_reservation_units`.

**Rationale**: Alinha ao artigo Shopify sobre reservas por linhas bloqueáveis; demonstra `SKIP LOCKED` de forma visível na demo; evita race condition de `UPDATE stock_quantity = stock_quantity - 1` sem lock adequado.

**Alternatives considered**:
- *Decremento em `products.stock_quantity`*: mais simples, mas não demonstra `SKIP LOCKED` nem linhas concorrentes — rejeitado pela spec.
- *Tabela de “slots” por produto com contador versionado (optimistic locking)*: menos linhas, mais difícil de explicar presencialmente.

---

## R2 — Backfill de unidades a partir do módulo 001

**Decision**: Após migration, `InventoryUnitsBackfill` executado no startup (junto a migrate/seed) com guard `if (await db.InventoryUnits.AnyAsync()) return;`. Para cada produto, inserir `stock_quantity` linhas em `inventory_units` com `status = 'available'`. Pedidos históricos do seed **não** consomem unidades retroativamente.

**Rationale**: Base existente já tem `stock_quantity` alto (1k–10k por produto); backfill único materializa estoque vendável sem reescrever seed do módulo 001. Guard idempotente como no `DatabaseSeeder`.

**Alternatives considered**:
- *Backfill só via SQL na migration*: funciona, mas lógica C# reutiliza EF e fica testável; preferido em lotes via `ExecuteSqlRaw` ou bulk insert.
- *Reduzir `stock_quantity` no seed para acelerar backfill*: quebraria números já documentados no módulo 001; rejeitado.

**Nota operacional**: primeiro startup após módulo 002 pode levar dezenas de segundos (50 produtos × ~5k unidades médias ≈ 250k linhas). Aceitável em dev; testes usam seed menor ou backfill proporcional ao `StockQuantity` do perfil teste.

---

## R3 — Transação de criação (Dapper + MySqlConnection)

**Decision**: Um método estático/co-located `CreateOrderTransaction.ExecuteAsync(MySqlConnection, request, ct)` abre transação explícita (`connection.BeginTransaction()`), define isolamento **READ COMMITTED** (padrão InnoDB; comentário no código), executa reserva → insert pedido/itens → outbox → commit. Validação estrutural do payload **antes** de abrir transação; validação de produtos existentes **dentro** da transação após ordenar itens por `product_id`.

**Rationale**: Mantém SQL transacional junto ao controller (mesmo padrão de `OrdersQueries.cs`); transação curta só com I/O de banco; rollback automático em 409.

**Alternatives considered**:
- *EF Core `SaveChanges` dentro de transação*: mistura ORM na hot path concorrente; rejeitado — Dapper na escrita crítica.
- *Service class + interface `IOrderCreator`*: cerimônia desnecessária para uma fatia.

**Fluxo SQL (resumo)**:

```text
BEGIN;
  -- por cada product_id ASC:
  SELECT id FROM inventory_units
  WHERE product_id = @pid AND status = 'available'
  ORDER BY id
  LIMIT @qty
  FOR UPDATE SKIP LOCKED;
  -- se rows.Count < qty → ROLLBACK → 409
  INSERT INTO orders (...);
  INSERT INTO order_items (...);  -- agrupado por produto, unit_price do products
  UPDATE inventory_units SET status = 'reserved' WHERE id IN (...);
  INSERT INTO order_reservation_units (...);
  INSERT INTO order_processing_events (..., status='pending', payload=JSON);
COMMIT;
```

---

## R4 — Validação de payload (400)

**Decision**: Validação em C# no controller antes da transação:

| Regra | Erro |
| --- | --- |
| `items` null ou vazio | 400 |
| `quantity` ≤ 0 ou não inteiro | 400 |
| `productId` ≤ 0 | 400 |
| `productId` duplicado no mesmo payload | 400 |
| JSON malformado | 400 (model binding) |

Produto inexistente: verificar dentro da transação com `SELECT id FROM products WHERE id IN (...)`; se faltar algum → rollback → **400** (erro de cliente, não conflito de estoque).

**Rationale**: Duplicata é erro de contrato (spec); produto inexistente é 400, estoque insuficiente é 409.

---

## R5 — `customerName` opcional

**Decision**: Campo opcional no request; omitido, null ou string vazia/`whitespace` → persistir `NULL` em `orders.customer_name`.

**Rationale**: Spec e input do usuário; não bloqueia criação.

---

## R6 — Outbox local

**Decision**: Tabela `order_processing_events` com colunas mínimas: `order_id`, `event_type` (`OrderCreated`), `status` (`pending`), `payload` JSON (`{"orderId":123}`), `created_at`. Inserção na mesma transação do pedido. Nenhum worker neste módulo.

**Rationale**: Prepara módulo Node sem RabbitMQ/Redis; padrão outbox transacional.

**Alternatives considered**:
- *Canal in-process / Channel*: fora de escopo e viola “somente banco”.

---

## R7 — Resposta 201

**Decision**: Reutilizar shape `OrderResponse` do módulo 001 (`id`, `createdAt`, `status`, `total`, `items[]`). Após commit, montar resposta com query Dapper existente (`OrderById` + itens) ou retorno montado a partir dos dados inseridos.

**Rationale**: Compatibilidade com `GET /api/orders/{id}` e contrato único.

Header: `Location: /api/orders/{id}` (opcional mas recomendado).

---

## R8 — Teste de concorrência

**Decision**: Teste `CreateOrder_ConcurrentRequests_DoNotOverbook` insere produto de teste com **U** unidades (ex.: 5) via SQL direto no teste, dispara **M** tasks paralelas (`Task.WhenAll`) cada uma pedindo quantidade 1, asserta:

- Soma de `quantity` nos pedidos 201 ≤ U
- Unidades `available` + `reserved` = U (conservação)
- Nenhuma unidade em dois pedidos (`order_reservation_units.inventory_unit_id` único)

**Rationale**: Prova comportamento sob contenção real MySQL; não depende do seed massivo.

---

## R9 — Preservação dos endpoints do módulo 001

**Decision**: Nenhuma alteração de contrato em GET; migration aditiva; teste de regressão `Regression_Module001_ReadEndpointsStillWork` na mesma collection Testcontainers.

**Rationale**: AC-013 da spec.

---

## R10 — Índices para SKIP LOCKED

**Decision**: Índice composto `IX_inventory_units_product_status_id` em `(product_id, status, id)` para acelerar `WHERE product_id = ? AND status = 'available' ORDER BY id LIMIT n FOR UPDATE SKIP LOCKED`.

**Rationale**: Evita full scan com centenas de milhares de linhas pós-backfill.

---

## Referências

- [Scaling inventory reservations — Shopify Engineering](https://shopify.engineering/scaling-inventory-reservations)
- MySQL 8 `SELECT ... FOR UPDATE SKIP LOCKED` (InnoDB, READ COMMITTED)
