# Plano Técnico: Módulo 004 — Tela Web React para Pedidos

**Branch**: `004-tela-web-pedidos` | **Data**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Especificação em `specs/004-tela-web-pedidos/spec.md`

---

## Summary

Criar uma aplicação **React + Vite** em `src/TestOrder.Web`, em **JavaScript simples** (sem TypeScript), com uma única tela funcional que (1) lista pedidos paginados via `GET /api/orders` e (2) permite criar um novo pedido via `POST /api/orders`, carregando produtos de `GET /api/products` para o formulário. Estado 100% local (`useState`/`useEffect`), sem Redux/Zustand/React Query, sem bibliotecas de UI pesadas, CSS próprio e responsivo. Comunicação com o backend via proxy do Vite (`/api` → `http://localhost:5069`), **sem** alterar `src/TestOrder.Api`. Sem suíte de testes automatizados de frontend neste módulo — validação via `npm run build` (compilação) e checklist manual; a suíte do backend (**46/46**) deve permanecer intacta.

---

## Technical Context

| Item | Valor |
| --- | --- |
| **Language/Version** | JavaScript (ES2020+) — sem TypeScript |
| **Primary Dependencies** | React 18.x, Vite 5.x/6.x, `@vitejs/plugin-react` (dependências padrão do template Vite+React) |
| **Storage** | N/A — a tela não persiste dados; consome exclusivamente a API já existente |
| **Testing** | Nenhuma suíte automatizada de frontend neste módulo (fora de escopo, ver spec.md NFR-006); `npm run build` como smoke check de compilação. Backend: suíte existente `dotnet test TestOrder.slnx` (xUnit + Testcontainers), **inalterada** — deve continuar 46/46 |
| **Target Platform** | Navegador (desktop + mobile web), servido via `npm run dev` (Vite dev server) em desenvolvimento local |
| **Project Type** | Web frontend (SPA) consumindo API REST já existente — segunda fatia vertical do repositório, ao lado do backend |
| **Performance Goals** | Primeira página de pedidos visível em < 5s em ambiente local (SC-001, observacional) |
| **Constraints** | Sem alteração de backend; sem bibliotecas de estado/dados pesadas (Redux/Zustand/React Query); sem bibliotecas de UI pesadas (Material UI/Ant Design/Bootstrap); sem autenticação; sem Dockerfile de frontend; sem deploy |
| **Scale/Scope** | 1 tela, ~1–3 arquivos de componente (`App.jsx` principal, subcomponentes apresentacionais opcionais), uso individual/demo (não multiusuário concorrente) |

---

## Constitution Check

*GATES: `.cursor/rules/testorder.mdc` + spec do módulo 004 (o arquivo `.specify/memory/constitution.md` do projeto ainda é um template genérico não preenchido — os gates efetivos vêm das regras do workspace).*

| Gate | Status | Notas |
| --- | --- | --- |
| React web UI como primeira tela real, não landing page | ✅ PASS | `App.jsx` renderiza diretamente listagem + formulário, sem página de marketing |
| Sem microserviço Node neste módulo | ✅ PASS | Fora de escopo explícito (spec.md) |
| Sem RabbitMQ/Kafka/Redis/infra extra | ✅ PASS | Nenhuma infraestrutura nova — só um dev server Vite |
| Sem Clean Architecture/DDD/CQRS/repositories genéricos | ✅ PASS | Estado local via hooks; `src/api.js` são funções simples, não uma camada de service |
| Sem Redux/Zustand/React Query | ✅ PASS | `useState`/`useEffect` apenas (research.md R2) |
| Sem bibliotecas de UI pesadas | ✅ PASS | CSS próprio em `styles.css` |
| Não altera backend | ✅ PASS | Nenhum arquivo em `src/TestOrder.Api` é tocado; proxy do Vite evita CORS |
| Preserva contratos e testes do backend | ✅ PASS | `dotnet test TestOrder.slnx` deve continuar 46/46 |
| Poucos arquivos, fácil de explicar | ✅ PASS | Estrutura mínima do Vite + `App.jsx` + `styles.css` + `api.js` (helper obrigatório) |

**Pós-design (Phase 1)**: Nenhuma violação. Nenhum novo endpoint de backend, nenhuma nova dependência além do template padrão React+Vite.

---

## Project Structure

### Documentação (esta feature)

```text
specs/004-tela-web-pedidos/
├── spec.md
├── plan.md                 # este arquivo
├── research.md             # Phase 0
├── data-model.md           # Phase 1 — modelo de estado do frontend (sem schema novo)
├── quickstart.md           # Phase 1 — validação backend + frontend
├── contracts/
│   └── ui.md               # Phase 1 — contrato de consumo da API + contrato de comportamento da UI
└── checklists/
    └── requirements.md     # da /speckit-specify
```

