# Especificação: Módulo 004 — Tela Web React para Pedidos

**Feature Branch**: `004-tela-web-pedidos`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Implementar uma interface web em React que permita listar pedidos e criar um novo pedido, consumindo os endpoints já existentes do backend (`GET /api/products`, `GET /api/orders`, `GET /api/orders/{id}`, `POST /api/orders`). Deve ser a tela real do sistema (não landing page), com listagem paginada e formulário de criação de pedido, mantendo UI simples e sem bibliotecas de estado/UI pesadas.

**Depende de**: Módulos 001 (base/listagem), 002 (criação de pedido com reservas) e 003 (faturamento por período) concluídos e mergeados no `main`. O módulo 004 **não** altera o backend, exceto se for estritamente impossível rodar a tela sem uma alteração mínima (a ser justificada no plano, se ocorrer).

---

## Objetivo do módulo

Entregar a **tela obrigatória do desafio**: uma aplicação web React que permite (1) listar pedidos existentes de forma paginada e (2) criar um novo pedido escolhendo produtos e quantidades. A tela consome diretamente os endpoints já implementados nos módulos 001/002 — nenhuma nova rota de backend é criada neste módulo. Faturamento (módulo 003) **não** tem tela própria aqui; fica fora de escopo um dashboard gráfico.

A aplicação vive em `src/TestOrder.Web`, ao lado de `src/TestOrder.Api`, como uma segunda fatia vertical do mesmo repositório.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Operador da loja** | Ver pedidos existentes e registrar um novo pedido rapidamente. |
| **Avaliador do desafio** | Confirmar que a tela obrigatória existe, é funcional (não é uma landing page) e reflete o comportamento real do backend, incluindo erros. |

Não há autenticação nem perfis de acesso neste módulo.

---

## Jornadas do módulo

### Jornada 1 — Listar pedidos paginados (P1)

**Como** operador, **quero** ver os pedidos existentes com paginação, **para** acompanhar o que já foi registrado no sistema.

**Por que P1**: Sem listagem funcional, a tela não cumpre o requisito mínimo do desafio.

**Teste independente**: Abrir a tela com o backend rodando e o seed populado exibe uma lista de pedidos com id, data, status, total e itens resumidos, sem exigir nenhuma outra ação.

**Cenários de aceite**:

1. **Dado** o backend rodando com pedidos existentes, **quando** a tela carrega, **então** a lista exibe id, data (formatada), status, total e um resumo dos itens de cada pedido da página atual.
2. **Dado** que existe mais de uma página de pedidos, **quando** clico em "próxima", **então** a lista é substituída pelos pedidos da página seguinte, sem recarregar a página inteira do navegador.
3. **Dado** que estou na página 2 ou superior, **quando** clico em "anterior", **então** a lista volta para a página anterior.
4. **Dado** que estou na primeira página, **quando** a tela é exibida, **então** o botão/ação "anterior" fica desabilitado ou sem efeito.
5. **Dado** que estou na última página, **quando** a tela é exibida, **então** o botão/ação "próxima" fica desabilitado ou sem efeito.
6. **Dado** a lista carregada, **quando** clico em "atualizar", **então** a lista é buscada novamente no backend e reflete o estado atual.

---

### Jornada 2 — Criar novo pedido (P1)

**Como** operador, **quero** montar um pedido escolhendo produtos e quantidades, **para** registrar uma nova venda.

**Por que P1**: Entrega central do módulo — sem criação funcional, a tela não atende ao requisito mínimo do desafio.

**Teste independente**: Preencher o formulário com pelo menos um item válido e enviar resulta em um novo pedido criado no backend (`201`), visível na listagem após a atualização automática.

**Cenários de aceite**:

1. **Dado** que a tela carregou a lista de produtos (`GET /api/products`), **quando** abro o formulário de criação, **então** vejo os produtos disponíveis para seleção.
2. **Dado** o formulário aberto, **quando** escolho um produto e uma quantidade e clico em "adicionar item", **então** o item aparece na lista de itens do pedido em construção.
3. **Dado** um ou mais itens adicionados, **quando** clico em "remover" em um deles, **então** o item é removido da lista antes do envio.
4. **Dado** ao menos um item válido na lista e o campo `customerName` preenchido ou vazio, **quando** clico em "criar pedido", **então** o sistema envia `POST /api/orders` e, em sucesso, limpa o formulário, exibe uma mensagem de sucesso e atualiza a listagem de pedidos.
5. **Dado** o campo `customerName` vazio, **quando** envio o pedido, **então** o pedido é criado normalmente (campo é opcional).

---

### Jornada 3 — Tratar erros de criação de pedido (P1)

**Como** operador, **quero** entender por que um pedido não foi criado, **para** corrigir o problema ou tentar novamente.

**Por que P1**: O backend responde com `400` (validação) e `409` (estoque insuficiente) — a tela precisa refletir isso com clareza, sem travar ou mostrar tela em branco.

**Teste independente**: Enviar um pedido com quantidade maior que o estoque disponível retorna `409` do backend e a tela exibe uma mensagem de conflito de estoque sem perder os dados já preenchidos no formulário.

**Cenários de aceite**:

1. **Dado** um pedido sem itens, **quando** tento enviar, **então** a tela impede o envio ou exibe a mensagem de erro retornada pelo backend (`400`), sem quebrar a interface.
2. **Dado** um pedido com quantidade maior que o estoque disponível, **quando** envio, **então** a tela exibe uma mensagem clara de conflito de estoque (`409`) e mantém os itens preenchidos para nova tentativa.
3. **Dado** qualquer erro de rede ou backend indisponível, **quando** a ação de listar ou criar falha, **então** a tela exibe uma mensagem de erro genérica compreensível, sem travar a aplicação.

---

### Jornada 4 — Estados de carregamento e lista vazia (P2)

**Como** operador, **quero** perceber quando os dados estão carregando ou quando não há pedidos, **para** não confundir "vazio" com "erro" ou "travado".

**Por que P2**: Melhora a clareza operacional e a experiência em demonstração, mas não bloqueia o fluxo principal de listar/criar.

**Teste independente**: Em um ambiente sem pedidos (ou filtrando artificialmente), a tela exibe uma mensagem de lista vazia em vez de uma tabela em branco sem explicação.

**Cenários de aceite**:

1. **Dado** que a listagem está sendo buscada, **quando** a requisição ainda não retornou, **então** a tela exibe um indicador de carregamento.
2. **Dado** que a listagem retornou zero itens, **quando** a tela renderiza, **então** exibe uma mensagem de "nenhum pedido encontrado" em vez de uma tabela vazia sem contexto.
3. **Dado** que a criação de pedido está em andamento (requisição pendente), **quando** o usuário observa o formulário, **então** o botão de envio fica desabilitado ou sinaliza "enviando" para evitar duplo envio.

---

### Casos de borda

- **Lista vazia**: nenhum pedido cadastrado ainda (ambiente novo) — tratado na Jornada 4.
- **Backend indisponível**: falha de rede ao carregar produtos, pedidos ou ao criar pedido — mensagem de erro genérica, sem crash da aplicação (tela branca).
- **Envio de pedido sem itens**: bloqueado no cliente antes de chamar a API, ou tratado via erro `400` do backend caso o bloqueio no cliente falhe.
- **Produto duplicado no formulário**: adicionar o mesmo produto duas vezes deve ser tratado de forma explícita — a UI soma a quantidade no item existente **ou** bloqueia com mensagem, mas não deve gerar dois itens com o mesmo `productId` no payload enviado (o backend rejeita com `400` em caso de duplicidade).
- **Quantidade inválida**: quantidade `<= 0` ou não numérica não deve ser adicionável como item no formulário.
- **Estoque insuficiente (409)**: a tela mantém os dados do formulário para nova tentativa, sem forçar o usuário a preencher tudo novamente.
- **Paginação nos limites**: primeira página não permite "anterior"; última página não permite "próxima" (baseado em `totalPages` retornado pelo backend).
- **Nome do cliente com espaços**: tratado como opcional pelo backend (`customerName` vazio/whitespace vira `null`); a tela não precisa validar isso além de permitir o campo vazio.
- **Viewport mobile**: a tela deve permanecer utilizável (sem sobreposição de elementos ou rolagem horizontal quebrada) em larguras menores, mesmo que o layout não seja otimizado pixel a pixel para mobile.

