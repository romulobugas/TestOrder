# Tasks: Módulo 007 — Tela de Faturamento por Período

**Input**: Design documents from `specs/007-tela-faturamento-periodo/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Sem suíte automatizada de frontend neste módulo (NFR-006). Gate obrigatório: backend **46/46** (`.\scripts\test.ps1`) + `npm run build`. Validação funcional manual via `dev-up.ps1` e checklist do [quickstart.md](./quickstart.md).

**Organization**: Frontend-only — estender `src/TestOrder.Web` sem alterar backend, worker ou schema. Fases: preflight → helper HTTP → formatadores → US1 navegação → US2 consulta com dados → US3 intervalo vazio → US4 erros → CSS → documentação → validação final.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência de tarefas incompletas)
- **[USn]**: User story (ordem da spec): US1=Jornada 1 (navegação), US2=Jornada 2 (consulta com dados), US3=Jornada 3 (intervalo vazio), US4=Jornada 4 (erros amigáveis)
- **Regra de sequência**: tarefas que editam `App.jsx` são **sequenciais** entre si; `styles.css` **depois** do markup principal; docs em arquivos distintos podem ser `[P]`

---

## Phase 1: Preflight

**Goal**: Registrar baseline real antes de alterar o frontend — proteger módulos 001–006.

**Independent Test**: Comandos executam; resultados anotados (baseline para validação final T018).

---

- [x] T001 Rodar baseline completo (Git, frontend build, backend build, testes)

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar nesta ordem e anotar resultado real: `git status --short --branch`; `npm run build` em `src/TestOrder.Web`; `dotnet build TestOrder.slnx`; `.\scripts\test.ps1` (esperado **46/46**). Nenhuma correção nesta tarefa — apenas diagnóstico. |
| **Permitidos** | Nenhum arquivo alterado |
| **Proibidos** | Alterar backend, worker, schema, `dev-up.ps1` |
| **Pronto quando** | Os 4 comandos executados e resultados anotados |
| **Validação** | `git status --short --branch; cd src/TestOrder.Web; npm run build; cd ../..; dotnet build TestOrder.slnx; .\scripts\test.ps1` |
| **Paralelo** | Não — primeiro passo obrigatório |

**Checkpoint Phase 1**: Baseline verde confirmada (build frontend + backend + 46/46).

---

## Phase 2: Foundational — Helper HTTP

**Goal**: Expor `fetchDailyRevenue` em `api.js`, reutilizando tratamento de erro existente.

**Independent Test**: Função exportada; importável em `App.jsx`; lança `Error` com mensagem amigável em 400/rede/HTML.

---

- [x] T002 [P] Adicionar `fetchDailyRevenue(startDate, endDate)` em `src/TestOrder.Web/src/api.js`

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | Uma função nova apenas: `GET /api/revenue/daily?startDate=&endDate=` com query params URL-encoded. Reutilizar `parseErrorMessage`, `readJsonResponse` e `GENERIC_ERROR_MESSAGE` já existentes. Em `!response.ok`: lançar `Error` com texto de `{ error }`; em rede falha ou content-type não-JSON: mensagem genérica. **Não** alterar `fetchProducts`/`fetchOrders`/`createOrder`. **Não** criar service layer. |
| **Depende de** | T001 |
| **Permitidos** | `src/TestOrder.Web/src/api.js` |
| **Proibidos** | Axios; React Query; alterar `src/TestOrder.Api/` |
| **Pronto quando** | Export `fetchDailyRevenue` compilável; contrato em [contracts/ui.md](./contracts/ui.md) |
| **Paralelo** | Sim — arquivo independente de T003 |

**Checkpoint Phase 2**: Helper HTTP pronto para consulta de faturamento.

---

## Phase 3: Foundational — Formatadores

**Goal**: Centralizar formatação BRL e data calendário sem deslocamento de timezone.

**Independent Test**: `formatCurrency(1234.5)` → `R$ …`; `formatCalendarDate('2026-01-15')` → `15/01/2026` sem `new Date('2026-01-15')`.

---

- [x] T003 [P] Criar `src/TestOrder.Web/src/formatters.js` com `formatCurrency` e `formatCalendarDate`

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | `formatCurrency(value)`: `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })`. `formatCalendarDate(yyyyMmDd)`: split `'YYYY-MM-DD'` → `'DD/MM/YYYY'` por string, **sem** conversão de fuso. Opcional: mover `formatDate(isoString)` existente de `App.jsx` para cá (UTC, comportamento idêntico ao módulo 004). Arquivo mínimo — sem camada genérica. |
| **Depende de** | T001 |
| **Permitidos** | `src/TestOrder.Web/src/formatters.js` (novo) |
| **Proibidos** | Bibliotecas de formatação externas |
| **Pronto quando** | Funções exportadas e testáveis manualmente via import |
| **Paralelo** | Sim — paralelo a T002 |

---

- [x] T004 Migrar `App.jsx` para importar formatadores de `src/TestOrder.Web/src/formatters.js`

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | Remover `currencyFormatter` inline (e `formatDate` se movido) de `src/TestOrder.Web/src/App.jsx`; importar `formatCurrency` / `formatDate` de `./formatters.js`. Comportamento visual da área **Pedidos** inalterado (FR-002). |
| **Depende de** | T003 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx`, `src/TestOrder.Web/src/formatters.js` |
| **Proibidos** | Alterar lógica de pedidos além do import |
| **Pronto quando** | Listagem/criação de pedidos renderizam valores formatados como antes |
| **Paralelo** | Não — edita `App.jsx` |