(`tasks.md` gerado — ver tarefas de implementação neste diretório.)

### Código-fonte — novo projeto ao lado do backend

```text
F:\repository\TestOrder\
└── src/
    ├── TestOrder.Api/              # inalterado neste módulo
    └── TestOrder.Web/              # NOVO
        ├── package.json
        ├── vite.config.js          # proxy /api -> http://localhost:5069
        ├── index.html
        └── src/
            ├── main.jsx            # ponto de entrada React
            ├── App.jsx             # tela principal: listagem + formulário + estado
            ├── styles.css          # CSS próprio, responsivo
            └── api.js              # helper local obrigatório — fetchProducts, fetchOrders, createOrder (research.md R5)
```

**Nenhum arquivo de `src/TestOrder.Api/`, `tests/TestOrder.Api.Tests/`, migrations ou `docker-compose.yml` é criado ou alterado.** `.gitignore` na raiz precisa cobrir `src/TestOrder.Web/node_modules/` e `src/TestOrder.Web/dist/` (verificação/ajuste pontual em T008 de `tasks.md`, sem reescrever o arquivo inteiro).

**Structure Decision**: Projeto frontend standalone (`package.json` próprio, sem workspace/monorepo tooling) dentro de `src/TestOrder.Web`, paralelo ao backend `.NET` em `src/TestOrder.Api`. Mesma filosofia dos módulos anteriores: uma fatia vertical pequena e auto-contida, sem pastas artificiais (`components/`, `services/`, `hooks/` etc. só seriam criadas se o número de arquivos justificasse — não é o caso aqui).

---

## Decisões de implementação

| # | Decisão | Detalhe |
| --- | --- | --- |
| 1 | Stack | React 18 + Vite, template JavaScript (não TypeScript) |
| 2 | Estado | `useState`/`useEffect` em `App.jsx`; sem Redux/Zustand/React Query (research.md R2) |
| 3 | Organização de componentes | `App.jsx` concentra a lógica de estado e a maior parte do JSX; subcomponentes puramente apresentacionais (ex.: uma linha de pedido, uma opção de produto) podem ser extraídos **somente** se reduzirem duplicação/melhorarem leitura — decisão final registrada em `tasks.md`/implementação |
| 4 | Chamadas HTTP | `fetch` nativo via `src/TestOrder.Web/src/api.js` — **helper local obrigatório** neste módulo, com três funções (`fetchProducts`, `fetchOrders`, `createOrder`); sem classes, interfaces ou camada de service genérica (research.md R5) |
| 5 | Paginação | `pageSize` fixo em `20`; estado `{ page, pageSize, totalCount, totalPages }` vindo diretamente de `PagedOrdersResponse` |
| 6 | Produto duplicado no formulário | Bloqueado com mensagem inline, não somado automaticamente (research.md R3) |
| 7 | Pós-criação (201) | Formulário limpo, mensagem de sucesso, `page` resetado para `1` e `GET /api/orders` disparado novamente (research.md R4) |
| 8 | Erros (400/409/rede) | `draftOrder` preservado; mensagem extraída de `ErrorResponse.error` quando disponível, senão mensagem genérica (research.md R7) |
| 9 | Loading | Três flags independentes: `loadingProducts`, `loadingOrders`, `creating` |
| 10 | Proxy | `vite.config.js` → `server.proxy['/api'] = { target: 'http://localhost:5069', changeOrigin: true }` (research.md R6) |
| 11 | CSS/responsividade | `styles.css` próprio, uma media query (~640px) para empilhar formulário/listagem em telas estreitas (research.md R8) |

---

## Fluxo da tela (sequência)

```text
1. Montagem do componente (App.jsx):
   - useEffect #1: fetchProducts() -> products / loadingProducts / productsError
   - useEffect #2 (depende de `page`): fetchOrders(page, 20) -> orders / pagination / loadingOrders / ordersError

2. Navegação de página:
   - Clique em "anterior"/"próxima" -> setPage(page - 1 | page + 1) (respeitando limites de pagination)
   - Mudança de `page` dispara novamente o useEffect #2

3. Botão "atualizar":
   - Repete fetchOrders(page atual, 20) manualmente

4. Formulário — adicionar item:
   - Usuário seleciona produto + quantidade -> validação local (produto existe, quantidade > 0, produto não duplicado)
   - Se válido: adiciona a `draftOrder.items`
   - Se inválido: mensagem inline, nenhuma chamada de rede

5. Formulário — remover item:
   - Remove o item do array `draftOrder.items` pelo índice/productId

6. Formulário — enviar:
   - Bloqueia se `draftOrder.items.length === 0`
   - setCreating(true); createOrder(payload)
   - 201 -> reset draftOrder, createSuccessMessage, setPage(1) (ou refetch direto se já estiver na página 1)
   - 400/409/erro de rede -> createError preenchido, draftOrder mantido
   - finally -> setCreating(false)
```

