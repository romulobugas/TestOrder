# Guia de Apresentacao - TestOrder

Este documento e para apoiar a apresentacao presencial. Ele deve ser atualizado ao fim de cada modulo com decisoes, validacoes e referencias de codigo.

## Mensagem central

O projeto foi construido como uma solucao pequena e explicavel, priorizando boas decisoes em vez de excesso de arquitetura. A regra foi manter o codigo perto do problema: controllers MVC, modelo claro, SQL direto onde performance importa e documentacao viva do uso de IA.

## Decisoes iniciais

- **ASP.NET Core MVC, nao Minimal APIs**: escolha feita para manter uma organizacao familiar por controllers sem cair em camadas artificiais.
- **Sem Clean Architecture/DDD/CQRS por ritual**: o dominio do desafio e pequeno; criar muitas pastas e interfaces atrapalharia a leitura.
- **MySQL 8**: escolhido para demonstrar concorrencia com `FOR UPDATE SKIP LOCKED`, alinhado ao artigo da Shopify sobre reservas de inventario.
- **Docker como padrao local (modulo 001)**: MySQL sobe via `docker-compose.yml`; nao exige MySQL instalado na maquina. Comando principal: `.\scripts\dev-up.ps1`. MySQL nativo e apenas fallback opcional.
- **EF Core + Dapper**: EF Core para modelo, schema e seed; Dapper para consultas e pontos onde SQL explicito ajuda performance e clareza.
- **Microservico Node sem fila externa**: quando entrar, ele processara uma tabela de outbox/fila no proprio MySQL, evitando RabbitMQ/Kafka/Redis.
- **Spec Kit por modulo**: cada modulo tera spec, plano, tarefas, implementacao e revisao.
- **IA com trilha auditavel**: o repositorio inclui Spec Kit para Cursor, Claude, Codex e Antigravity, alem de `AI_NOTES.md` e `docs/SPECKIT_SETUP.md`.

## Ordem de apresentacao planejada

1. Base, modelo e listagem de pedidos. **(concluido)**
2. Criacao de pedidos com reserva concorrente. **(concluido)**
3. Faturamento por periodo. **(concluido)**
4. Tela React. **(concluido)**
5. Microservico Node e outbox.
6. README, AI_NOTES e decisoes finais.

## Referencias externas

- Shopify Engineering: https://shopify.engineering/scaling-inventory-reservations
- GitHub Spec Kit: https://github.com/github/spec-kit

## Referencias de codigo

