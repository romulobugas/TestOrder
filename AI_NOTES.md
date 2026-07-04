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

### Ajustes de qualidade realizados

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

### Ajustes de qualidade realizados

- Erro de materializacao do Dapper (`COUNT(DISTINCT)` como `BIGINT`) so apareceu ao rodar os testes reais contra MySQL — corrigido trocando `int` por `long` no row interno.
- Evitar CTE recursiva/serie de datas em SQL para os dias zerados — decisao deliberada de preencher em C#, mantendo a query SQL simples e legivel.
- Total de testes do modulo passou de 9 (estimativa inicial do `tasks.md`) para **10 metodos de teste** apos incorporar os 3 casos extras do `/speckit-analyze` (dia unico, borda de 366/367 dias obrigatoria, agregacao do seed) — numero real refletido em `tasks.md`, `quickstart.md` e aqui.

### Decisoes manuais

- **MVC**, sem Minimal APIs — `RevenueController` novo, mesmo padrao de `OrdersController`.
- **Dapper** para a agregacao; nenhuma entidade EF, repository, service generico, interface, MediatR, CQRS ou AutoMapper.
- Dados de teste deterministicos inseridos via SQL direto (produto e datas exclusivos por teste) para os testes de agregacao; teste de regressao usa um intervalo amplo cobrindo o periodo real do seed (`2025-07-02` a `2026-07-01`) para confirmar que pedidos historicos tambem sao contabilizados.
- `OrdersController.cs`, `CreateOrderCommands.cs`, `ProductsController.cs`, `Program.cs`, `TestOrderDbContext.cs` e migrations **nao** foram tocados.

## Modulo 004 - Tela web React para pedidos

**Status: concluido** (T001-T026).

### O que foi implementado

- Projeto novo `src/TestOrder.Web/` (React 18 + Vite 6, **JavaScript puro**, sem TypeScript).
- `src/api.js`: helper local **obrigatorio** com apenas `fetchProducts`, `fetchOrders`, `createOrder` (fetch nativo, sem Axios/service layer generica).
- `src/App.jsx` concentra todo o estado (`useState`/`useEffect`, sem Redux/Zustand/React Query): listagem paginada, formulario de criacao, tratamento de erros 400/409, loading/empty state.
- `src/styles.css`: CSS proprio, sem framework, uma media query (~640px) para empilhar layout em mobile.
- `vite.config.js`: proxy `/api` -> `http://localhost:5069` (sem CORS no backend).
- **Nenhum arquivo de `src/TestOrder.Api/` ou `tests/` foi tocado**.

### Decisoes de UX confirmadas na implementacao

- Quantidade **0, negativa, vazia ou nao numerica** e rejeitada antes de entrar no rascunho (validacao com regex `^-?\d+$` + `Number.isInteger` + `> 0`); mensagem inline, nenhuma chamada de rede.
- Produto duplicado no rascunho e **bloqueado** com mensagem inline (nao soma quantidade), conforme research.md R3.
- Apos `201`: rascunho limpo, mensagem de sucesso, `page` resetado para `1` e nova busca disparada via `refreshKey` (evita fetch duplicado quando a pagina ja era `1`).
- Erros `400`/`409`/rede preservam o rascunho (`draftOrder` nao e resetado); mensagem extraida de `{ error }` do backend.

### Validacao real

| Comando | Resultado |
| --- | --- |
| `dotnet build TestOrder.slnx` (baseline pre-frontend) | PASS |
| `.\scripts\test.ps1` (baseline pre-frontend) | PASS — **46/46** |
| `cd src/TestOrder.Web; npm install` | PASS — 66 pacotes, 0 vulnerabilidades |
| `npm run build` | PASS — `dist/` gerado (~150 KB JS, ~48 KB gzip) |
| Revisao de `package.json` | PASS — dependencias finais: `react`, `react-dom`, `vite`, `@vitejs/plugin-react` (nenhuma proibida) |
| `dotnet build TestOrder.slnx` + `.\scripts\test.ps1` (regressao pos-frontend) | PASS — **46/46** |
| Validacao manual via `curl`/`Invoke-RestMethod` contra o proxy Vite (`http://localhost:5173/api/*`) | PASS — `GET /api/products` (200, 50 itens), `GET /api/orders` (200, paginado), `POST /api/orders` valido (201), produto duplicado no payload (400), quantidade absurda (409) — corpo `{ "error": "..." }` em ambos os casos de erro, compativel com o parsing de `api.js` |

**Validação complementar**: o fluxo de criacao/erros foi validado por chamadas HTTP diretas ao proxy do Vite, confirmando o contrato usado pela interface. A revisao visual final (responsividade, mensagens na tela, estados de loading) esta documentada no checklist do `quickstart.md`/T026 para execução durante a apresentacao.

### Onde a IA ajudou

- **Spec Kit**: `/speckit-specify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` (duas rodadas) e `/speckit-implement` para gerar e revisar spec/plan/tasks antes de implementar.
- **`/speckit-analyze`** identificou 8 achados (U1, U2, C1, I1-I4, D1) antes do implement: quantidade invalida pouco explicita, ausencia de verificacao de dependencias proibidas no checklist, dependencia `T009 -> T010` implicita, `api.js` descrito como "opcional" quando na pratica e obrigatorio, e paralelismo enganoso entre T018/T019 (mesma preocupacao ja vista no modulo 002). Todos os achados foram corrigidos nos artefatos **antes** de qualquer codigo ser escrito.
- Prompt unico cobrindo T001-T026 (scaffold, api.js, App.jsx, styles.css, build, regressao, docs), respeitando fases e arquivos permitidos.

### Ajustes de qualidade realizados

- `@vitejs/plugin-react@6.x` exige `vite@^8`; o `plan.md` previa Vite 5.x/6.x — resolvido fixando `@vitejs/plugin-react@^5.2.0` (compativel com Vite 6) em vez de subir para Vite 8, mantendo a decisao original do plano.
- Sem suite de testes automatizados de frontend neste modulo (decisao explicita da spec) — validacao por build + contrato HTTP + checklist manual, nao por testes unitarios/E2E.
- Validação manual prevista no roteiro — o checklist de UI cobre os passos de clique-a-clique que complementam build e contrato HTTP.

