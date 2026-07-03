# Tasks: Módulo 004 — Tela Web React para Pedidos

**Input**: Design documents from `specs/004-tela-web-pedidos/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Sem suíte automatizada de frontend neste módulo — validação via `npm run build` (smoke) + checklist manual. Backend deve permanecer **46/46**.

**Organization**: Frontend-only, sem alteração de backend. Fases: preflight → scaffold Vite → helper HTTP → UI por user story → build/regressão → docs → validação manual.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência de tarefas incompletas)
- **[USn]**: User story de referência (ordem da spec): US1=listar pedidos paginados, US2=criar novo pedido, US3=tratar erros 400/409, US4=estados loading/vazio/responsivo
- Caminhos de arquivo explícitos

---

## Phase 1: Preflight

**Goal**: Proteger módulos 001/002/003 — confirmar build e testes do backend verdes antes de qualquer alteração.

**Independent Test**: `dotnet build` + `dotnet test` passam sem alteração de código.

---

- [x] T001 Validar build e testes do backend (`dotnet build TestOrder.slnx && dotnet test TestOrder.slnx`)

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar qualquer API local; confirmar **46/46** testes e build verde antes de criar o frontend. |
| **Permitidos** | Nenhum arquivo alterado |
| **Proibidos** | Alterações de código |
| **Pronto quando** | Build + testes passam |
| **Validação** | `Get-Process TestOrder.Api -ErrorAction SilentlyContinue \| Stop-Process -Force; dotnet build TestOrder.slnx; dotnet test TestOrder.slnx` |
| **Paralelo** | Não — primeiro passo obrigatório |

**Checkpoint Phase 1**: Baseline backend verde confirmada.

---

## Phase 2: Scaffold Vite (React + JavaScript)

**Goal**: Criar o projeto `src/TestOrder.Web` com Vite, proxy `/api` e layout operacional mínimo (não landing page).

**Independent Test**: `cd src/TestOrder.Web && npm install && npm run dev` sobe sem erros; tela exibe cabeçalho + seções vazias de formulário e listagem.

---

- [x] T002 Criar `src/TestOrder.Web/package.json` com React 18 + Vite + `@vitejs/plugin-react` (JavaScript, sem TypeScript)

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | Scripts: `"dev": "vite"`, `"build": "vite build"`, `"preview": "vite preview"`. Dependências mínimas do template Vite+React JS. **Proibido**: Redux, Zustand, React Query, Material UI, Ant Design, Bootstrap, TypeScript. |
| **Permitidos** | `src/TestOrder.Web/package.json` |
| **Proibidos** | Alterar `src/TestOrder.Api/` |
| **Pronto quando** | `npm install` conclui sem erros |
| **Paralelo** | Não — base do projeto frontend |

---

- [x] T003 [P] Criar `src/TestOrder.Web/vite.config.js` com proxy `/api` → `http://localhost:5069`

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | `import { defineConfig } from 'vite'` + `react()` plugin. `server.proxy: { '/api': { target: 'http://localhost:5069', changeOrigin: true } }`. Todas as chamadas do frontend usam caminhos relativos (`/api/products`, etc.). |
| **Permitidos** | `src/TestOrder.Web/vite.config.js` |
| **Proibidos** | CORS no backend; alterar `Program.cs` |
| **Pronto quando** | Arquivo compila quando o projeto estiver completo |
| **Paralelo** | Sim — independente de T004–T007 |

---

- [x] T004 [P] Criar `src/TestOrder.Web/index.html` com `#root` e script para `src/main.jsx`

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | HTML mínimo do Vite; título "TestOrder"; sem conteúdo de marketing/hero. |
| **Permitidos** | `src/TestOrder.Web/index.html` |
| **Paralelo** | Sim |

---

- [x] T005 [P] Criar `src/TestOrder.Web/src/main.jsx` montando `<App />` em `#root`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | `import React from 'react'`, `createRoot`, import `./styles.css`, render `<App />`. |
| **Permitidos** | `src/TestOrder.Web/src/main.jsx` |
| **Paralelo** | Sim |

---

