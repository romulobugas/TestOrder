# Contrato de UI: Módulo 004 — Tela Web React para Pedidos

Este módulo **não expõe** uma nova API — ele é **consumidor** dos endpoints já existentes e validados nos módulos 001/002. Este documento formaliza (1) o contrato de consumo desses endpoints do ponto de vista da UI e (2) o contrato de comportamento visível da tela (estados, mensagens, transições), já que não há um contrato HTTP novo a especificar.

---

## 1. Endpoints consumidos (contratos existentes, inalterados)

### `GET /api/products`

- **Quando é chamado**: uma vez, ao montar a tela (`useEffect` com array de dependências vazio).
- **Uso**: popular a lista de opções de produto no formulário de criação.
- **Sucesso (200)**: `ProductResponse[]` → armazenado em `products`.
- **Erro**: qualquer status de erro ou falha de rede → `productsError` preenchido; formulário de criação fica com a seleção de produto desabilitada/vazia, mas o restante da tela (listagem) continua funcional.

### `GET /api/orders?page={page}&pageSize=20`

- **Quando é chamado**: ao montar a tela, ao clicar em "anterior"/"próxima", ao clicar em "atualizar", e automaticamente após uma criação de pedido bem-sucedida (com `page` resetado para `1`).
- **Uso**: preencher a tabela de pedidos e os metadados de paginação.
- **Sucesso (200)**: `PagedOrdersResponse` → `orders = body.items`, `pagination = { page: body.page, pageSize: body.pageSize, totalCount: body.totalCount, totalPages: body.totalPages }`.
- **Erro**: qualquer status de erro ou falha de rede → `ordersError` preenchido; tabela exibe mensagem de erro em vez de linhas ou de mensagem de "vazio".

### `POST /api/orders`

- **Quando é chamado**: ao submeter o formulário de criação, apenas se `draftOrder.items.length > 0`.
- **Payload enviado**:
  ```json
  {
    "customerName": "string ou null",
    "items": [
      { "productId": 1, "quantity": 2 }
    ]
  }
  ```
- **Sucesso (201)**:
  - `draftOrder` resetado (`customerName: ""`, `items: []`).
  - `createSuccessMessage` exibido (ex.: `"Pedido #{id} criado com sucesso."`, usando o `id` do `OrderResponse` retornado).
  - `pagination.page` resetado para `1` e `GET /api/orders` disparado novamente.
- **Erro 400** (validação — payload inválido, produto inexistente, produto duplicado, etc.): `createError` preenchido com o texto de `ErrorResponse.error`; `draftOrder` **não** é limpo.
- **Erro 409** (estoque insuficiente): `createError` preenchido com uma mensagem que deixa claro que é um problema de estoque (usa o texto do backend, que já indica isso; a UI pode prefixar com um rótulo visual de "conflito de estoque" para reforçar). `draftOrder` **não** é limpo.
- **Falha de rede / backend indisponível**: `createError` preenchido com mensagem genérica de conexão; `draftOrder` **não** é limpo.

### `GET /api/orders/{id}`

- **Não consumido** por esta tela (ver Assumptions em [spec.md](../spec.md)) — a listagem paginada já traz os itens resumidos de cada pedido.

### `GET /api/revenue/daily`

- **Não consumido** por esta tela — fora de escopo (sem dashboard de faturamento neste módulo).

---

## 2. Contrato de comportamento da UI (estados visíveis)

| Situação | Comportamento esperado |
| --- | --- |
| Tela recém-aberta, produtos e pedidos ainda carregando | Indicadores de carregamento visíveis tanto na seção de formulário (produtos) quanto na seção de listagem (pedidos), independentes um do outro |
| `GET /api/orders` retorna lista vazia (`items: []`) | Seção de listagem exibe mensagem "Nenhum pedido encontrado", não uma tabela em branco |
| `GET /api/products` ou `GET /api/orders` falha | Mensagem de erro visível na seção correspondente, com texto genérico de falha de comunicação; resto da tela permanece utilizável |
| Usuário na página 1 | Ação "anterior" desabilitada ou sem efeito |
| Usuário na última página (`page == totalPages`) | Ação "próxima" desabilitada ou sem efeito |
| Usuário clica em "atualizar" | Nova chamada a `GET /api/orders` com o `page` atual; indicador de carregamento reaparece |
| Usuário tenta adicionar item sem produto selecionado ou quantidade `<= 0` | Item não é adicionado ao rascunho; nenhuma chamada de rede é feita |
| Usuário tenta adicionar produto já presente no rascunho | Item não é duplicado; mensagem inline explica que o produto já foi adicionado (ver R3 em [research.md](../research.md)) |
| Usuário tenta enviar o formulário sem nenhum item | Envio bloqueado no cliente (sem chamar `POST /api/orders`); mensagem local indica que é necessário ao menos um item |
| Envio em andamento (`POST /api/orders` pendente) | Botão de envio desabilitado e sinaliza "enviando", evitando duplo clique/duplo envio |
| Envio retorna `201` | Mensagem de sucesso, formulário limpo, listagem atualizada na página 1 |
| Envio retorna `400` | Mensagem de erro de validação exibida; formulário mantém os dados preenchidos |
| Envio retorna `409` | Mensagem de conflito de estoque exibida; formulário mantém os dados preenchidos |
| Viewport estreita (mobile) | Layout empilha verticalmente formulário e listagem; nenhuma rolagem horizontal indevida |

---

## 3. Fora do contrato desta tela

- Nenhum novo endpoint de backend é criado ou modificado.
- Nenhuma autenticação/autorização é aplicada às chamadas.
- Nenhum estado é persistido entre recarregamentos de página (F5 reinicia tudo — comportamento aceito, não é um requisito tratar persistência local/`localStorage`).