---

## Contratos consumidos

Detalhamento completo em [contracts/ui.md](./contracts/ui.md).

| Endpoint | Novo? | Uso nesta tela |
| --- | --- | --- |
| `GET /api/products` | Não | Popular seleção de produto no formulário |
| `GET /api/orders?page=&pageSize=20` | Não | Listagem paginada de pedidos |
| `POST /api/orders` | Não | Criar novo pedido |
| `GET /api/orders/{id}` | Não | **Não consumido** nesta tela |
| `GET /api/revenue/daily` | Não | **Não consumido** nesta tela (fora de escopo) |

Nenhum contrato de backend é criado ou alterado — este módulo é puramente consumidor.

---

## Estratégia de validação

Não há suíte de testes automatizados de frontend neste módulo (spec.md NFR-006). A validação combina:

1. **Build do frontend** (`npm run build`) — smoke check de sintaxe/compilação.
2. **Regressão do backend** (`dotnet build TestOrder.slnx && .\scripts\test.ps1`) — deve continuar **46/46**, confirmando que nada foi alterado no backend.
3. **Checklist manual** (detalhado em [quickstart.md](./quickstart.md)) cobrindo: listar, paginar, atualizar, criar pedido válido (com e sem `customerName`), criar pedido inválido (400), criar pedido com estoque insuficiente (409), estados de carregamento/vazio, responsividade básica.

---

## Phase 0 & Phase 1 — Artefatos gerados

| Artefato | Status |
| --- | --- |
| [research.md](./research.md) | ✅ |
| [data-model.md](./data-model.md) | ✅ (modelo de estado do frontend, sem schema novo) |
| [contracts/ui.md](./contracts/ui.md) | ✅ |
| [quickstart.md](./quickstart.md) | ✅ |

---

## Documentação pós-implementação (não fazer neste passo)

### `AI_NOTES.md` — seção Módulo 004 (template)

Preencher após implementação:

- Estrutura final de componentes (se algum subcomponente foi extraído de `App.jsx` e por quê).
- `src/api.js` criado como helper local obrigatório com `fetchProducts`, `fetchOrders` e `createOrder` (não service layer genérica).
- Confirmação da decisão de bloquear produto duplicado (vs. somar) na prática.
- Resultado da validação (build do frontend, regressão do backend 46/46, checklist manual).
- O que a IA sugeriu e foi recusado (ex.: React Query, Dockerfile de frontend, TypeScript).
- Prompts Spec Kit usados neste módulo.

### `docs/PRESENTATION_GUIDE.md` — adições

- Linha de referência: `src/TestOrder.Web/src/App.jsx`, `vite.config.js`.
- Roteiro de demo: subir backend + frontend, listar, paginar, criar pedido válido, criar pedido com 409.
- Tabela pass/fail dos checks manuais do módulo 004.

### `README.md` / quickstart

- Comandos combinados de backend (`.\scripts\dev-up.ps1`) + frontend (`npm install && npm run dev` em `src/TestOrder.Web`).

---

## Complexity Tracking

*Nenhuma violação de constitution/regras do workspace a justificar. Módulo adiciona um projeto novo (frontend), mas mantém a mesma filosofia de simplicidade dos módulos anteriores — nenhuma camada arquitetural extra.*

---

## Próximos passos

1. **`/speckit-implement`** — executar T001–T026 de [`tasks.md`](./tasks.md), uma fatia por vez.
2. Não avançar para o módulo Node/outbox até fechar T* do módulo 004.

(`tasks.md` já gerado; `/speckit-analyze` concluído com ajustes em spec/plan/tasks.)

---

## Referências cruzadas

| Documento | Uso |
| --- | --- |
| [spec.md](./spec.md) | Requisitos e critérios de aceite |
| [research.md](./research.md) | Decisões R1–R8 |
| [data-model.md](./data-model.md) | Estado do frontend (sem schema novo) |
| [contracts/ui.md](./contracts/ui.md) | Contrato de consumo da API + comportamento da UI |
| [quickstart.md](./quickstart.md) | Validação manual e comandos combinados backend+frontend |
| [../003-faturamento-por-periodo/plan.md](../003-faturamento-por-periodo/plan.md) | Padrão de documentação/plano dos módulos anteriores |
| [../002-criacao-pedido-reservas/contracts/api.md](../002-criacao-pedido-reservas/contracts/api.md) | Contrato de `POST /api/orders` consumido por esta tela |
| [../001-base-listagem-pedidos/contracts/api.md](../001-base-listagem-pedidos/contracts/api.md) | Contratos de `GET /api/products` e `GET /api/orders` consumidos por esta tela |