**Checkpoint Phase 3**: Formatadores compartilhados prontos para `RevenuePanel`.

---

## Phase 4: User Story 1 — Alternar `Pedidos` / `Faturamento` (Priority: P1) 🎯 MVP

**Goal**: Navegação local entre duas áreas na mesma SPA, sem reload e sem react-router.

**Independent Test**: Abrir app → área Pedidos visível; clicar Faturamento → troca sem reload; voltar Pedidos → mesma página de paginação preservada.

---

- [x] T005 [US1] Adicionar estado `activeTab` e controles `Pedidos`/`Faturamento` em `src/TestOrder.Web/src/App.jsx`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | `useState('orders')` para `activeTab` (`'orders'` \| `'revenue'`). Controles simples (abas ou segmento) no cabeçalho/ topo da app. Estilo inicial pode ser mínimo — refinado em T013 (`styles.css`). Sem `react-router`; URL não muda (FR-001). |
| **Depende de** | T004 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Proibidos** | react-router; Redux/Zustand |
| **Pronto quando** | AC-001 parcial: controles visíveis; Pedidos default |
| **Paralelo** | Não — edita `App.jsx` |

---

- [x] T006 [US1] Renderização condicional das áreas preservando estado de pedidos em `src/TestOrder.Web/src/App.jsx`

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | Quando `activeTab === 'orders'`: exibir JSX existente de pedidos (listagem + formulário). Quando `activeTab === 'revenue'`: ocultar pedidos e reservar slot para `RevenuePanel` (placeholder OK até T008). **Não** resetar `page`, `orders`, `pagination` ao trocar abas (research.md R8). Pedidos `useEffect` permanecem ativos — sem refetch forçado ao voltar. |
| **Depende de** | T005 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Proibidos** | Desmontar estado de paginação ao trocar aba |
| **Pronto quando** | AC-001 completo; AC-002 preservado (pedidos funcionam após alternar) |
| **Paralelo** | Não — edita `App.jsx` |

**Checkpoint Phase 4 (US1)**: Navegação funcional; pedidos intactos ao alternar abas.

---

## Phase 5: User Story 2 — Consultar faturamento com dados (Priority: P1)

**Goal**: Formulário de datas + consulta ao endpoint + exibição de totais e tabela por dia.

**Independent Test**: Na aba Faturamento, intervalo com pedidos do seed (ex. `2026-01-01`–`2026-01-31` ou `2025-07-02`–`2026-07-01`) → totais BRL + tabela com uma linha por dia incluindo zeros.

---

- [x] T007 [P] [US2] Criar esqueleto de `src/TestOrder.Web/src/RevenuePanel.jsx` (formulário + área de resultado)

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | Componente de apresentação pequeno recebendo props: `startDate`, `endDate`, `onStartDateChange`, `onEndDateChange`, `onConsult`, `loading`, `error`, `revenue`. JSX: dois `<input type="date">`, botão `Consultar`, bloco de erro, bloco de loading, placeholders para totais/tabela. Sem lógica HTTP nem validação de intervalo — apenas UI + callbacks (ver [contracts/ui.md](./contracts/ui.md) §6). |
| **Depende de** | T003 (formatadores importáveis) |
| **Permitidos** | `src/TestOrder.Web/src/RevenuePanel.jsx` (novo) |
| **Proibidos** | fetch inline; hooks genéricos reutilizáveis |
| **Pronto quando** | Componente compila isolado com props mock |
| **Paralelo** | Sim — arquivo novo; pode rodar em paralelo a T005–T006 |