| Modulo | Arquivo | O que explicar |
| --- | --- | --- |
| Setup | `docs/SPECKIT_SETUP.md` | Como a IA foi organizada para trabalhar por especificacao e modulo |
| 001 | `docker-compose.yml` | MySQL 8, volume persistente, credenciais dev |
| 001 | `scripts/dev-up.ps1` | Comando unico de demo: compose + build + run |
| 001 | `scripts/test.ps1` | Valida Docker + `dotnet test` com Testcontainers |
| 001 | `src/TestOrder.Api/Program.cs` | MVC, EF, Dapper, migrate+seed no startup, JSON UTC |
| 001 | `src/TestOrder.Api/Data/TestOrderDbContext.cs` | Schema EF, snake_case, indices |
| 001 | `src/TestOrder.Api/Data/Seed/DatabaseSeeder.cs` | Seed configuravel (`Seed:*`), guard anti-duplicacao |
| 001 | `src/TestOrder.Api/Controllers/ProductsController.cs` | `GET /api/products` com Dapper |
| 001 | `src/TestOrder.Api/Controllers/OrdersController.cs` | Paginacao, validacao 400, detalhe 404 |
| 001 | `src/TestOrder.Api/Controllers/OrdersQueries.cs` | SQL da listagem (3 queries, total por subselect) |
| 001 | `src/TestOrder.Api/Json/UtcDateTimeJsonConverter.cs` | `createdAt` em UTC com sufixo `Z` |
| 001 | `tests/TestOrder.Api.Tests/Integration/` | Fixture Testcontainers, factory, 17 testes |
| 002 | `src/TestOrder.Api/Controllers/OrdersController.cs` | `POST /api/orders`, validacao 400 pre-transacao |
| 002 | `src/TestOrder.Api/Controllers/CreateOrderCommands.cs` | Transacao Dapper: SKIP LOCKED, outbox, comentarios de concorrencia |
| 002 | `src/TestOrder.Api/Data/Seed/InventoryUnitsBackfill.cs` | Backfill idempotente ~237k unidades em dev |
| 002 | `src/TestOrder.Api/Migrations/20260703184137_AddInventoryAndOutbox.cs` | Schema: inventory_units, reservas, outbox, customer_name |
| 002 | `tests/TestOrder.Api.Tests/Integration/CreateOrderEndpointTests.cs` | 15 testes POST + regressao smoke (32 total na suite) |
| 003 | `src/TestOrder.Api/Controllers/RevenueController.cs` | `GET /api/revenue/daily`, validacoes 400, preenchimento de dias zerados |
| 003 | `src/TestOrder.Api/Controllers/RevenueQueries.cs` | SQL agregado unico, intervalo semiaberto UTC |
| 003 | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` | 10 metodos de teste (14 casos) — agregacao, validacao, regressao (46 total na suite) |
| 004 | `src/TestOrder.Web/src/App.jsx` | Estado local, listagem paginada, formulario, validacoes, tratamento 400/409 |
| 004 | `src/TestOrder.Web/src/api.js` | Helper obrigatorio: `fetchProducts`, `fetchOrders`, `createOrder` |
| 004 | `src/TestOrder.Web/vite.config.js` | Proxy `/api` -> `http://localhost:5069`, sem CORS no backend |
| 004 | `src/TestOrder.Web/src/styles.css` | CSS proprio, responsivo, sem framework |

## Modulo 001 — roteiro de demo (5–10 min)

1. **Subir ambiente**: `.\scripts\dev-up.ps1` — mostrar logs de migration/seed na primeira execucao.
2. **Produtos**: `curl http://localhost:5069/api/products` — 50 itens, campos `id`, `name`, `unitPrice`.
3. **Pedidos paginados**: `curl "http://localhost:5069/api/orders?page=1&pageSize=5"` — `totalCount=5000`, itens aninhados, `total` coerente.
4. **Contrato JSON**: apontar `createdAt` terminando em `Z` (UTC).
5. **Erros**: `page=0` → 400; id inexistente → 404.
6. **Performance observacional**: primeira pagina (`pageSize=20`) responde em < 2s em demo local (meta do modulo).
7. **Testes**: `.\scripts\test.ps1` — 17/17 com MySQL efemero via Testcontainers.
8. **Arquitetura**: EF para schema/seed; Dapper para leituras; SQL visivel em `OrdersQueries.cs`.

### Numeros do seed (dev)

| Metrica | Valor |
| --- | --- |
| Produtos | 50 |
| Pedidos | 5000 |
| Itens | 17499 |
| Media itens/pedido | 3,50 |

## Validacoes — Modulo 001

| Validacao | Status | Evidencia |
| --- | --- | --- |
| `dotnet build TestOrder.slnx` | PASS | Build sem erros |
| `.\scripts\test.ps1` | PASS | 17/17 testes |
| `dotnet test TestOrder.slnx` | PASS | Idem |
| `GET /api/products` | PASS | 200, 50 produtos |
| `GET /api/orders?page=1&pageSize=5` | PASS | 200, totalCount=5000 |
| `GET /api/orders?page=999&pageSize=20` | PASS | 200, lista vazia |
| `GET /api/orders?page=0&pageSize=20` | PASS | 400 + error |
| `GET /api/orders/1` | PASS | 200 com itens |
| `GET /api/orders/99999999` | PASS | 404 |
| `createdAt` com sufixo `Z` | PASS | `UtcDateTimeJsonConverter` + testes |
| Paginacao sem IDs duplicados | PASS | Teste + desempate `id DESC` |
| Primeira pagina < 2s (demo local) | PASS | Observacional com seed 5k |

