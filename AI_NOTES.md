# AI Notes

Este arquivo sera atualizado a cada modulo. O objetivo nao e vender que a IA fez tudo, mas mostrar como ela foi usada com controle, revisao e limites claros.

## Diretrizes iniciais

- Usei IA para estruturar o fluxo de trabalho, organizar prompts e revisar decisoes de arquitetura.
- A decisao humana principal foi manter o sistema simples: ASP.NET Core MVC, poucas pastas, sem Clean Architecture/DDD/CQRS por ritual.
- Minimal APIs foram evitadas por preferencia de organizacao e apresentacao.
- O fluxo de reserva foi inspirado no artigo da Shopify sobre MySQL 8 `FOR UPDATE SKIP LOCKED`, mas sera aplicado de forma menor e didatica.
- O projeto sera construido por modulos com Spec Kit para manter rastreabilidade: spec, plano, tarefas, implementacao e revisao.
- O Spec Kit foi instalado no repositorio para Cursor, Claude Code, Codex CLI e Antigravity. Cursor ficou como integracao default.
- Codex e Antigravity compartilham `.agents/skills` nesta versao do Spec Kit; isso foi documentado em `docs/SPECKIT_SETUP.md`.

## Modulo 001 - Base e listagem de pedidos

**Status: concluido** (T001–T038).

### Infra e ambiente

- **Docker Compose** sobe MySQL 8 (`docker-compose.yml`); nao exige MySQL instalado na maquina.
- Comando principal de demo: `.\scripts\dev-up.ps1` (compose + build + `dotnet run` em foreground).
- Testes: `.\scripts\test.ps1` → `dotnet test TestOrder.slnx` com **Testcontainers.MySql** (MySQL efemero por collection).
- Migrations e seed automaticos no startup da API (`Program.cs` + `DatabaseSeeder.cs`).
- Perfil dev: `appsettings.Development.json` com `Seed:OrderCount=5000`.
- Perfil teste: `tests/TestOrder.Api.Tests/appsettings.Test.json` com `Seed:OrderCount=3000`.

### EF Core vs Dapper

- **EF Core**: entidades, `TestOrderDbContext`, migrations, seed.
- **Dapper**: leituras em `ProductsController`, `OrdersController` e SQL em `OrdersQueries.cs` (max. 3 queries por listagem paginada, sem N+1).

### Volume do seed (dev, validado)

| Metrica | Valor |
| --- | --- |
| Produtos | 50 |
| Pedidos | 5000 |
| Itens de pedido | 17499 |
| Media itens/pedido | 3,50 |

Segunda execucao da API nao duplica dados (`Orders.AnyAsync()` guard no seeder).

### Endpoints e contrato JSON

- `GET /api/products` — lista completa do catalogo.
- `GET /api/orders` — paginacao (default page=1, pageSize=20; max 100); ordenacao `createdAt DESC`, desempate `id DESC`.
- `GET /api/orders/{id}` — 200 com itens ou 404.
- `createdAt` serializa em **UTC com sufixo `Z`** via `UtcDateTimeJsonConverter` em `Program.cs`.
- Total do pedido = soma `quantity × unitPrice` dos itens (subselect SQL, nao coluna persistida).

### Validacao manual (porta 5069)

| Request | Resultado |
| --- | --- |
| `GET /api/products` | 200, 50 produtos |
| `GET /api/orders?page=1&pageSize=5` | 200, `totalCount=5000` |
| `GET /api/orders?page=999&pageSize=20` | 200, `items: []` |
| `GET /api/orders?page=0&pageSize=20` | 400, `{ "error": "..." }` |
| `GET /api/orders/1` | 200 com itens e total |
| `GET /api/orders/99999999` | 404 |

### Testes automatizados

- **17/17** testes de integracao passando (`tests/TestOrder.Api.Tests`).
- Cobertura: seed, products, paginacao, total, ordenacao, paginas sem overlap, 400 invalidos, detalhe 200/404, formato UTC `Z` em `createdAt`.

### Observacao operacional (Windows)

Parar a API manualmente antes de `dotnet build` ou `dotnet test` se ela estiver rodando — o `TestOrder.Api.exe` fica bloqueado e o build falha.

### Uso de IA neste modulo

