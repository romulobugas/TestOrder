# Tasks: Módulo 002 — Criação de Pedido com Reservas Concorrentes

**Input**: Design documents from `specs/002-criacao-pedido-reservas/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, quickstart.md

**Tests**: Incluídos — módulo exige suíte de integração com Testcontainers + MySQL real.

**Organization**: Fases incrementais; user stories all P1 e interdependentes no mesmo endpoint `POST /api/orders`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência de tarefas incompletas)
- **[USn]**: User story de referência: US1=sucesso, US2=validação 400, US3=estoque 409, US4=concorrência
- Caminhos de arquivo explícitos

---

## Phase 1: Preflight

**Goal**: Proteger módulo 001 — confirmar build e testes verdes antes de qualquer alteração.

**Independent Test**: `dotnet build` + `dotnet test` passam sem alteração de código.

---

- [x] T001 Validar build e testes do módulo 001 (`dotnet build TestOrder.slnx && dotnet test TestOrder.slnx`)

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar qualquer API local; confirmar 17/17 testes e build verde antes de qualquer mudança de código. |
| **Permitidos** | Nenhum arquivo alterado |
| **Proibidos** | Alterações de código |
| **Pronto quando** | Build + testes passam |
| **Validação** | `dotnet build TestOrder.slnx && dotnet test TestOrder.slnx` |
| **Paralelo** | Não — primeiro passo obrigatório |

**Checkpoint Phase 1**: Baseline verde confirmada.

---

## Phase 2: Schema EF (entidades, DbContext, migration)

**Goal**: Evoluir schema com novas tabelas e coluna `customer_name`, sem quebrar módulo 001.

**Independent Test**: `dotnet build TestOrder.slnx` compila; migration gerada; EF model snapshot coerente.

---

- [x] T002 [P] Adicionar `CustomerName` nullable à entidade `Order` em `src/TestOrder.Api/Data/Entities/Order.cs`

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | `public string? CustomerName { get; set; }` |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/Order.cs` |
| **Proibidos** | Alterar colunas existentes |
| **Pronto quando** | Propriedade nullable existe, compila |
| **Paralelo** | Sim — independente de T003–T005 |

---

- [x] T003 [P] Criar entidade `InventoryUnit` em `src/TestOrder.Api/Data/Entities/InventoryUnit.cs`

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | `{ long Id, int ProductId, string Status }` — conforme data-model.md |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/InventoryUnit.cs` |
| **Proibidos** | Navigation properties complexas |
| **Pronto quando** | Classe compila |
| **Paralelo** | Sim |

---

- [x] T004 [P] Criar entidade `OrderReservationUnit` em `src/TestOrder.Api/Data/Entities/OrderReservationUnit.cs`

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | `{ long Id, long OrderId, long InventoryUnitId }` — FK + unique constraint |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/OrderReservationUnit.cs` |
| **Proibidos** | — |
| **Pronto quando** | Classe compila |
| **Paralelo** | Sim |

---

