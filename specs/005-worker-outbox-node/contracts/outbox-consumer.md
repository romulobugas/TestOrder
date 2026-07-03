# Contrato do consumidor de outbox: Módulo 005 — Worker Node

Este módulo **não expõe HTTP**. O contrato é entre o **produtor** (backend .NET, módulo 002) e o **consumidor** (worker Node, módulo 005), mediado exclusivamente pela tabela `order_processing_events`.

Detalhes de schema e transições: [data-model.md](../data-model.md). SQL e decisões: [research.md](../research.md).

---

## 1. Produtor (backend .NET — inalterado neste módulo)

### Quando grava

Dentro da mesma transação de `POST /api/orders` bem-sucedido (201).

### INSERT esperado

| Campo | Valor |
| --- | --- |
| `order_id` | ID do pedido recém-criado |
| `event_type` | `OrderCreated` |
| `status` | `pending` |
| `payload` | JSON `{"orderId": <order_id>}` |
| `created_at` | UTC, momento da criação |

Referência de implementação: `src/TestOrder.Api/Controllers/CreateOrderCommands.cs`.

---

## 2. Consumidor (worker Node)

### Pré-condições

- MySQL 8 acessível com credenciais de `docker-compose.yml` (ou env overrides).
- Tabela `order_processing_events` existente (migration módulo 002).
- Worker rodando em loop de polling (via `dev-up.ps1` ou `node index.js` manual).

### Seleção de eventos (claim)

```sql
SELECT id, order_id, event_type, payload, created_at
FROM order_processing_events
WHERE status = 'pending'
  AND event_type = 'OrderCreated'
ORDER BY created_at ASC, id ASC
LIMIT ?
FOR UPDATE SKIP LOCKED;
```

- Executado **dentro de transação** READ COMMITTED.
- `LIMIT` = `BATCH_SIZE` (default 10).
- Eventos de outros `event_type` **nunca** são selecionados.

**Ignorar outro `event_type`**: significa **não incluir** a linha no claim do polling — o worker não falha, não emite log de erro e não altera essas linhas. Elas podem permanecer `pending` para sempre se inseridas fora do contrato (ex.: teste manual). Dead-letter, alerta ou fila de erro dedicada estão **fora de escopo** deste módulo.

### Processamento (side effect)

Para cada linha retornada pelo claim:

1. Parsear `payload` como JSON.
2. Se parse falhar: log de erro com `eventId`/`orderId`; prosseguir (ver R6 em research.md).
3. Se parse OK: emitir log estruturado JSON no stdout, contendo no mínimo:
   - `eventId` (coluna `id`)
   - `orderId` (de `payload.orderId` e/ou `order_id`)
   - `eventType` (`OrderCreated`)
   - timestamp de processamento (ISO 8601 UTC)

Nenhuma chamada HTTP, fila externa ou e-mail.

### Marcação como processado (commit)

```sql
UPDATE order_processing_events
SET status = 'processed'
WHERE id = ?
  AND status = 'pending';
```

- Executado **na mesma transação** do SELECT (antes do COMMIT).
- Se `affectedRows = 0` para um id: evento já foi processado por outra instância — ignorar silenciosamente (idempotência).
- Após COMMIT bem-sucedido, o evento **não** deve voltar a `pending`.

### Ciclo e concorrência

| Aspecto | Comportamento |
| --- | --- |
| Intervalo entre ciclos | `POLL_INTERVAL_MS` (default 2000 ms) |
| Fila vazia | Aguardar próximo ciclo; sem log de erro |
| MySQL indisponível | Log de erro; aguardar próximo ciclo; processo continua |
| Múltiplas instâncias | Seguras via SKIP LOCKED + UPDATE condicional |
| Shutdown (`SIGINT`) | Concluir ciclo em andamento; fechar pool; sair sem stack trace |

---

## 3. Garantias e não-garantias

### Garantias (dentro do escopo)

| Garantia | Mecanismo |
| --- | --- |
| Cada evento `OrderCreated` pending é processado **no máximo uma vez** com efeito de log | SKIP LOCKED + UPDATE condicional |
| Eventos processados não são reprocessados em condições normais | Filtro `status = 'pending'` no SELECT |
| Ordem FIFO entre eventos pendentes | `ORDER BY created_at ASC, id ASC` |
| Desacoplamento produtor/consumidor | Apenas tabela MySQL; sem HTTP entre processos |

### Não-garantias (fora de escopo)

| Item | Nota |
| --- | --- |
| Entrega "exactly-once" end-to-end com side effects externos | Side effect é só log; sem integração externa real |
| Retry com backoff após falha de processamento | Apenas repetição no próximo ciclo se status permanecer `pending` |
| Dead-letter para payload inválido | Payload inválido é logado e marcado `processed` (R6) |
| Latência sub-segundo | Polling de 2s; aceitável para demo (SC-001: até 10s) |
| Ordem estrita global entre instâncias paralelas | SKIP LOCKED distribui trabalho; ordem global não é requisito |

---

## 4. Validação do contrato (checks rápidos)

| Check | Comando / ação | Resultado esperado |
| --- | --- | --- |
| Evento criado após POST/UI | `SELECT status FROM order_processing_events WHERE order_id = ?` | Inicia `pending`, depois `processed` |
| Log do worker | Observar janela `TestOrder - Worker` | JSON com `orderId` matching |
| Sem duplicidade (2 workers) | Duas instâncias + 3 pedidos novos | 3 linhas `processed`; cada `id` processado uma vez nos logs |
| Backend intacto | `.\scripts\test.ps1` | 46/46 |

Checklist completo: [quickstart.md](../quickstart.md).