- **Spec Kit**: `/speckit-specify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-implement` por fases (infra → modelo → seed → endpoints → testes → docs).
- IA gerou artefatos iniciais; revisao humana manteve escopo minimo (sem repositories, AutoMapper, Minimal APIs).
- Prompts incrementais por tarefa (T019–T023 endpoints, T024–T034 testes, T035–T038 docs) com escopo explicito de arquivos permitidos.

### Pontos revisados por humano

- Ordenacao paginada com desempate por `id` (muitos pedidos com mesmo `created_at` no seed).
- Injecao de config em testes via env vars + `appsettings.Test.json` (limitacao do `WebApplicationFactory` com top-level `Program.cs`).
- Serializacao UTC explicita para cumprir contrato ISO 8601 com `Z`.

## Modulo 002 - Criacao de pedido com reservas concorrentes

**Status: concluido** (T001–T025).

### O que foi implementado

- `POST /api/orders` com reserva transacional de estoque via `inventory_units`.
- Migration **`20260703184137_AddInventoryAndOutbox`**: coluna `orders.customer_name`, tabelas `inventory_units`, `order_reservation_units`, `order_processing_events`.
- Backfill idempotente em `InventoryUnitsBackfill.cs` (guard `AnyAsync()`); primeira subida local materializou **~237k** linhas `available` em dev.
- Transacao critica em **`CreateOrderCommands.cs`** (Dapper/MySqlConnector): `READ COMMITTED`, itens ordenados por `productId ASC`, reserva com `SELECT ... FOR UPDATE SKIP LOCKED`.
- Na mesma transacao: `orders`, `order_items`, update `inventory_units` → `reserved`, `order_reservation_units`, `order_processing_events` (`OrderCreated`, `pending`).
- **`products.stock_quantity`** permanece legado/indicador — **nao** e decrementado no POST.
- Respostas: **201** / **400** / **409**. `customerName` opcional (vazio → `NULL`); nao exposto na response 201.

### Validacao real

| Comando | Resultado |
| --- | --- |
| `dotnet build TestOrder.slnx` | PASS |
| `.\scripts\test.ps1` | PASS — **32/32** |
| `dotnet test TestOrder.slnx` | PASS — **32/32** |

Teste de concorrencia (`CreateOrder_ConcurrentRequests_DoNotOverbook`): **10 POSTs paralelos**, **5 unidades** disponiveis → **5×201 + 5×409**, zero overbooking.

Codigo de producao **nao precisou de ajuste** durante a fatia de testes T014–T021.

### Onde a IA ajudou

- **Spec Kit**: `/speckit-specify`, `/speckit-plan`, `/speckit-tasks` para gerar spec, plan e tasks do modulo 002.
- **Prompts por fatia**: T001–T009 (schema/backfill), T010–T013 (POST/transacao), T014–T021 (testes), T022–T025 (docs).
- **`/speckit-analyze`**: revisao de cobertura e inconsistencias **antes** do implement (paralelismo enganoso, side effects, outbox em 409).
- **Desenho dos testes de concorrencia**: produto controlado + 10 tasks paralelas com asserts SQL via Dapper.

### Onde a IA foi limitada ou corrigida

- **Paralelismo enganoso** em `tasks.md` (T014–T020 marcadas `[P]` no mesmo arquivo) — corrigido no analyze antes do implement.
- **Evitar services/repositories/interfaces** desnecessarias — transacao ficou em `CreateOrderCommands.cs` estatico ao lado do controller.
- **Regressao do modulo 001** mantida no mesmo arquivo `CreateOrderEndpointTests.cs` (nao criar `RegressionReadEndpointsTests.cs` separado).

### Decisoes manuais

- **MVC**, sem Minimal APIs — POST em `OrdersController` existente.
- **Dapper** para transacao critica; **EF Core** apenas para schema, migration, entidades e backfill.
- **Sem RabbitMQ/Kafka/Redis** neste modulo — outbox local em `order_processing_events` para consumo futuro pelo Node.
- **`inventory_units`** como fonte de verdade da reserva (nao decremento cego em `stock_quantity`).
- Pedidos historicos do seed (modulo 001) **nao** consomem unidades retroativamente.

## Modulo 003 - Faturamento por periodo

**Status: concluido** (T001–T018).

### O que foi implementado

