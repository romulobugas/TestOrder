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

**Limitacao explicita**: a validacao acima do fluxo de criacao/erros foi feita via chamadas HTTP diretas ao proxy do Vite (confirmando contrato e proxy), nao via clique manual na interface em um navegador real — o agente de IA nao possui ferramenta de automacao de navegador neste ambiente. A revisao visual final (responsividade, mensagens na tela, estados de loading) deve ser conferida por um humano seguindo o checklist do `quickstart.md`/T026 antes da apresentacao.

### Onde a IA ajudou

- **Spec Kit**: `/speckit-specify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` (duas rodadas) e `/speckit-implement` para gerar e revisar spec/plan/tasks antes de implementar.
- **`/speckit-analyze`** identificou 8 achados (U1, U2, C1, I1-I4, D1) antes do implement: quantidade invalida pouco explicita, ausencia de verificacao de dependencias proibidas no checklist, dependencia `T009 -> T010` implicita, `api.js` descrito como "opcional" quando na pratica e obrigatorio, e paralelismo enganoso entre T018/T019 (mesma preocupacao ja vista no modulo 002). Todos os achados foram corrigidos nos artefatos **antes** de qualquer codigo ser escrito.
- Prompt unico cobrindo T001-T026 (scaffold, api.js, App.jsx, styles.css, build, regressao, docs), respeitando fases e arquivos permitidos.

### Onde a IA foi limitada ou corrigida

- `@vitejs/plugin-react@6.x` exige `vite@^8`; o `plan.md` previa Vite 5.x/6.x — resolvido fixando `@vitejs/plugin-react@^5.2.0` (compativel com Vite 6) em vez de subir para Vite 8, mantendo a decisao original do plano.
- Sem suite de testes automatizados de frontend neste modulo (decisao explicita da spec) — validacao por build + contrato HTTP + checklist manual, nao por testes unitarios/E2E.
- Sem ferramenta de automacao de navegador disponivel — validacao de clique-a-clique na UI depende de revisao humana (ver limitacao acima).

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
- **Detalhe tecnico movido para ca**: durante o desenvolvimento deste script, o agente de IA validou seu comportamento a partir de um shell em sandbox que encerra processos abertos via `Start-Process` ao final de cada chamada de ferramenta — por isso as 3 janelas CMD nao puderam ser observadas "vivas" simultaneamente durante a validacao automatizada nesta sessao; os comandos internos (`dotnet run`, `npm run dev`) foram entao validados diretamente (fora de `Start-Process`) com sucesso. Isso e uma limitacao do ambiente de implementacao, nao do script, que usa o padrao documentado do Windows e funciona normalmente em uma sessao interativa comum do usuario.
- **Validacao desta limpeza**: `dotnet build TestOrder.slnx` PASS, `.\scripts\test.ps1` PASS (46/46). `.\scripts\dev-up.ps1` executado de ponta a ponta: nenhum aviso de porta 5069 (livre no momento do check pre-build), aviso correto de porta 5173 ocupada (entrada TCP remanescente e transitoria do proprio sandbox, sem processo dono identificavel) e mensagem final ajustada corretamente para "check the TestOrder - Web window for the Vite port". O bind da API na porta 5069 falhou nesta sessao por causa dessa mesma instabilidade de rede do sandbox (nao reproduz um defeito do script/codigo); validado com sucesso subindo a API em porta alternativa (`dotnet run --project src\TestOrder.Api --urls http://127.0.0.1:5079`) — `GET /api/products` retornou 200 com 50 produtos — e o frontend (`npm run dev`, que subiu em `5178` apos varias portas ocupadas) retornou 200 com `<title>TestOrder</title>` presente.

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

### Onde a IA foi limitada ou corrigida

- **Processos em background nao sao confiaveis entre chamadas de ferramenta neste ambiente de sandbox**: `Stop-Process` em um PID de wrapper (`pwsh.exe`/`cmd.exe`) nao mata o processo `node.exe` filho real, deixando instancias orfas do worker rodando sem que o agente perceba. Isso gerou confusao inicial durante a validacao E2E (eventos sendo processados por uma instancia orfa, sem log aparecer no terminal esperado). Corrigido identificando os PIDs reais via `Get-CimInstance Win32_Process` (filtrando por `CommandLine`) e adotando scripts PowerShell auto-contidos (inicia processos, testa, limpa, tudo numa unica chamada) para os testes de E2E, concorrencia e resiliencia, evitando depender de estado de processo entre chamadas separadas.
- **Sem console interativo real disponivel para testar `Ctrl+C` da forma literal** (o agente nao tem uma ferramenta de "enviar teclas" a um terminal em foreground) — resolvido com uma tecnica de baixo nivel do Windows (`AttachConsole` + `GenerateConsoleCtrlEvent(CTRL_C_EVENT)` via P/Invoke) que envia o sinal real ao processo do worker, validando o comportamento de shutdown de forma equivalente a um `Ctrl+C` manual, nao apenas por revisao de codigo.
- Sem suite automatizada do worker (decisao explicita da spec, NFR-007/R9) — validacao via smoke `node index.js` + checklist manual objetivo, incluindo os cenarios acima.

### Decisoes manuais

- **JavaScript puro (ESM), sem TypeScript, sem framework** — apenas `mysql2` como dependencia de producao; sem classes, repository pattern, service layer, DI container ou ORM.
- **3 arquivos no worker** (`config.js`, `worker.js`, `index.js`) — suficiente para o escopo, sem pastas `src/`, `lib/` ou camadas adicionais.
- **Sem status intermediario `processing`** — a combinacao `SELECT ... FOR UPDATE SKIP LOCKED` + `UPDATE ... WHERE status='pending'` condicional e suficiente para evitar duplicidade entre instancias, sem precisar de um terceiro estado.
- **Sem alteracao de schema** — idempotencia alcancada com o schema atual da tabela `order_processing_events` (criada no modulo 002).
- **Comentario de codigo restrito ao bloco SQL de concorrencia** em `worker.js`, conforme regra do projeto — nenhum outro comentario narrativo foi adicionado.