- [x] T006 [P] Criar esqueleto de `src/TestOrder.Web/src/App.jsx` com layout operacional

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | Layout de app (não landing page): cabeçalho compacto ("TestOrder"), seção de criação de pedido (placeholder), seção de listagem paginada (placeholder). Sem lógica de API ainda — apenas estrutura JSX. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Sim |

---

- [x] T007 [P] Criar `src/TestOrder.Web/src/styles.css` com layout base responsivo

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | CSS próprio: tipografia legível, cabeçalho compacto, seções empilhadas, tabela/lista de pedidos, formulário. Uma media query (~640px) empilhando seções em mobile (research.md R8). Sem framework CSS. |
| **Permitidos** | `src/TestOrder.Web/src/styles.css` |
| **Paralelo** | Sim |

---

- [x] T008 Verificar `.gitignore` na raiz cobre `src/TestOrder.Web/node_modules/` e `src/TestOrder.Web/dist/`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | Append mínimo ao `.gitignore` existente se faltar — não reescrever o arquivo inteiro. |
| **Permitidos** | `.gitignore` (append apenas) |
| **Proibidos** | Alterar backend |
| **Pronto quando** | `node_modules/` e `dist/` do frontend ignorados |
| **Paralelo** | Não — após T002 |

**Checkpoint Phase 2**: `npm install && npm run dev` sobe; tela exibe layout operacional vazio.

---

## Phase 3: Helper HTTP (Foundational)

**Goal**: Centralizar chamadas `fetch` em funções simples, sem camada de service genérica.

**Independent Test**: Funções exportadas compilam; importáveis em `App.jsx`.

---

- [x] T009 [P] Criar `src/TestOrder.Web/src/api.js` com `fetchProducts`, `fetchOrders`, `createOrder`

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | Três funções puras usando `fetch` nativo: `fetchProducts()` → `GET /api/products`; `fetchOrders(page, pageSize=20)` → `GET /api/orders?page=&pageSize=`; `createOrder({ customerName, items })` → `POST /api/orders` com `Content-Type: application/json`. Em resposta não-ok: parsear `{ error }` de JSON e lançar `Error` com essa mensagem; em falha de rede, mensagem genérica (research.md R7). **Não** criar classes, interfaces ou factory genérica. |
| **Permitidos** | `src/TestOrder.Web/src/api.js` |
| **Proibidos** | Axios; React Query; service layer genérica |
| **Pronto quando** | Importável em `App.jsx` |
| **Paralelo** | Sim — arquivo independente |

**Checkpoint Phase 3**: Helper HTTP pronto; `App.jsx` pode importar as três funções.

---

## Phase 4: Listagem paginada de pedidos (US1)

**Goal**: Carregar e exibir pedidos paginados com navegação anterior/próxima e botão atualizar.

**Independent Test**: Com backend rodando, a tela exibe pedidos do seed com id, data, status, total e itens resumidos; paginação funciona.

---

- [x] T010 [US1] Implementar carregamento de produtos e pedidos em `src/TestOrder.Web/src/App.jsx`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | Estado: `products`, `loadingProducts`, `productsError`; `orders`, `pagination` (`page`, `pageSize=20`, `totalCount`, `totalPages`), `loadingOrders`, `ordersError`. `useEffect` #1 (mount): `fetchProducts()`. `useEffect` #2 (depende de `page`): `fetchOrders(page, 20)`. Formatar `createdAt` para exibição legível (UTC com sufixo `Z` do backend). **Importar** `fetchProducts` e `fetchOrders` de `src/TestOrder.Web/src/api.js` (T009). |
| **Depende de** | **T009** — helper `api.js` obrigatório antes desta task |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Proibidos** | Redux/Zustand/React Query |
| **Paralelo** | Não — mesmo arquivo que T011–T018 |

---

