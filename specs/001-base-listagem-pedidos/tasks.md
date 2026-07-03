# Tasks: Módulo 001 — Base e Listagem de Pedidos

**Input**: `specs/001-base-listagem-pedidos/` (spec, plan, research, data-model, contracts, quickstart)

**Prerequisites**: Plano técnico revisado com Docker como padrão local.

**Branch**: `001-base-listagem-pedidos`

**Organização**: Fases 1–7 seguem ordem obrigatória de implementação. User stories da spec: US1 (ambiente/dados), US2 (produtos), US3 (pedidos paginados), US4 (detalhe por id).

**Restrições globais**: Sem Minimal APIs, Clean Architecture, DDD, CQRS, mediator, repositories genéricos, AutoMapper ou interfaces desnecessárias. Sem `POST /api/orders`, faturamento, React, Node, Dockerfile da API. Compose apenas com MySQL.

---

## Formato das tarefas

Cada tarefa usa checkbox rastreável:

```text
- [ ] Tnnn [P?] [US?] Descrição com caminho de arquivo
```

Detalhes expandidos (permitidos, proibidos, pronto, validação, paralelo) ficam na subseção **Detalhe** abaixo de cada checkbox.

---

## Phase 1: Infra local (US1)

**Goal**: Avaliador sobe MySQL via Docker sem instalação nativa; scripts de dev e teste prontos.

**Independent Test**: `docker compose up -d mysql` + healthcheck OK; connection string aponta para `localhost:3306`.

---

- [x] T001 Criar `docker-compose.yml` com serviço MySQL 8

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Compose na raiz com serviço `mysql` (`mysql:8`), porta `3306:3306`, `MYSQL_DATABASE=testorder`, `MYSQL_USER`/`MYSQL_PASSWORD=testorder`, volume nomeado, healthcheck `mysqladmin ping`. |
| **Permitidos** | `docker-compose.yml` |
| **Proibidos** | Dockerfile da API; serviços frontend/Node; alterações em `src/` |
| **Pronto quando** | `docker compose config` válido; apenas serviço `mysql` definido |
| **Validação** | `docker compose up -d mysql` e `docker compose ps` mostra healthy |
| **Paralelo** | Não — primeira tarefa |

---

- [x] T002 Criar `scripts/dev-up.ps1`

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | Script: validar Docker → `docker compose up -d mysql` → aguardar conexão (timeout ~60s) → `dotnet build TestOrder.slnx` → `dotnet run --project src/TestOrder.Api` em **foreground**. |
| **Permitidos** | `scripts/dev-up.ps1` |
| **Proibidos** | Containerizar API; lógica de domínio |
| **Pronto quando** | Script executa sequência completa; falha clara se Docker indisponível |
| **Validação** | `.\scripts\dev-up.ps1` (após T008+T014+T018 mínimo para API subir) |
| **Paralelo** | Não — depende de T001 |

---

- [x] T003 [P] Criar `scripts/test.ps1`

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | Script: validar Docker → `dotnet test TestOrder.slnx` → repassar exit code. |
| **Permitidos** | `scripts/test.ps1` |
| **Proibidos** | Substituir Testcontainers por SQLite/InMemory |
| **Pronto quando** | `.\scripts\test.ps1` invoca testes da solução |
| **Validação** | `.\scripts\test.ps1` (após suíte de testes criada) |
| **Paralelo** | Sim — independente de T002 após T001 |

---

- [x] T004 Configurar connection string padrão para MySQL Docker

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | Em `appsettings.Development.json`: `ConnectionStrings:Default` = `Server=localhost;Port=3306;Database=testorder;User=testorder;Password=testorder;` e seção `Seed` com contagens dev (5000 pedidos). Ajustar `appsettings.json` base se necessário (sem secrets). |
| **Permitidos** | `src/TestOrder.Api/appsettings.json`, `src/TestOrder.Api/appsettings.Development.json` |
| **Proibidos** | Hardcode de connection string em código C# |
| **Pronto quando** | Connection string alinhada ao Compose; Seed section presente |
| **Validação** | Revisar JSON; após API rodando, conexão bem-sucedida nos logs |
| **Paralelo** | Sim [P] com T003 após T001 |

