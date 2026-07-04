# Plano Técnico: Módulo 007 — Tela de Faturamento por Período

**Branch**: `007-tela-faturamento-periodo` | **Data**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Especificação em `specs/007-tela-faturamento-periodo/spec.md`

---

## Summary

Adicionar, na aplicação React existente (`src/TestOrder.Web`), uma segunda área **Faturamento** acessível por navegação local (`Pedidos` / `Faturamento`), que consulta visualmente o endpoint já implementado no módulo 003:

`GET /api/revenue/daily?startDate=&endDate=`

Sem alterar backend .NET, worker Node, schema ou contratos HTTP. Estado 100% local (`useState`), uma função nova em `api.js` (`fetchDailyRevenue`), componente de apresentação `RevenuePanel.jsx` extraído por **legibilidade** (não arquitetura). Validação: `npm run build`, regressão backend **46/46**, checklist manual via `dev-up.ps1`.

---

## Technical Context

| Item | Valor |
| --- | --- |
| **Language/Version** | JavaScript (ES2020+) — sem TypeScript |
| **Primary Dependencies** | React 18.x, Vite (já presentes no módulo 004) — **sem novas dependências** |
| **Storage** | N/A — consulta read-only; nenhuma persistência local |
| **Testing** | Sem testes automatizados de frontend (NFR-006); `npm run build` como smoke. Backend: `dotnet test TestOrder.slnx` inalterado — **46/46** |
| **Target Platform** | Navegador; `dev-up.ps1` → Vite `5173` + API `5069` |
| **Project Type** | Extensão do SPA existente — nova área de visualização |
| **Performance Goals** | SC-001: faturamento visível em < 15s após abrir app (intervalo manual + um clique em Consultar) |
| **Constraints** | Sem backend/worker/schema; sem react-router/Redux/Zustand/React Query/UI kit/gráficos; `api.js` +1 função apenas |
| **Scale/Scope** | 2–5 arquivos tocados (`App.jsx`, `RevenuePanel.jsx`, `api.js`, `formatters.js`, `styles.css`) |

---

## Constitution Check

*GATES: `.cursor/rules/testorder.mdc` + spec do módulo 007 (`.specify/memory/constitution.md` permanece template genérico).*

| Gate | Status | Notas |
| --- | --- | --- |
| React web UI operacional, não landing | ✅ PASS | Segunda área na mesma SPA |
| Sem alteração backend .NET | ✅ PASS | FR-011 |
| Sem alteração worker Node | ✅ PASS | Outbox inalterado |
| Sem schema/migrations novas | ✅ PASS | Somente consumidor |
| Sem Clean Architecture / camada service genérica | ✅ PASS | `fetchDailyRevenue` + estado local |
| Sem Redux/Zustand/React Query | ✅ PASS | `useState` em `App.jsx` |
| Sem react-router | ✅ PASS | `activeTab` local |
| Sem bibliotecas UI/gráficos | ✅ PASS | CSS existente |
| Dapper/SQL no backend inalterado | ✅ PASS | Nenhum arquivo em `TestOrder.Api` |
| Poucos arquivos, fácil de explicar | ✅ PASS | Extração mínima `RevenuePanel.jsx` |
| `dev-up.ps1` caminho principal | ✅ PASS | quickstart alinhado |

**Pós-design (Phase 1)**: Nenhuma violação. Nenhum endpoint novo, nenhuma dependência nova.

---

## Project Structure

### Documentação (esta feature)

```text
specs/007-tela-faturamento-periodo/
├── spec.md
├── plan.md                 # este arquivo
├── research.md             # Phase 0
├── data-model.md           # Phase 1
├── quickstart.md           # Phase 1
├── contracts/
│   └── ui.md               # Phase 1
└── checklists/
    └── requirements.md
```

(`tasks.md` será gerado por `/speckit-tasks`.)

### Código-fonte — alterações previstas

```text
F:\repository\TestOrder\
└── src/
    ├── TestOrder.Api/              # INALTERADO
    ├── TestOrder.OrderProcessor/   # INALTERADO
    └── TestOrder.Web/
        └── src/
            ├── main.jsx            # inalterado
            ├── api.js              # + fetchDailyRevenue
            ├── App.jsx             # activeTab + estado faturamento + navegação
            ├── RevenuePanel.jsx    # NOVO — formulário + totais + tabela
            ├── formatters.js       # NOVO obrigatório mínimo — formatCurrency, formatCalendarDate (+ formatDate se movido)
            └── styles.css          # classes abas + bloco faturamento
```

**Structure Decision**: Mesma filosofia do módulo 004 — fatia vertical mínima no frontend existente. `RevenuePanel.jsx` na raiz de `src/`, sem pastas artificiais.

---

## Decisões de implementação