- [x] T011 [US1] Renderizar listagem de pedidos com id, data, status, total e itens resumidos em `src/TestOrder.Web/src/App.jsx`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | Tabela ou lista densa: por pedido, exibir `id`, data formatada, `status`, `total`, resumo de `items[]` (ex.: `"Produto X × 2, Produto Y × 1"`). Sem chamar `GET /api/orders/{id}` — dados vêm da listagem paginada (spec Assumptions). |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx`, ajustes em `src/TestOrder.Web/src/styles.css` se necessário |
| **Paralelo** | Não — sequencial após T010 |

---

- [x] T012 [US1] Implementar paginação anterior/próxima, botão atualizar e metadados em `src/TestOrder.Web/src/App.jsx`

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | Botões "anterior"/"próxima": desabilitar na página 1 / última página (`page === 1`, `page === totalPages`). Exibir `Página {page} de {totalPages} ({totalCount} pedidos)`. Botão "atualizar" refaz `fetchOrders(page atual, 20)`. Mudança de `page` dispara o `useEffect` de pedidos. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial após T011 |

**Checkpoint Phase 4 (US1)**: Listagem paginada funcional com backend rodando.

---

## Phase 5: Formulário de criação de pedido (US2)

**Goal**: Montar pedido com produtos/quantidades e enviar `POST /api/orders`; sucesso limpa formulário e atualiza listagem na página 1.

**Independent Test**: Criar pedido válido → mensagem de sucesso, formulário limpo, novo pedido visível na listagem.

---

- [x] T013 [US2] Implementar formulário de criação (customerName, produto, quantidade, adicionar/remover item) em `src/TestOrder.Web/src/App.jsx`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | Estado `draftOrder`: `{ customerName: '', items: [] }`. Select de produto populado de `products`. Input quantidade (inteiro `> 0`). Botão "adicionar item": **rejeitar** quantidade **0**, **negativa**, **vazia** ou **não numérica** — o item **não** entra no rascunho; exibir mensagem inline (ex.: "Informe uma quantidade inteira maior que zero."). Só após validação válida insere `DraftOrderItem` (`productId`, `productName`, `unitPrice`, `quantity`). Botão "remover" por item. `customerName` opcional (texto livre). |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial |

---

- [x] T014 [US2] Bloquear produto duplicado no rascunho com mensagem inline em `src/TestOrder.Web/src/App.jsx`

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | Antes de adicionar: se `draftOrder.items.some(i => i.productId === selectedId)` → **não** adicionar; exibir mensagem inline ("Este produto já foi adicionado — remova-o para alterar a quantidade."). Decisão research.md R3: bloquear, não somar quantidade. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial após T013 |

---

- [x] T015 [US2] Implementar envio `POST /api/orders` com fluxo de sucesso em `src/TestOrder.Web/src/App.jsx`

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Botão "criar pedido": bloquear se `draftOrder.items.length === 0`. `creating=true` durante envio. Payload: `{ customerName: draftOrder.customerName \|\| null, items: [{ productId, quantity }] }`. Em `201`: resetar `draftOrder`, `createSuccessMessage` (ex.: `"Pedido #123 criado."`), `setPage(1)` e refetch de pedidos (research.md R4). Desabilitar botão enquanto `creating`. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial após T014 |

**Checkpoint Phase 5 (US2)**: Criação de pedido válido funcional.

---

## Phase 6: Tratamento de erros na criação (US3)

**Goal**: Exibir erros 400/409/rede sem perder o rascunho do formulário.

**Independent Test**: Enviar pedido com quantidade absurda → mensagem de conflito de estoque; itens do formulário permanecem.

---

- [x] T016 [US3] Exibir erros 400/409/rede na criação sem limpar formulário em `src/TestOrder.Web/src/App.jsx`

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Estado `createError`. Em falha: preencher com texto de `ErrorResponse.error` (via `api.js`) ou mensagem genérica de conexão. Para `409`: mensagem deixa claro conflito de estoque (pode usar texto do backend + rótulo visual). **`draftOrder` preservado** — não resetar em erro. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial |

---

- [x] T017 [US3] Bloquear envio sem itens e exibir feedback local em `src/TestOrder.Web/src/App.jsx`

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | Se `draftOrder.items.length === 0` ao clicar "criar pedido": não chamar API; exibir mensagem local ("Adicione ao menos um item."). **Reforça** o bloqueio mínimo de T015 (que impede o envio) com feedback explícito ao usuário — T015 e T017 permanecem separados: T015 cobre o fluxo feliz + guard no envio; T017 cobre a mensagem local antes da chamada HTTP. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial após T016 |

**Checkpoint Phase 6 (US3)**: Erros 400/409 visíveis; formulário preservado em falha.

---

## Phase 7: Estados de carregamento, lista vazia e responsividade (US4)

**Goal**: UX operacional completa — loading, vazio, erro de listagem/produtos, mobile básico.

**Independent Test**: Indicadores de carregamento visíveis; lista vazia com mensagem; layout utilizável em ~375px.

---

- [x] T018 [US4] Implementar estados loading/error/empty para produtos e pedidos em `src/TestOrder.Web/src/App.jsx`

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | `loadingProducts`/`loadingOrders`/`creating` → indicadores visíveis ("Carregando..."). `orders.length === 0` e não loading → "Nenhum pedido encontrado." (não tabela em branco). `productsError`/`ordersError` → mensagens na seção correspondente; resto da tela permanece utilizável. |
| **Permitidos** | `src/TestOrder.Web/src/App.jsx` |
| **Paralelo** | Não — sequencial |

---

- [x] T019 [US4] Finalizar CSS responsivo e polish visual em `src/TestOrder.Web/src/styles.css`

**Detalhe T019**
| Campo | Valor |
| --- | --- |
| **Descrição** | Refinar layout operacional: cabeçalho compacto, formulário e listagem legíveis, botões de paginação, mensagens de erro/sucesso destacadas mas discretas. Media query mobile empilha seções; sem rolagem horizontal indevida. Visual denso e apresentável para entrevista — sem hero/marketing. |
| **Permitidos** | `src/TestOrder.Web/src/styles.css` (ajustes pontuais em `App.jsx` se necessário para classes) |
| **Paralelo** | Não — sequencial após T018 (polish CSS depende das classes/estrutura definidas em T018) |

**Checkpoint Phase 7 (US4)**: Tela completa com todos os estados visíveis.

---

## Phase 8: Build e regressão do backend

**Goal**: Confirmar build do frontend e que o backend permanece intacto (46/46).

**Independent Test**: `npm run build` passa; `dotnet test` passa 46/46.

---

- [x] T020 Validar build do frontend (`cd src/TestOrder.Web && npm install && npm run build`)

**Detalhe T020**
| Campo | Valor |
| --- | --- |
| **Descrição** | `npm install` (se ainda não feito) + `npm run build` sem erros. Saída em `src/TestOrder.Web/dist/`. **Revisar `package.json`**: confirmar que `dependencies` e `devDependencies` **não** incluem Redux, Zustand, React Query, Material UI, Ant Design, Bootstrap ou TypeScript (AC-014 / T002). |
| **Permitidos** | Nenhuma alteração de código (salvo fixes mínimos se build falhar) |
| **Validação** | `cd src/TestOrder.Web; npm install; npm run build` |
| **Paralelo** | Não |

---

- [x] T021 Validar regressão do backend (`dotnet build TestOrder.slnx && .\scripts\test.ps1`)

**Detalhe T021**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar API local antes. Confirmar **46/46** testes — nenhum arquivo de `src/TestOrder.Api/` ou `tests/` foi alterado neste módulo. |
| **Permitidos** | Nenhum arquivo alterado |
| **Validação** | `Get-Process TestOrder.Api -ErrorAction SilentlyContinue \| Stop-Process -Force; dotnet build TestOrder.slnx; .\scripts\test.ps1; dotnet test TestOrder.slnx` |
| **Paralelo** | Não — após T020 |

**Checkpoint Phase 8**: Build frontend OK + backend 46/46 intacto.

---

## Phase 9: Documentação pós-implementação

**Goal**: Atualizar artefatos de documentação com fatos reais pós-implementação.

**Independent Test**: Revisão humana dos documentos.

---

- [x] T022 [P] Atualizar `AI_NOTES.md` com seção Módulo 004

**Detalhe T022**
| Campo | Valor |
| --- | --- |
| **Descrição** | Status concluído; estrutura final de componentes; decisão de bloquear produto duplicado; proxy Vite; resultado de build/regressão 46/46; checklist manual; o que a IA sugeriu e foi recusado; prompts Spec Kit usados. |
| **Permitidos** | `AI_NOTES.md` |
| **Paralelo** | Sim |

---

- [x] T023 [P] Atualizar `docs/PRESENTATION_GUIDE.md` com seção Módulo 004

**Detalhe T023**
| Campo | Valor |
| --- | --- |
| **Descrição** | Marcar módulo 004 concluído na ordem de apresentação. Referências: `src/TestOrder.Web/src/App.jsx`, `vite.config.js`, `api.js`. Roteiro demo: subir backend + frontend, listar, paginar, criar pedido válido, criar pedido com 409. Tabela pass/fail dos checks manuais. |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Paralelo** | Sim |

---

- [x] T024 Revisar `specs/004-tela-web-pedidos/quickstart.md` com comandos finais validados

**Detalhe T024**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar porta Vite, comandos reais de `npm install`/`npm run dev`/`npm run build`, checklist manual e resultado de regressão 46/46. |
| **Permitidos** | `specs/004-tela-web-pedidos/quickstart.md` |
| **Paralelo** | Não — após T020–T021 |

---

- [x] T025 Criar ou atualizar `README.md` na raiz com comandos backend + frontend

**Detalhe T025**
| Campo | Valor |
| --- | --- |
| **Descrição** | README mínimo na raiz (não existia antes): como subir backend (`.\scripts\dev-up.ps1`), frontend (`cd src/TestOrder.Web && npm install && npm run dev`), build e testes. Link para `docs/PRESENTATION_GUIDE.md`. |
| **Permitidos** | `README.md` (criar ou atualizar) |
| **Paralelo** | Não — após validação T020 |

**Checkpoint Phase 9**: Documentação pronta para demo.

---

## Phase 10: Validação manual final

**Goal**: Executar checklist completo de aceite da spec com backend + frontend rodando.

**Independent Test**: Todos os passos do checklist manual passam.

---

- [x] T026 Validação manual obrigatória do módulo

**Detalhe T026**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar checklist completo: subir backend + frontend, abrir tela (não landing page), listar pedidos, navegar paginação, atualizar lista, criar pedido válido (com/sem customerName), validar quantidade inválida no formulário (0/negativa/não numérica), tentar envio sem itens (400/bloqueio local), criar pedido com quantidade absurda (409), verificar mobile básico (~375px), revisar `package.json` sem dependências proibidas, confirmar build frontend e backend 46/46. |
| **Permitidos** | Nenhuma alteração de código (salvo fixes mínimos se checklist falhar) |
| **Pronto quando** | Todos os passos abaixo passam |
| **Paralelo** | Não — último passo |

**Validações finais obrigatórias**:

```powershell
# Backend
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1

