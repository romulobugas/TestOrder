# Plano Técnico: Módulo 002 — Criação de Pedido com Reservas Concorrentes

**Branch**: `002-criacao-pedido-reservas` | **Data**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Especificação em `specs/002-criacao-pedido-reservas/spec.md`

---

## Summary

Implementar **`POST /api/orders`** no backend existente (ASP.NET Core MVC + MySQL 8 Docker), com validação de payload, reserva transacional de estoque via **`inventory_units`** + **`SELECT ... FOR UPDATE SKIP LOCKED`**, persistência de pedido/itens/reservas/outbox em **uma transação READ COMMITTED**, e respostas **201 / 400 / 409**. EF Core evolui schema/migrations/entidades e executa backfill inicial de unidades; **Dapper/MySqlConnector** executa a transação crítica. Endpoints GET do módulo 001 permanecem intactos. Suíte de integração estendida com Testcontainers (**7 testes** em `CreateOrderEndpointTests.cs`, incluindo regressão GET do módulo 001).

**Referência**: [Shopify — Scaling inventory reservations](https://shopify.engineering/scaling-inventory-reservations)

---

## Technical Context

| Item | Valor |
| --- | --- |
| **Language/Version** | C# / .NET 10 (`net10.0`) |
| **Primary Dependencies** | ASP.NET Core MVC, Pomelo EF Core MySQL 9, Dapper 2.x, MySqlConnector |
| **Storage** | MySQL 8 (Docker Compose dev + Testcontainers testes) |
| **Testing** | xUnit, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.MySql |
| **Performance Goals** | POST típico (≤ 5 itens) < 3s demo local; backfill inicial pode levar ~30–60s em dev |
| **Constraints** | Transação curta; sem fila externa; sem Minimal APIs; sem camadas ceremoniais |
| **Scale/Scope** | ~275k `inventory_units` pós-backfill dev; testes de concorrência com estoque controlado |

---

## Constitution Check

*GATES: `.cursor/rules/testorder.mdc` + spec módulo 002.*

| Gate | Status | Notas |
| --- | --- | --- |
| MVC controllers (sem Minimal APIs) | ✅ PASS | `POST` em `OrdersController` |
| EF Core schema/migrations/entidades | ✅ PASS | Novas tabelas + `customer_name` |
| Dapper/SQL transação crítica | ✅ PASS | `CreateOrderCommands.cs` adjacente ao controller |
| MySQL 8 + SKIP LOCKED | ✅ PASS | InnoDB READ COMMITTED |
| Sem RabbitMQ/Kafka/Redis | ✅ PASS | Outbox só em tabela |
| Sem Clean Architecture/DDD/CQRS/MediatR | ✅ PASS | Fatia vertical no controller + SQL estático |
| Sem repositories/AutoMapper/interfaces genéricas | ✅ PASS | Records request/response simples |
| Preservar GET módulo 001 | ✅ PASS | Sem breaking change de contrato |
| Testcontainers MySQL real | ✅ PASS | Proibido SQLite/InMemory |
| Escopo: sem React/Node/faturamento | ✅ PASS | Outbox `pending` apenas |

**Pós-design (Phase 1)**: Nenhuma violação. Três tabelas novas são exigência da spec, não over-engineering.

---

## Project Structure

### Documentação (esta feature)

```text
specs/002-criacao-pedido-reservas/
├── spec.md
├── plan.md                 # este arquivo
├── research.md             # Phase 0
├── data-model.md           # entidades e índices
├── quickstart.md           # validação POST + testes
├── contracts/
│   └── api.md              # POST + referência GET módulo 001
├── checklists/
│   └── requirements.md     # da /speckit-specify
└── tasks.md                # (/speckit-tasks — próximo passo)
```

### Código-fonte — delta sobre módulo 001

```text
F:\repository\TestOrder\
├── src/TestOrder.Api/
│   ├── Program.cs                          # + backfill após migrate (1 linha de chamada)
│   ├── Controllers/
│   │   ├── OrdersController.cs             # + POST CreateOrder
│   │   ├── OrdersQueries.cs                # (existente — leituras)
│   │   └── CreateOrderCommands.cs          # NOVO — SQL transacional + ExecuteAsync
│   ├── Models/
│   │   ├── Responses/ApiResponses.cs       # (existente)
│   │   └── Requests/CreateOrderRequest.cs  # NOVO — records request
│   ├── Data/
│   │   ├── TestOrderDbContext.cs           # + DbSets novas entidades
│   │   ├── Entities/
│   │   │   ├── Order.cs                    # + CustomerName?
│   │   │   ├── InventoryUnit.cs            # NOVO
│   │   │   ├── OrderReservationUnit.cs     # NOVO
│   │   │   └── OrderProcessingEvent.cs     # NOVO
│   │   └── Seed/
│   │       ├── DatabaseSeeder.cs           # (inalterado — pedidos históricos)
│   │       └── InventoryUnitsBackfill.cs   # NOVO — materializa unidades
│   └── Migrations/
│       └── <timestamp>_AddInventoryAndOutbox.cs
└── tests/TestOrder.Api.Tests/
    └── Integration/
        └── CreateOrderEndpointTests.cs     # NOVO — 7 testes (POST + regressão GET módulo 001)
```

**Structure Decision**: Mesma fatia vertical do módulo 001. **Um** arquivo SQL transacional (`CreateOrderCommands.cs`) ao lado de `OrdersQueries.cs`. Sem pasta `Services/`, `Repositories/` ou `Application/`.

---

## Decisões de implementação (10 pontos do escopo)

| # | Decisão | Implementação |
| --- | --- | --- |
| 1 | `POST /api/orders` | `[HttpPost]` em `OrdersController` |
| 2 | `customerName` opcional | Request nullable; persistir `NULL` se omitido/vazio |
| 3 | Validação 400 | Pré-transação: items vazio, qty ≤ 0, productId inválido, duplicata; in-transação: produto inexistente |
| 4 | 409 estoque | Após SKIP LOCKED, `rows.Count < quantity` → rollback |
| 5 | Transação única | pedido + itens + reservas + outbox + update units |
| 6 | READ COMMITTED | Padrão InnoDB; comentário no `BeginTransaction` |
| 7 | Ordem `product_id` ASC | `items.OrderBy(i => i.ProductId)` antes do loop de reserva |
| 8 | SKIP LOCKED | Ver SQL em [research.md](./research.md) R3 |
| 9 | Sem fila externa | Apenas `order_processing_events` |
| 10 | Outbox `pending` | `event_type=OrderCreated`, `payload={"orderId":...}` |

---

## Fluxo POST (sequência)

```text
1. Model bind CreateOrderRequest
2. ValidatePayload() → 400 se falhar
3. connection.OpenAsync()
4. BEGIN TRANSACTION (READ COMMITTED)
5. Ordenar items por productId ASC
6. Verificar products existem (IN @ids) → 400 se faltar
7. Para cada item:
     SELECT id ... FOR UPDATE SKIP LOCKED (LIMIT quantity)
     se count < quantity → ROLLBACK → 409
8. INSERT orders (status='created', customer_name, created_at=UTC)
9. INSERT order_items (agrupado por produto, unit_price snapshot)
10. UPDATE inventory_units SET status='reserved' WHERE id IN (...)
11. INSERT order_reservation_units (uma linha por unidade)
12. INSERT order_processing_events (pending, OrderCreated, JSON payload)
13. COMMIT
14. Montar OrderResponse (query Dapper existente ou dados in-memory)
15. return 201 Created + Location header
```

Comentários no código **somente** nos passos 6–12 (concorrência/transação).

---

## Migration e backfill

### Migration EF `AddInventoryAndOutbox`

- Coluna `orders.customer_name` NULL
- Tabelas `inventory_units`, `order_reservation_units`, `order_processing_events`
- Índices conforme [data-model.md](./data-model.md)

### Backfill `InventoryUnitsBackfill`

Chamado em `Program.cs` após `MigrateAsync`, antes ou depois de `SeedAsync` (ordem: **Migrate → Seed (pedidos) → Backfill unidades** se seed não depende de units; preferir **Migrate → Backfill** antes de aceitar POST, seed de pedidos históricos independente):

```text
MigrateAsync()
DatabaseSeeder.SeedAsync()      // inalterado — 5k pedidos se vazio
InventoryUnitsBackfill.RunAsync() // se inventory_units vazio → N linhas/produto
```

Inserção em lotes (ex.: 5.000 linhas por `ExecuteSqlRaw` bulk) para performance.

---

## Contratos JSON

Detalhamento em [contracts/api.md](./contracts/api.md).

| Endpoint | Novo? | Resumo |
| --- | --- | --- |
| `POST /api/orders` | **Sim** | Request `{ customerName?, items[] }` → 201 Order / 400 / 409 |
| `GET /api/products` | Não | Inalterado |
| `GET /api/orders` | Não | Inalterado |
| `GET /api/orders/{id}` | Não | Inalterado |

**Request records** (`Models/Requests/`):

```csharp
public record CreateOrderRequest(string? CustomerName, IReadOnlyList<CreateOrderItemRequest> Items);
public record CreateOrderItemRequest(int ProductId, int Quantity);
```

---

## Estratégia da suíte de testes

Estender `tests/TestOrder.Api.Tests` — mesma `MySqlContainerFixture` e collection.

| Teste | Assert principal |
| --- | --- |
| `CreateOrder_Success_Returns201AndPersistsOrder` | 201; GET by id; total; reservas count = qty; `customerName` omitido/vazio → `customer_name IS NULL` |
| `CreateOrder_InvalidPayload_Returns400` | items vazio, qty 0/negativa, productId ≤ 0, inexistente, JSON malformado; sem side effects em orders/units/reservas/outbox |
| `CreateOrder_DuplicateProduct_Returns400` | mesmo productId duas vezes |
| `CreateOrder_InsufficientStock_Returns409` | qty > available; sem pedido, reserva parcial ou linha em `order_processing_events`; unidades `available` |
| `CreateOrder_WritesPendingOutboxEvent` | evento pending + payload orderId |
| `CreateOrder_ConcurrentRequests_DoNotOverbook` | paralelo; reservas ≤ estoque inicial |
| `Regression_Module001_ReadEndpointsStillWork` | GET products/orders/orders/{id} → 200 (mesmo arquivo `CreateOrderEndpointTests.cs`) |

**Concorrência**: teste insere produto + N unidades via SQL; estoque pequeno (5); M=10 tasks paralelas qty=1.

**Backfill em testes**: roda no factory startup; testes de 409/concorrência usam produto dedicado com estoque controlado.

---

## Phase 0 & Phase 1 — Artefatos gerados

| Artefato | Status |
| --- | --- |
| [research.md](./research.md) | ✅ |
| [data-model.md](./data-model.md) | ✅ |
| [contracts/api.md](./contracts/api.md) | ✅ |
| [quickstart.md](./quickstart.md) | ✅ |

---

## Documentação pós-implementação (não fazer neste passo)

### `AI_NOTES.md` — seção Módulo 002 (template)

Preencher após implementação:

- Status concluído e dependência do módulo 001
- Por que `inventory_units` vs. decremento cego
- Fluxo transacional em 5 bullets
- Resultado do teste de concorrência (N threads, U unidades, 201/409)
- Backfill: tempo observado, contagem de linhas
- O que a IA sugeriu e foi recusado (services, repositories)
- Prompts Spec Kit usados

### `docs/PRESENTATION_GUIDE.md` — adições

- Linhas na tabela de referências: `CreateOrderCommands.cs`, migration, backfill, teste concorrente
- Roteiro demo 5 min: POST curl → SQL `inventory_units`/`order_reservation_units`/`order_processing_events`
- Trecho SQL SKIP LOCKED comentado
- Link Shopify + adaptação em escala reduzida
- Tabela pass/fail dos 7 testes novos

---

## Complexity Tracking

*Nenhuma violação de constitution a justificar.*

---

## Próximos passos

1. **`/speckit-tasks`** — gerar `tasks.md` com fases: migration → backfill → POST → testes → docs
2. **`/speckit-implement`** — uma fatia por branch/review
3. Não avançar módulo 003 (faturamento) ou React até fechar T* do módulo 002

---

## Referências cruzadas

| Documento | Uso |
| --- | --- |
| [spec.md](./spec.md) | Requisitos e critérios de aceite |
| [research.md](./research.md) | Decisões R1–R10 |
| [data-model.md](./data-model.md) | DDL lógico e índices |
| [contracts/api.md](./contracts/api.md) | Contrato POST |
| [quickstart.md](./quickstart.md) | Validação manual e testes |
| [../001-base-listagem-pedidos/plan.md](../001-base-listagem-pedidos/plan.md) | Base infra/scripts/testes |