- [x] T005 [P] Criar entidade `OrderProcessingEvent` em `src/TestOrder.Api/Data/Entities/OrderProcessingEvent.cs`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | `{ long Id, long OrderId, string EventType, string Status, string Payload, DateTime CreatedAt }` |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/OrderProcessingEvent.cs` |
| **Proibidos** | — |
| **Pronto quando** | Classe compila |
| **Paralelo** | Sim |

---

- [x] T006 Registrar DbSets e configurar mapeamentos em `src/TestOrder.Api/Data/TestOrderDbContext.cs`

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | Adicionar `DbSet<InventoryUnit>`, `DbSet<OrderReservationUnit>`, `DbSet<OrderProcessingEvent>`. Configurar snake_case, FKs, índices compostos (`IX_inventory_units_product_status_id`, `UQ_order_reservation_units_inventory_unit_id`, `IX_order_processing_events_status_created`, `IX_order_processing_events_order_id`). Mapear `Order.CustomerName` → `customer_name`. |
| **Permitidos** | `src/TestOrder.Api/Data/TestOrderDbContext.cs` |
| **Proibidos** | Alterar mapeamentos existentes do módulo 001 |
| **Pronto quando** | Compila; modelo EF coerente com data-model.md |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende T002–T005 |

---

- [x] T007 Gerar migration `AddInventoryAndOutbox` via `dotnet ef migrations add`

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar `dotnet ef migrations add AddInventoryAndOutbox --project src/TestOrder.Api`. Revisar migration gerada: coluna `customer_name` nullable em `orders`; tabelas `inventory_units`, `order_reservation_units`, `order_processing_events` com índices. |
| **Permitidos** | `src/TestOrder.Api/Migrations/<timestamp>_AddInventoryAndOutbox.cs` |
| **Proibidos** | Migrations manuais sem EF |
| **Pronto quando** | `dotnet build` compila; migration aplicável |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende T006 |

**Checkpoint Phase 2**: Schema evolui; build verde; testes do módulo 001 continuam passando (schema novo não quebra dados existentes).

---

## Phase 3: Backfill de unidades

**Goal**: Materializar linhas `available` em `inventory_units` a partir do `stock_quantity` de cada produto, idempotente.

**Independent Test**: Após startup, `SELECT COUNT(*) FROM inventory_units WHERE status = 'available'` = Σ products.stock_quantity.

---

- [x] T008 Criar `InventoryUnitsBackfill.RunAsync` em `src/TestOrder.Api/Data/Seed/InventoryUnitsBackfill.cs`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | Método estático idempotente: `if (await db.InventoryUnits.AnyAsync()) return;`. Para cada produto, inserir `stock_quantity` linhas com `status = 'available'` em lotes (5.000 por batch). Usar `ExecuteSqlRaw` bulk ou EF `AddRange` + `SaveChanges`. |
| **Permitidos** | `src/TestOrder.Api/Data/Seed/InventoryUnitsBackfill.cs` |
| **Proibidos** | Alterar `DatabaseSeeder.cs` |
| **Pronto quando** | Compila; guard idempotente funciona |
| **Paralelo** | Não — depende T007 (schema precisa existir) |

---

- [x] T009 Chamar `InventoryUnitsBackfill.RunAsync` em `src/TestOrder.Api/Program.cs` após migrate/seed

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | Adicionar `await InventoryUnitsBackfill.RunAsync(db);` após `DatabaseSeeder.SeedAsync(db, builder.Configuration);`. Manter ordem: Migrate → Seed → Backfill. |
| **Permitidos** | `src/TestOrder.Api/Program.cs` |
| **Proibidos** | Alterar lógica de Migrate ou Seed existente |
| **Pronto quando** | API sobe e backfill executa na primeira vez; segunda execução não duplica |
| **Validação** | `dotnet build TestOrder.slnx`; subir API localmente e conferir COUNT |
| **Paralelo** | Não — depende T008 |

**Checkpoint Phase 3**: Build verde; testes módulo 001 passam; backfill idempotente preenchendo inventory_units.

---

## Phase 4: Contrato POST e validação (US1, US2)

**Goal**: Expor `POST /api/orders` com model binding, validação estrutural (400), sem lógica transacional ainda.

**Independent Test**: POST com itens vazios → 400; POST com produto duplicado → 400. Build verde.

---

- [x] T010 [P] Criar records de request em `src/TestOrder.Api/Models/Requests/CreateOrderRequest.cs`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | `record CreateOrderRequest(string? CustomerName, IReadOnlyList<CreateOrderItemRequest> Items);` e `record CreateOrderItemRequest(int ProductId, int Quantity);` |
| **Permitidos** | `src/TestOrder.Api/Models/Requests/CreateOrderRequest.cs` |
| **Proibidos** | FluentValidation, DataAnnotations |
| **Pronto quando** | Compila |
| **Paralelo** | Sim — independente de T011 |

---

- [x] T011 [US2] Implementar `POST /api/orders` com validação 400 em `src/TestOrder.Api/Controllers/OrdersController.cs`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | `[HttpPost]` no `OrdersController` existente. Validar pré-transação: items null/vazio, qty ≤ 0, productId ≤ 0, productId duplicado. Retornar `BadRequest(new ErrorResponse(...))`. Por enquanto, retornar 501 NotImplemented após validação passar (transação vem na fase 5). |
| **Permitidos** | `src/TestOrder.Api/Controllers/OrdersController.cs` |
| **Proibidos** | Lógica de banco na validação estrutural; Minimal APIs |
| **Pronto quando** | POST com payload inválido → 400; POST válido → 501 (temporário) |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende T010 |

**Checkpoint Phase 4**: Validação 400 funcional; build verde.

---

## Phase 5: SQL transacional Dapper (US1, US3, US4)

**Goal**: Implementar a transação de criação/reserva/outbox com `FOR UPDATE SKIP LOCKED`, cobrindo 201/400(produto inexistente)/409.

**Independent Test**: POST válido com estoque → 201; POST com qty > estoque → 409; reservas coerentes; outbox pending.

---

- [x] T012 Criar `CreateOrderCommands` em `src/TestOrder.Api/Controllers/CreateOrderCommands.cs`

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | Classe estática com SQL consts e método `ExecuteAsync(MySqlConnection, CreateOrderRequest, CancellationToken)`. Fluxo: open → BEGIN (READ COMMITTED) → ordenar items por productId ASC → verificar produtos existem (SELECT IN) → por cada produto: `SELECT id FROM inventory_units WHERE product_id=@pid AND status='available' ORDER BY id LIMIT @qty FOR UPDATE SKIP LOCKED` → se count < qty: ROLLBACK → 409 → INSERT orders, order_items, UPDATE inventory_units SET reserved, INSERT order_reservation_units, INSERT order_processing_events (pending, OrderCreated, JSON) → COMMIT → retornar orderId + createdAt. Comentários no SQL de concorrência. |
| **Permitidos** | `src/TestOrder.Api/Controllers/CreateOrderCommands.cs` |
| **Proibidos** | EF na transação; repositories; interfaces |
| **Pronto quando** | Compila; SQL documentado nos consts |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende T009 (backfill existe para ter unidades) |

---

- [x] T013 [US1] Integrar `CreateOrderCommands` no `POST` do `OrdersController` em `src/TestOrder.Api/Controllers/OrdersController.cs`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | Substituir 501 temporário por chamada a `CreateOrderCommands.ExecuteAsync`. Tratar retorno: sucesso → montar OrderResponse (reutilizar query existente `OrderById` + itens) → `CreatedAtAction(201)` com Location header. Produto inexistente → 400. Estoque insuficiente → 409. |
| **Permitidos** | `src/TestOrder.Api/Controllers/OrdersController.cs` |
| **Proibidos** | Quebrar GET existentes |
| **Pronto quando** | POST funcional: 201/400/409 conforme contrato |
| **Validação** | `dotnet build TestOrder.slnx`; teste manual `curl POST` |
| **Paralelo** | Não — depende T012 |

**Checkpoint Phase 5**: POST completo; build verde; testes módulo 001 ainda passam; endpoints GET intactos.

---

## Phase 6: Testes de integração

**Goal**: Suíte de 7 testes com Testcontainers + MySQL real cobrindo US1–US4 e regressão do módulo 001.

**Independent Test**: `dotnet test TestOrder.slnx` — todos passam.

---

- [x] T014 [US1] Teste `CreateOrder_Success_Returns201AndPersistsOrder` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | POST payload válido → 201; GET /api/orders/{id} reflete pedido; total = Σ qty×price; verificar via Dapper que `order_reservation_units` tem count = Σ qty e que `inventory_units` reservadas. **Incluir casos de `customerName` opcional**: (a) request **sem** `customerName` → 201 e pedido persistido; (b) request com `customerName` vazio (`""`) ou omitido → via SQL assert `orders.customer_name IS NULL` (conforme contrato). |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Proibidos** | SQLite/InMemory |
| **Pronto quando** | Teste passa contra MySQL real Testcontainers |
| **Paralelo** | Não — mesmo arquivo que T015–T020; implementar em sequência |

---

- [x] T015 [US2] Teste `CreateOrder_InvalidPayload_Returns400` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Theory/InlineData: items vazio, qty 0, qty negativa, **productId ≤ 0**, productId inexistente, **JSON malformado/corpo inválido** (ex.: string no lugar de objeto, JSON truncado). Todos → 400 + `error`. **Sem efeitos colaterais (AC-006)**: antes/depois de cada caso, assert via SQL que contagens permanecem inalteradas em `orders`, `inventory_units`, `order_reservation_units` e `order_processing_events`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T014–T020; implementar em sequência |

---

- [x] T016 [US2] Teste `CreateOrder_DuplicateProduct_Returns400` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Payload com mesmo productId em dois itens → 400 + `error` com mensagem de duplicata. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T014–T020; implementar em sequência |

---

- [x] T017 [US3] Teste `CreateOrder_InsufficientStock_Returns409` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | Inserir produto de teste com estoque controlado (ex: 2 unidades via SQL direto). POST qty=5 → 409. Confirmar via SQL: **nenhuma reserva parcial** (`order_reservation_units` inalterado para a tentativa); unidades continuam `available`; **nenhuma linha nova em `order_processing_events`** (rollback total — sem pedido, reserva parcial ou outbox). |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T014–T020; implementar em sequência |

---

- [x] T018 [US1] Teste `CreateOrder_WritesPendingOutboxEvent` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | POST sucesso → via Dapper: SELECT event_type, status, payload FROM order_processing_events WHERE order_id = @id. Assert: event_type='OrderCreated', status='pending', payload contém orderId. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T014–T020; implementar em sequência |

---

- [x] T019 [US4] Teste `CreateOrder_ConcurrentRequests_DoNotOverbook` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T019**
| Campo | Valor |
| --- | --- |
| **Descrição** | Inserir produto de teste com U=5 unidades. Disparar M=10 tasks paralelas via Task.WhenAll, cada POST qty=1. Assert: count(201) ≤ 5; count(409) ≥ 5; total reservado = count(201); nenhuma unidade aparece em dois pedidos (UNIQUE constraint). |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Proibidos** | — |
| **Pronto quando** | Teste passa 100% das vezes |
| **Paralelo** | Não — mesmo arquivo que T014–T018; implementar após T018 |

---

- [x] T020 Teste `Regression_Module001_ReadEndpointsStillWork` em `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs`

**Detalhe T020**
| Campo | Valor |
| --- | --- |
| **Descrição** | Smoke: GET /api/products → 200, não vazio; GET /api/orders?page=1&pageSize=5 → 200, totalCount ≥ esperado; GET /api/orders/1 → 200 ou primeiro pedido existente. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T014–T019; implementar em sequência |

---

- [x] T021 Validar build e suíte completa (`dotnet build TestOrder.slnx && .\scripts\test.ps1`)

**Detalhe T021**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar qualquer API local antes. Executar build + `test.ps1`. Todos os testes módulo 001 (17) + módulo 002 (7) = **24+** devem passar. |
| **Permitidos** | Nenhum arquivo alterado |
| **Pronto quando** | Exit code 0 em ambos |
| **Validação** | `dotnet build TestOrder.slnx && .\scripts\test.ps1` |
| **Paralelo** | Não — depende T014–T020 |

**Checkpoint Phase 6**: Suíte completa verde; POST funcional; concorrência validada; regressão ok.

---

## Phase 7: Documentação pós-implementação

**Goal**: Atualizar artefatos de documentação com fatos reais pós-implementação.

**Independent Test**: Revisão humana dos documentos.

---

- [x] T022 [P] Atualizar `AI_NOTES.md` com seção Módulo 002

**Detalhe T022**
| Campo | Valor |
| --- | --- |
| **Descrição** | Status concluído; inventory_units vs decremento cego; fluxo transacional; resultado teste concorrência; backfill tempo e contagem; IA recusadas (services, repos); prompts Spec Kit. |
| **Permitidos** | `AI_NOTES.md` |
| **Proibidos** | Módulos 003+ |
| **Pronto quando** | Seção 002 preenchida com fatos reais |
| **Paralelo** | Sim |

---

- [x] T023 [P] Atualizar `docs/PRESENTATION_GUIDE.md` com seção Módulo 002

**Detalhe T023**
| Campo | Valor |
| --- | --- |
| **Descrição** | Referências na tabela de código: `CreateOrderCommands.cs`, migration, backfill, teste concorrente. Roteiro demo: curl POST → SQL reserva/outbox. Trecho SKIP LOCKED. Link Shopify. Tabela pass/fail. |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Paralelo** | Sim |

---

- [x] T024 Revisar `quickstart.md` com comandos finais validados em `specs/002-criacao-pedido-reservas/quickstart.md`

**Detalhe T024**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar porta, exemplos curl reais e números de testes. |
| **Permitidos** | `specs/002-criacao-pedido-reservas/quickstart.md` |
| **Paralelo** | Não — após validação T021 |

---

- [x] T025 Validação final obrigatória do módulo

**Detalhe T025**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar checklist completo de aceite: build, testes, POST manual 201/400/409, SQL confirma reservas e outbox, GET módulo 001 intacto. |
| **Permitidos** | Nenhuma alteração de código |
| **Pronto quando** | Todos os comandos abaixo passam |
| **Paralelo** | Não — último passo |

**Validações finais obrigatórias**:

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

**Checkpoint Phase 7**: Módulo 002 completo.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 (preflight)
T002,T003,T004,T005 (parallel — entidades) → T006 → T007
T007 → T008 → T009
T009 + T010 (parallel) → T011 → T012 → T013
T013 → T014 → T015 → T016 → T017 → T018 → T019 → T020 → T021
T021 → T022,T023 (parallel) → T024 → T025
```