---

## Requisitos funcionais

- **FR-001**: O sistema DEVE ser uma aplicação React (Vite) localizada em `src/TestOrder.Web`, com a tela funcional de pedidos como primeira tela — **não** uma landing page de marketing.
- **FR-002**: A tela DEVE listar pedidos paginados via `GET /api/orders?page=&pageSize=`, exibindo `id`, data de criação, status, total e um resumo dos itens (produto, quantidade) de cada pedido.
- **FR-003**: A tela DEVE permitir navegar para a página anterior e para a próxima página da listagem, respeitando os limites (`page`, `totalPages`) retornados pelo backend.
- **FR-004**: A tela DEVE permitir atualizar (refresh) a listagem sob demanda, refazendo a requisição ao backend.
- **FR-005**: A tela DEVE carregar a lista de produtos via `GET /api/products` para uso no formulário de criação de pedido.
- **FR-006**: O formulário de criação DEVE permitir escolher um produto (dentre os carregados) e uma quantidade, e adicionar esse item a uma lista local de itens do pedido em construção.
- **FR-007**: O formulário DEVE permitir remover um item já adicionado antes do envio.
- **FR-008**: O formulário DEVE ter um campo `customerName` opcional (texto livre, pode ficar vazio).
- **FR-009**: Ao confirmar a criação, o sistema DEVE enviar `POST /api/orders` com o payload `{ customerName, items: [{ productId, quantity }] }`.
- **FR-010**: Em sucesso (`201`), o sistema DEVE limpar o formulário, exibir uma mensagem de sucesso e atualizar a listagem de pedidos automaticamente.
- **FR-011**: Em erro `400` (validação) ou `409` (estoque insuficiente), o sistema DEVE exibir uma mensagem de erro compreensível ao usuário, sem perder os itens já preenchidos no formulário, e sem quebrar a interface.
- **FR-012**: A tela DEVE exibir um estado de carregamento distinto enquanto busca pedidos, produtos, ou enquanto uma criação está em andamento.
- **FR-013**: A tela DEVE exibir uma mensagem explícita quando a listagem de pedidos retornar vazia.
- **FR-014**: A tela DEVE usar o proxy do Vite (`/api` → `http://localhost:5069`) para chamar o backend, evitando configuração de CORS no backend.
- **FR-015**: O sistema NÃO DEVE alterar o backend (`src/TestOrder.Api`) neste módulo, exceto se for estritamente necessário para viabilizar a tela — qualquer exceção deve ser justificada explicitamente no plano técnico.
- **FR-016**: O sistema NÃO DEVE introduzir bibliotecas de gerenciamento de estado genérico (Redux, Zustand, React Query) nem camadas de serviço/abstração genéricas — chamadas HTTP ficam próximas dos componentes ou em um helper local simples.
- **FR-017**: O sistema NÃO DEVE usar bibliotecas visuais pesadas (ex.: Material UI, Ant Design, Bootstrap completo) — CSS simples e próprio.

### Key Entities