### Decisoes manuais

- **React + Vite + JavaScript** (sem TypeScript), estado 100% local, sem bibliotecas de estado/dados ou UI pesadas.
- `App.jsx` unico (sem subcomponentes extraidos) — numero de elementos da tela nao justificou dividir em arquivos menores.
- `api.js` com exatamente 3 funcoes, sem classes/interfaces/DI.
- `.gitignore` recebeu apenas um append pontual (`src/TestOrder.Web/dist/`) — `node_modules/` ja estava coberto genericamente.

### Correcao pos-implementacao (revisao humana apos primeira entrega)

Tres problemas identificados apos a primeira implementacao, corrigidos em `App.jsx`/`styles.css`:

1. **Overflow horizontal em mobile (~375px)**: causa raiz era o grid `.app-main` sem `minmax(0, 1fr)` na coluna da listagem — um item de grid sem `min-width: 0` nao encolhe abaixo do min-content da tabela, entao a tabela (com celulas largas) empurrava o `body` inteiro para alem da viewport. Corrigido com `minmax(0, 1fr)` no grid, `min-width: 0` em `.panel`, e a tabela agora fica dentro de um `.orders-table-wrapper` com `overflow-x: auto` proprio — o scroll fica confinado ao wrapper, nao ao `body`. `html`/`body` tambem ganharam `overflow-x: hidden` como rede de seguranca.
2. **`itemError` (mensagem de produto duplicado) sobrevivia apos criar pedido com sucesso**: `handleCreateOrder` nao limpava esse estado. Corrigido adicionando `setItemError(null)` no inicio de `handleCreateOrder`, junto com `createError`/`createSuccessMessage`.
3. **`formatDate` deslocava o dia por timezone local**: `toLocaleString('pt-BR')` sem `timeZone` usa o fuso do navegador, podendo mostrar dia diferente do UTC retornado pelo backend (`createdAt` com sufixo `Z`). Corrigido forçando `timeZone: 'UTC'` na formatacao.

**Validacao real em navegador** (nao apenas contrato HTTP desta vez): usado Playwright (Chromium headless) instalado **temporariamente** via `npm install --no-save` em `src/TestOrder.Web` — nao ficou registrado em `package.json`/`package-lock.json`, removido do `node_modules` ao final com `npm install` normal. Resultados:

| Verificacao | Resultado |
| --- | --- |
| Desktop (1280×800) sem erro de console | PASS — `consoleErrors: []`, `pageErrors: []` |
| Mobile (375×667) sem overflow horizontal do body | PASS — `document.documentElement.scrollWidth === clientWidth === 375` |
| Produto duplicado exibe mensagem de erro | PASS |
| Criar pedido valido logo apos o erro de duplicado | PASS — mensagem de sucesso exibida, `itemError` **nao** mais visivel |
| `formatDate` em UTC (testado com timezone de navegador `Pacific/Kiritimati`, UTC+14) | PASS — data renderizada identica ao valor UTC do backend, sem deslocar dia |

Apos a correcao: `npm run build` PASS, `dotnet build` + `.\scripts\test.ps1` PASS — **46/46** intacto.

### Ajuste visual - tema escuro operacional

Pedido: ajustar a UI do `TestOrder` para uma estetica mais operacional (fundo verde-carvao, cards escuros, acentos olive/emerald), somente CSS + pequenos ajustes de markup em `App.jsx`, sem tocar em `api.js`, backend, dependencias ou logica de criacao/listagem.

**O que mudou:**

- `styles.css` reescrito com tokens `:root` para o tema escuro operacional (`--color-background: #171c17`, `--color-card: #1e251e`, `--color-card-hover: #262e26`, `--color-border: #2d362d`, `--color-text-main: #e2e8e0`, `--color-text-muted: #94a390`, mais `--color-primary`/`--color-primary-strong` (olive) e `--color-danger`/`--color-success` para mensagens). Todos os elementos (header, panels, botoes, tabela, inputs, paginacao, badges) passaram a consumir esses tokens em vez de cores hardcoded.
- Tabela de pedidos ganhou visual de dashboard operacional: cabecalho com fundo mais escuro que o card, texto em uppercase/tracking, linhas com hover (`--color-card-hover`) e bordas discretas, tudo em CSS puro.
- Inputs/selects: fundo mais escuro que o card, borda `--color-border`, foco com `box-shadow` na cor primaria (equivalente ao `focus:ring` do Tailwind, sem a dependencia).
- Bordas padronizadas em 6–8px (dentro da faixa 6–10px pedida), sombras evitadas para manter densidade de "tela operacional" em vez de visual de marketing.
- `App.jsx`: adicionado um badge de status compacto no header (`Sistema operacional` / `Instabilidade detectada`, calculado a partir de `productsError`/`ordersError` ja existentes — nenhuma logica nova) e chips de contadores no painel de pedidos (`{totalCount} pedidos`, `pág. {page}/{totalPages}`), substituindo o texto redundante que antes ficava dentro da barra de paginacao (que agora tem so os botoes Anterior/Proxima). Nenhum estado novo foi criado; os valores ja existiam em `pagination`.
- Nenhum emoji usado; nenhum componente novo extraido — tudo permanece em `App.jsx`.

**Validacao:**

| Verificacao | Resultado |
| --- | --- |
| `package.json` / `package-lock.json` | Inalterados (confirmado via `git status --porcelain` e busca por "playwright" nos dois arquivos — nenhuma ocorrencia) |
| `npm run build` | PASS — `dist/` gerado (~151 KB JS / ~48,6 KB gzip, CSS ~6,6 KB / ~1,7 KB gzip) |
| `dotnet build TestOrder.slnx` | PASS — 0 erros |
| `.\scripts\test.ps1` | PASS — **46/46** |
| Validacao visual real (Playwright/Chromium, instalado temporariamente com `npm install --no-save` e removido do `node_modules` ao final) | Desktop 1280×800: `getComputedStyle(body).backgroundColor` = `rgb(23, 28, 23)` (`#171c17`, confirma tema aplicado); 0 `console.error`/`pageerror`. Mobile 375×700: `scrollWidth === clientWidth === 375` em `body` e `documentElement` (sem overflow horizontal). Fluxo completo testado: produto duplicado exibe erro inline, criacao de pedido valido logo apos limpa o erro (`itemError` some, mensagem de sucesso aparece), tabela mantém scroll interno em mobile. |