# Frontend build
cd src/TestOrder.Web
npm run build
cd ../..
```

**Validação manual (dois terminais)**:

```powershell
# Terminal 1 — backend
.\scripts\dev-up.ps1

# Terminal 2 — frontend
cd src/TestOrder.Web
npm run dev
```

Checklist (marcar pass/fail em `docs/PRESENTATION_GUIDE.md`):

1. Abrir URL do Vite — tela operacional (listagem + formulário), não landing page
2. Listagem carrega pedidos do seed (id, data, status, total, itens resumidos)
3. Paginação anterior/próxima funciona
4. Botão "atualizar" refaz a busca
5. Produtos aparecem no select do formulário
6. Adicionar/remover itens no rascunho
7. Tentar quantidade **0**, **negativa**, **vazia** ou **não numérica** → item **não** entra no rascunho; mensagem exibida
8. Produto duplicado bloqueado com mensagem
9. Criar pedido válido → sucesso, formulário limpo, pedido na página 1
10. Enviar sem itens → bloqueio/mensagem local
11. Quantidade absurda → erro 409 visível, formulário preservado
12. Redimensionar para mobile (~375px) — layout utilizável
13. `npm run build` passa
14. Revisar `package.json` — `dependencies`/`devDependencies` **sem** Redux, Zustand, React Query, Material UI, Ant Design, Bootstrap ou TypeScript
15. Backend **46/46** intacto

**Checkpoint Phase 10**: Módulo 004 completo.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 (preflight)
T002 → T003, T004, T005, T006, T007 (parallel após T002) → T008
T009 (api.js, parallel com T006/T007 se T002 feito) → T010
T010 → T011 → T012 (US1, sequencial — App.jsx)
T013 → T014 → T015 (US2, sequencial — App.jsx)
T016 → T017 (US3, sequencial — App.jsx)
T018 (US4, App.jsx) → T019 (styles.css — sequencial; polish CSS depende de T018)
T020 → T021 (build/regressão)
T022, T023 [P] (docs paralelos) → T024 → T025 → T026
```

