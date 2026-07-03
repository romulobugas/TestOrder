# Research: Módulo 004 — Tela Web React para Pedidos

**Input**: [spec.md](./spec.md) | **Contexto adicional**: prompt técnico do usuário para `/speckit-plan`

Todas as decisões técnicas relevantes já vieram definidas no contexto do usuário (stack, estrutura de arquivos, proxy, bibliotecas proibidas). Este documento registra as decisões que **não** estavam 100% especificadas e precisavam de uma escolha explícita, mais o racional das escolhas já dadas.

---

## R1 — JavaScript simples (não TypeScript)

- **Decision**: Projeto em JavaScript puro (`.jsx`), sem TypeScript, sem `tsconfig.json`.
- **Rationale**: Reduz configuração (sem `@types/react`, sem compilação de tipos) e tempo de setup para uma tela pequena e de curta duração de vida útil no desafio. Consistente com a diretriz do usuário ("Preferir JavaScript simples se isso reduzir configuração e tempo").
- **Alternatives considered**: TypeScript com `tsconfig` mínimo — rejeitado por adicionar fricção (tipagem de respostas da API, configuração extra do Vite) sem benefício claro para uma tela de escopo tão reduzido.

## R2 — Sem Redux/Zustand/React Query/camada de service genérica

- **Decision**: Estado 100% local via `useState`/`useEffect` dentro de `App.jsx`; chamadas HTTP via `fetch` nativo centralizadas em `src/api.js` (**helper local obrigatório** neste módulo) com três funções simples — `fetchProducts`, `fetchOrders`, `createOrder` — sem classes, sem interfaces, sem DI e **sem** service layer genérica.
- **Rationale**: A tela tem um único "container" de estado (lista de pedidos + produtos + rascunho de formulário). Não há necessidade de cache global, invalidação cruzada de componentes ou store compartilhada — `useState` resolve sem cerimônia. Alinhado à regra do projeto de evitar abstrações antes de haver necessidade comprovada.
- **Alternatives considered**: React Query (cache/refetch automático) — rejeitado por ser uma dependência extra para um único componente com poucas requisições; Context API — rejeitado por não haver múltiplos níveis de componentes que precisem compartilhar estado.

## R3 — Produto duplicado no formulário: bloquear vs. somar quantidade

- **Decision**: **Bloquear** a adição de um produto já presente no rascunho do pedido, exibindo uma mensagem inline (“Este produto já foi adicionado — remova-o para alterar a quantidade.”) em vez de somar automaticamente a quantidade ao item existente.
- **Rationale**: Bloquear é a implementação mais simples: uma verificação (`items.some(i => i.productId === selected.productId)`) antes de inserir, sem precisar localizar o item existente, mesclar quantidade e atualizar o estado de forma diferente do fluxo normal de "adicionar". Também deixa o comportamento mais previsível para o usuário (cada linha do rascunho representa exatamente uma ação de "adicionar"), e casa com a regra de negócio do backend, que já rejeita `productId` duplicado em `POST /api/orders` com `400` — a UI apenas antecipa essa regra de forma amigável.
- **Alternatives considered**: Somar quantidade automaticamente ao item existente — rejeitado por exigir lógica extra de merge e por poder confundir o usuário sobre "quando" a soma acontece (ex.: se ele quisesse dois itens separados por engano, não teria como distinguir).

## R4 — Recarregar listagem após criar pedido: manter página atual vs. voltar para página 1

- **Decision**: Após criação bem-sucedida (`201`), a listagem é recarregada com **`page = 1`** (não mantém a página em que o usuário estava).
- **Rationale**: O backend ordena pedidos por `created_at DESC, id DESC` (módulo 001) — um pedido recém-criado sempre aparece na primeira página. Resetar para a página 1 garante que o usuário veja imediatamente o pedido que acabou de criar, sem precisar navegar manualmente. Se o backend mudar a ordenação padrão no futuro, essa decisão precisa ser revisitada (fora de escopo agora).
- **Alternatives considered**: Manter a página atual e apenas atualizar os dados — rejeitado porque, se o usuário estiver em uma página diferente da 1, ele não veria o pedido recém-criado, quebrando o critério de aceite AC-008 ("atualiza a listagem de pedidos" após sucesso, de forma que o resultado seja visível).

