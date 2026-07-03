# Data Model: Módulo 004 — Tela Web React para Pedidos

**Input**: [spec.md](./spec.md) Key Entities | [research.md](./research.md)

Nenhuma entidade de backend é criada ou alterada neste módulo. Este documento descreve exclusivamente o **modelo de estado do frontend** (formas de dados em memória no React) e como ele deriva dos contratos JSON já existentes do backend.

---

## Entidades vindas do backend (somente leitura, sem transformação de schema)

### `Product`

Vem de `GET /api/products` (`ProductResponse[]`, já existente — módulo 001).

| Campo | Tipo | Origem |
| --- | --- | --- |
| `id` | number | `ProductResponse.Id` |
| `name` | string | `ProductResponse.Name` |
| `unitPrice` | number | `ProductResponse.UnitPrice` |

### `Order`

Vem de `GET /api/orders` (dentro de `PagedOrdersResponse.items`, `OrderResponse[]` — módulos 001/002).

| Campo | Tipo | Origem |
| --- | --- | --- |
| `id` | number | `OrderResponse.Id` |
| `createdAt` | string (ISO 8601 UTC, sufixo `Z`) | `OrderResponse.CreatedAt` |
| `status` | string | `OrderResponse.Status` (ex.: `"created"`) |
| `total` | number | `OrderResponse.Total` |
| `items` | `OrderItem[]` | `OrderResponse.Items` |

### `OrderItem` (somente leitura, dentro de `Order.items`)

| Campo | Tipo | Origem |
| --- | --- | --- |
| `productId` | number | `OrderItemResponse.ProductId` |
| `productName` | string | `OrderItemResponse.ProductName` |
| `quantity` | number | `OrderItemResponse.Quantity` |
| `unitPrice` | number | `OrderItemResponse.UnitPrice` |

### `PagedOrders` (envelope de paginação)

| Campo | Tipo | Origem |
| --- | --- | --- |
| `page` | number | `PagedOrdersResponse.Page` |
| `pageSize` | number | `PagedOrdersResponse.PageSize` (sempre `20` nesta tela) |
| `totalCount` | number | `PagedOrdersResponse.TotalCount` |
| `totalPages` | number | `PagedOrdersResponse.TotalPages` |
| `items` | `Order[]` | `PagedOrdersResponse.Items` |

### `ApiError`

| Campo | Tipo | Origem |
| --- | --- | --- |
| `error` | string | `ErrorResponse.Error` (corpo de `400`/`409`) |

---

## Estado local do formulário de criação (não persistido, existe só em memória do React)

### `DraftOrderItem`

Item ainda não enviado, exibido na lista de itens do pedido em construção.

| Campo | Tipo | Regra |
| --- | --- | --- |
| `productId` | number | Deve existir na lista de `Product` carregada |
| `productName` | string | Copiado do `Product` selecionado, só para exibição (evita novo lookup ao renderizar) |
| `unitPrice` | number | Copiado do `Product` selecionado, só para exibição (subtotal estimado no rascunho) |
| `quantity` | number | Inteiro `> 0`; não é adicionável ao rascunho se inválido |

### `DraftOrder`

Estado completo do formulário antes do envio.

| Campo | Tipo | Regra |
| --- | --- | --- |
| `customerName` | string | Opcional; enviado como está (backend já normaliza vazio/whitespace para `null`) |
| `items` | `DraftOrderItem[]` | Não pode haver dois itens com o mesmo `productId` (R3 em [research.md](./research.md) — bloqueado na UI antes de adicionar) |

Ao enviar, `DraftOrder` é convertido para o payload de `POST /api/orders`:

```json
{
  "customerName": "string ou null",
  "items": [
    { "productId": 1, "quantity": 2 }
  ]
}
```

(`unitPrice`/`productName` do rascunho **não** são enviados — são apenas auxiliares de exibição; o backend calcula o preço a partir do produto no momento da transação.)

---

## Estados de UI (derivados, não persistidos)

Modelados como variáveis de estado simples (`useState`) em `App.jsx`, sem máquina de estados formal — cada uma é independente:

| Estado | Tipo | Uso |
| --- | --- | --- |
| `products` | `Product[]` | Carregado uma vez ao montar a tela |
| `loadingProducts` | boolean | `true` enquanto `GET /api/products` está pendente |
| `productsError` | string \| null | Mensagem de erro se o carregamento de produtos falhar |
| `orders` | `Order[]` | Itens da página atual |
| `pagination` | `{ page, pageSize, totalCount, totalPages }` | Metadados da página atual |
| `loadingOrders` | boolean | `true` enquanto `GET /api/orders` está pendente |
| `ordersError` | string \| null | Mensagem de erro se a listagem falhar |
| `draftOrder` | `DraftOrder` | Estado do formulário em construção |
| `creating` | boolean | `true` enquanto `POST /api/orders` está pendente (desabilita o botão de envio) |
| `createError` | string \| null | Mensagem de erro da última tentativa de criação (`400`/`409`/rede) |
| `createSuccessMessage` | string \| null | Mensagem transitória de sucesso após `201` |

### Transições relevantes (informais, não é uma FSM formal)

- `loadingOrders: true` → requisição de `GET /api/orders` retorna → `loadingOrders: false` + (`orders`/`pagination` atualizados) **ou** (`ordersError` preenchido).
- Envio do formulário → `creating: true`, `createError: null` → resposta:
  - `201` → `creating: false`, `draftOrder` resetado, `createSuccessMessage` preenchido, `pagination.page` resetado para `1`, nova busca de `GET /api/orders` disparada (ver R4 em research.md).
  - `400`/`409`/erro de rede → `creating: false`, `createError` preenchido com a mensagem do backend (ou mensagem genérica), `draftOrder` **preservado** (não é limpo).