**Checkpoint Phase 1**: MySQL sobe via Docker; scripts existem; connection string documentada.

---

## Phase 2: Backend base (US1)

**Goal**: API MVC registra EF, Dapper e aplica migrate+seed no startup.

**Independent Test**: API inicia, aplica migration e executa seeder sem erro (banco vazio).

---

- [x] T005 Configurar MVC e pipeline em `Program.cs`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | `AddControllers()`, `MapControllers()`, middleware mínimo; **sem** Minimal APIs. |
| **Permitidos** | `src/TestOrder.Api/Program.cs` |
| **Proibidos** | `app.MapGet` / Minimal APIs; pastas Application/Domain |
| **Pronto quando** | Projeto compila; rota de controller pode ser mapeada |
| **Validação** | `dotnet build src/TestOrder.Api/TestOrder.Api.csproj` |
| **Paralelo** | Não — base do Program |

---

- [x] T006 Registrar EF Core + Pomelo MySQL em `Program.cs`

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | `AddDbContext<TestOrderDbContext>` com Pomelo e connection string de configuração. |
| **Permitidos** | `src/TestOrder.Api/Program.cs`, `src/TestOrder.Api/TestOrder.Api.csproj` (pacote já referenciado ou confirmado) |
| **Proibidos** | DbContext factory genérico; múltiplos contexts |
| **Pronto quando** | DI resolve `TestOrderDbContext` (stub vazio OK até T012) |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende de T005 |

---

- [x] T007 Registrar factory `MySqlConnection` para Dapper em `Program.cs`

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | `AddTransient<MySqlConnection>` (ou factory equivalente) usando mesma connection string. |
| **Permitidos** | `src/TestOrder.Api/Program.cs` |
| **Proibidos** | Interface `IDbConnection` + implementação separada sem necessidade |
| **Pronto quando** | Controllers poderão injetar `MySqlConnection` |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — mesmo arquivo Program (sequencial após T006) |

---

- [x] T008 Implementar migrate + seed no startup em `Program.cs`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | Após `app.Build()`, escopo de serviço: `Database.Migrate()` + `DatabaseSeeder.SeedAsync()` antes de `Run()`. |
| **Permitidos** | `src/TestOrder.Api/Program.cs` |
| **Proibidos** | `EnsureCreated()`; scripts SQL manuais de schema |
| **Pronto quando** | Primeira execução aplica migration e chama seeder |
| **Validação** | `dotnet run --project src/TestOrder.Api` com MySQL Docker up (após T012–T018) |
| **Paralelo** | Não — depende T006; seeder stub até T015 |

**Checkpoint Phase 2**: Program.cs completo com MVC, EF, Dapper e hook de migrate/seed.

---

## Phase 3: Modelo e banco (US1)

**Goal**: Schema relacional inicial com snake_case e índices.

**Independent Test**: Migration gera tabelas `products`, `orders`, `order_items` com índices.

---

- [x] T009 [P] [US1] Criar entidade `Product` em `src/TestOrder.Api/Data/Entities/Product.cs`

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | Propriedades: `Id`, `Name`, `UnitPrice`, `StockQuantity` conforme [data-model.md](./data-model.md). |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/Product.cs` |
| **Proibidos** | DTOs duplicados; interfaces de entidade |
| **Pronto quando** | Classe compila e reflete modelo |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Sim [P] entre T009–T011 |

---

- [x] T010 [P] [US1] Criar entidade `Order` em `src/TestOrder.Api/Data/Entities/Order.cs`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | `Id` (long), `CreatedAt`, `Status`. Sem coluna `Total` persistida. |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/Order.cs` |
| **Proibidos** | Atributos de cliente/endereço (YAGNI) |
| **Pronto quando** | Classe compila |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Sim [P] |

---