| # | Decisão | Detalhe |
| --- | --- | --- |
| 1 | Componentização | Extrair `RevenuePanel.jsx` — `App.jsx` já ~361 linhas (research.md R1) |
| 2 | Navegação | `activeTab`: `'orders'` \| `'revenue'`; sem URL routing (R2) |
| 3 | Defaults de data | 1º dia do mês → hoje; sem auto-consulta ao abrir aba (R3) |
| 4 | Formatação | `formatCurrency` compartilhado; datas `YYYY-MM-DD` por string split (R4) |
| 5 | HTTP | `fetchDailyRevenue(startDate, endDate)` em `api.js` (R5) |
| 6 | Validação local | Datas vazias / invertidas antes do fetch (R6) |
| 7 | Race ao trocar aba | Ignorar resposta tardia com flag `cancelled` ou `requestId` (R7) |
| 8 | Pedidos preservados | Não resetar `page` ao alternar abas (R8) |
| 9 | CSS | Reutilizar `.table-wrapper` + classes novas mínimas para abas (R9) |
| 10 | Escopo | Sem faturar pedido, baixa estoque, gráficos, exportação (spec) |

### Estado React (faturamento) — nomes fixos

| Estado | Tipo inicial | Uso |
| --- | --- | --- |
| `startDate` | string `YYYY-MM-DD` | Input data inicial |
| `endDate` | string `YYYY-MM-DD` | Input data final |
| `revenue` | `null` | Último `DailyRevenueResponse` bem-sucedido |
| `loadingRevenue` | `false` | Consulta em andamento |
| `revenueError` | `null` | Mensagem amigável |

Mais `activeTab` (`'orders'` default) para navegação.

---

## Fluxo da área Faturamento (sequência)

```text
1. Usuário clica aba "Faturamento":
   - Renderiza RevenuePanel
   - Se primeira visita: startDate/endDate defaults (R3)
   - revenue/revenueError anteriores permanecem se existirem

2. Usuário clica "Consultar":
   - Validação local (R6) → revenueError ou continua
   - setLoadingRevenue(true); setRevenueError(null)
   - fetchDailyRevenue(startDate, endDate)
   - 200 → setRevenue(body) se ainda em aba Faturamento e não cancelled
   - erro → setRevenueError(message); revenue inalterado
   - finally → setLoadingRevenue(false)

3. Usuário clica aba "Pedidos" durante loading:
   - activeTab = 'orders'
   - Resposta pendente ignorada (R7)

4. Área Pedidos:
   - Comportamento módulo 004 intacto (FR-002)
```

---

## Contratos consumidos

Detalhamento em [contracts/ui.md](./contracts/ui.md).

| Endpoint | Novo? | Uso neste módulo |
| --- | --- | --- |
| `GET /api/revenue/daily` | Não (módulo 003) | **Consumido** — consulta por período |
| `GET /api/products` | Não | Área Pedidos — inalterado |
| `GET /api/orders` | Não | Área Pedidos — inalterado |
| `POST /api/orders` | Não | Área Pedidos — inalterado |

---

## Estratégia de validação

1. **`npm run build`** em `src/TestOrder.Web` — compilação (NFR-004, AC-015).
2. **`dotnet build TestOrder.slnx && .\scripts\test.ps1`** — **46/46** (NFR-005, AC-014).
3. **`.\scripts\dev-up.ps1`** + checklist manual ([quickstart.md](./quickstart.md)) — AC-001–AC-013, AC-017.
4. **Console browser** — sem erros JSON/HTML (AC implícito spec checks manuais).

---

## Phase 0 & Phase 1 — Artefatos gerados

| Artefato | Status |
| --- | --- |
| [research.md](./research.md) | ✅ |
| [data-model.md](./data-model.md) | ✅ |
| [contracts/ui.md](./contracts/ui.md) | ✅ |
| [quickstart.md](./quickstart.md) | ✅ |

---

## Documentação pós-implementação (não fazer neste passo)

### `AI_NOTES.md`

- Visualização apenas do endpoint existente; sem regra de negócio nova.
- Fora de escopo: editar/faturar pedido, baixa estoque; Node continua worker outbox.
- Decisão `RevenuePanel.jsx` + resultados de validação.

### `docs/PRESENTATION_GUIDE.md`

- Referências: `RevenuePanel.jsx`, `fetchDailyRevenue`, navegação `activeTab`.
- Roteiro: alternar aba → consulta com dados → intervalo vazio → erro invertido → voltar Pedidos.

### `README.md`

- Mencionar aba **Faturamento** na descrição da UI.

---

## Complexity Tracking

*Nenhuma violação a justificar. Módulo restrito ao frontend existente, sem camadas extras.*

---

## Próximos passos

1. **`/speckit-tasks`** — gerar `tasks.md` a partir deste plano e da spec.
2. **`/speckit-implement`** — implementar tarefas (quando solicitado).
3. Não alterar backend/worker neste módulo.

---

## Referências cruzadas

| Documento | Uso |
| --- | --- |
| [spec.md](./spec.md) | Requisitos FR/AC |
| [research.md](./research.md) | Decisões R1–R10 |
| [data-model.md](./data-model.md) | Estado frontend |
| [contracts/ui.md](./contracts/ui.md) | Contrato UI + consumo API |
| [quickstart.md](./quickstart.md) | Validação manual |
| [../004-tela-web-pedidos/plan.md](../004-tela-web-pedidos/plan.md) | Padrão SPA pedidos |
| [../003-faturamento-por-periodo/](../003-faturamento-por-periodo/) | Endpoint e testes backend |