## Modulo 002 — roteiro de demo (5–10 min)

### Mensagem para a sala

Inspirado no artigo da Shopify sobre reservas por linhas bloqueaveis, mas adaptado de forma **pequena e explicavel** com MySQL 8: uma linha em `inventory_units` = uma unidade vendavel. A reserva usa `SELECT ... FOR UPDATE SKIP LOCKED` — threads concorrentes pegam unidades diferentes sem fila externa (sem Rabbit/Kafka/Redis). Tudo fica visivel em SQL ao lado do controller.

### Passos

1. **Subir ambiente**: `.\scripts\dev-up.ps1` — na primeira execucao apos migration 002, aguardar backfill (~237k unidades em dev).
2. **POST sucesso (201)**:
   ```powershell
   curl -s -X POST http://localhost:5069/api/orders -H "Content-Type: application/json" -d "{\"customerName\":\"Demo\",\"items\":[{\"productId\":1,\"quantity\":2}]}"
   ```
   Apontar `createdAt` com `Z`, `total` = soma dos itens, header `Location`.
3. **POST invalido (400)** — items vazio:
   ```powershell
   curl -s -w "`n%{http_code}" -X POST http://localhost:5069/api/orders -H "Content-Type: application/json" -d "{\"items\":[]}"
   ```
4. **POST estoque insuficiente (409)**:
   ```powershell
   curl -s -w "`n%{http_code}" -X POST http://localhost:5069/api/orders -H "Content-Type: application/json" -d "{\"items\":[{\"productId\":1,\"quantity\":999999999}]}"
   ```
5. **SQL — reservas** (substituir `{ORDER_ID}`):
   ```sql
   SELECT COUNT(*) FROM order_reservation_units WHERE order_id = {ORDER_ID};
   SELECT status, COUNT(*) FROM inventory_units GROUP BY status;
   ```
6. **SQL — outbox pending**:
   ```sql
   SELECT event_type, status, payload FROM order_processing_events WHERE order_id = {ORDER_ID};
   -- OrderCreated, pending, {"orderId":...}
   ```
7. **GET confirma pedido**: `curl -s http://localhost:5069/api/orders/{ORDER_ID}` — mesmo shape do modulo 001.
8. **Concorrencia (teste automatizado)**: 10 POSTs paralelos, 5 unidades → 5×201 + 5×409, zero overbooking.
9. **Testes**: `.\scripts\test.ps1` — **32/32**.

### Trecho SQL central (CreateOrderCommands.cs)

```sql
SELECT id FROM inventory_units
WHERE product_id = @ProductId AND status = 'available'
ORDER BY id
LIMIT @Quantity
FOR UPDATE SKIP LOCKED
```

Itens processados em ordem **`product_id ASC`** dentro da transacao **READ COMMITTED** para reduzir deadlock.

## Validacoes — Modulo 002

| Validacao | Status | Evidencia |
| --- | --- | --- |
| `dotnet build TestOrder.slnx` | PASS | Build sem erros |
| `.\scripts\test.ps1` | PASS | 32/32 testes |
| `dotnet test TestOrder.slnx` | PASS | Idem |
| Migration `20260703184137_AddInventoryAndOutbox` | PASS | Aplicada no startup |
| Backfill idempotente | PASS | ~237k `inventory_units` em dev; segunda subida nao duplica |
| `POST /api/orders` valido | PASS | 201 + Location + OrderResponse |
| `POST` items vazio | PASS | 400 + error |
| `POST` productId duplicado | PASS | 400 + error |
| `POST` qty > estoque | PASS | 409 + rollback total |
| Outbox `pending` | PASS | `order_processing_events` na mesma transacao |
| Concorrencia 5/5 | PASS | Teste `CreateOrder_ConcurrentRequests_DoNotOverbook` |
| GET modulo 001 intacto | PASS | products, orders, orders/{id} → 200 |
| `products.stock_quantity` nao decrementado | PASS | Legado/indicador apenas |

### Comandos de validacao final

```powershell
# Parar API se estiver rodando (Windows — evita lock do .exe)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

### Observacao operacional

No Windows, encerrar a API antes de `dotnet build`/`dotnet test` quando ela foi iniciada com `dev-up.ps1` ou `dotnet run` — o executavel fica em uso e o build falha.

## Modulo 003 — roteiro de demo (5 min)

### Mensagem para a sala

"Faturamento" aqui e estritamente bruto — soma de `quantity * unitPrice` dos itens de pedidos com status `created`, **nao** e nota fiscal, pagamento ou baixa financeira. O endpoint e leitura pura: nenhuma tabela nova, nenhuma alteracao em pedidos/reservas/estoque. Uma unica consulta SQL agregada por dia; os dias sem pedido sao preenchidos com zero na camada C#.

### Passos

1. **Subir ambiente**: `.\scripts\dev-up.ps1` (nenhuma migration nova neste modulo).
2. **Intervalo com pedidos (200)**:
   ```powershell
   curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-01-01&endDate=2026-01-31"
   ```
   Apontar `days` com 31 entradas, `totalRevenue`/`totalOrders` = soma dos dias.
3. **Intervalo sem pedidos (200 zerado)**:
   ```powershell
   curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2099-01-01&endDate=2099-01-07"
   ```
4. **Erro 400 (data invalida)**:
   ```powershell
   curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-13-40&endDate=2026-01-31"
   ```
5. **SQL de conferencia**:
   ```sql
   SELECT DATE(o.created_at) AS day, SUM(oi.quantity * oi.unit_price) AS revenue, COUNT(DISTINCT o.id) AS order_count
   FROM orders o
   INNER JOIN order_items oi ON oi.order_id = o.id
   WHERE o.status = 'created'
     AND o.created_at >= '2026-01-01'
     AND o.created_at < '2026-02-01'
   GROUP BY DATE(o.created_at);
   ```
6. **Testes**: `.\scripts\test.ps1` — **46/46**.

### Trecho SQL central (RevenueQueries.cs)

```sql
SELECT DATE(o.created_at) AS Date,
       SUM(oi.quantity * oi.unit_price) AS Revenue,
       COUNT(DISTINCT o.id) AS OrderCount
FROM orders o
INNER JOIN order_items oi ON oi.order_id = o.id
WHERE o.status = 'created'
  AND o.created_at >= @StartUtc
  AND o.created_at < @EndExclusiveUtc
GROUP BY DATE(o.created_at)
```

Intervalo **semiaberto** (`>= start`, `< end+1dia`) evita depender de fracao de segundo no limite superior; dias ausentes no resultado sao preenchidos com zero em C#, iterando de `startDate` a `endDate`.

## Validacoes — Modulo 003

| Validacao | Status | Evidencia |
| --- | --- | --- |
| `dotnet build TestOrder.slnx` | PASS | Build sem erros |
| `.\scripts\test.ps1` | PASS | 46/46 testes |
| `dotnet test TestOrder.slnx` | PASS | Idem |
| Nenhuma migration/schema novo | PASS | Leitura pura sobre `orders`/`order_items` |
| Intervalo com pedidos → 200 | PASS | `days` cobre intervalo, valores agregados corretos |
| Intervalo de 1 dia (`startDate == endDate`) → 200 com 1 item | PASS | `GetDailyRevenue_SingleDayRange_ReturnsExactlyOneDay` |
| Intervalo sem pedidos → 200 zerado | PASS | `GetDailyRevenue_EmptyRange_ReturnsZeroedDays` |
| `totalRevenue`/`totalOrders` = soma de `days` | PASS | `GetDailyRevenue_TotalRevenue_MatchesSumOfDays` |
| Parametros ausentes/invalidos/invertidos → 400 | PASS | `MissingStartDate`, `MissingEndDate`, `InvalidDate`, `StartAfterEnd` |
| 366 dias aceito / 367 rejeitado | PASS | `GetDailyRevenue_RangeBoundary_AcceptsUpTo366AndRejectsOver` |
| Considera pedidos do seed (nao so dados de teste) | PASS | `Regression_Modules001And002_StillWork` (intervalo `2025-07-02`–`2026-07-01`) |
| GET modulo 001/002 e POST intactos | PASS | products, orders, orders/{id}, POST orders → 200/201 |
| MVC mantido, sem Minimal APIs | PASS | `RevenueController` |
| Sem N+1 | PASS | Uma unica query agregada em `RevenueQueries.cs` |

### Comandos de validacao final

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

## Modulo 004 — roteiro de demo (5–10 min)

### Mensagem para a sala

Primeira tela real do desafio: React + Vite consumindo a API existente, sem tocar em nenhum arquivo de `src/TestOrder.Api/`. Estado 100% local (`useState`/`useEffect`), sem Redux/Zustand/React Query, sem bibliotecas de UI pesadas — CSS proprio. O proxy do Vite evita CORS no backend.

### Passos

1. **Subir tudo com um comando**: `.\scripts\dev-up.ps1` — sobe MySQL, builda o backend, instala dependencias do frontend (primeira vez) e abre **3 janelas CMD separadas** (`TestOrder - MySQL`, `TestOrder - API`, `TestOrder - Web`), cada uma com seus logs em tempo real. Terminal principal mostra `http://localhost:5069` (backend) e `http://localhost:5173` (frontend).
2. Abrir a URL do frontend no navegador (a janela "TestOrder - Web" confirma a porta exata caso `5173` esteja ocupada).
3. **Tela operacional, nao landing page**: listagem de pedidos + formulario de criacao visiveis de cara.
4. **Paginacao**: "anterior"/"proxima" e botao "atualizar".
5. **Criar pedido valido**: selecionar produto + quantidade, "adicionar item", "criar pedido" — formulario limpa, mensagem de sucesso, pedido aparece na pagina 1.
6. **Quantidade invalida**: tentar `0`, negativa, vazia ou texto no campo de quantidade — item nao entra no rascunho, mensagem inline.
7. **Produto duplicado**: tentar adicionar o mesmo produto duas vezes — bloqueado com mensagem.
8. **Erro 409 (estoque)**: adicionar item com quantidade absurda (ex.: `999999`) e criar — mensagem de conflito de estoque; formulario preservado.
9. **Responsivo**: reduzir a janela (~375px) — layout empilha sem rolagem horizontal.
10. **Build**: `npm run build` — gera `dist/`.
11. **Regressao backend**: `.\scripts\test.ps1` — **46/46** intacto.

### Trecho central (api.js)

```javascript
export async function createOrder({ customerName, items }) {
  const response = await fetch('/api/orders', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ customerName: customerName || null, items }),
  });
  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }
  return response.json();
}
```

Sem classes, interfaces ou service layer generica — apenas 3 funcoes (`fetchProducts`, `fetchOrders`, `createOrder`).

## Validacoes — Modulo 004

| Validacao | Status | Evidencia |
| --- | --- | --- |
| `dotnet build TestOrder.slnx` (baseline) | PASS | Build sem erros antes do frontend |
| `.\scripts\test.ps1` (baseline) | PASS | 46/46 antes do frontend |
| `npm install` | PASS | 66 pacotes, 0 vulnerabilidades |
| `npm run build` | PASS | `dist/` gerado (~150 KB JS / ~48 KB gzip) |
| `package.json` sem dependencias proibidas | PASS | Apenas `react`, `react-dom`, `vite`, `@vitejs/plugin-react` |
| `dotnet build` + `.\scripts\test.ps1` (regressao pos-frontend) | PASS | 46/46 — nenhum arquivo de backend alterado |
| Proxy `/api/products` via Vite | PASS | 200, 50 produtos |
| Proxy `/api/orders` paginado via Vite | PASS | 200, `totalCount`/`totalPages` coerentes |
| `POST /api/orders` valido via proxy | PASS | 201 |
| `POST /api/orders` produto duplicado via proxy | PASS | 400 + `{ "error": "..." }` |
| `POST /api/orders` quantidade absurda via proxy | PASS | 409 + `{ "error": "..." }` |
| Quantidade 0/negativa/vazia/nao numerica no formulario | PASS (revisao de codigo) | Validado em `App.jsx` — item nao entra no rascunho |
| Produto duplicado bloqueado no rascunho | PASS | Confirmado em navegador real (Playwright/Chromium) — mensagem inline exibida |
| Responsividade mobile (~375px), sem overflow do body | PASS | Confirmado em navegador real — `document.documentElement.scrollWidth === 375` (igual à viewport) |
| Sem erro de console (desktop e mobile) | PASS | Confirmado em navegador real — nenhum `console.error`/`pageerror` |
| Criar pedido válido logo após erro de produto duplicado (sem mensagem antiga) | PASS | Confirmado em navegador real — sucesso exibido, `itemError` não permanece visível |
| `createdAt` exibido em UTC, sem deslocar dia por timezone local | PASS | Testado com timezone de navegador `Pacific/Kiritimati` (UTC+14); data renderizada igual ao valor UTC do backend |
| `.\scripts\dev-up.ps1` abre 3 janelas CMD (MySQL/API/Web) sem erro | PASS | Script executa Docker + build + npm install condicional + aviso de porta ocupada sem lancar excecao |
| Aviso de porta 5069 ocupada aparece antes do `dotnet build` | PASS | Mensagem especifica sobre risco de `dotnet build` falhar no Windows com o `.exe` da API em uso |
| `GET http://localhost:5069/api/products` com servicos subidos via `dev-up.ps1` | PASS | 200, 50 produtos |
| Frontend carrega com servicos subidos via `dev-up.ps1` | PASS | 200, `<title>TestOrder</title>` presente |
| Tema escuro operacional aplicado (tokens `:root`) | PASS | `getComputedStyle(document.body).backgroundColor` = `rgb(23, 28, 23)` (`#171c17`) em navegador real |
| `package.json`/`package-lock.json` inalterados apos ajuste visual | PASS | `git status --porcelain` sem mudancas; nenhuma ocorrencia de "playwright" em nenhum dos dois arquivos |
| `npm run build` apos ajuste visual | PASS | `dist/` gerado, CSS ~6,6 KB / ~1,7 KB gzip |
| `dotnet build` + `.\scripts\test.ps1` apos ajuste visual | PASS | 46/46 — nenhum arquivo de backend tocado |
| Fluxo completo (duplicado bloqueia, sucesso limpa erro, mobile sem overflow) apos ajuste visual | PASS | Confirmado em navegador real (Playwright/Chromium temporario) |

### Comandos de validacao final

```powershell
# Backend
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1

# Frontend
cd src/TestOrder.Web
npm install
npm run build
```

### Observacao operacional

A validacao inicial de fluxo (201/400/409) foi confirmada via chamadas HTTP diretas ao proxy do Vite. Numa correcao posterior (overflow mobile, `itemError` residual, `formatDate` em UTC), a validacao foi refeita com um navegador real headless (Playwright/Chromium, instalado apenas de forma temporaria e removido ao final — nao consta em `package.json`), cobrindo: ausencia de erro de console em desktop/mobile, ausencia de overflow horizontal do body em 375px, bloqueio de produto duplicado, criacao de pedido apos esse bloqueio sem mensagem antiga residual, e formatacao de data em UTC com timezone de navegador alterado. Ainda assim, recomenda-se repetir o checklist do `quickstart.md` manualmente antes da apresentacao para os itens nao cobertos por automacao (ex.: leitura visual subjetiva do layout).

Em uma sessao PowerShell normal, o script abre tres janelas CMD. Antes da apresentacao, confirme visualmente que as janelas `TestOrder - MySQL`, `TestOrder - API` e `TestOrder - Web` permaneceram abertas.