Screenshots de validacao foram gerados e inspecionados durante a sessao, depois descartados (nao versionados) — nao ha necessidade de mante-los no repositorio.

### Limpeza final — dev-up.ps1 e documentacao

- `scripts/dev-up.ps1`: checagem das portas 5069/5173 movida para **antes** do `dotnet build` (aviso especifico se 5069 estiver ocupada, pois o build pode falhar no Windows com o `.exe` da API em uso); mensagens convertidas para ASCII puro (sem `—`); mensagem final do frontend agora e condicional (`http://localhost:5173` ou aviso para checar a janela `TestOrder - Web` quando a porta estava ocupada). Nenhum processo e encerrado automaticamente pelo script.
- Documentos publicos (`README.md`, `quickstart.md`, `docs/PRESENTATION_GUIDE.md`) tiveram a explicacao longa sobre o comportamento do `Start-Process` simplificada para uma frase curta pedindo confirmacao visual das 3 janelas.
- **Validação reproduzível por comandos locais**: os comandos internos (`dotnet run`, `npm run dev`) foram validados diretamente, e o `dev-up.ps1` foi executado de ponta a ponta com mensagens de porta corretas.
- **Validacao desta limpeza**: `dotnet build`/`.\scripts\test.ps1` PASS (46/46); API validada por `GET /api/products` 200 e frontend respondendo com titulo correto.

## Modulo 005 - Microservico Node para processamento do outbox

**Status: concluido** (T001-T023).

### O que foi implementado

- Projeto novo `src/TestOrder.OrderProcessor/` (Node.js, **JavaScript puro**, ESM, sem TypeScript).
- `config.js`: le variaveis de ambiente com defaults do `docker-compose.yml` (`MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`) e permite override opcional de `POLL_INTERVAL_MS` (default 2000) e `BATCH_SIZE` (default 10).
- `worker.js`: funcao `processPendingEvents(pool)` — transacao curta com `SELECT ... FOR UPDATE SKIP LOCKED` sobre `order_processing_events` (`status='pending' AND event_type='OrderCreated'`), log JSON estruturado por evento e `UPDATE ... SET status='processed' WHERE id=? AND status='pending'` condicional (idempotencia via `affectedRows === 0`, sem erro se outra instancia venceu a corrida).
- `index.js`: bootstrap do pool `mysql2/promise`, loop de polling continuo com `try/catch` (erro loga e continua, nunca `process.exit`), shutdown limpo via `SIGINT` (flag `shuttingDown`, aguarda ciclo em andamento, fecha o pool, so entao `process.exit(0)`).
- `package.json`: **unica** dependencia de producao `mysql2`; script `start`.
- `scripts/dev-up.ps1`: quarta janela `TestOrder - Worker` (`node index.js`), com `npm install` condicional (so roda se `node_modules` nao existir), mesmo padrao ja usado para o frontend.
- **Nenhum schema, migration, backend .NET ou frontend React alterado** — comunicacao 100% via a tabela `order_processing_events` ja existente (modulo 002), sem endpoint HTTP novo, sem broker externo (Rabbit/Kafka/Redis/BullMQ), sem Dockerfile do worker.

### Validacao real

| Comando/cenario | Resultado |
| --- | --- |
| `dotnet build TestOrder.slnx` (baseline e pos-implementacao) | PASS |
| `.\scripts\test.ps1` (baseline e pos-implementacao) | PASS — **46/46**, sem alteracao no backend |
| `cd src/TestOrder.OrderProcessor; npm install` | PASS — 13 pacotes (`mysql2` + deps), 0 vulnerabilidades |
| `node index.js` (smoke, MySQL rodando) | PASS — conecta, processa lote inicial, log JSON limpo |
| Fluxo E2E (API `POST /api/orders` -> worker -> MySQL) | PASS — pedido criado, worker logou `order-created-processed` em ~2s (1 ciclo de polling), linha em `order_processing_events` mudou para `processed` |
| Concorrencia — 2 instancias simultaneas + 3 pedidos novos | PASS — os 3 eventos foram `processed` exatamente uma vez cada; nenhum `eventId` duplicado entre os logs das duas instancias (`FOR UPDATE SKIP LOCKED` funcionando) |
| Fila vazia (worker sozinho, sem eventos pendentes) | PASS — nenhum log de erro/spam durante os ciclos de polling |
| `docker compose stop mysql` com worker rodando | PASS — worker loga `{"level":"error","action":"poll-cycle-failed",...}` a cada ciclo e **continua rodando** (processo nao morre) |
| `docker compose start mysql` + novo pedido | PASS — worker retoma processamento sozinho, sem reinicio manual |
| Shutdown via `Ctrl+C` (simulado com `GenerateConsoleCtrlEvent`/CTRL_C_EVENT do Windows, ja que o ambiente de implementacao nao tem console interativo anexado ao processo em background) | PASS — processo recebeu o sinal e encerrou sozinho (sem necessidade de `Stop-Process -Force`), confirmando o fluxo `SIGINT -> shuttingDown=true -> pool.end() -> process.exit(0)` |
| AC-008 — `npm install` condicional do worker no `dev-up.ps1` | PASS — 1ª execucao (sem `node_modules`) mostrou "Worker dependencies not found - running npm install"; 2ª execucao (com `node_modules` presente) **nao** mostrou essa mensagem |
| AC-004 (opcional) — evento com `event_type` diferente de `OrderCreated` | PASS — inserido manualmente, worker rodou 3 ciclos sem erro e sem log de processamento; linha permaneceu `pending` (fora do SELECT do worker, dead-letter fora de escopo) |
| `package.json` do worker sem dependencia proibida | PASS — apenas `mysql2` |
| `git diff --name-only` apos implementacao | PASS — apenas arquivos do worker, `scripts/dev-up.ps1`, docs e specs; nenhum arquivo de `src/TestOrder.Api/`, `src/TestOrder.Web/`, `tests/` ou migrations tocado |

