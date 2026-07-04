# TestOrder

Sistema de pedidos construído por módulos com [Spec Kit](https://github.com/github/spec-kit): backend ASP.NET Core MVC + MySQL 8 e uma tela web React (Vite) com duas áreas — `Pedidos` (listar/criar com paginação numerada) e `Faturamento` (consultar visualmente o faturamento por período).

Contexto completo de decisões, trade-offs e roteiro de demo em [`docs/PRESENTATION_GUIDE.md`](docs/PRESENTATION_GUIDE.md). Uso de IA documentado em [`AI_NOTES.md`](AI_NOTES.md).

## Pré-requisitos

- .NET 10 SDK
- Docker Desktop (MySQL 8 via `docker-compose.yml`)
- Node.js 18+ e npm (frontend e worker)

## Subir o ambiente completo (backend + frontend + worker)

```powershell
.\scripts\dev-up.ps1
```

Um único comando: fecha instâncias anteriores reconhecidas do próprio TestOrder (API, frontend, worker e janelas de log), valida o Docker, sobe o MySQL via Docker Compose, aplica migrations/seed automaticamente, builda a solução, instala as dependências do frontend e do worker na primeira execução (se `node_modules` não existir em cada um) e abre **quatro janelas CMD separadas**, uma por serviço, para acompanhar os logs em tempo real:

| Janela | Título | Comando |
| --- | --- | --- |
| MySQL | `TestOrder - MySQL` | `docker compose logs -f mysql` |
| Backend | `TestOrder - API` | `dotnet run --project src\TestOrder.Api` |
| Frontend | `TestOrder - Web` | `npm run dev` (em `src/TestOrder.Web`) |
| Worker | `TestOrder - Worker` | `node index.js` (em `src/TestOrder.OrderProcessor`) |

Ao final, o terminal principal imprime:

```
Backend:  http://localhost:5069
Frontend: http://localhost:5173
MySQL:    localhost:3306
Worker:   see "TestOrder - Worker" window
```

O proxy configurado em `vite.config.js` encaminha `/api/*` do frontend para `http://localhost:5069`, sem necessidade de CORS no backend. O script libera as portas `5069` e `5173` quando elas pertencem a processos antigos do próprio TestOrder; se alguma delas continuar ocupada por outro processo, a execução para com uma mensagem indicando o PID responsável. Para parar um serviço, feche a janela correspondente ou use `Ctrl+C` dentro dela.

O worker Node (`src/TestOrder.OrderProcessor`) também pode ser executado manualmente, sem o `dev-up.ps1`:

```powershell
cd src/TestOrder.OrderProcessor
node index.js
```

Ele consome a tabela `order_processing_events` (sem fila externa, sem broker) via polling com `SELECT ... FOR UPDATE SKIP LOCKED`, processa eventos `OrderCreated` pendentes e marca `processed` de forma idempotente — múltiplas instâncias podem rodar simultaneamente sem duplicar processamento. Detalhes em [`specs/005-worker-outbox-node/quickstart.md`](specs/005-worker-outbox-node/quickstart.md).

## Testes do backend

```powershell
# Parar a API local antes (Windows bloqueia o .exe em uso)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet build TestOrder.slnx
.\scripts\test.ps1
```

Suíte atual: **57/57** testes de integração (xUnit + Testcontainers/MySQL).

## Build do frontend

```powershell
cd src/TestOrder.Web
npm run build
```

Saída em `src/TestOrder.Web/dist/`. Sem suíte automatizada de frontend nestes módulos — validação por build + checklist manual (ver [`specs/004-tela-web-pedidos/quickstart.md`](specs/004-tela-web-pedidos/quickstart.md) e [`specs/007-tela-faturamento-periodo/quickstart.md`](specs/007-tela-faturamento-periodo/quickstart.md)).

## Aba Faturamento

A aba `Faturamento` (ao lado de `Pedidos`, mesma aplicação React, sem `react-router`) consulta visualmente o endpoint `GET /api/revenue/daily?startDate=&endDate=` (módulo 003, com datas opcionais desde o follow-up do módulo 007): duas datas + botão `Consultar` retornam total de faturamento, total de pedidos e uma tabela por dia. A tela inclui atalhos de período (`Hoje`, `7 dias`, `15 dias`, `30 dias`, `90 dias`, `Último ano`) e `Limpar datas` (1 clique limpa as duas datas). Datas vazias não são erro: sem `startDate`/`endDate` o backend agrega **todos os dias disponíveis** (sem preencher dias zerados, já que não há um intervalo fechado para "explodir"); com as duas datas preenchidas o comportamento de preenchimento de dias zerados é preservado. A tabela de dias tem **paginação numerada no frontend** (`Início`, `Anterior`, números, `Próxima`, `Fim` — mesmo padrão visual da listagem de `Pedidos`), pois a resposta já vem inteira e agregada por dia do backend. É **apenas visualização** — não altera pedidos, não "fatura" nada e não baixa estoque. Detalhes em [`specs/007-tela-faturamento-periodo/`](specs/007-tela-faturamento-periodo/).

## Filtros de Pedidos

A listagem de `Pedidos` tem filtros **server-side** por `status`, `data inicial` e `data final` (`GET /api/orders?status=&startDate=&endDate=`), porque a listagem é paginada e pode ter milhares de registros — filtrar em memória no cliente não seria viável. `status` vazio ("Todos") ou datas vazias não limitam a busca. Os mesmos atalhos de período do Faturamento (`Hoje`, `7 dias`, …, `Último ano`) preenchem as datas do filtro (sem buscar automaticamente — só no botão `Filtrar`). O botão `Filtrar` reseta a paginação para a página 1 e mantém os filtros ativos ao trocar de página; `Limpar filtros` (1 clique) volta tudo ao padrão.

## Padrão de duplo clique nos campos de filtro/consulta

Em todos os campos de filtro/consulta (status e datas de `Pedidos`; datas de `Faturamento`), **duplo clique dentro do campo limpa apenas aquele campo** (volta ao padrão, no caso do select de status). Isso é diferente dos botões `Limpar filtros`/`Limpar datas`, que continuam funcionando normalmente com **1 clique** e limpam todos os campos da respectiva seção de uma vez. O duplo clique só limpa o valor do campo — a busca só é refeita quando o usuário clica em `Filtrar`/`Consultar`.

## Smoke test do worker

```powershell
cd src/TestOrder.OrderProcessor
node index.js
# Ctrl+C para encerrar (shutdown limpo)
```

Requer o MySQL do `docker-compose.yml` no ar. Sem suíte automatizada do worker neste módulo — validação por smoke + checklist manual (ver [`specs/005-worker-outbox-node/quickstart.md`](specs/005-worker-outbox-node/quickstart.md)).

## Validação manual mínima (criar pedido → outbox processado)

1. Suba o ambiente com `.\scripts\dev-up.ps1`.
2. Crie um pedido pela tela React (`http://localhost:5173`) ou via `POST /api/orders`.
3. Observe a janela `TestOrder - Worker` — um log JSON referenciando o pedido aparece em poucos segundos.
4. Confirme no MySQL que o evento em `order_processing_events` mudou de `pending` para `processed`.

## Dados de desenvolvimento (seed)

O seed automático (executado no primeiro start da API, de forma idempotente — não duplica em reinícios) cria dados determinísticos de desenvolvimento, não dados de produção:

| Métrica | Valor |
| --- | --- |
| Produtos | 50 |
| Pedidos | 5000 |
| Itens de pedido | ~17.499 (média ~3,5 itens/pedido) |
| Unidades de inventário (`inventory_units`, backfill) | ~237.000 |

Histórico completo da origem desses números em [`AI_NOTES.md`](AI_NOTES.md) (módulos 001 e 002).

## Endpoints principais

| Endpoint | Descrição |
| --- | --- |
| `GET /api/products` | Lista de produtos do catálogo |
| `GET /api/orders?page=&pageSize=&status=&startDate=&endDate=` | Pedidos paginados, com filtros opcionais |
| `GET /api/orders/{id}` | Detalhe de um pedido |
| `POST /api/orders` | Cria pedido com reserva transacional de estoque |
| `GET /api/revenue/daily?startDate=&endDate=` | Faturamento agregado por dia (datas opcionais) |

## Estrutura do repositório

```text
src/
├── TestOrder.Api/            # Backend ASP.NET Core MVC + EF Core + Dapper + MySQL 8
├── TestOrder.Web/            # Frontend React + Vite (JavaScript, sem TypeScript)
│   └── src/
│       ├── App.jsx           # Shell: header, abas, renderiza a página ativa
│       ├── main.jsx
│       ├── styles.css        # CSS único
│       ├── api/api.js        # fetch nativo (sem service layer)
│       ├── components/       # PageNav.jsx (paginação compartilhada)
│       ├── pages/
│       │   ├── orders/OrdersPage.jsx
│       │   └── revenue/RevenuePage.jsx
│       └── shared/           # dateRanges, formatters, pagination
└── TestOrder.OrderProcessor/ # Worker Node.js — outbox de order_processing_events
tests/
└── TestOrder.Api.Tests/
specs/                 # Artefatos Spec Kit por módulo (spec, plan, tasks, research...)
docs/                  # Guia de apresentação
```

## Módulos entregues

1. Base, modelo e listagem de pedidos.
2. Criação de pedidos com reserva concorrente (`FOR UPDATE SKIP LOCKED`).
3. Faturamento por período (endpoint `GET /api/revenue/daily`).
4. Tela web React (listagem + criação de pedidos).
5. Worker Node.js para processamento assíncrono do outbox (`order_processing_events`), sem fila externa.
6. Fechamento final da entrega (auditoria e documentação).
7. Tela web React — aba `Faturamento` (visualização do endpoint do módulo 003); follow-up: filtros server-side de `status`/data em `Pedidos`, datas opcionais em `GET /api/revenue/daily`, paginação numerada da tabela de dias e padrão de duplo clique para limpar campos de filtro.