### User Story Mapping

| Story | Prioridade | Tarefas principais |
| --- | --- | --- |
| US1 — Criar pedido com sucesso | P1 | T010–T013, T014, T018 |
| US2 — Rejeitar payload inválido | P1 | T011, T015, T016 |
| US3 — Estoque insuficiente 409 | P1 | T012–T013, T017 |
| US4 — Reserva concorrente | P1 | T012–T013, T019 |

### Parallel Opportunities

- **T002 + T003 + T004 + T005** — entidades em arquivos separados
- **T010 + T008** — request records vs backfill (T010 não depende de schema se não compila contra DB)
- **T014 → T020** — testes em **sequência** no mesmo arquivo `CreateOrderEndpointTests.cs` (não paralelizar edições)
- **T022 + T023** — docs em paralelo

---

## MVP Scope

**MVP mínimo demonstrável**: Phase 1–5 (T001–T013) + validação manual curl.

POST funcional com 201/400/409 é a fatia mínima de valor; testes (Phase 6) são obrigatórios para aceite do módulo mas podem ser demonstrados após o POST funcionar.

Módulo **completo** exige Phase 6–7 (T014–T025).

---

## Implementation Strategy

### MVP First (US1 funcional)

1. Phase 1: Preflight → build verde
2. Phase 2: Schema → migration aplicável
3. Phase 3: Backfill → unidades materializadas
4. Phase 4: Validação 400 → POST rejeita inválidos
5. Phase 5: Transação → POST 201/409 funcional
6. **STOP and VALIDATE**: curl manual (201, 400, 409)