### Onde a IA ajudou

- **Spec Kit completo**: `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` (uma rodada) e `/speckit-implement` para gerar e revisar spec/plan/tasks antes de qualquer codigo.
- **`/speckit-analyze`** identificou 2 HIGH, 5 MEDIUM e 4 LOW antes do implement: ordem invertida entre validacao manual e documentacao no `tasks.md` (H1), falta de passos reproduziveis para validar o `npm install` condicional/AC-008 (H2), assumption desatualizada sobre variaveis de ambiente de polling (M1), ausencia de validacao explicita para `event_type` fora do contrato/AC-004 (M2), tratamento de `affectedRows === 0` nao explicito nas tarefas (M3), MVP minimo dependendo implicitamente de uma tarefa de fase posterior (M4), ambiguidade sobre o que significa "ignorar" outros `event_type` (M5), alem de referencias cruzadas erradas e duplicacao de tarefa (LOWs). Todos corrigidos nos artefatos **antes** de escrever qualquer linha de codigo.
- Execucao do `/speckit-implement` seguiu `tasks.md` em ordem estrita, com validacao manual real (nao apenas revisao de codigo) para E2E, concorrencia, resiliencia e shutdown.

### Ajustes de qualidade realizados

- **Validação reproduzível por comandos locais**: os testes do worker usam scripts PowerShell autocontidos para iniciar, validar e limpar processos reais, evitando dependencia de estado entre terminais.
- **Shutdown validado com sinal real do Windows**: `AttachConsole` + `GenerateConsoleCtrlEvent(CTRL_C_EVENT)` envia o mesmo sinal tratado pelo worker em `SIGINT`, validando o comportamento equivalente a um `Ctrl+C` manual.
- Sem suite automatizada do worker (decisao explicita da spec, NFR-007/R9) — validacao via smoke `node index.js` + checklist manual objetivo, incluindo os cenarios acima.

### Decisoes manuais

- **JavaScript puro (ESM), sem TypeScript, sem framework** — apenas `mysql2` como dependencia de producao; sem classes, repository pattern, service layer, DI container ou ORM.
- **3 arquivos no worker** (`config.js`, `worker.js`, `index.js`) — suficiente para o escopo, sem pastas `src/`, `lib/` ou camadas adicionais.
- **Sem status intermediario `processing`** — a combinacao `SELECT ... FOR UPDATE SKIP LOCKED` + `UPDATE ... WHERE status='pending'` condicional e suficiente para evitar duplicidade entre instancias, sem precisar de um terceiro estado.
- **Sem alteracao de schema** — idempotencia alcancada com o schema atual da tabela `order_processing_events` (criada no modulo 002).
- **Comentario de codigo restrito ao bloco SQL de concorrencia** em `worker.js`, conforme regra do projeto — nenhum outro comentario narrativo foi adicionado.

## Modulo 006 - Fechamento final da entrega

**Status: concluido** (T001–T022) — auditoria de documentacao e higiene do repositorio antes do envio, sem funcionalidade de negocio nova.

### O que foi feito

- Auditoria de `README.md`, `AI_NOTES.md` (este arquivo) e `docs/PRESENTATION_GUIDE.md` quanto a completude, consistencia e ausencia de placeholders.
- Auditoria de arquivos versionados indevidamente (`node_modules/`, `dist/`, `bin/`, `obj/`, screenshots) e do `.gitignore`.
- Busca por conteudo sensivel (credenciais reais, dados pessoais, projetos externos privados, caminhos locais desnecessarios) em `specs/`, `docs/`, `AI_NOTES.md` e `README.md`.
- Releitura de consistencia de `specs/001-*` a `specs/005-*` (incluindo `quickstart.md` de cada um) e confirmacao de que `dev-up.ps1` e sempre o caminho principal de subida.
- Consolidacao dos numeros do seed de desenvolvimento no `README.md`.
- Criacao do checklist final de entrega em `docs/DELIVERY_CHECKLIST.md`.

### Correcoes pontuais aplicadas

- **`specs/004-tela-web-pedidos/spec.md`** (secao "Checks manuais esperados"): o passo 2 pedia subir o frontend isoladamente (`npm install && npm run dev`), inconsistente com o `dev-up.ps1` atual, que ja sobe backend e frontend com um unico comando (evoluido em modulo posterior ao spec original). Corrigido para refletir `dev-up.ps1` como caminho unico, com nota remetendo ao `quickstart.md` para o comando manual alternativo. Lista renumerada (12 → 11 passos). Justificativa: inconsistencia evidente de primazia do `dev-up.ps1` (auditoria T011–T015 do Modulo 006).
- **`src/TestOrder.Web/src/App.jsx`**: apos validacao manual no navegador, a tela ficava apenas com o fundo verde e o console mostrava `React is not defined`. A causa era JSX transformado para chamadas que esperavam `React` no escopo do modulo, enquanto `App.jsx` importava apenas hooks nomeados. Correcao minima: adicionar o import default `React`, mantendo a estrutura existente.
- **Helper HTTP do frontend**: quando o Vite devolvia HTML em `/api/*` (API desligada, proxy indisponivel ou dev server antigo), a tela exibia o erro tecnico `Unexpected token '<'`. Correcao minima: validar `content-type: application/json` e capturar falha de parse tambem em respostas 200, exibindo a mensagem operacional generica em vez do erro bruto.
- **`.gitignore` / cache Vite**: `src/TestOrder.Web/.vite/` estava com arquivos rastreados (`_metadata.json` e `package.json`) e novos caches apareciam apos `npm run build`. Removido do indice com `git rm --cached` e adicionado `src/TestOrder.Web/.vite/` ao `.gitignore`.
- **`scripts/dev-up.ps1`**: apos a correcao acima, varias instancias antigas do Vite continuavam abertas em portas diferentes e uma delas servia `index.html` em `/api/*`, causando erro visual mesmo com a API correta. O script passou a limpar processos antigos reconhecidos do proprio TestOrder (API, frontend/Vite/esbuild, worker e janelas de log) antes do build, validar que `5069` e `5173` ficaram livres, e falhar com PID/comando se uma porta critica continuar ocupada por processo externo. Validado executando `.\scripts\dev-up.ps1`: encerrou as instancias antigas, subiu API em `5069`, frontend em `5173`, proxy `/api/products` retornou JSON, e a UI carregou com `Sistema operacional` e 20 pedidos.
- Divergencias pontuais entre numeros de teste **estimados em `plan.md`** (fase de planejamento, modulos 001–003) e a contagem final real (refletida corretamente em `quickstart.md`/`tasks.md`/aqui) foram **mantidas sem alteracao** — sao evolucoes normais de planejamento → implementacao; corrigi-las retroativamente no `plan.md` reescreveria contexto historico sem ganho real.