- [x] T011 [P] [US1] Criar entidade `OrderItem` em `src/TestOrder.Api/Data/Entities/OrderItem.cs`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | `Id`, `OrderId`, `ProductId`, `Quantity`, `UnitPrice`. |
| **Permitidos** | `src/TestOrder.Api/Data/Entities/OrderItem.cs` |
| **Proibidos** | Navigation properties obrigatórias para Dapper |
| **Pronto quando** | Classe compila |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Sim [P] |

---

- [x] T012 [US1] Criar `TestOrderDbContext` com mapeamento snake_case

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | `DbSet` para três entidades; `OnModelCreating` com `ToTable`/`HasColumnName` snake_case; FKs e `ON DELETE CASCADE` em `order_items.order_id`. |
| **Permitidos** | `src/TestOrder.Api/Data/TestOrderDbContext.cs` |
| **Proibidos** | Configurações em assembly separado; Fluent em múltiplos arquivos |
| **Pronto quando** | Modelo EF alinhado a [data-model.md](./data-model.md) |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende T009–T011 |

---

- [x] T013 [US1] Definir índices no `TestOrderDbContext`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | `IX_orders_created_at`, `IX_order_items_order_id`, `IX_order_items_product_id`. |
| **Permitidos** | `src/TestOrder.Api/Data/TestOrderDbContext.cs` |
| **Proibidos** | Índices extras não listados no plano |
| **Pronto quando** | Índices configurados em Fluent API |
| **Validação** | Revisar migration gerada em T014 |
| **Paralelo** | Não — sequencial com T012 |

---

- [x] T014 [US1] Criar migration inicial EF Core

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | Garantir suporte a migrations (`Microsoft.EntityFrameworkCore.Design` no projeto API e `dotnet-ef` disponível, se necessário) e executar `dotnet ef migrations add Initial` → arquivos em `src/TestOrder.Api/Migrations/`. |
| **Permitidos** | `src/TestOrder.Api/TestOrder.Api.csproj`, `src/TestOrder.Api/Migrations/*` |
| **Proibidos** | SQL manual solto como substituto de migration |
| **Pronto quando** | Migration cria três tabelas e índices |
| **Validação** | `dotnet ef migrations list` e `dotnet ef database update` (ou startup T008) contra MySQL Docker |
| **Paralelo** | Não — depende T012–T013 |

**Checkpoint Phase 3**: Schema versionado aplicável via `Migrate()`.

---

## Phase 4: Seed (US1)

**Goal**: Banco populado de forma determinística e idempotente.

**Independent Test**: Segunda execução não duplica pedidos; contagens batem com config.

---

- [x] T015 [US1] Criar esqueleto `DatabaseSeeder` com guard idempotente

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Classe em `Data/Seed/DatabaseSeeder.cs` com `SeedAsync(DbContext, IConfiguration)`; início com `if (await context.Orders.AnyAsync()) return`. |
| **Permitidos** | `src/TestOrder.Api/Data/Seed/DatabaseSeeder.cs` |
| **Proibidos** | Hosted service; jobs em background |
| **Pronto quando** | Seeder invocável do Program.cs sem duplicar em re-run |
| **Validação** | Subir API duas vezes; contagem de pedidos estável |
| **Paralelo** | Não |

---

- [x] T016 [US1] Implementar seed de 50 produtos com `Random(42)`

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Nomes/preços determinísticos; `StockQuantity` alto (1000–10000). |
| **Permitidos** | `src/TestOrder.Api/Data/Seed/DatabaseSeeder.cs` |
| **Proibidos** | Bogus/Faker; SQL bulk externo |
| **Pronto quando** | `SELECT COUNT(*) FROM products` = 50 após seed |
| **Validação** | Logs + query SQL opcional |
| **Paralelo** | Não — mesmo arquivo |

---