- **Order (frontend)**: representação local do pedido vindo de `GET /api/orders` — `id`, `createdAt`, `status`, `total`, `items[]` (cada item com `productId`, `productName`, `quantity`, `unitPrice`).
- **Product (frontend)**: representação local do produto vindo de `GET /api/products` — `id`, `name`, `unitPrice`.
- **DraftOrderItem (estado local do formulário)**: item ainda não enviado — `productId`, `productName` (para exibição), `quantity`.
- **DraftOrder (estado local do formulário)**: `customerName` (opcional), lista de `DraftOrderItem`.

---

## Requisitos não funcionais

- **NFR-001 (Simplicidade)**: Sem Redux/Zustand/React Query/CQRS/camadas de service genéricas; estado local via hooks nativos do React (`useState`/`useEffect`).
- **NFR-002 (Sem alteração de backend)**: Nenhuma alteração em `src/TestOrder.Api` é esperada; proxy do Vite resolve a comunicação local sem CORS.
- **NFR-003 (Sem dependências pesadas)**: Sem bibliotecas de UI completas; CSS próprio, simples e responsivo.
- **NFR-004 (Build)**: `npm run build` DEVE compilar sem erros.
- **NFR-005 (Regressão do backend)**: A suíte automatizada do backend DEVE continuar passando **46/46** — este módulo não deve introduzir regressão no backend.
- **NFR-006 (Testabilidade manual)**: Este módulo não exige suíte de testes automatizados de frontend; a validação é manual, documentada em checklist reproduzível.
- **NFR-007 (Rastreabilidade)**: Decisões de implementação DEVEM ser documentadas em `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md` após a implementação.

---

## Modelo de dados esperado (alto nível)

Nenhuma nova entidade de backend é criada. O frontend consome exclusivamente os contratos JSON já existentes:

```text
GET /api/products        → ProductResponse[]           { id, name, unitPrice }
GET /api/orders           → PagedOrdersResponse         { page, pageSize, totalCount, totalPages, items: OrderResponse[] }
GET /api/orders/{id}      → OrderResponse (não usado nesta tela — resumo já vem na listagem)
POST /api/orders          → OrderResponse | ErrorResponse
                             request: { customerName?, items: [{ productId, quantity }] }
```

- `GET /api/orders/{id}` já existe no backend, mas **não é consumido** por esta tela (a listagem paginada já traz os itens resumidos de cada pedido).
- `GET /api/revenue/daily` (módulo 003) **não** é consumido nesta tela — não há dashboard de faturamento neste módulo.

---

## Contrato HTTP consumido (resumo)

| Endpoint | Uso na tela | Observação |
| --- | --- | --- |
| `GET /api/products` | Carregar produtos para o formulário de criação | Sem paginação — lista completa (padrão do módulo 001) |
| `GET /api/orders?page=&pageSize=` | Listagem paginada de pedidos | `page`/`pageSize` controlados pela UI; itens resumidos já incluídos |
| `POST /api/orders` | Criar novo pedido | `201` sucesso, `400` validação, `409` estoque insuficiente |

Nenhum contrato existente é alterado; a tela é **somente consumidora** dos endpoints já validados nos módulos 001/002.

---

## Fora de escopo deste módulo