### Resultado da auditoria de conteudo sensivel

Busca com `rg` (case-insensitive) por termos de credencial e por nomes de projetos externos privados conhecidos, em `specs/`, `docs/`, `AI_NOTES.md` e `README.md`, ignorando `node_modules`/`dist`/`bin`/`obj`/`.git`. Unicos resultados: credenciais de desenvolvimento `testorder`/`testorder` (documentadas abertamente como dev-only desde o modulo 001) e o proprio padrao de busca citado nos artefatos deste modulo. Nenhuma ocorrencia de nomes de projetos/empresas externas, caminho pessoal de usuario (`C:\Users\<nome real>`) ou credencial real. Referencias publicas legitimas (artigo da Shopify) mantidas.

### Resultado da auditoria de arquivos versionados

`git status --porcelain --ignored` confirma `node_modules/` (worker e frontend), `dist/` (frontend), `.vite/` (frontend) e `bin/`/`obj/` (backend e testes) corretamente ignorados. `git ls-files -- '*.png' '*.jpg' '*.jpeg' '*.gif'` nao retornou nenhum arquivo — nenhum screenshot temporario versionado. O unico ajuste necessario foi remover o cache `.vite` ja rastreado e adicionar `src/TestOrder.Web/.vite/` ao `.gitignore`.

### Resultados das validacoes finais

Executadas duas vezes nesta sessao (baseline no inicio da Fase 1 e validacao final na Fase 9), com MySQL disponivel (`testorder-mysql` healthy no Docker):

| Validacao | Resultado |
| --- | --- |
| `dotnet build TestOrder.slnx` | PASS — 0 erros (baseline e final) |
| `.\scripts\test.ps1` | PASS — **46/46** (baseline e final) |
| `npm run build` (`src/TestOrder.Web`) | PASS — `dist/` gerado |
| `node index.js` (worker) | PASS — conectou e processou eventos reais sem erro |
| Fluxo real API → outbox `pending` → worker → `processed` | PASS — pedido `#5018` criado via `POST /api/orders`; evento `id=19` mudou de `pending` para `processed` em ~4s (1 ciclo de polling), confirmado por SQL direto e pelo log JSON do worker |
| Validacao visual/frontend pos-correcao | PASS — `npm run build` passou; navegador montou `.app`/`.app-header` sem erro de console; proxy Vite validado em instancia limpa com API temporaria (`/api/products` retornou JSON) |
| `dev-up.ps1` com limpeza ativa | PASS — encerrou instancias antigas do TestOrder, liberou `5069`/`5173`, subiu as janelas novas e `http://localhost:5173/api/products` retornou JSON |
| `git diff --check` | PASS — sem erros de whitespace |
| `git diff --name-only` (escopo) | PASS — documentacao de fechamento + correcao pontual de runtime do frontend (`App.jsx`/`api.js`) e higiene de cache Vite (`.gitignore`/remocao do indice) |

**Validação complementar**: o passo "criar pedido pela tela" foi validado tambem por chamada HTTP direta ao mesmo endpoint (`POST /api/orders`) usado pela tela React, confirmando API, outbox e worker com o mesmo fluxo de dados.

### Reflexao sobre o processo Spec Kit (6 modulos)

Os 6 modulos seguiram o mesmo ciclo Spec Kit (`specify` → `plan` → `tasks` → `analyze` → `implement`), com `/speckit-analyze` usado antes de cada implementacao para revisar cobertura e consistencia — pratica que identificou e corrigiu pontos de qualidade em todos os modulos (ver secoes "Ajustes de qualidade realizados" de 001 a 005, e os achados C1/C2/M1/M2/L1-L4 deste modulo 006) **antes** de qualquer linha de codigo ou documento final ser escrita. O modulo 006 mostrou que o mesmo processo funciona tambem para um modulo puramente documental/auditoria, sem alterar o significado central do fluxo: especificar, planejar, quebrar em tarefas rastreaveis, e so entao executar.

## Modulo 007 - Tela de Faturamento por Periodo

**Status: concluido** (T001–T019) — visualizacao React para o endpoint `GET /api/revenue/daily`, ja implementado e testado desde o modulo 003. Nenhuma regra de negocio nova.

### Decisao central: so visualizacao, nada de negocio novo

- O endpoint `GET /api/revenue/daily` ja existia e ja tinha cobertura de testes (modulo 003) — este modulo **nao** cria endpoint, **nao** altera `src/TestOrder.Api`, **nao** altera `src/TestOrder.OrderProcessor`, **nao** altera schema/migrations.
- Ficaram explicitamente fora de escopo (mantidos assim mesmo sendo tentacoes naturais de "ja que estou mexendo na tela"): edicao de pedido existente, "faturar pedido" (mudanca de status), baixa de estoque a partir da tela de faturamento, alteracao de status de pedido. O conceito de "faturado" simplesmente nao existe no sistema — a aba so mostra a soma que o backend ja calcula.
- O worker Node continua exclusivamente como worker de outbox (consumo de `order_processing_events`); nao ganhou nenhuma responsabilidade nova relacionada a faturamento.

### Decisao de componentizacao

- A implementacao inicial separou apenas a area de faturamento, mas o polish final reorganizou o frontend em uma estrutura minima e mais apresentavel: `App.jsx` como shell, `pages/orders/OrdersPage.jsx`, `pages/revenue/RevenuePage.jsx`, `components/PageNav.jsx`, `api/api.js` e `shared/` para helpers pequenos.
- Essa organizacao nao cria uma arquitetura pesada: nao ha router, store global, pasta de hooks, service layer, DI ou biblioteca de componentes. Cada pagina concentra seu proprio estado e os helpers compartilhados existem porque removem duplicacao real (`dateRanges`, formatacao e paginacao).
- `api/api.js` continua sendo helper local de `fetch` nativo, com funcoes explicitas (`fetchProducts`, `fetchOrders`, `createOrder`, `fetchDailyRevenue`) e tratamento de erro reutilizado.