- [x] T017 [US1] Implementar seed de pedidos e itens em lotes

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | `OrderCount` de configuração (5000 dev); 2–5 itens/pedido; datas nos últimos 365 dias; `unit_price` snapshot; `AddRange` + `SaveChanges` a cada 500 pedidos. |
| **Permitidos** | `src/TestOrder.Api/Data/Seed/DatabaseSeeder.cs` |
| **Proibidos** | Inserção pedido-a-pedido sem batching |
| **Pronto quando** | ≥ 5000 pedidos em dev; média > 1 item/pedido |
| **Validação** | `SELECT COUNT(*) FROM orders` ≥ 5000 |
| **Paralelo** | Não — sequencial após T016 |

---

- [x] T018 [US1] Configurar perfis de seed dev vs teste

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | `appsettings.Development.json`: `OrderCount=5000`; criar `tests/TestOrder.Api.Tests/appsettings.Test.json` com `OrderCount=3000` (arquivo pode ser stub até Phase 6). |
| **Permitidos** | `src/TestOrder.Api/appsettings.Development.json`, `tests/TestOrder.Api.Tests/appsettings.Test.json` |
| **Proibidos** | Contagens hardcoded sem config |
| **Pronto quando** | Seeder lê `Seed:*` de IConfiguration |
| **Validação** | Dev 5k; testes (após T032) ≥ 3k |
| **Paralelo** | Sim [P] para criar appsettings.Test.json após pasta de testes existir (T026) — pode ser feito em T026 se preferir ordem |

**Checkpoint Phase 4**: `.\scripts\dev-up.ps1` deixa banco populado na primeira execução.

---

## Phase 5: Endpoints (US2, US3, US4)

**Goal**: Três endpoints de leitura conforme [contracts/api.md](./contracts/api.md).

**Independent Test**: curl nos três endpoints retorna JSON esperado.

---

- [x] T019 [P] Criar records de resposta JSON

**Detalhe T019**
| Campo | Valor |
| --- | --- |
| **Descrição** | Records para Product, OrderItem, Order, PagedOrders em `Models/Responses/` ou junto aos controllers — sem AutoMapper. |
| **Permitidos** | `src/TestOrder.Api/Models/Responses/*.cs` |
| **Proibidos** | AutoMapper; camada Mapping |
| **Pronto quando** | Tipos batem com contracts/api.md (camelCase via serialização padrão) |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Sim — após Phase 3 |

---

- [x] T020 [US2] Implementar `GET /api/products` em `ProductsController.cs`

**Detalhe T020**
| Campo | Valor |
| --- | --- |
| **Descrição** | Controller MVC `[ApiController] [Route("api/products")]`; Dapper `SELECT id, name, unit_price FROM products ORDER BY id`. |
| **Permitidos** | `src/TestOrder.Api/Controllers/ProductsController.cs` |
| **Proibidos** | EF LINQ na listagem; Minimal APIs |
| **Pronto quando** | HTTP 200, lista não vazia após seed |
| **Validação** | `curl http://localhost:{PORT}/api/products` |
| **Paralelo** | Não |

---

- [x] T021 [US3] Implementar validação de paginação (400) em `OrdersController.cs`

**Detalhe T021**
| Campo | Valor |
| --- | --- |
| **Descrição** | Defaults `page=1`, `pageSize=20`, max 100; retornar `{ "error": "..." }` para parâmetros inválidos. |
| **Permitidos** | `src/TestOrder.Api/Controllers/OrdersController.cs` |
| **Proibidos** | ProblemDetails framework pesado |
| **Pronto quando** | `page=0` e `pageSize=101` retornam 400 |
| **Validação** | `curl` com query inválida |
| **Paralelo** | Não — base do OrdersController |

---

- [x] T022 [US3] Implementar `GET /api/orders` com Dapper (sem N+1)

**Detalhe T022**
| Campo | Valor |
| --- | --- |
| **Descrição** | Três queries: COUNT; página com subselect de total; itens `IN (@orderIds)` com join em products. Ordenação `created_at DESC`. SQL em const no controller ou `OrdersQueries.cs` adjacente. |
| **Permitidos** | `src/TestOrder.Api/Controllers/OrdersController.cs`, opcional `src/TestOrder.Api/Controllers/OrdersQueries.cs` |
| **Proibidos** | Repository; query por pedido em loop |
| **Pronto quando** | ≤ pageSize pedidos; metadados; totais corretos; máx. 3 queries/request |
| **Validação** | `curl ".../api/orders?page=1&pageSize=20"` |
| **Paralelo** | Não — depende T021 |

