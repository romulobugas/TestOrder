# Plano Técnico: Módulo 001 — Base e Listagem de Pedidos

**Branch**: `001-base-listagem-pedidos` | **Data**: 2026-07-03 (revisado) | **Spec**: [spec.md](./spec.md)

**Input**: Especificação em `specs/001-base-listagem-pedidos/spec.md`

---

## Summary

Montar a fundação do backend TestOrder: ASP.NET Core MVC com MySQL 8, modelo `Product` / `Order` / `OrderItem`, seed determinístico com milhares de pedidos, e três endpoints de leitura (`GET /api/products`, `GET /api/orders`, `GET /api/orders/{id}`). EF Core cuida de schema, migrations e seed; Dapper executa SQL parametrizado nas listagens. Suíte de integração em `tests/TestOrder.Api.Tests` com Testcontainers + MySQL 8 real.

**Execução local (decisão revisada)**:

- **Docker é o padrão**: o avaliador/desenvolvedor **não** precisa instalar MySQL na máquina. O banco sobe via `docker-compose.yml` (serviço `mysql` apenas neste módulo).
- **Comando principal de demonstração**: `.\scripts\dev-up.ps1` — sobe MySQL, aguarda readiness, faz `dotnet build` e inicia a API em foreground (`dotnet run`).
- **Testes**: `.\scripts\test.ps1` → `dotnet test TestOrder.slnx` (Testcontainers usa Docker em paralelo ao Compose de dev).
- **MySQL instalado localmente** permanece apenas como **fallback opcional** documentado no quickstart; não é pré-requisito principal.
- A API aplica **migrations + seed na inicialização**; não há SQL manual de criação de banco/usuário no caminho feliz.

---

## Technical Context

| Item | Valor |
| --- | --- |
| **Language/Version** | C# / .NET 10 (`net10.0`) |
| **Primary Dependencies** | ASP.NET Core MVC, Pomelo EF Core MySQL 9, Dapper 2.x, MySqlConnector |
| **Storage** | MySQL 8 via Docker Compose (dev) + Testcontainers (testes) |
| **Local runtime** | Docker Desktop / Docker Engine + .NET 10 SDK |
| **Testing** | xUnit, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.MySql |
| **Target Platform** | API local Windows/Linux/macOS |
| **Performance Goals** | 1ª página de pedidos < 2s com ≥ 3.000 pedidos (spec SC-002) |
| **Constraints** | Sem Minimal APIs, sem camadas ceremoniais, SQL perto do uso |
| **Scale/Scope** | 50 produtos, 5.000 pedidos dev / 3.000 pedidos testes, 2–5 itens/pedido |

---

## Constitution Check

*GATES: derivados de `.cursor/rules/testorder.mdc` e `desafio-pleno.md` (constitution Spec Kit ainda é template).*

| Gate | Status | Notas |
| --- | --- | --- |
| MVC controllers para HTTP | ✅ PASS | `ProductsController`, `OrdersController` |
| EF Core para schema/modelo/seed | ✅ PASS | `TestOrderDbContext`, migrations, `DatabaseSeeder` |
| Dapper/SQL para leitura pesada | ✅ PASS | Queries em controllers ou arquivos `Sql/` adjacentes |
| MySQL 8 | ✅ PASS | `docker-compose.yml` (dev) + Testcontainers `mysql:8` (testes) |
| Docker como padrão local | ✅ PASS | Sem exigir MySQL instalado; `scripts/dev-up.ps1` |
| Sem Clean Architecture/DDD/CQRS | ✅ PASS | Sem pastas Domain/Application/Infrastructure |
| Sem RabbitMQ/Redis/Kafka | ✅ PASS | N/A neste módulo |
| Escopo limitado ao módulo 001 | ✅ PASS | Sem POST, SKIP LOCKED, React, Node |
| Projeto buildável ao fim | ✅ PASS | `dotnet build` / `dotnet test` na solução |

**Pós-design (Phase 1)**: Nenhuma violação. Projeto de testes adicional é exigência explícita do módulo, não over-engineering.

---

## Project Structure