### Erros comuns de IA evitados deliberadamente

- **Deslocamento de data por timezone**: a tentacao natural seria `new Date('2026-01-15').toLocaleDateString(...)`, mas isso pode exibir `14/01/2026` em fusos negativos (o construtor interpreta a string como meia-noite UTC). `formatCalendarDate` faz apenas `split('-')` e remonta como `DD/MM/YYYY`, sem nunca instanciar `Date` a partir da string do backend.
- **Tratar intervalo vazio como erro**: o backend retorna `200` com `totalRevenue=0`/`totalOrders=0`/dias zerados para intervalos sem pedidos (comportamento correto do modulo 003) — a tela nao trata isso como excecao; `revenueError` so e preenchido em falha real (400/rede). O total zerado aparece normalmente, sem mensagem de erro.
- **Resposta tardia ao trocar de aba**: a revisao evitou transformar isso em mecanismo global. `RevenuePage.jsx` usa um `requestId` local para descartar resposta superada por outra consulta; ao sair da aba, a pagina desmonta e nao compartilha resultado com `Pedidos`.
- **Ajuste antes do commit**: adicionados atalhos de periodo (`Hoje`, `7 dias`, `15 dias`, `30 dias`, `90 dias`, `Ultimo ano`) tambem em `Pedidos`, datas opcionais no faturamento e paginacao numerada compartilhada com `Inicio`/`Fim`. O polish reorganizou o frontend em `pages/`, `components/`, `api/` e `shared/` sem dependencia, router ou store.
- **Auto-fetch ao abrir a aba**: os campos de data vem preenchidos com defaults (1º dia do mes corrente ate hoje), mas a consulta HTTP so acontece no clique em `Consultar` — evita uma chamada de rede nao solicitada pelo usuario ao simplesmente trocar de aba.

### Onde a IA ajudou

- **Spec Kit completo**: `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` e `/speckit-implement`, na ordem, antes de qualquer linha de codigo.
- **`/speckit-analyze`** encontrou 5 achados MEDIUM antes do implement (nenhum CRITICAL/HIGH): ambiguidade sobre auto-fetch no FR-004 (I1), formato de data exigindo string identica em vez de mesmo dia calendario formatado (I2), divisao de responsabilidades do frontend nao explicita no contrato (D1), ordem de dependencia errada entre tarefas de documentacao e checklist manual (D2), cenario de intervalo > 366 dias faltando na tarefa de validacao final (C1). Todos corrigidos em `spec.md`/`plan.md`/`research.md`/`data-model.md`/`contracts/ui.md`/`tasks.md`/`checklists/requirements.md` antes de escrever qualquer codigo.

### Ajustes de qualidade realizados

- **Revisao posterior ao implement encontrou um bug de estado assincrono**: ao sair da aba `Faturamento` durante uma consulta, a resposta era descartada corretamente, mas o `loadingRevenue` tambem deixava de ser finalizado. Corrigido em `App.jsx`: `revenue`/`revenueError` continuam ignorados quando a resposta fica obsoleta, enquanto o loading e encerrado se a resposta pertence a ultima consulta conhecida.
- **Validação complementar de UI**: a aba `Faturamento`, a consulta com datas default, as mensagens de validação e a ausência de overflow horizontal foram revisadas no navegador; cenarios de intervalo customizado, intervalo vazio e data invertida tambem foram conferidos por chamada HTTP direta ao proxy real do Vite e por revisao do handler.
- O endpoint consumido ja estava estavel e testado desde o modulo 003, entao a superficie de risco deste modulo era essencialmente o frontend novo.

### Resultados das validacoes

| Validacao | Resultado |
| --- | --- |
| `npm run build` (`src/TestOrder.Web`) | PASS — `dist/` gerado sem erros |
| `dotnet build TestOrder.slnx` | PASS — 0 erros, nenhum arquivo de backend alterado |
| `.\scripts\test.ps1` | PASS — **46/46** (baseline e final, identico ao modulo 006) |
| `.\scripts\dev-up.ps1` | PASS — 4 janelas sobem normalmente; script nao foi alterado |
| Navegador real, aba `Faturamento` | PASS — renderizou a aba, consultou o periodo default, exibiu total/tabela e manteve `scrollWidth === clientWidth` em desktop |
| Navegador real, datas obrigatorias | PASS — campos vazios exibem `Informe as duas datas do período.`, sem loading preso e preservando o resultado anterior |
| Atalhos de periodo + paginacao numerada | PASS — `npm run build`; revisao de codigo confirmou alteracao restrita ao frontend/docs |
| `GET /api/revenue/daily` via proxy Vite (`2025-07-02` a `2026-07-01`) | PASS — `totalRevenue=6018360.60`, `totalOrders=4522`, `days[]` completo com valores diarios |
| `GET /api/revenue/daily` intervalo vazio (`2030-01-01` a `2030-01-03`) | PASS — `200`, totais e dias zerados, sem erro |
| `GET /api/revenue/daily` `startDate > endDate` | PASS — `400`, `{"error":"startDate must not be after endDate."}` |
| Bundle final contem `Faturamento`/`Consultar`; sem `react-router`/`Redux` | PASS |
| `package.json` sem dependencia nova | PASS — continua so `react`, `react-dom`, `vite`, `@vitejs/plugin-react` |
| `git status --short` (escopo) | PASS — frontend/docs do modulo 007, sem worker, migrations ou dependencia nova |

### Prompts Spec Kit usados

`/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` (uma rodada, 5 achados MEDIUM corrigidos), `/speckit-implement` — mesma sequencia dos modulos anteriores.

## Modulo 007 (follow-up) - Filtros de pedidos, datas opcionais no faturamento e paginacao de dias