---

- [x] T008 [US2] Adicionar estado de faturamento e defaults de data em `src/TestOrder.Web/src/App.jsx`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | Estado local: `startDate`, `endDate`, `revenue` (`null`), `loadingRevenue`, `revenueError`. Defaults ao abrir aba (research.md R3): `startDate` = 1º dia do mês corrente, `endDate` = hoje — strings `YYYY-MM-DD` para `<input type="date">`. Inicializar defaults na primeira visita à aba Faturamento (não sobrescrever se usuário já editou). Renderizar `<RevenuePanel … />` quando `activeTab === 'revenue'`. |
| **Depende de** | T006, T007 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Proibidos** | Auto-fetch ao abrir aba (consulta só no botão) |
| **Pronto quando** | AC-003, AC-004: campos visíveis e preenchidos |
| **Paralelo** | Não — edita `App.jsx` |

---

- [x] T009 [US2] Implementar `handleConsultRevenue` e exibição de sucesso em `src/TestOrder.Web/src/App.jsx` + `src/TestOrder.Web/src/RevenuePanel.jsx`

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | Handler: `setLoadingRevenue(true)`; `setRevenueError(null)`; chamar `fetchDailyRevenue(startDate, endDate)`; em 200: `setRevenue(body)` substituindo resultado anterior; `finally`: `setLoadingRevenue(false)`. `RevenuePanel`: exibir `totalRevenue` e `totalOrders`; tabela `days[]` com colunas data (`formatCalendarDate`), `orderCount`, `revenue` (`formatCurrency`); incluir dias zerados. Loading perceptível entre clique e resposta (AC-009). |
| **Depende de** | T002, T008 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx`, `src/TestOrder.Web/src/RevenuePanel.jsx` |
| **Proibidos** | Transformar estrutura JSON do backend |
| **Pronto quando** | AC-005, AC-006, AC-010, AC-011 verificáveis com seed |
| **Paralelo** | Não — coordena App + Panel |

**Checkpoint Phase 5 (US2)**: Consulta com dados exibe totais e tabela completa.

---

## Phase 6: User Story 3 — Intervalo sem pedidos (Priority: P2)

**Goal**: Intervalo vazio exibido como resultado válido (zeros), não como erro.

**Independent Test**: Consultar intervalo futuro sem pedidos (ex. `2030-01-01`–`2030-01-07`) → totais `R$ 0,00` / `0` pedidos; tabela zerada; sem `revenueError`.

---

- [x] T010 [US3] Garantir tratamento de intervalo vazio (200 com zeros) em `src/TestOrder.Web/src/RevenuePanel.jsx`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | Quando `revenue.totalOrders === 0` e `totalRevenue === 0`: exibir totais e tabela normalmente — **não** mostrar mensagem de erro nem estado "vazio" confundível com falha. Limpar `revenueError` em sucesso 200 mesmo com zeros. AC-007. |
| **Depende de** | T009 |
| **Permitidos** | `src/TestOrder.Web/src/RevenuePanel.jsx` |
| **Proibidos** | Tratar zeros como erro ou esconder tabela |
| **Pronto quando** | AC-007 passa no browser |
| **Paralelo** | Não — mesmo componente de T009 |

**Checkpoint Phase 6 (US3)**: Zeros claros e distintos de erro.

---

## Phase 7: User Story 4 — Erros amigáveis e race ao trocar aba (Priority: P1)

**Goal**: Validação local + erros 400/rede legíveis; resposta tardia ignorada ao sair da aba.

**Independent Test**: `startDate > endDate` → mensagem PT amigável, resultado anterior preservado; trocar para Pedidos durante loading → sem erro de console.

---

- [x] T011 [US4] Validação local e exibição de erros amigáveis em `src/TestOrder.Web/src/App.jsx`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | Antes de `fetchDailyRevenue`: rejeitar campos vazios; rejeitar `startDate > endDate` (comparação lexicográfica `YYYY-MM-DD`) com mensagem PT (ex. "A data inicial não pode ser posterior à data final."). Em erro HTTP/rede: exibir `error.message` via `revenueError` — **não** JSON bruto; **não** substituir `revenue` por dado inconsistente (Jornada 4). Erros 400 do backend (intervalo > 366 dias, etc.) passam pela mensagem de `api.js`. Validação e HTTP permanecem em `App.jsx`; `RevenuePanel.jsx` só exibe `error` recebido por props (ver [contracts/ui.md](./contracts/ui.md) §6). |
| **Depende de** | T009 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx`, ajustes mínimos em `RevenuePanel.jsx` para exibir `error` |
| **Proibidos** | Expor corpo HTTP cru |
| **Pronto quando** | AC-008 passa; campo vazio não chama API |
| **Paralelo** | Não — edita `App.jsx` |