### Documentação (esta feature)

```text
specs/001-base-listagem-pedidos/
├── spec.md
├── plan.md                 # este arquivo
├── research.md             # decisões Phase 0
├── data-model.md           # entidades e índices
├── quickstart.md           # validação manual e dotnet test
├── contracts/
│   └── api.md              # contratos JSON
├── checklists/
│   └── requirements.md
└── tasks.md                # (/speckit-tasks — próximo passo)
```

### Código-fonte proposto

```text
F:\repository\TestOrder\
├── TestOrder.slnx
├── docker-compose.yml                      # serviço mysql:8 (módulo 001)
├── scripts/
│   ├── dev-up.ps1                          # compose up + wait + build + dotnet run (foreground)
│   └── test.ps1                            # dotnet test TestOrder.slnx
├── src/
│   └── TestOrder.Api/
│       ├── Program.cs                      # DI, Migrate, Seed, MapControllers
│       ├── appsettings.json
│       ├── appsettings.Development.json    # connection string Docker + Seed:*
│       ├── Controllers/
│       │   ├── ProductsController.cs       # GET /api/products + SQL Dapper
│       │   └── OrdersController.cs         # GET /api/orders, GET /api/orders/{id}
│       ├── Data/
│       │   ├── TestOrderDbContext.cs
│       │   ├── Entities/
│       │   │   ├── Product.cs
│       │   │   ├── Order.cs
│       │   │   └── OrderItem.cs
│       │   └── Seed/
│       │       └── DatabaseSeeder.cs       # Random(42), guard AnyAsync, batch insert
│       └── Migrations/
│           └── <timestamp>_Initial.cs
└── tests/
    └── TestOrder.Api.Tests/
        ├── TestOrder.Api.Tests.csproj
        ├── Integration/
        │   ├── MySqlContainerFixture.cs    # Testcontainers, connection string
        │   ├── CustomWebApplicationFactory.cs
        │   ├── SeedIntegrationTests.cs
        │   ├── ProductsEndpointTests.cs
        │   └── OrdersEndpointTests.cs
        └── appsettings.Test.json           # Seed: OrderCount=3000
```

> **Sem Dockerfile da API** neste módulo. Compose completo (API + frontend + Node) fica para o módulo 006.

**Structure Decision**: Um projeto API e um projeto de testes. Sem camada `Services/` ou `Repositories/` — controllers recebem `TestOrderDbContext` (só para seed/migrate se necessário) e `MySqlConnection` factory para Dapper. SQL como `const string` no topo do controller ou arquivo estático `OrdersQueries.cs` na mesma pasta do controller (não interface).

---

## Decisão de banco e configuração

### Docker Compose (caminho padrão — módulo 001)

Arquivo `docker-compose.yml` na raiz com **apenas** o serviço `mysql`:

| Config | Valor |
| --- | --- |
| Imagem | `mysql:8` |
| Porta publicada | `3306:3306` |
| Database | `testorder` (via `MYSQL_DATABASE`) |
| User / password | `testorder` / `testorder` |
| Root password | `testorder` (dev only) |
| Volume | nomeado `testorder_mysql_data` para persistir dados entre reinícios |
| Healthcheck | `mysqladmin ping` — usado pelo `dev-up.ps1` para aguardar readiness |

O Compose cria banco e usuário automaticamente; **não** exige SQL manual no fluxo principal.

### Connection string padrão (desenvolvimento → MySQL do Docker)

Chave: `ConnectionStrings:Default`

```
Server=localhost;Port=3306;Database=testorder;User=testorder;Password=testorder;
```

- Definida em `appsettings.Development.json`
- Override: variável `ConnectionStrings__Default`
- Alinhada à porta publicada pelo Compose em `localhost:3306`

### MySQL instalado localmente (fallback opcional)

Se o avaliador já tiver MySQL 8 nativo, pode apontar a mesma connection string (ou ajustar porta) **sem** subir o Compose — desde que database/user/senha existam. Documentado como seção secundária em [quickstart.md](./quickstart.md); **não** é o caminho de demonstração recomendado.

