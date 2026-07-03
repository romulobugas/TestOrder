# API Contracts — Módulo 002

**Base URL**: `/api`  
**Content-Type**: `application/json`  
**Spec**: [spec.md](../spec.md) | **Módulo 001**: [../../001-base-listagem-pedidos/contracts/api.md](../../001-base-listagem-pedidos/contracts/api.md)

## Convenções (herdadas do módulo 001)

- Propriedades JSON em **camelCase**.
- Valores monetários como número decimal JSON.
- Datas em **ISO 8601** UTC com sufixo **`Z`**.
- Sem autenticação.
- Erros: `{ "error": "mensagem" }`.

---

## Endpoints preservados (módulo 001)

Sem alteração de contrato:

- `GET /api/products`
- `GET /api/orders`
- `GET /api/orders/{id}`

Ver contrato completo em [módulo 001](../../001-base-listagem-pedidos/contracts/api.md).

---

## POST /api/orders

Cria pedido, reserva unidades de estoque e registra evento outbox `pending` em transação única.

### Request body

```json
{
  "customerName": "Cliente opcional",
  "items": [
    { "productId": 1, "quantity": 2 },
    { "productId": 3, "quantity": 1 }
  ]
}
```

| Campo | Tipo | Obrigatório | Regras |
| --- | --- | --- | --- |
| `customerName` | string | não | Omitido, null ou vazio → persistido como `null`; não bloqueia criação |
| `items` | array | sim | ≥ 1 item |
| `items[].productId` | integer | sim | > 0, deve existir em `products` |
| `items[].quantity` | integer | sim | > 0 |

**Regras de validação (400)**:
- `items` ausente, null ou array vazio
- `productId` ≤ 0 ou inválido
- `quantity` ≤ 0 ou não inteiro
- **Produto duplicado** no mesmo payload (mesmo `productId` em duas entradas)
- Produto inexistente no catálogo
- JSON malformado / corpo ausente

**Regra de estoque (409)**:
- Soma de unidades `available` em `inventory_units` para algum produto < `quantity` solicitada

### Response 201 Created

Corpo: mesmo objeto **`Order`** do módulo 001:

```json
{
  "id": 5001,
  "createdAt": "2026-07-03T15:30:00Z",
  "status": "created",
  "total": 127.50,
  "items": [
    {
      "productId": 1,
      "productName": "Produto 01",
      "quantity": 2,
      "unitPrice": 45.00
    },
    {
      "productId": 3,
      "productName": "Produto 03",
      "quantity": 1,
      "unitPrice": 37.50
    }
  ]
}
```

| Campo | Tipo | Obrigatório |
| --- | --- | --- |
| `id` | integer (long) | sim |
| `createdAt` | string (date-time UTC `Z`) | sim |
| `status` | string | sim (`created`) |
| `total` | number | sim (= Σ quantity × unitPrice) |
| `items` | array | sim |
| `items[].productId` | integer | sim |
| `items[].productName` | string | sim |
| `items[].quantity` | integer | sim |
| `items[].unitPrice` | number | sim (snapshot do catálogo no momento da criação) |

**Headers recomendados**:
- `Location: /api/orders/{id}`

**Efeitos colaterais (mesma transação)**:
- Linhas em `orders`, `order_items`
- Unidades em `inventory_units` → `reserved`
- Linhas em `order_reservation_units`
- Linha em `order_processing_events` (`eventType`: `OrderCreated`, `status`: `pending`)

> `customerName` **não** é exposto na resposta neste módulo (persistido apenas no banco). Pode ser adicionado em módulo futuro se necessário.

### Response 400 — validação

```json
{
  "error": "items must contain at least one entry."
}
```

Exemplos de mensagens (implementação pode variar texto, semântica fixa):

| Situação | Exemplo de mensagem |
| --- | --- |
| Itens vazios | `items must contain at least one entry.` |
| Quantidade inválida | `quantity must be greater than 0.` |
| Produto duplicado | `duplicate productId in items.` |
| Produto inexistente | `product not found: {id}.` |

### Response 409 — estoque insuficiente

```json
{
  "error": "insufficient inventory for product 1."
}
```

- Nenhum pedido, reserva parcial ou evento outbox persistido.
- Contagens de `inventory_units` com `status = 'available'` inalteradas em relação ao estado pré-requisição.

---

## Erros gerais (módulo 002)

| Status | Quando |
| --- | --- |
| 201 | Pedido criado com sucesso |
| 400 | Payload ou regra de validação |
| 409 | Estoque/reserva insuficiente |
| 500 | Falha de banco ou erro não tratado |

---

## Outbox (referência interna, não exposta via HTTP neste módulo)

Registro criado em `order_processing_events`:

| Campo DB | Valor exemplo |
| --- | --- |
| `event_type` | `OrderCreated` |
| `status` | `pending` |
| `payload` | `{"orderId":5001}` |

Consumo pelo microserviço Node: **módulo futuro**.