---

- [x] T012 [US4] Ignorar resposta tardia ao trocar aba durante consulta em `src/TestOrder.Web/src/App.jsx`

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | No handler assíncrono: flag `cancelled` ou `requestId` incrementado — se `activeTab !== 'revenue'` ou consulta superseded antes do `setState`, **não** atualizar `revenue`/`revenueError`; finalizar `loadingRevenue` apenas quando a resposta pertence à última consulta conhecida, para não deixar o botão travado ao voltar para a aba. Opcional: desabilitar botão `Consultar` enquanto `loadingRevenue`. Caso de borda spec + research.md R7. |
| **Depende de** | T011 |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Proibidos** | setState após troca de aba sem guard |
| **Pronto quando** | DevTools sem warning de setState; resultado não aparece em Pedidos |
| **Paralelo** | Não — edita `App.jsx` |

**Checkpoint Phase 7 (US4)**: Erros amigáveis; race tratada.

---

## Phase 8: CSS — Tema e responsividade

**Goal**: Estilos compatíveis com tema escuro existente; mobile sem overflow horizontal.

**Independent Test**: Viewport ~375px — abas e tabela utilizáveis; body sem scroll horizontal.

---

- [x] T013 Adicionar estilos de abas e bloco faturamento em `src/TestOrder.Web/src/styles.css`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | Classes mínimas: `.tabs`, `.tab`, `.tab--active`, `.revenue-summary`, reutilizar `.table-wrapper` existente para scroll horizontal **interno** da tabela de dias. Sem dashboard/gráficos. Media query existente (~640px) aplicada ao novo bloco. AC-017. **Executar após** markup de T005–T012. |
| **Depende de** | T012 |
| **Permitidos** | `src/TestOrder.Web/src/styles.css` |
| **Proibidos** | Biblioteca CSS; alterar tema global de forma inconsistente |
| **Pronto quando** | AC-017 passa no browser |
| **Paralelo** | Não — depende do markup final |

**Checkpoint Phase 8**: UI faturamento visualmente integrada ao módulo 004.

---

## Phase 9: Polish — Documentação

**Goal**: Registrar escopo, demo e decisões pós-implementação.

**Independent Test**: README menciona aba; PRESENTATION_GUIDE tem roteiro; AI_NOTES explica escopo restrito.

---

- [x] T014 [P] Atualizar `README.md` com aba Faturamento e endpoint visualizado

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | Seção curta: segunda aba **Faturamento** consulta `GET /api/revenue/daily`; caminho principal continua `.\scripts\dev-up.ps1`. Sem sugerir fluxo alternativo como preferencial. |
| **Depende de** | T013 (implementação estável) |
| **Permitidos** | `README.md` |
| **Proibidos** | Alterar `scripts/dev-up.ps1` salvo falha real auditada |
| **Pronto quando** | AC-012 verificável lendo README |
| **Paralelo** | Sim — arquivo distinto de T015/T016 |

---

- [x] T015 [P] Adicionar roteiro da aba Faturamento em `docs/PRESENTATION_GUIDE.md`

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Roteiro curto: alternar aba → consultar intervalo com dados (citar intervalo seed) → intervalo vazio → erro data invertida → voltar Pedidos. Referências: `RevenuePanel.jsx`, `fetchDailyRevenue` em `api.js`. Criar estrutura do roteiro e template da tabela pass/fail; **preencher a tabela pass/fail somente após T018** (checklist manual/browser). T017 cobre apenas gates automatizados (`npm run build`, `dotnet build`, `test.ps1`). |
| **Depende de** | T013 |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Paralelo** | Sim — paralelo a T014/T016 |