### Incremental Delivery

1. Setup + Schema + Backfill → infra pronta
2. Validação 400 → testável independente
3. Transação completa → US1+US3+US4 testáveis
4. Suíte testes → prova automatizada
5. Docs → apresentação pronta

---

## Critérios de pronto por User Story

| Story | Critério | Evidência |
| --- | --- | --- |
| US1 | POST válido → 201; GET by id reflete; outbox pending; `customerName` opcional | T014, T018 |
| US2 | 5+ variantes inválidas → 400; sem side effects em orders/units/reservas/outbox | T015, T016 |
| US3 | qty > estoque → 409; zero reserva parcial; zero outbox | T017 |
| US4 | 10 requests paralelas; reservas ≤ estoque; 0 overbooking | T019 |

---

## Notes

- Parar API local antes de `dotnet build`/`dotnet test` no Windows (exe fica bloqueado).
- Testes de concorrência (T019) usam produto + estoque inseridos via SQL direto no teste, independente do seed massivo.
- Backfill pode demorar ~30–60s na primeira execução dev (275k linhas); aceitável.
- `customerName` não exposto na response 201 (campo DB only neste módulo).
- Não avançar para módulo 003/004 até T025 passar.
- Commit sugerido após cada fase ou par lógico (T002–T007 junto, T012–T013 junto).