- Microserviço Node e consumo de outbox
- Worker de processamento assíncrono
- Autenticação e autorização
- Deploy (produção, CI/CD, hospedagem)
- Dockerfile do frontend
- Dashboard gráfico de faturamento (módulo 003 fica sem tela própria por enquanto)
- Filtros avançados de pedidos (por status, cliente, data, produto)
- Edição ou cancelamento de pedido existente
- Exportação de dados (CSV/PDF)
- Testes automatizados de frontend (unitários/E2E) — validação manual apenas
- Internacionalização (i18n) — apenas idioma único (Português)

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | Tela inicial é funcional, não landing page | Abrir `http://localhost:5173` (ou porta do Vite) exibe listagem + formulário reais |
| AC-002 | Listagem exibe campos obrigatórios | id, data, status, total, itens resumidos visíveis por pedido |
| AC-003 | Paginação anterior/próxima funciona | Navegar entre ao menos 2 páginas usando o seed do módulo 001 |
| AC-004 | Botão de atualizar refaz a busca | Criar pedido em outra aba/terminal e confirmar que "atualizar" traz o novo pedido |
| AC-005 | Produtos carregados no formulário | Lista de produtos do `GET /api/products` aparece nas opções |
| AC-006 | Adicionar/remover item no formulário | Adicionar 2+ itens, remover 1, confirmar lista final antes do envio |
| AC-007 | `customerName` opcional | Criar pedido com e sem `customerName` preenchido — ambos funcionam |
| AC-008 | Criação com sucesso limpa formulário e atualiza lista | Após `201`, formulário vazio, mensagem de sucesso, novo pedido na listagem |
| AC-009 | Erro 400 exibido claramente | Tentar enviar payload inválido (ex.: sem itens) exibe mensagem de erro |
| AC-010 | Erro 409 exibido claramente | Criar pedido com quantidade maior que o estoque exibe mensagem de conflito |
| AC-011 | Estado de carregamento visível | Indicador de carregamento perceptível ao abrir a tela ou enviar formulário |
| AC-012 | Lista vazia tratada | Mensagem "nenhum pedido encontrado" em vez de tabela em branco |
| AC-013 | Sem alteração de contrato do backend | `dotnet test` continua **46/46** após a implementação |
| AC-014 | Sem bibliotecas pesadas de estado/UI | `package.json` não inclui Redux/Zustand/React Query/Material UI/Ant Design/Bootstrap |
| AC-015 | Build do frontend passa | `npm run build` sem erros |
| AC-016 | Responsividade mínima | Tela utilizável em viewport mobile padrão (sem quebra grosseira de layout) |

---

## Checks manuais esperados

1. Subir backend: `.\scripts\dev-up.ps1` (porta `5069`).
2. Subir frontend: `cd src/TestOrder.Web && npm install && npm run dev`.
3. Abrir a URL do Vite no navegador — confirmar que a primeira tela já é a tela de pedidos (não uma landing page).
4. Confirmar que a listagem de pedidos carrega com dados do seed (id, data, status, total, itens).
5. Navegar para a próxima página e depois voltar para a anterior.
6. Clicar em "atualizar" e confirmar nova busca.
7. Abrir o formulário de criação, selecionar produto e quantidade, adicionar 2+ itens, remover 1.
8. Enviar pedido válido (com e sem `customerName`) — confirmar `201`, mensagem de sucesso, formulário limpo, listagem atualizada.
9. Tentar enviar pedido sem itens — confirmar bloqueio local ou mensagem de erro `400`.
10. Tentar criar pedido com quantidade absurdamente alta (maior que o estoque) — confirmar mensagem de conflito `409` e que os dados do formulário não se perdem.
11. Redimensionar a janela do navegador para largura mobile e confirmar que a tela permanece utilizável.
12. Rodar `dotnet build TestOrder.slnx && .\scripts\test.ps1` no backend e confirmar que os **46/46** testes continuam passando (nenhuma regressão).
13. Rodar `npm run build` no frontend e confirmar que compila sem erros.
14. Registrar comandos e resultados em `docs/PRESENTATION_GUIDE.md`.

---

## Expectativa de validação (sem suíte de testes automatizados de frontend)

Este módulo **não** exige testes automatizados de frontend (unitários ou E2E) — não fazem parte do escopo mínimo do desafio para esta tela. A validação é:

- **Automatizada (backend)**: suíte existente `dotnet test TestOrder.slnx` deve continuar **46/46**, confirmando que nada no backend foi quebrado.
- **Automatizada (frontend, apenas build)**: `npm run build` deve compilar sem erros — funciona como um "smoke test" de sintaxe/tipos.
- **Manual (frontend, funcional)**: checklist de 14 passos acima, executado e registrado em `docs/PRESENTATION_GUIDE.md`.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Decisões de estrutura de componentes (quantos componentes, onde ficou o helper de HTTP, se houve algum).
- Por que JavaScript simples (sem TypeScript) foi escolhido, se for o caso.
- Como o proxy do Vite evitou mexer em CORS no backend.
- Erros comuns da IA (ex.: sugerir Redux/React Query, sugerir Dockerfile de frontend fora de escopo, esquecer estado de erro 409).
- Resultados da validação manual e do build do frontend.
- Prompts Spec Kit usados neste módulo.

---

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Referência: estrutura de pastas de `src/TestOrder.Web` e principais componentes.
- Roteiro de demo: subir backend + frontend, listar, criar pedido válido, criar pedido com erro 409, navegar paginação.
- Explicar a escolha de não usar Redux/React Query — estado local é suficiente para o escopo.
- Tabela pass/fail dos checks manuais.

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Um usuário consegue visualizar a primeira página de pedidos em menos de 5 segundos após abrir a tela, em ambiente local.
- **SC-002**: Um usuário consegue montar e enviar um pedido válido com 1 a 3 itens em menos de 1 minuto usando apenas o formulário.
- **SC-003**: **100%** das tentativas de criação com estoque insuficiente resultam em mensagem de erro compreensível exibida na tela, sem tela em branco ou travamento.
- **SC-004**: **100%** das navegações de página (anterior/próxima) atualizam a listagem corretamente, sem recarregar a página inteira do navegador.
- **SC-005**: A tela permanece utilizável, sem sobreposição ou quebra grosseira de layout, tanto em resolução desktop quanto em uma largura de viewport mobile padrão (ex.: 375px).

---

## Assumptions

- O backend (`src/TestOrder.Api`) já está rodando localmente em `http://localhost:5069` via `.\scripts\dev-up.ps1` antes de abrir o frontend.
- Não há requisito de autenticação — a tela é de uso interno/demonstração.
- O idioma da interface é Português, consistente com o restante da documentação do projeto.
- O resumo de itens de cada pedido na listagem pode ser montado a partir do próprio `items[]` já retornado por `GET /api/orders` — não é necessário chamar `GET /api/orders/{id}` por pedido.
- `pageSize` padrão da tela segue o padrão já validado do backend (20), sem necessidade de configuração pelo usuário neste módulo.
- JavaScript simples (sem TypeScript) é aceitável e preferido para reduzir configuração e tempo de implementação, conforme indicado no contexto do módulo.
- O proxy do Vite (`vite.config`) resolve o encaminhamento de `/api` para `http://localhost:5069` em desenvolvimento, sem exigir alteração de CORS no backend.
- Duplicidade de produto no formulário: **bloquear** a adição com mensagem inline (não somar quantidade automaticamente) — decisão final research.md R3; o backend continua rejeitando duplicidade com `400` como camada de segurança.

---

## Restrições de arquitetura (contexto do desafio)

Limites obrigatórios da entrega, alinhados a `.cursor/rules/testorder.mdc` e ao contexto passado pelo usuário:

- **React + Vite**, aplicação em `src/TestOrder.Web/`, JavaScript simples (não TypeScript, salvo necessidade trivial).
- **Proxy do Vite** para `/api` → `http://localhost:5069` — sem alterar CORS no backend.
- **Sem alteração do backend** neste módulo, exceto se for estritamente impossível rodar a tela sem isso (a ser justificado no plano, se ocorrer).
- **Sem bibliotecas de estado/dados pesadas**: nada de Redux, Zustand, React Query, CQRS, camada de service genérica.
- **Sem bibliotecas de UI pesadas**: CSS próprio, simples, profissional e responsivo.
- Chamadas HTTP ficam próximas dos componentes ou em um helper local pequeno, apenas se isso reduzir repetição — sem criar uma camada de abstração genérica.
- Sem Dockerfile de frontend, sem deploy, sem autenticação neste módulo.