### Testes automatizados

Connection string injetada pelo `WebApplicationFactory` apontando para container **Testcontainers** (`mysql:8`). Independente do Compose de dev (Testcontainers sobe seu próprio container efêmero).

### Registro em `Program.cs`

```text
AddDbContext<TestOrderDbContext>(Pomelo, connection string)
AddTransient<MySqlConnection>(sp => new MySqlConnection(connection string))
```

### Startup (Development + Test factory)

Na inicialização da API (e no factory de testes):

1. `db.Database.Migrate()` — aplica schema versionado
2. `await DatabaseSeeder.SeedAsync(db, configuration)` — popula se vazio; no-op se já houver pedidos

O avaliador **não** roda migrations manualmente no caminho feliz.

### Scripts PowerShell

#### `scripts/dev-up.ps1` (comando principal)

Fluxo:

1. Validar que Docker está disponível (`docker info`)
2. `docker compose up -d mysql` (na raiz do repositório)
3. Aguardar MySQL aceitar conexão (loop com `mysqladmin ping` no container ou tentativa `MySqlConnection.Open` até timeout ~60s)
4. `dotnet build TestOrder.slnx` — falha aborta o script
5. `dotnet run --project src/TestOrder.Api` — **foreground** (bloqueia o terminal; Ctrl+C encerra API, container mysql permanece up)

Não sobe a API em container neste módulo.

#### `scripts/test.ps1`

1. Validar Docker (Testcontainers)
2. `dotnet test TestOrder.slnx` — com verbosidade normal; repassa exit code

### Configuração de seed (`Seed` section)

| Chave | Dev | Testes |
| --- | --- | --- |
| `ProductCount` | 50 | 50 |
| `OrderCount` | 5000 | 3000 |
| `MinItemsPerOrder` | 2 | 2 |
| `MaxItemsPerOrder` | 5 | 5 |

---

## Estratégia de seed

1. **Determinístico**: `new Random(42)` para quantidades, produtos por pedido e offsets de data.
2. **Idempotente**: `if (await context.Orders.AnyAsync(ct)) return;` no início do seeder.
3. **Produtos**: lista fixa de ~50 nomes + preços gerados por fórmula (`10 + (i * 7.3m) % 500`).
4. **Pedidos**: loop `OrderCount` com `CreatedAt = referenceDate.AddDays(-random.Next(0, 365))`.
5. **Itens**: por pedido, `itemCount = random.Next(Min, Max+1)`; produtos distintos por pedido no seed; `unit_price` copiado de `Product.UnitPrice`.
6. **Performance**: `AddRange` em lotes de 500 pedidos (com itens filhos) + `SaveChangesAsync` por lote.
7. **Estoque**: `StockQuantity = random.Next(1000, 10000)` por produto — prepara módulo 002.

**Tempo alvo**: seed de 5k pedidos < 30s em dev; 3k < 20s em testes.

---

## Contratos JSON dos endpoints

Detalhamento completo em [contracts/api.md](./contracts/api.md).

| Endpoint | Resumo |
| --- | --- |
| `GET /api/products` | `Array<{ id, name, unitPrice }>` |
| `GET /api/orders` | `{ page, pageSize, totalCount, totalPages, items: Order[] }` |
| `GET /api/orders/{id}` | `Order` ou 404 `{ error }` |

`Order` = `{ id, createdAt, status, total, items: [{ productId, productName, quantity, unitPrice }] }`.

Tipos de resposta implementados como **records** simples em `Controllers/` ou pasta `Models/Responses/` (sem AutoMapper).

---

## Estratégia de paginação

| Regra | Valor |
| --- | --- |
| Padrão `page` | 1 |
| Padrão `pageSize` | 20 |
| Máximo `pageSize` | 100 |
| Ordenação | `orders.created_at DESC` |
| `totalPages` | `ceil(totalCount / pageSize)` |
| Offset SQL | `(page - 1) * pageSize` |

**Validação** (retorno 400):
- `page < 1`
- `pageSize < 1` ou `pageSize > 100`
- Query não numérica → model binding ASP.NET retorna 400 padrão (aceitável)

