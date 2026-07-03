# TestOrder

Sistema de pedidos construído por módulos com [Spec Kit](https://github.com/github/spec-kit): backend ASP.NET Core MVC + MySQL 8 e uma tela web React (Vite) para listar e criar pedidos.

Contexto completo de decisões, trade-offs e roteiro de demo em [`docs/PRESENTATION_GUIDE.md`](docs/PRESENTATION_GUIDE.md). Uso de IA documentado em [`AI_NOTES.md`](AI_NOTES.md).

## Pré-requisitos

- .NET 10 SDK
- Docker Desktop (MySQL 8 via `docker-compose.yml`)
- Node.js 18+ e npm (frontend e worker)

## Subir o ambiente completo (backend + frontend + worker)

```powershell
.\scripts\dev-up.ps1
```

Um único comando: valida o Docker, sobe o MySQL via Docker Compose, aplica migrations/seed automaticamente, builda a solução, instala as dependências do frontend e do worker na primeira execução (se `node_modules` não existir em cada um) e abre **quatro janelas CMD separadas**, uma por serviço, para acompanhar os logs em tempo real:

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

O proxy configurado em `vite.config.js` encaminha `/api/*` do frontend para `http://localhost:5069`, sem necessidade de CORS no backend. Se a porta `5069` já estiver em uso, o script avisa **antes do `dotnet build`** (o build pode falhar no Windows enquanto o executável antigo da API estiver em uso); os avisos de porta ocupada (`5069`/`5173`) também aparecem antes de abrir as janelas (o Vite escolhe outra porta automaticamente nesse caso — confira a janela "TestOrder - Web"). Para parar um serviço, feche a janela correspondente ou use `Ctrl+C` dentro dela.

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

Suíte atual: **46/46** testes de integração (xUnit + Testcontainers/MySQL).

## Build do frontend

```powershell
cd src/TestOrder.Web
npm run build
```

Saída em `src/TestOrder.Web/dist/`. Sem suíte automatizada de frontend neste módulo — validação por build + checklist manual (ver [`specs/004-tela-web-pedidos/quickstart.md`](specs/004-tela-web-pedidos/quickstart.md)).

## Endpoints principais

| Endpoint | Descrição |
| --- | --- |
| `GET /api/products` | Lista de produtos do catálogo |
| `GET /api/orders?page=&pageSize=` | Pedidos paginados |
| `GET /api/orders/{id}` | Detalhe de um pedido |
| `POST /api/orders` | Cria pedido com reserva transacional de estoque |
| `GET /api/revenue/daily?startDate=&endDate=` | Faturamento agregado por dia |

## Estrutura do repositório

```text
src/
├── TestOrder.Api/            # Backend ASP.NET Core MVC + EF Core + Dapper + MySQL 8
├── TestOrder.Web/            # Frontend React + Vite (JavaScript, sem TypeScript)
└── TestOrder.OrderProcessor/ # Worker Node.js (JavaScript, sem TypeScript) — outbox de order_processing_events
tests/
└── TestOrder.Api.Tests/
specs/                 # Artefatos Spec Kit por módulo (spec, plan, tasks, research...)
docs/                  # Guia de apresentação
```

## Módulos entregues

1. Base, modelo e listagem de pedidos.
2. Criação de pedidos com reserva concorrente (`FOR UPDATE SKIP LOCKED`).
3. Faturamento por período.
4. Tela web React (listagem + criação de pedidos).
5. Worker Node.js para processamento assíncrono do outbox (`order_processing_events`), sem fila externa.