- `GET /api/revenue/daily?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD` em `RevenueController.cs` (rota `api/revenue`).
- `RevenueQueries.cs`: uma unica consulta Dapper agregada (`JOIN orders/order_items`, `WHERE status='created'`, `GROUP BY DATE(created_at)`), sem N+1.
- **Nenhuma migration, entidade EF ou alteracao de schema** — leitura pura sobre `orders`/`order_items` existentes.
- Validacoes 400: `startDate`/`endDate` ausentes ou vazios, formato invalido (`DateOnly.TryParseExact` estrito `yyyy-MM-dd`), `startDate > endDate`, intervalo > 366 dias.
- Intervalo **semiaberto em UTC** na SQL (`created_at >= StartUtc AND created_at < EndExclusiveUtc`) para incluir corretamente o dia final sem depender de fracao de segundo.
- Dias sem pedido preenchidos com `revenue=0`/`orderCount=0` **em C#** (iteracao de `startDate` a `endDate`), nao com CTE recursiva/serie de datas em SQL.
- `totalRevenue`/`totalOrders` somados a partir da lista final de `days` (ja com zeros), sem segunda query.
- Datas na resposta como **`string` `yyyy-MM-dd`** (nao `DateTime`) para nao colidir com o `UtcDateTimeJsonConverter` global usado em `createdAt`.

### Validacao real

| Comando | Resultado |
| --- | --- |
| `dotnet build TestOrder.slnx` | PASS |
| `.\scripts\test.ps1` | PASS — **46/46** (32 dos modulos 001/002 + 14 casos novos em 10 metodos de teste do modulo 003) |
| `dotnet test TestOrder.slnx` | PASS — **46/46** |

Codigo de producao (`RevenueController.cs`/`RevenueQueries.cs`) **nao precisou de ajuste** apos os testes, exceto uma correcao de tipo: `COUNT(DISTINCT o.id)` retorna `BIGINT` no MySQL/MySqlConnector, entao o record interno `RevenueDayRow` usa `long OrderCount` (com cast para `int` apenas na resposta), nao `int` direto — o Dapper falhava ao materializar o registro com o tipo errado.

### Onde a IA ajudou

- **Spec Kit**: `/speckit-specify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` para gerar e revisar spec/plan/tasks antes do implement.
- **`/speckit-analyze`** identificou 3 lacunas de cobertura antes do implement: caso de borda `startDate == endDate` (dia unico), caso positivo obrigatorio de 366 dias (estava descrito como opcional em uma unica task) e ausencia de verificacao automatizada de que o endpoint tambem agrega pedidos do seed (nao so dados de teste isolados). As tres foram endereçadas na implementacao antes de escrever os testes.
- Prompt unico cobrindo T001–T018 (build/test/docs), respeitando fases e arquivos permitidos.

### Onde a IA foi limitada ou corrigida

- Erro de materializacao do Dapper (`COUNT(DISTINCT)` como `BIGINT`) so apareceu ao rodar os testes reais contra MySQL — corrigido trocando `int` por `long` no row interno.
- Evitar CTE recursiva/serie de datas em SQL para os dias zerados — decisao deliberada de preencher em C#, mantendo a query SQL simples e legivel.
- Total de testes do modulo passou de 9 (estimativa inicial do `tasks.md`) para **10 metodos de teste** apos incorporar os 3 casos extras do `/speckit-analyze` (dia unico, borda de 366/367 dias obrigatoria, agregacao do seed) — numero real refletido em `tasks.md`, `quickstart.md` e aqui.

### Decisoes manuais

- **MVC**, sem Minimal APIs — `RevenueController` novo, mesmo padrao de `OrdersController`.
- **Dapper** para a agregacao; nenhuma entidade EF, repository, service generico, interface, MediatR, CQRS ou AutoMapper.
- Dados de teste deterministicos inseridos via SQL direto (produto e datas exclusivos por teste) para os testes de agregacao; teste de regressao usa um intervalo amplo cobrindo o periodo real do seed (`2025-07-02` a `2026-07-01`) para confirmar que pedidos historicos tambem sao contabilizados.
- `OrdersController.cs`, `CreateOrderCommands.cs`, `ProductsController.cs`, `Program.cs`, `TestOrderDbContext.cs` e migrations **nao** foram tocados.