**Página além do fim**: 200 com `items: []`.

---

## Estratégia de cálculo de total

- **Fonte de verdade**: soma de `order_items.quantity * order_items.unit_price`.
- **Listagem**: subselect correlacionado na query de pedidos (ver [research.md](./research.md) R3).
- **Detalhe por id**: mesmo subselect ou `SUM` em query dedicada.
- **Resposta JSON**: campo `total` decimal; testes validam `total == Sum(items.quantity * items.unitPrice)`.
- **Não persistir** coluna `total` em `orders` neste módulo.

---

## Índices mínimos

Definidos em migration EF (ver [data-model.md](./data-model.md)):

- `IX_orders_created_at` em `orders(created_at)`
- `IX_order_items_order_id` em `order_items(order_id)`
- `IX_order_items_product_id` em `order_items(product_id)`

PKs e FKs com `ON DELETE CASCADE` em `order_items.order_id` para facilitar reset em testes.

---

## Estratégia da suíte de testes

### Projeto

- **Nome**: `tests/TestOrder.Api.Tests`
- **Adicionar** à `TestOrder.slnx` em pasta `/tests/`
- **Pacotes**: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers`, `Testcontainers.MySql`, `Dapper` (asserts SQL diretos opcionais)

### Fixture compartilhada

- `MySqlContainerFixture` (collection fixture xUnit): sobe `mysql:8`, expõe connection string.
- `CustomWebApplicationFactory`: substitui `ConnectionStrings:Default`, executa migrate+seed no `ConfigureWebHost`.
- **Uma collection** para não subir múltiplos containers.

### Perfil de seed em testes

`appsettings.Test.json` com `Seed:OrderCount = 3000` — atende requisito “≥ 3.000 pedidos no perfil de validação”.

### Casos de teste obrigatórios

| Teste | Assert principal |
| --- | --- |
| `Seed_CreatesProducts` | `SELECT COUNT(*) FROM products` = 50 |
| `Seed_CreatesAtLeast3000Orders` | `COUNT orders` ≥ 3000 |
| `GetProducts_Returns200AndNonEmptyList` | status 200, array não vazio, schema id/name/unitPrice |
| `GetOrders_Page1_Returns200_Max20WithMetadata` | ≤ 20 itens, `totalCount` ≥ 3000, `page`/`pageSize`/`totalPages` presentes |
| `GetOrders_OrderTotal_MatchesItemSum` | para cada pedido na página, `total` = soma dos itens |
| `GetOrders_Page1AndPage2_NoDuplicateIds` | interseção de ids vazia |
| `GetOrders_InvalidPagination_Returns400` | `page=0`, `pageSize=0`, `pageSize=101` |
| `GetOrders_DefaultParams_MatchesPage1PageSize20` | sem query == explícito `page=1&pageSize=20` |
| `GetOrderById_Existing_Returns200WithItems` | pedido id=1 (ou primeiro do seed) |
| `GetOrderById_NotFound_Returns404` | id 9_999_999 |

### O que não testar neste módulo

- Performance benchmark automatizado (validação manual/observação)
- POST, reserva, faturamento

### Comandos de validação final

```powershell
.\scripts\test.ps1
# ou diretamente:
dotnet build TestOrder.slnx
dotnet test TestOrder.slnx
```

Demonstração local:

```powershell
.\scripts\dev-up.ps1
```

---

## Decisão: incluir `GET /api/orders/{id}`

**Sim, incluir.** Custo baixo (reuso de SQL de itens + um SELECT por id), benefício alto para React e testes 404/200. Documentado em [research.md](./research.md) R5.

---

## Riscos técnicos do módulo

| Risco | Impacto | Mitigação |
| --- | --- | --- |
| Docker não instalado/parado | Dev e testes falham | Mensagem clara nos scripts; documentar instalação Docker Desktop |
| Porta 3306 ocupada (MySQL nativo + Compose) | Conflito de bind | Parar MySQL local ou alterar porta no Compose + connection string (fallback doc) |
| Seed lento em máquinas fracas | Dev/testes demorados | Inserção em lotes; `OrderCount` configurável |
| Dois containers MySQL (Compose dev + Testcontainers) | Uso de RAM | Aceitável; são fluxos distintos; testes não dependem do Compose de dev |
| N+1 na montagem de itens | Latência > 2s | Máx. 3 queries por request; `IN` clause para itens da página |
| Duplicação de seed | Dados inconsistentes | Guard `Orders.AnyAsync()` |
| Divergência EF snake_case vs Dapper | Queries quebradas | Nomes explícitos em `OnModelCreating`; testes de integração |
| Pomelo + .NET 10 preview | Build instável | Fixar versões NuGet; `dotnet build` no CI local |
| OFFSET grande em páginas altas | Lentidão em página 999 | Aceitável no desafio; índice em `created_at` ajuda ordenação |

---

## Validações manuais

Conforme [quickstart.md](./quickstart.md):

1. `.\scripts\dev-up.ps1` — Docker sobe MySQL, build e API em foreground (migrations + seed automáticos no startup)
2. `curl` em `/api/products`, `/api/orders`, `/api/orders/{id}`
3. Conferir `totalCount` ≥ 3000 na listagem (volume do seed)
4. Conferir total de um pedido vs soma dos itens no JSON
5. Testar paginação e 400 em parâmetros inválidos
6. `.\scripts\test.ps1` — testes de integração com Testcontainers

Opcional: inspecionar volume via cliente SQL conectando em `localhost:3306` (não obrigatório para aprovar o módulo).

Registrar resultados em `docs/PRESENTATION_GUIDE.md`.

---

## Atualizações esperadas em `AI_NOTES.md` (pós-implementação)

- Status do módulo 001: concluído
- Volume final do seed e tempo medido
- Decisão EF vs Dapper com referência aos arquivos reais
- Decisão Docker Compose como padrão local (sem MySQL instalado)
- Uso de `scripts/dev-up.ps1` e `scripts/test.ps1`
- Sugestões de IA recusadas (repositories, AutoMapper, Minimal API)
- Erros da IA corrigidos (se houver): paginação, totais, snake_case
- Prompts Spec Kit usados: specify → plan → tasks → implement

---

## Atualizações esperadas em `docs/PRESENTATION_GUIDE.md` (pós-implementação)

Preencher linha do módulo 001 na tabela de referências:

| Arquivo | O que explicar |
| --- | --- |
| `docker-compose.yml` | MySQL 8 reprodutível sem instalação local |
| `scripts/dev-up.ps1` | Um comando para demo: banco + API |
| `scripts/test.ps1` | Testes com Testcontainers |
| `Data/TestOrderDbContext.cs` | Mapeamento e índices |
| `Data/Seed/DatabaseSeeder.cs` | Determinismo e volume |
| `Controllers/OrdersController.cs` | Paginação, SQL Dapper, montagem JSON |
| `Controllers/ProductsController.cs` | Listagem simples |
| `Migrations/*_Initial.cs` | Schema inicial |
| `tests/.../OrdersEndpointTests.cs` | Como a qualidade é garantida |

Incluir em **Validações**: comandos executados, pass/fail, tempo da 1ª listagem, screenshot ou sample JSON opcional.

---

## Complexity Tracking

Nenhuma violação de simplicidade a justificar. O segundo projeto (`TestOrder.Api.Tests`) é requisito explícito do módulo.

---

## Phase 0 / Phase 1 — Artefatos gerados

| Artefato | Caminho | Status |
| --- | --- | --- |
| Research | [research.md](./research.md) | ✅ |
| Data model | [data-model.md](./data-model.md) | ✅ |
| API contracts | [contracts/api.md](./contracts/api.md) | ✅ |
| Quickstart | [quickstart.md](./quickstart.md) | ✅ |

---

## Próximo passo

Executar `/speckit-tasks` para gerar `tasks.md` com tarefas ordenadas de implementação (incluindo `docker-compose.yml`, `scripts/dev-up.ps1`, `scripts/test.ps1`). **Não implementar código neste prompt.**