---

- [x] T023 [US4] Implementar `GET /api/orders/{id}` com 404

**Detalhe T023**
| Campo | Valor |
| --- | --- |
| **Descrição** | Mesmo shape de Order da listagem; 404 `{ "error": "Order not found." }` para id inexistente. |
| **Permitidos** | `src/TestOrder.Api/Controllers/OrdersController.cs` |
| **Proibidos** | Endpoint separado em Minimal API |
| **Pronto quando** | 200 para id válido; 404 para id inválido |
| **Validação** | `curl .../api/orders/1` e `.../99999999` |
| **Paralelo** | Não — mesmo controller (após T022) |

**Checkpoint Phase 5**: Endpoints manuais passam via curl após `dev-up.ps1`.

---

## Phase 6: Suíte de testes (US1–US4)

**Goal**: Integração com Testcontainers + WebApplicationFactory cobrindo seed e endpoints.

**Independent Test**: `.\scripts\test.ps1` passa com Docker disponível.

---

- [x] T024 Criar projeto `tests/TestOrder.Api.Tests`

**Detalhe T024**
| Campo | Valor |
| --- | --- |
| **Descrição** | `dotnet new xunit`; referenciar `TestOrder.Api`; pacotes: `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers`, `Testcontainers.MySql`, Dapper se necessário. |
| **Permitidos** | `tests/TestOrder.Api.Tests/TestOrder.Api.Tests.csproj`, `tests/TestOrder.Api.Tests/GlobalUsings.cs` (se necessário) |
| **Proibidos** | SQLite/InMemory como banco principal |
| **Pronto quando** | Projeto compila (testes vazios OK) |
| **Validação** | `dotnet build tests/TestOrder.Api.Tests/TestOrder.Api.Tests.csproj` |
| **Paralelo** | Não — inicia Phase 6 |

---

- [x] T025 Adicionar projeto de testes à `TestOrder.slnx`

**Detalhe T025**
| Campo | Valor |
| --- | --- |
| **Descrição** | Incluir `tests/TestOrder.Api.Tests` na pasta `/tests/` da solução. |
| **Permitidos** | `TestOrder.slnx` |
| **Proibidos** | Alterar estrutura de src desnecessariamente |
| **Pronto quando** | `dotnet test TestOrder.slnx` descobre o projeto |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — após T024 |

---

- [x] T026 Configurar `MySqlContainerFixture` com Testcontainers

**Detalhe T026**
| Campo | Valor |
| --- | --- |
| **Descrição** | Fixture xUnit collection com `mysql:8`; expor connection string; skip claro se Docker indisponível. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/MySqlContainerFixture.cs`, `tests/TestOrder.Api.Tests/Integration/IntegrationCollection.cs` |
| **Proibidos** | Depender do Compose de dev |
| **Pronto quando** | Container sobe uma vez por collection |
| **Validação** | Teste smoke que abre conexão |
| **Paralelo** | Não |

---

- [x] T027 Configurar `CustomWebApplicationFactory` + `appsettings.Test.json`

**Detalhe T027**
| Campo | Valor |
| --- | --- |
| **Descrição** | Factory injeta connection string do fixture; ambiente Test; `Seed:OrderCount=3000`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/CustomWebApplicationFactory.cs`, `tests/TestOrder.Api.Tests/appsettings.Test.json` |
| **Proibidos** | Mock de DbContext |
| **Pronto quando** | `WebApplicationFactory` aplica migrate+seed no host de teste |
| **Validação** | Factory cria client HTTP |
| **Paralelo** | Não — depende T026 |

---

- [x] T028 [P] [US1] Testes de seed: produtos e ≥ 3000 pedidos