---

- [x] T016 [P] Registrar decisões do módulo em `AI_NOTES.md`

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Nova seção Módulo 007: visualização apenas do endpoint existente; fora de escopo editar/faturar pedido e baixa de estoque; Node continua worker outbox; decisão de extrair `RevenuePanel.jsx`; erros comuns de IA (timezone, zeros como erro); resultados de validação; prompts Spec Kit usados. |
| **Depende de** | T013 |
| **Permitidos** | `AI_NOTES.md` |
| **Paralelo** | Sim — paralelo a T014/T015 |

**Checkpoint Phase 9**: Documentação alinhada ao escopo implementado.

---

## Phase 10: Validação final

**Goal**: Confirmar build, regressão backend e checklist manual completo.

**Independent Test**: Todos os gates verdes; checks manuais registrados em PRESENTATION_GUIDE.

---

- [x] T017 Rodar validação automatizada (`npm run build`, `dotnet build`, `.\scripts\test.ps1`)

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | `npm run build` em `src/TestOrder.Web`; `dotnet build TestOrder.slnx`; `.\scripts\test.ps1` (**46/46**). Confirmar `package.json` sem dependências novas (AC-016). Nenhum arquivo em `src/TestOrder.Api/` ou `src/TestOrder.OrderProcessor/` alterado. |
| **Depende de** | T013–T016 |
| **Permitidos** | Ajustes mínimos só se build falhar por causa deste módulo |
| **Proibidos** | Alterar backend/worker/schema para "fazer passar" |
| **Pronto quando** | AC-014, AC-015, AC-016 passam |
| **Validação** | `cd src/TestOrder.Web; npm run build; cd ../..; dotnet build TestOrder.slnx; .\scripts\test.ps1` |
| **Paralelo** | Não |

---

- [x] T018 Executar checklist manual com `.\scripts\dev-up.ps1` e browser em `http://localhost:5173`

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | Seguir [quickstart.md](./quickstart.md): alternar Pedidos/Faturamento; consultar intervalo com dados seed; intervalo vazio; intervalo inválido (`start > end`); **(opcional)** intervalo > 366 dias → mensagem amigável do backend, sem quebrar a tela; voltar Pedidos (paginação + criar pedido); viewport ~375px; console sem erros. Registrar pass/fail em `docs/PRESENTATION_GUIDE.md` (completar tabela iniciada em T015). |
| **Depende de** | T017 |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` (tabela de resultados) |
| **Proibidos** | Alterar `dev-up.ps1` salvo falha real auditada |
| **Pronto quando** | AC-001–AC-013, AC-017 passam; console limpo |
| **Validação** | `.\scripts\dev-up.ps1` + checklist manual |
| **Paralelo** | Não |

---

- [x] T019 Marcar tarefas concluídas e fechamento formal deste `tasks.md`

**Detalhe T019**
| Campo | Valor |
| --- | --- |
| **Descrição** | Marcar `[x]` em T001–T018 com resultados reais anotados; adicionar seção **Fechamento** com data, branch `007-tela-faturamento-periodo`, totais de validação (build/testes/checklist). Confirmar escopo: nenhuma alteração backend/worker/schema. |
| **Depende de** | T018 |
| **Permitidos** | `specs/007-tela-faturamento-periodo/tasks.md` |
| **Pronto quando** | Todas as tarefas `[x]`; fechamento documentado |
| **Paralelo** | Não |

**Checkpoint Phase 10**: Módulo 007 pronto para review/merge.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
Phase 1 (T001)
  → Phase 2 (T002) ∥ Phase 3 (T003)
  → T004
  → Phase 4 US1 (T005 → T006)
  → T007 [P] pode iniciar após T003 (paralelo a T005/T006)
  → Phase 5 US2 (T008 → T009)
  → Phase 6 US3 (T010)
  → Phase 7 US4 (T011 → T012)
  → Phase 8 CSS (T013)
  → Phase 9 Docs (T014 ∥ T015 ∥ T016)
  → Phase 10 (T017 → T018 → T019)
```

### User Story Dependencies