### User Story Mapping

| Story | Prioridade | Tarefas principais |
| --- | --- | --- |
| US1 — Listar pedidos paginados | P1 | T010, T011, T012 |
| US2 — Criar novo pedido | P1 | T013, T014, T015 |
| US3 — Tratar erros 400/409 | P1 | T016, T017 |
| US4 — Loading/vazio/responsivo | P2 | T018, T019 |

### Parallel Opportunities

- **T003 + T004 + T005 + T006 + T007** — arquivos diferentes após T002 (`package.json`)
- **T009** — `api.js` independente de T006/T007 (pode ser feito em paralelo ao scaffold); **T010 depende de T009**
- **T022 + T023** — docs em arquivos diferentes
- **T010 → T018 → T019** — edições em `App.jsx` (T010–T018) e polish CSS (T019) devem ser **sequenciais**

---

## MVP Scope

**MVP mínimo demonstrável**: Phase 1–2 (T001–T008) + Phase 3 (T009) + US1+US2 (T010–T015) + build (T020).

Tela funcional que lista pedidos paginados e cria pedido válido — fatia mínima de valor do desafio.

**MVP recomendado para demo**: MVP acima + US3 erros (T016–T017) + build/regressão (T020–T021).

Tratamento de 409 e mensagens de erro são esperados pelo avaliador — incluir US3 no MVP de demo.