**Detalhe T028**
| Campo | Valor |
| --- | --- |
| **Descrição** | `Seed_CreatesProducts` (50); `Seed_CreatesAtLeast3000Orders` em `SeedIntegrationTests.cs`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/SeedIntegrationTests.cs` |
| **Proibidos** | Mocks de seed |
| **Pronto quando** | Asserts em SQL/COUNT via Dapper ou API interna |
| **Validação** | `dotnet test --filter Seed` |
| **Paralelo** | Sim [P] após T027 |

---

- [x] T029 [P] [US2] Testes `GET /api/products`

**Detalhe T029**
| Campo | Valor |
| --- | --- |
| **Descrição** | 200, lista não vazia, campos `id`/`name`/`unitPrice`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/ProductsEndpointTests.cs` |
| **Proibidos** | Testes unitários mockando HTTP |
| **Pronto quando** | Teste passa contra MySQL real |
| **Validação** | `dotnet test --filter Products` |
| **Paralelo** | Sim [P] após T027 |

---

- [x] T030 [US3] Testes `GET /api/orders` paginação e metadados

**Detalhe T030**
| Campo | Valor |
| --- | --- |
| **Descrição** | 200; ≤ 20 itens; `totalCount` ≥ 3000; defaults sem query = page 1 size 20; pedidos ordenados por `createdAt` decrescente; `page=999` retorna 200 com `items: []`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/OrdersEndpointTests.cs` |
| **Proibidos** | — |
| **Pronto quando** | Asserts de estrutura JSON, ordenação decrescente e página além do fim |
| **Validação** | `dotnet test --filter Orders` |
| **Paralelo** | Não — mesmo arquivo que T031–T033 |

---

- [x] T031 [US3] Teste total do pedido = soma dos itens

**Detalhe T031**
| Campo | Valor |
| --- | --- |
| **Descrição** | Para cada pedido na página, `total` == Σ quantity × unitPrice. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/OrdersEndpointTests.cs` |
| **Proibidos** | — |
| **Pronto quando** | Assert falha se total divergir |
| **Validação** | `dotnet test --filter OrderTotal` |
| **Paralelo** | Não — sequencial em OrdersEndpointTests |

---

- [x] T032 [US3] Teste páginas 1 e 2 sem duplicar IDs

**Detalhe T032**
| Campo | Valor |
| --- | --- |
| **Descrição** | Interseção de ids entre page=1 e page=2 vazia com pageSize=20. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/OrdersEndpointTests.cs` |
| **Proibidos** | — |
| **Pronto quando** | Teste passa |
| **Validação** | `dotnet test --filter Page` |
| **Paralelo** | Não |

---

- [x] T033 [US3] Testes parâmetros de paginação inválidos (400)

**Detalhe T033**
| Campo | Valor |
| --- | --- |
| **Descrição** | `page=0`, `pageSize=0`, `pageSize=101` → 400 + corpo `error`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/OrdersEndpointTests.cs` |
| **Proibidos** | — |
| **Pronto quando** | Três casos cobertos |
| **Validação** | `dotnet test --filter Invalid` |
| **Paralelo** | Não |

---

- [x] T034 [US4] Testes `GET /api/orders/{id}` 200 e 404