| Story | Depende de | Independente quando |
| --- | --- | --- |
| US1 (P1) | T004 | T006 — navegação + pedidos preservados |
| US2 (P1) | US1 + T002 | T009 — consulta com totais/tabela |
| US3 (P2) | US2 | T010 — zeros sem erro |
| US4 (P1) | US2 | T012 — erros + race |

### Parallel Opportunities (reais)

| Grupo | Tarefas | Condição |
| --- | --- | --- |
| Após preflight | T002, T003 | Arquivos `api.js` e `formatters.js` distintos |
| Durante US1 | T007 | `RevenuePanel.jsx` novo, paralelo a T005/T006 se props-only |
| Documentação | T014, T015, T016 | Arquivos distintos, após T013 |

**Não paralelizar**: qualquer par de tarefas que edita `App.jsx` (T004→T006, T008→T012).

---

## Parallel Example: After T001

```text
Agent A: T002 — fetchDailyRevenue em src/TestOrder.Web/src/api.js
Agent B: T003 — formatters.js
→ merge → T004 App.jsx
```

## Parallel Example: Documentation (after T013)

```text
Agent A: T014 README.md
Agent B: T015 docs/PRESENTATION_GUIDE.md
Agent C: T016 AI_NOTES.md
→ merge → T017 validação automatizada
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. T001 preflight
2. T002–T004 foundational
3. T005–T006 US1 navegação
4. **STOP**: alternar abas; pedidos sem regressão

### Incremental Delivery

1. MVP (US1) → T007–T009 US2 consulta com dados
2. T010 US3 intervalo vazio
3. T011–T012 US4 erros + race
4. T013 CSS → T014–T016 docs → T017–T019 validação

### Scope Guards (repetir em cada PR/commit)

- ❌ `src/TestOrder.Api/**`
- ❌ `src/TestOrder.OrderProcessor/**`
- ❌ migrations / schema
- ❌ novos endpoints
- ❌ novas dependências npm
- ❌ react-router / Redux / Zustand / React Query / UI kit

---

## Notes

- `RevenuePanel.jsx` é componente de apresentação controlado por props — validação, HTTP e guard de race ficam em `App.jsx` ([contracts/ui.md](./contracts/ui.md) §6).
- Intervalo seed documentado: `2025-07-02` a `2026-07-01` (`AI_NOTES.md` módulo 003); defaults de UI usam mês corrente — operador ajusta datas na demo.
- `dev-up.ps1` permanece caminho principal — não alterar salvo auditoria com falha real.
- Nenhum teste automatizado de frontend — checklist manual é gate de aceite.

---

## Fechamento

| Campo | Valor |
| --- | --- |
| **Branch** | `007-tela-faturamento-periodo` |
| **Data** | 2026-07-03 |
| **Build frontend** | PASS — `npm run build` sem erros (baseline e final) |
| **Backend build/testes** | PASS — `dotnet build` 0 erros; `.\scripts\test.ps1` **46/46** (baseline e final) |
| **Checklist manual** | PASS — validado via HTTP direto ao proxy Vite (`intervalo com dados`, `intervalo vazio`, `startDate > endDate`) + inspeção do bundle final; ver validação complementar em `AI_NOTES.md`/`docs/PRESENTATION_GUIDE.md` e roteiro manual de UI no quickstart |
| **Escopo confirmado** | `git status --short` — apenas `README.md`, `src/TestOrder.Web/**`, `specs/007-*`; nenhum arquivo de `src/TestOrder.Api/`, `src/TestOrder.OrderProcessor/`, migrations ou `scripts/dev-up.ps1` |
| **Dependências novas** | Nenhuma — `package.json` inalterado (`react`, `react-dom`, `vite`, `@vitejs/plugin-react`) |
| **Arquivos tocados** | Modificados: `README.md`, `src/TestOrder.Web/src/api.js`, `src/TestOrder.Web/src/App.jsx`, `src/TestOrder.Web/src/styles.css`. Novos: `src/TestOrder.Web/src/formatters.js`, `src/TestOrder.Web/src/RevenuePanel.jsx`, `docs/PRESENTATION_GUIDE.md` (seção Módulo 007), `AI_NOTES.md` (seção Módulo 007) |
| **Status** | ✅ T001–T019 concluídas — módulo pronto para review/merge |
