# API Contracts — Módulo 001

**Base URL**: `/api`  
**Content-Type**: `application/json`  
**Spec**: [spec.md](../spec.md)

## Convenções

- Propriedades JSON em **camelCase**.
- Valores monetários como número decimal JSON (ex.: `19.90`), duas casas na serialização.
- Datas em **ISO 8601** UTC (ex.: `2026-03-15T14:30:00Z`).
- Sem autenticação neste módulo.

---

## GET /api/products

Lista todos os produtos do catálogo (sem paginação neste módulo).

### Response 200

```json
[
  {
    "id": 1,
    "name": "Teclado Mecânico",
    "unitPrice": 349.90
  }
]
```

| Campo | Tipo | Obrigatório |
| --- | --- | --- |
| `id` | integer | sim |
| `name` | string | sim |
| `unitPrice` | number | sim |

> `stockQuantity` **não** é exposto neste módulo (uso interno / módulo 002).

---

## GET /api/orders

Lista pedidos paginados com itens aninhados e total.

### Query parameters

| Parâmetro | Tipo | Padrão | Regras |
| --- | --- | --- | --- |
| `page` | integer | `1` | ≥ 1 |
| `pageSize` | integer | `20` | 1–100 |

### Response 200

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 5000,
  "totalPages": 250,
  "items": [
    {
      "id": 5000,
      "createdAt": "2026-07-01T18:22:11Z",
      "status": "created",
      "total": 127.50,
      "items": [
        {
          "productId": 12,
          "productName": "Mouse Sem Fio",
          "quantity": 2,
          "unitPrice": 45.00
        },
        {
          "productId": 3,
          "productName": "Pad para Mouse",
          "quantity": 1,
          "unitPrice": 37.50
        }
      ]
    }
  ]
}
```

| Campo | Tipo | Obrigatório |
| --- | --- | --- |
| `page` | integer | sim |
| `pageSize` | integer | sim |
| `totalCount` | integer | sim |
| `totalPages` | integer | sim |
| `items` | array | sim |
| `items[].id` | integer (long) | sim |
| `items[].createdAt` | string (date-time) | sim |
| `items[].status` | string | sim |
| `items[].total` | number | sim |
| `items[].items` | array | sim |
| `items[].items[].productId` | integer | sim |
| `items[].items[].productName` | string | sim |
| `items[].items[].quantity` | integer | sim |
| `items[].items[].unitPrice` | number | sim |

**Ordenação**: `items` (pedidos) por `createdAt` decrescente.

**Página além do fim**: `items: []`, `totalCount` inalterado, `page` reflete o solicitado.

### Response 400 — parâmetros inválidos

```json
{
  "error": "page must be greater than or equal to 1."
}
```

Mensagens esperadas (exemplos):
- `page must be greater than or equal to 1.`
- `pageSize must be between 1 and 100.`

---

## GET /api/orders/{id}

Retorna um pedido com itens e total.

### Path parameters

| Parâmetro | Tipo |
| --- | --- |
| `id` | long |

### Response 200

Mesmo objeto de pedido usado em `items[]` da listagem:

```json
{
  "id": 42,
  "createdAt": "2026-01-10T09:15:00Z",
  "status": "processed",
  "total": 89.80,
  "items": [
    {
      "productId": 5,
      "productName": "Hub USB-C",
      "quantity": 1,
      "unitPrice": 89.80
    }
  ]
}
```

### Response 404

```json
{
  "error": "Order not found."
}
```

---

## Erros gerais

| Status | Quando |
| --- | --- |
| 400 | Validação de query/path |
| 404 | Pedido inexistente |
| 500 | Falha de banco ou erro não tratado |

Neste módulo não há envelope de erro padronizado além do campo `error` string.
