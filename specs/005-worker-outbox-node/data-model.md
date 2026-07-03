# Data Model — Módulo 005

**Data**: 2026-07-03  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Este módulo **não cria nem altera tabelas**. Documenta o modelo existente (`order_processing_events`, criado no módulo 002) do ponto de vista do **consumidor Node** e as transições de estado que o worker passa a realizar.

Diagrama (inalterado desde módulo 002):

```text
orders (1) ──< order_processing_events   [outbox]
                      │
                      │ escrita pelo .NET (POST /api/orders)
                      │ lida/atualizada pelo worker Node (polling)
                      v
                 status: pending → processed
```

---

## Tabela reutilizada: `order_processing_events`

| Coluna | Tipo MySQL | Nulo | Escrita por | Lida/atualizada por worker |
| --- | --- | --- | --- | --- |
| `id` | `BIGINT` AUTO_INCREMENT | PK | .NET (INSERT) | SELECT, UPDATE WHERE id |
| `order_id` | `BIGINT` | NOT NULL, FK → `orders.id` | .NET | SELECT (correlação/log) |
| `event_type` | `VARCHAR(64)` | NOT NULL | .NET (`OrderCreated`) | Filtro WHERE |
| `status` | `VARCHAR(16)` | NOT NULL | .NET (`pending`) | Filtro WHERE + UPDATE → `processed` |
| `payload` | `JSON` | NOT NULL | .NET (`{"orderId":N}`) | SELECT + JSON.parse |
| `created_at` | `DATETIME(6)` | NOT NULL | .NET (UTC) | ORDER BY (FIFO) |

**Índices existentes (módulo 002 — não alterar)**:

| Nome | Colunas | Uso pelo worker |
| --- | --- | --- |
| `IX_order_processing_events_status_created` | `(status, created_at)` | Filtro `status='pending'` + ordenação FIFO |
| `IX_order_processing_events_order_id` | `order_id` | Consultas manuais de demo/validação |

---

## Valores de domínio

### `event_type`

| Valor | Emitido por backend? | Tratamento do worker |
| --- | --- | --- |
| `OrderCreated` | Sim (módulo 002) | Processar (claim + log + `processed`) |
| *(outros)* | Não (hoje) | **Ignorar** = não selecionar no polling (`WHERE event_type = 'OrderCreated'`) |

**Nota sobre “ignorar” outros tipos**: o worker **não** falha nem loga erro por linhas que nunca entram no SELECT. Se alguém inserir manualmente um evento com outro `event_type`, ele pode permanecer `pending` indefinidamente — isso é aceitável neste módulo. Dead-letter, alerta operacional ou reprocessamento de tipos desconhecidos estão **fora de escopo**.

### `status`

| Valor | Quem define | Significado |
| --- | --- | --- |
| `pending` | Backend .NET ao criar pedido | Aguardando processamento pelo worker |
| `processed` | Worker Node após processamento simulado | Evento consumido; não deve ser reprocessado |

**Transição permitida neste módulo**:

```text
pending ──(worker, UPDATE condicional)──> processed
```

Não há transição reversa. Não há status `failed`, `processing` ou dead-letter.

---

## Payload JSON (contrato mínimo)

Escrito pelo backend em `CreateOrderCommands.cs`:

```json
{ "orderId": 42 }
```

| Campo | Tipo | Obrigatório | Uso do worker |
| --- | --- | --- | --- |
| `orderId` | number | Sim (implícito pelo backend) | Log estruturado; validação opcional vs. `order_id` da linha |

Se `orderId` ausente ou JSON inválido: ver [research.md R6](./research.md) — log de erro, marcar `processed` para evitar loop infinito.

---

## Regras de concorrência (worker)

| Regra | Mecanismo |
| --- | --- |
| Apenas uma instância processa um evento por vez | `SELECT ... FOR UPDATE SKIP LOCKED` |
| Idempotência na marcação final | `UPDATE ... WHERE id = ? AND status = 'pending'` |
| Ordem de processamento | `ORDER BY created_at ASC, id ASC` (FIFO) |
| Múltiplos eventos por ciclo | `LIMIT ?` (batch size, default 10) |
| Isolamento | READ COMMITTED (padrão InnoDB; transação curta) |

---

## Entidades EF / C# — sem alteração

A entidade `OrderProcessingEvent` em `src/TestOrder.Api/Data/Entities/OrderProcessingEvent.cs` permanece a fonte de schema EF; **nenhuma migration nova** neste módulo.

---

## Variáveis de ambiente (configuração do worker — não schema)

| Variável | Default | Descrição |
| --- | --- | --- |
| `MYSQL_HOST` | `localhost` | Host do MySQL (docker-compose) |
| `MYSQL_PORT` | `3306` | Porta |
| `MYSQL_DATABASE` | `testorder` | Database |
| `MYSQL_USER` | `testorder` | Usuário |
| `MYSQL_PASSWORD` | `testorder` | Senha |
| `POLL_INTERVAL_MS` | `2000` | Intervalo entre ciclos de polling |
| `BATCH_SIZE` | `10` | Máximo de eventos por transação |

Compatíveis com `docker-compose.yml` e credenciais de dev existentes.

---

## Consultas SQL úteis para validação manual

```sql
-- Ultimos eventos e status
SELECT id, order_id, event_type, status, created_at
FROM order_processing_events
ORDER BY id DESC
LIMIT 10;

-- Contagem por status
SELECT status, COUNT(*) AS cnt
FROM order_processing_events
GROUP BY status;

-- Eventos ainda pendentes (deve ir a zero apos worker processar)
SELECT COUNT(*) FROM order_processing_events
WHERE status = 'pending' AND event_type = 'OrderCreated';
```