**Status: concluido.** Pedido direto do usuario, sem nova rodada completa de Spec Kit (a especificacao ja chegou detalhada e sem ambiguidade) — correcao de entendimento + extensao do modulo 007, com as unicas mudancas de backend feitas por este modulo (`GET /api/orders`, `GET /api/revenue/daily`).

### Correcao de entendimento: duplo clique, nao dois cliques em botao

- **Entendimento errado que foi corrigido antes de qualquer codigo**: um "clique duplo" não é confirmação em dois cliques sobre um botão `Limpar`. O padrao correto e: **duplo clique dentro do proprio campo** (`<input type="date">`, `<select>`) limpa **apenas aquele campo** (select volta ao valor padrao `Todos`); os botoes `Limpar filtros` (Pedidos) e `Limpar datas` (Faturamento) continuam funcionando normalmente com **1 clique** e limpam todos os campos da secao de uma vez.
- O duplo clique **so limpa o valor do campo** — nao dispara busca. A busca so e refeita quando o usuario clica em `Filtrar` (Pedidos) ou `Consultar` (Faturamento). Isso evita side-effect surpreendente (limpar e já buscar sozinho) e mantem a semantica de que o duplo clique se comporta como "selecionar tudo e apagar" dentro do campo.
- Implementado reaproveitando callbacks locais: `RevenuePage.jsx` limpa suas datas via `onDoubleClick`, e `OrdersPage.jsx` limpa `status`/datas do rascunho do filtro com o mesmo padrao — sem hook generico ou camada compartilhada so para isso.

### Filtros de Pedidos sao server-side (decisao deliberada)

- `GET /api/orders` ganhou `status`, `startDate`, `endDate` como query params **opcionais**. Justificativa: a listagem de pedidos e paginada e tem milhares de registros (5000+ no seed) — filtrar no cliente exigiria buscar tudo para filtrar em memoria, o que quebra a paginacao real e o proposito do `LIMIT/OFFSET`. Fazer o filtro no banco (WHERE dinamico, ainda parametrizado) e a unica opcao que preserva a paginacao com contagem correta.
- Regras: `status` vazio ou ausente = sem filtro (a UI usa `""` para "Todos"); datas vazias = sem limite naquele lado; `startDate > endDate` quando as duas vierem preenchidas = `400`. Intervalo de data usa o mesmo padrao semiaberto do faturamento (`created_at >= startDate AND created_at < endDate + 1 dia`), pelo mesmo motivo (evita problema de fracao de segundo no limite superior e mantem o indice de `created_at` usavel sem funcao na coluna).
- SQL construido em `OrdersQueries.BuildCountOrders`/`BuildPageOrders` com uma clausula `WHERE` opcional montada em `OrdersController.BuildWhereClause` — sempre parametrizada (`@Status`/`@StartUtc`/`@EndExclusiveUtc`), nunca concatena valor de usuario direto na string; so a **presenca** das condicoes varia, nunca o valor.
- Clicar `Filtrar` reseta a pagina para 1 (novo resultado, nova contagem); trocar de pagina com filtro ativo mantem o filtro (os filtros aplicados sao estado separado do rascunho dos campos, entao a pagina so muda o `page`, nao o filtro).

### Paginacao da tabela de dias do Faturamento e client-side (decisao deliberada, oposta a de Pedidos)

- Diferente de Pedidos, a tabela de dias do `Faturamento` **nao** ganhou paginacao no backend. Justificativa: `GET /api/revenue/daily` sempre devolve a lista **inteira e ja agregada por dia** (no maximo 366 linhas quando o intervalo e fechado; sem limite fixo quando aberto, mas ainda pequeno na pratica porque só existem dias com pedido real) — nao ha motivo para outra chamada HTTP so para trocar de pagina de uma lista que ja esta inteira em memoria no browser.
- Extraido um componente `PageNav.jsx` (apresentacao pura, sem estado de negocio) reaproveitando a mesma logica de `pagination.js` (`buildPageItems`) e o mesmo markup/classes CSS que a paginacao de `Pedidos` ja usava desde o modulo 004 — por isso o padrao visual `Inicio | Anterior | numeros | Proxima | Fim` e identico nas duas telas, sem duplicar JSX.
- O estado de "qual pagina de dias esta visivel" vive **dentro de `RevenuePage.jsx`** (um `useState` local) — e puramente uma preferencia de exibicao sobre dados que ja chegaram, nao envolve HTTP nem precisa ser compartilhado; um `useEffect` reseta a pagina para 1 sempre que chega um novo resultado (`revenue` muda de identidade).

### Datas opcionais no faturamento — comportamento revisado

- `GET /api/revenue/daily` deixou de exigir as duas datas. Regras novas: datas vazias nao filtram; se as duas vierem vazias, agrega **todos os dias que realmente tem pedido** (sem preencher dias zerados — nao ha um intervalo fechado para "explodir" em zeros); se so uma vier vazia, mesma logica (lado aberto, sem zero-fill); se as duas vierem preenchidas, o comportamento antigo e preservado integralmente (preenche cada dia do intervalo, incluindo os zerados).
- O limite de 366 dias do backend so se aplica quando as duas datas sao conhecidas (e o unico caso em que ha um laco dia-a-dia que poderia gerar uma resposta grande); sem esse laco no caso aberto, o limite deixou de fazer sentido e foi removido para esse cenario.
- **Comportamento anterior removido do frontend**: ao consultar com as duas datas vazias, a tela nao sintetiza mais um intervalo dos ultimos 366 dias (`getRecentRevenueRange(366)`) antes de chamar o backend. Agora `handleConsultRevenue` so valida `startDate > endDate` quando as duas vierem preenchidas, e repassa as datas (vazias ou nao) direto para `fetchDailyRevenue`, que por sua vez (`api.js`) so inclui no `URLSearchParams` os parametros que tiverem valor.
- Os atalhos de periodo (`Hoje`, `7 dias`, ..., `Ultimo ano`) e o botao `Limpar datas` continuam existindo e inalterados na semantica de 1 clique — nao fazem parte da correcao pedida, so o metodo de consulta com datas vazias mudou.

### Nenhuma alteracao no worker Node