## R5 — Helper de API (`src/api.js`)

- **Decision**: Criar `src/api.js` como **helper local obrigatório** neste módulo, com **apenas** três funções puras: `fetchProducts()`, `fetchOrders(page, pageSize)`, `createOrder(payload)`. Cada uma faz `fetch`, verifica `response.ok`, faz `response.json()` e lança um erro com a mensagem extraída de `{ error }` quando a resposta não for `ok`. **Não** é service layer genérica — sem classes, interfaces, DI ou abstrações reutilizáveis além dessas três funções.
- **Rationale**: Evita repetir 3 vezes o mesmo boilerplate de `fetch(...).then(...).catch(...)` dentro de `App.jsx`, sem introduzir uma "camada de service" com interfaces, DI ou abstrações genéricas — são só funções, sem estado, sem classes.
- **Alternatives considered**: Inline `fetch` diretamente em cada handler dentro de `App.jsx` — rejeitado por repetir tratamento de erro em 3 lugares; o helper obrigatório concentra as três funções sem adicionar complexidade arquitetural.

## R6 — Proxy do Vite para o backend

- **Decision**: `vite.config.js` configura `server.proxy['/api'] = { target: 'http://localhost:5069', changeOrigin: true }`. Todas as chamadas do frontend usam caminhos relativos (`/api/products`, `/api/orders`, etc.), nunca a URL absoluta do backend.
- **Rationale**: Evita configurar CORS no backend (`src/TestOrder.Api`) — requisito explícito do usuário e do FR-014/FR-015 da spec. O proxy do Vite só funciona em `npm run dev` (ambiente de desenvolvimento), o que é suficiente para o escopo deste módulo (sem deploy).
- **Alternatives considered**: Adicionar middleware de CORS no backend — rejeitado por violar a restrição explícita de não alterar o backend neste módulo.

## R7 — Extração de mensagens de erro do backend

- **Decision**: Ao receber `400`/`409`/outro status de erro HTTP, a UI tenta ler o corpo JSON `{ error: string }` (contrato já usado em `ErrorResponse` nos módulos 001–003) e exibe esse texto. Se o corpo não puder ser parseado como JSON (ex.: falha de rede, backend fora do ar), exibe uma mensagem genérica fixa ("Não foi possível conectar ao servidor. Tente novamente.").
- **Rationale**: Reaproveita o contrato de erro já validado no backend sem duplicar texto de mensagens; cobre tanto erros de validação (`400`) quanto de negócio (`409`) com o mesmo mecanismo.
- **Alternatives considered**: Mapear cada `error` do backend para um texto customizado por cenário na UI — rejeitado por exigir uma tabela de mapeamento que a spec não pede e que aumentaria a superfície de manutenção sem benefício claro para o escopo do desafio.

## R8 — Responsividade sem framework CSS

- **Decision**: Uma única media query (breakpoint ~640px) em `styles.css` que empilha verticalmente a seção de formulário e a seção de listagem em telas estreitas, e ajusta o espaçamento/tamanho de fonte da tabela de pedidos.
- **Rationale**: Atende ao critério SC-005 (utilizável em desktop e mobile) sem adicionar nenhuma dependência de CSS framework, mantendo o CSS pequeno e fácil de explicar em entrevista.
- **Alternatives considered**: CSS Grid com `auto-fit`/`minmax` mais sofisticado — desnecessário para duas seções fixas (formulário + lista); uma media query simples resolve o requisito mínimo.

---

## Resumo das decisões já dadas pelo usuário (documentadas, não "pesquisadas")

| Item | Decisão |
| --- | --- |
| Stack | React + Vite, JavaScript (não TypeScript) |
| Localização | `src/TestOrder.Web/` |
| Estado | `useState`/`useEffect`, sem Redux/Zustand/React Query |
| CSS | Próprio, sem Material UI/Ant Design/Bootstrap |
| `pageSize` | Fixo em `20` |
| Proxy | `/api` → `http://localhost:5069` via `vite.config.js` |
| Backend | Não alterado neste módulo |
| Testes automatizados de frontend | Fora de escopo — apenas `npm run build` como smoke check |
