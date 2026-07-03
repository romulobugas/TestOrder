# Quickstart — Módulo 002

**Objetivo**: Validar `POST /api/orders`, reserva concorrente e outbox após evolução do schema.  
**Contratos**: [contracts/api.md](./contracts/api.md) | **Modelo**: [data-model.md](./data-model.md)

**Status**: implementado e validado — suíte **32/32** testes.

## Pré-requisitos

- Módulo 001 operacional (Docker, `dev-up.ps1`, listagens GET funcionando)
- .NET 10 SDK + Docker
- Migration **`20260703184137_AddInventoryAndOutbox`**
- **Primeira execução após migration 002**: backfill de `inventory_units` pode demorar (~**237k** linhas em dev)

```powershell
# Parar API antes de build/test no Windows (evita lock do .exe)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Subir ambiente

```powershell
.\scripts\dev-up.ps1
```

Aguardar logs de migration + backfill de unidades. API em **`http://localhost:5069`**.

### Confirmar backfill (opcional)

```sql
SELECT status, COUNT(*) FROM inventory_units GROUP BY status;
-- dev validado: ~237858 available após primeira subida (varia levemente se houver POSTs de teste)
```

Segunda subida da API **não duplica** unidades (guard idempotente em `InventoryUnitsBackfill`).

## Validar POST — sucesso (201)

```powershell
curl -s -w "`nHTTP:%{http_code}`n" -X POST http://localhost:5069/api/orders `
  -H "Content-Type: application/json" `
  -d '{"customerName":"Demo","items":[{"productId":1,"quantity":2}]}'
```

**Esperado**: HTTP **201**, corpo com `id`, `createdAt` terminando em `Z`, `items`, `total` coerente, header `Location: /api/orders/{id}`.

Confirmar leitura (substituir `{id}` pelo retornado):

```powershell
curl -s -w "`nHTTP:%{http_code}`n" http://localhost:5069/api/orders/{id}
```

**Esperado**: HTTP **200**, mesmo shape do módulo 001.

## Validar validação (400)

```powershell
# Itens vazios
curl -s -w "`nHTTP:%{http_code}`n" -X POST http://localhost:5069/api/orders `
  -H "Content-Type: application/json" `
  -d '{"items":[]}'

# Produto duplicado
curl -s -w "`nHTTP:%{http_code}`n" -X POST http://localhost:5069/api/orders `
  -H "Content-Type: application/json" `
  -d '{"items":[{"productId":1,"quantity":1},{"productId":1,"quantity":2}]}'

# Quantidade inválida
curl -s -w "`nHTTP:%{http_code}`n" -X POST http://localhost:5069/api/orders `
  -H "Content-Type: application/json" `
  -d '{"items":[{"productId":1,"quantity":0}]}'
```

**Esperado**: HTTP **400** + `"error"`.

## Validar estoque insuficiente (409)

Quantidade maior que unidades `available` (ex.: valor absurdamente alto):

```powershell
curl -s -w "`nHTTP:%{http_code}`n" -X POST http://localhost:5069/api/orders `
  -H "Content-Type: application/json" `
  -d '{"items":[{"productId":1,"quantity":999999999}]}'
```

**Esperado**: HTTP **409** + `"error"`. Nenhum pedido, reserva parcial ou outbox persistido.

## SQL útil para apresentação

Substituir `{ORDER_ID}` pelo id do POST 201:

```sql
-- Visão geral do inventário
SELECT status, COUNT(*) FROM inventory_units GROUP BY status;

-- Reservas do pedido (= soma das quantities)
SELECT COUNT(*) FROM order_reservation_units WHERE order_id = {ORDER_ID};

-- Outbox pending
SELECT event_type, status, payload
FROM order_processing_events
WHERE order_id = {ORDER_ID};
-- event_type = 'OrderCreated', status = 'pending', payload contém orderId
```

## Regressão módulo 001

```powershell
curl -s http://localhost:5069/api/products
curl -s "http://localhost:5069/api/orders?page=1&pageSize=5"
curl -s http://localhost:5069/api/orders/1
```

**Esperado**: HTTP 200 como antes.

## Testes automatizados

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

**Esperado**: **32/32** testes passando (17 módulo 001 + 15 módulo 002 em `CreateOrderEndpointTests.cs`).

| Teste | Filtro sugerido |
| --- | --- |
| `CreateOrder_Success_Returns201AndPersistsOrder` | `CreateOrder_Success` |
| `CreateOrder_InvalidPayload_*` | `InvalidPayload` |
| `CreateOrder_DuplicateProduct_Returns400` | `DuplicateProduct` |
| `CreateOrder_InsufficientStock_Returns409` | `InsufficientStock` |
| `CreateOrder_WritesPendingOutboxEvent` | `Outbox` |
| `CreateOrder_ConcurrentRequests_DoNotOverbook` | `Concurrent` |
| `Regression_Module001_ReadEndpointsStillWork` | `Regression` |

### Concorrência (automatizado)

`CreateOrder_ConcurrentRequests_DoNotOverbook`: produto com **5** unidades, **10** POSTs paralelos qty=1 → **5×201 + 5×409**, zero overbooking.

## Demo de concorrência (manual, opcional)

1. Identificar produto com poucas unidades disponíveis (SQL ou POST repetido até 409).
2. Abrir duas janelas e enviar POST simultâneo para a última unidade.
3. **Esperado**: uma resposta 201, outra 409; nunca duas 201 para a mesma unidade.

## Parar ambiente

- API: Ctrl+C no terminal do `dev-up.ps1`
- MySQL: `docker compose down` ou `docker compose down -v`

## Documentação pós-implementação

Atualizado em T022–T024:

- `AI_NOTES.md` — decisões de reserva, backfill, concorrência, uso de IA
- `docs/PRESENTATION_GUIDE.md` — roteiro demo POST + SQL SKIP LOCKED + outbox