**Detalhe T034**
| Campo | Valor |
| --- | --- |
| **Descrição** | Id existente retorna pedido com itens; id 99999999 retorna 404. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/OrdersEndpointTests.cs` |
| **Proibidos** | — |
| **Pronto quando** | Ambos cenários passam |
| **Validação** | `dotnet test --filter OrderById` |
| **Paralelo** | Não |

**Checkpoint Phase 6**: `.\scripts\test.ps1` verde com Docker.

---

## Phase 7: Documentação pós-implementação

**Goal**: Rastreabilidade para apresentação e IA.

---

- [x] T035 Atualizar `AI_NOTES.md` com resultados do módulo 001

**Detalhe T035**
| Campo | Valor |
| --- | --- |
| **Descrição** | Status concluído; Docker padrão; EF vs Dapper; volume seed; uso de IA; prompts Spec Kit. |
| **Permitidos** | `AI_NOTES.md` |
| **Proibidos** | Módulos 002+ |
| **Pronto quando** | Seção 001 preenchida com fatos reais pós-implementação |
| **Validação** | Revisão humana |
| **Paralelo** | Sim [P] com T036 |

---

- [x] T036 Atualizar `docs/PRESENTATION_GUIDE.md`

**Detalhe T036**
| Campo | Valor |
| --- | --- |
| **Descrição** | Tabela de referências (compose, scripts, controllers, seeder, testes); validações pass/fail; comandos demo. |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Proibidos** | — |
| **Pronto quando** | Linha módulo 001 completa |
| **Validação** | Revisão humana |
| **Paralelo** | Sim [P] |

---

- [x] T037 Revisar `quickstart.md` com comandos finais validados

**Detalhe T037**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar `dev-up.ps1` / `test.ps1`; porta real; ajustes se script mudou na implementação. |
| **Permitidos** | `specs/001-base-listagem-pedidos/quickstart.md` |
| **Proibidos** | — |
| **Pronto quando** | Quickstart reproduzível por terceiro |
| **Validação** | Seguir quickstart do zero |
| **Paralelo** | Não — após validação final T038 |

---

- [x] T038 Validação final obrigatória do módulo

**Detalhe T038**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar checklist completo de aceite do módulo, incluindo medição manual/observacional da primeira página de pedidos (< 2s como meta de demo local). |
| **Permitidos** | Nenhuma alteração de código (só fixes se validação falhar) |
| **Proibidos** | Escopo módulo 002+ |
| **Pronto quando** | Todos os comandos abaixo passam |
| **Validação** | Ver **Validações finais** abaixo |
| **Paralelo** | Não |

**Validações finais obrigatórias**:

```powershell
.\scripts\dev-up.ps1          # Ctrl+C após confirmar endpoints
.\scripts\test.ps1
dotnet build TestOrder.slnx
dotnet test TestOrder.slnx
```

---

## Dependencies & Execution Order

```text
T001 → T002,T003,T004
T005 → T006 → T007 → T008
T009,T010,T011 (parallel) → T012 → T013 → T014
T015 → T016 → T017 → T018
T019 (parallel pós-T014) ; T020 após T019,T017
T021 → T022 → T023
T024 → T025 → T026 → T027 → T028,T029 (parallel) → T030→T031→T032→T033→T034
T035,T036 (parallel) → T037 → T038
```

### User Story Mapping

| Story | Prioridade | Tarefas principais |
| --- | --- | --- |
| US1 — Ambiente e dados | P1 | T001–T018, T028 |
| US2 — Listar produtos | P1 | T019, T020, T029 |
| US3 — Pedidos paginados | P1 | T021–T022, T030–T033 |
| US4 — Detalhe por id | P3 | T023, T034 |

### Parallel Opportunities

- **T003 + T004** após T001
- **T009 + T010 + T011** em paralelo
- **T019** enquanto outro dev em seed (após entities)
- **T028 + T029** após factory de testes
- **T035 + T036** em paralelo

### MVP Scope

MVP mínimo demonstrável: **Phase 1–5** (T001–T023) + validação manual curl.  
Módulo **completo** exige Phase 6–7 (T024–T038).

---

## Implementation Strategy

1. **Infra primeiro** — Docker e scripts antes de código de domínio (onboarding zero MySQL nativo).
2. **Vertical slice incremental** — modelo → seed → um endpoint por vez.
3. **Testes após endpoints** — integração valida comportamento real MySQL.
4. **Um diff por tarefa** — facilita revisão humana e apresentação.
5. **Parar em checkpoints** — após T014 (schema), T018 (seed), T023 (API), T034 (testes).

---

## Notes

- Commit sugerido após cada tarefa ou par lógico (ex.: T009–T011 juntos).
- Se porta 3306 ocupada por MySQL nativo, documentar no quickstart (fallback) — não bloquear T001.
- `T018` appsettings.Test.json pode ser criado em T027 se a pasta de testes ainda não existir em T018.
- Não avançar para módulo 002 até T038 passar.