Módulo **completo** exige Phase 7–10 (T018–T026): estados loading/vazio, CSS responsivo, docs e checklist manual.

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Phase 1: Preflight → baseline backend 46/46
2. Phase 2: Scaffold Vite → layout operacional vazio
3. Phase 3: `api.js` → funções fetch prontas
4. Phase 4–5: Listagem + formulário + criação
5. **STOP and VALIDATE**: `npm run dev` + backend; listar e criar pedido válido

### Incremental Delivery

1. Phase 6: Erros 400/409 (US3)
2. Phase 7: Loading/vazio/responsivo (US4)
3. Phase 8: Build + regressão backend
4. Phase 9–10: Docs + checklist manual

---

## Critérios de pronto por User Story

| Story | Critério | Evidência |
| --- | --- | --- |
| US1 | Listagem paginada com anterior/próxima/atualizar | T010–T012 + checklist manual #2–4 |
| US2 | Criar pedido válido limpa formulário e atualiza lista | T013–T015 + checklist manual #5–9 |
| US3 | 400/409 visíveis; formulário preservado em erro | T016–T017 + checklist manual #10–11 |
| US4 | Loading/vazio/mobile básico | T018–T019 + checklist manual #1, #12 |

---

## Notes

- Parar API local antes de `dotnet build`/`dotnet test` no Windows (exe bloqueado).
- Frontend e backend rodam em **dois terminais** (`dev-up.ps1` + `npm run dev`).
- **Nenhum** arquivo de `src/TestOrder.Api/`, `tests/TestOrder.Api.Tests/`, migrations, `Program.cs` ou `docker-compose.yml` é tocado neste módulo.
- Sem suíte automatizada de frontend — `npm run build` é o único gate automatizado do frontend.
- Commit sugerido após cada fase (T001; T002–T009; T010–T019; T020–T021; T022–T026).
- Não avançar para módulo Node/outbox até T026 passar.