- `src/TestOrder.OrderProcessor` nao foi tocado neste follow-up — o worker continua exclusivamente como consumidor de `order_processing_events`. As unicas mudancas de backend deste modulo foram em `TestOrder.Api` (`OrdersController`, `OrdersQueries`, `RevenueController`, `RevenueQueries`, `Models/Responses/ApiResponses.cs`).
- Schema/migrations tambem nao foram alterados — os filtros novos usam colunas (`status`, `created_at`) que ja existiam desde o modulo 001.

### Testes adicionados

- `OrdersEndpointTests.cs`: `GetOrders_FilterByStatus_ReturnsOnlyMatchingStatus`, `GetOrders_EmptyStatus_BehavesAsNoFilter`, `GetOrders_FilterByDateRange_ReturnsOnlyOrdersWithinRange`, `GetOrders_FilterStartAfterEnd_Returns400`, `GetOrders_FilterInvalidDateFormat_Returns400` (Theory, 3 casos), `GetOrders_FilterWithoutDates_DoesNotLimitByDate`, `GetOrders_FilterKeepsPaginationMetadataConsistent`.
- `RevenueEndpointTests.cs`: `GetDailyRevenue_MissingStartDate_TreatedAsOpenLowerBound` e `GetDailyRevenue_MissingEndDate_TreatedAsOpenUpperBound` (substituem os antigos testes que esperavam `400`), `GetDailyRevenue_BothDatesEmpty_AggregatesAllAvailableDays`, `GetDailyRevenue_OnlyStartDate_ReturnsOnlyRealDaysWithoutZeroFill`, `GetDailyRevenue_OnlyEndDate_ReturnsOnlyRealDaysWithoutZeroFill`; o caso `("", "2030-01-10")` foi removido de `GetDailyRevenue_InvalidDate_Returns400` porque data vazia deixou de ser erro.
- Suite final: **57/57** (46 anteriores + 11 novos/reformulados).

### Resultados das validacoes

| Validacao | Resultado |
| --- | --- |
| `dotnet build TestOrder.slnx` | PASS — 0 erros |
| `.\scripts\test.ps1` | PASS — **57/57** |
| `npm run build` (`src/TestOrder.Web`) | PASS — `dist/` gerado sem erros |
| `git diff --check` | PASS — sem erros de whitespace |
| `package.json` (`src/TestOrder.Web`) | PASS — sem dependencia nova (`react`, `react-dom`, `vite`, `@vitejs/plugin-react`, inalterado) |

### O que foi alterado (resumo por camada)

- **Backend** (unico modulo de follow-up que tocou `TestOrder.Api`): `OrdersController.cs`, `OrdersQueries.cs`, `RevenueController.cs`, `RevenueQueries.cs`, `Models/Responses/ApiResponses.cs` (`DailyRevenueResponse.StartDate`/`EndDate` agora `string?`).
- **Testes**: `OrdersEndpointTests.cs`, `RevenueEndpointTests.cs`, `ApiDtos.cs` (`DailyRevenueDto` com datas nullable).
- **Frontend**: `api/api.js` (`fetchOrders` com filtros, `fetchDailyRevenue` sem parametros vazios), `App.jsx` (shell de abas), `pages/orders/OrdersPage.jsx` (criacao/listagem/filtros de pedidos), `pages/revenue/RevenuePage.jsx` (consulta/paginacao local de faturamento), `components/PageNav.jsx`, `shared/dateRanges.js`, `shared/formatters.js`, `shared/pagination.js` e `styles.css`.
- **Worker**: nenhuma alteracao.
- **Migrations/schema**: nenhuma alteracao.

## Modulo 007 (polish de apresentacao) - Presets em Pedidos e organizacao minima do frontend

**Status: concluido.** Ajustes finais antes do commit, **somente frontend + documentacao** — backend, worker, migrations/schema e testes backend nao foram alterados nesta etapa.

### Presets de periodo como padrao dos filtros com datas

- Os atalhos `Hoje`, `7 dias`, `15 dias`, `30 dias`, `90 dias` e `Ultimo ano` passaram a existir tambem nos filtros de `Pedidos`, nao so em `Faturamento`.
- Ao clicar em um preset em `Pedidos`, preenche `Data inicial` e `Data final` do rascunho do filtro; **nao dispara busca** — o usuario ainda precisa clicar `Filtrar`. O `status` selecionado e preservado.
- A logica de presets e datas locais foi centralizada em `shared/dateRanges.js` (`DATE_PRESETS`, `getRecentRange`, `getDefaultRevenueRange`).

### Organizacao minima do frontend (para apresentacao, nao arquitetura)

- `App.jsx` virou shell: header, abas `Pedidos`/`Faturamento`, estado `activeTab`, renderiza `OrdersPage` ou `RevenuePage`.
- `pages/orders/OrdersPage.jsx`: formulario de criacao, listagem, filtros server-side, presets, paginacao.
- `pages/revenue/RevenuePage.jsx`: consulta de faturamento, presets, paginacao client-side dos dias.
- `components/PageNav.jsx`: paginacao numerada compartilhada (Inicio/Anterior/numeros/Proxima/Fim).
- `api/api.js`: helpers fetch locais, sem classe/DI/service layer.
- `shared/`: `dateRanges.js`, `formatters.js`, `pagination.js`.
- Um unico `styles.css`; sem pastas `hooks/`, `contexts/`, `services/` ou `layouts/`.

### Divergencia documental corrigida (modulo 003 vs follow-up 007)

- O modulo 003 original exigia `startDate`/`endDate` em `GET /api/revenue/daily` (ausencia → 400). O follow-up do modulo 007 tornou essas datas opcionais.
- Adicionada nota de evolucao em `specs/003-faturamento-por-periodo/spec.md`, `contracts/api.md` e `quickstart.md` para nao contradizer o comportamento atual.

### Resultados das validacoes (esta etapa)

| Validacao | Resultado |
| --- | --- |
| `npm run build` (`src/TestOrder.Web`) | PASS |
| `dotnet build TestOrder.slnx` | PASS — 0 erros (backend inalterado) |
| `.\scripts\test.ps1` | PASS — **57/57** (testes inalterados) |
| `git diff --check` | PASS |
| Busca por referencias a projetos externos privados | PASS — zero ocorrencias |
| `package.json` | PASS — inalterado |
