# Research — Módulo 005

**Data**: 2026-07-03  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## R1 — Driver MySQL para Node

**Decision**: `mysql2` (pacote npm `mysql2`, API `mysql2/promise`) com pool de conexões (`createPool`).

**Rationale**: Biblioteca leve, amplamente usada, suporta transações explícitas (`getConnection` → `beginTransaction` → `commit`/`rollback`), prepared statements e SQL raw — alinhada ao padrão do backend (Dapper + SQL próximo ao código). Sem ORM, sem query builder.

**Alternatives considered**:
- *`mysql` (pacote legado)*: menos mantido; `mysql2` é o substituto natural.
- *Prisma/TypeORM/Sequelize*: violam NFR-001 (camadas desnecessárias) e adicionam TypeScript/cerimônia.
- *Chamar a API .NET via HTTP*: viola FR-011 — comunicação só via tabela.

---

## R2 — Estrutura de arquivos do worker

**Decision**: Três arquivos JavaScript em `src/TestOrder.OrderProcessor/`:

| Arquivo | Responsabilidade |
| --- | --- |
| `config.js` | Ler variáveis de ambiente com defaults (`MYSQL_*`, `POLL_INTERVAL_MS`, `BATCH_SIZE`) |
| `worker.js` | SQL de claim/processamento + função `processPendingEvents(pool)` |
| `index.js` | Bootstrap do pool, loop de polling, handler `SIGINT`, encerramento limpo |

**Rationale**: Separa configuração, lógica de banco/processamento e ciclo de vida do processo sem criar classes, interfaces ou pastas `services/`/`repositories/`. SQL fica em `worker.js`, espelhando `CreateOrderCommands.cs` no backend.

**Alternatives considered**:
- *Arquivo único `index.js`*: aceitável, mas mistura shutdown/polling com SQL longo — rejeitado por legibilidade.
- *Múltiplas pastas (`src/db`, `src/worker`, `src/lib`)*: cerimônia desnecessária para ~150 linhas totais.

---

## R3 — Ciclo de polling e tamanho de lote

**Decision**:
- **Intervalo de polling**: `2000 ms` (`POLL_INTERVAL_MS`, constante em `config.js`; override opcional via env).
- **Tamanho de lote**: `10` eventos por ciclo (`BATCH_SIZE`).

**Rationale**: 2s garante SC-001 (evento `processed` em até 10s após criação do pedido) com margem confortável, sem CPU perceptível quando a fila está vazia. Lote de 10 esvazia rajadas modestas de pedidos criados pela UI sem transações longas.

**Alternatives considered**:
- *Polling de 500ms*: mais responsivo, mas log/CPU desnecessários em demo com fila vazia.
- *Lote de 1*: mais simples de explicar, mas lento em rajadas; 10 ainda é transação curta.

---

## R4 — SQL de claim com `FOR UPDATE SKIP LOCKED`

**Decision**: Um ciclo = uma transação READ COMMITTED (padrão InnoDB):

```sql
-- 1) Reservar eventos pendentes (mais antigos primeiro)
SELECT id, order_id, event_type, payload, created_at
FROM order_processing_events
WHERE status = 'pending' AND event_type = 'OrderCreated'
ORDER BY created_at ASC, id ASC
LIMIT ?
FOR UPDATE SKIP LOCKED;

-- 2) Para cada linha retornada, após processamento simulado:
UPDATE order_processing_events
SET status = 'processed'
WHERE id = ? AND status = 'pending';
```

Comentário no código **somente** no bloco acima (comportamento de concorrência/idempotência).

**Rationale**: Mesmo padrão do módulo 002 (`inventory_units` + SKIP LOCKED). O índice existente `IX_order_processing_events_status_created (status, created_at)` cobre o filtro/ordenação. A cláusula `WHERE status = 'pending'` na atualização garante idempotência se duas instâncias disputarem residualmente.

**Alternatives considered**:
- *UPDATE ... LIMIT com subquery*: mais frágil; SELECT FOR UPDATE SKIP LOCKED é o padrão já documentado no projeto.
- *Marcar como `processing` intermediário*: exigiria novo status e possivelmente coluna extra — rejeitado (schema mínimo).

---

## R5 — Schema: sem alterações

**Decision**: **Não** adicionar colunas (`processed_at`, `error_message`, etc.) nem migrations EF neste módulo.

**Rationale**: A transição `pending → processed` com UPDATE condicional, dentro da mesma transação do SKIP LOCKED, é suficiente para idempotência e demo. Observabilidade de "quando processou" vem do log estruturado no console (timestamp no JSON de log). Retry/DLQ/auditoria de erro persistida estão fora de escopo.

**Alternatives considered**:
- *Adicionar `processed_at`*: útil operacionalmente, mas a spec pede preferência pelo schema atual; rejeitado salvo necessidade futura.
- *Status `failed` para payload inválido*: sem DLQ/retry sofisticado, não agrega valor proporcional.

---

## R6 — Payload malformado

**Decision**: Se `JSON.parse(payload)` falhar, logar erro estruturado com `eventId`/`orderId`, **marcar o evento como `processed` na mesma transação** e seguir para o próximo evento do lote.

**Rationale**: Sem dead-letter queue nem status `failed`, deixar `pending` causaria loop infinito. Marcar `processed` após log de erro evita travar o worker e documenta o problema no console — trade-off aceito dentro do escopo mínimo.

**Alternatives considered**:
- *Deixar `pending` e pular*: reprocessamento infinito a cada 2s — pior para demo.
- *Inserir em tabela de erro*: fora de escopo.

---

## R7 — Processamento simulado (side effect)

**Decision**: Após parse bem-sucedido do payload, emitir **uma linha JSON** no stdout:

```json
{"level":"info","action":"order-created-processed","eventId":123,"orderId":456,"eventType":"OrderCreated","processedAt":"2026-07-03T22:00:00.000Z"}
```

Sem chamada HTTP, e-mail ou fila externa.

**Rationale**: Atende FR-007 e permite correlacionar pedido criado na UI com log do worker durante a demo.

---

## R8 — Shutdown limpo (`SIGINT`)

**Decision**:
- Flag `shuttingDown` setada em `process.on('SIGINT', ...)`.
- Loop principal verifica a flag antes de iniciar novo ciclo; se um ciclo está em andamento, aguarda conclusão (commit/rollback) antes de `pool.end()`.
- Não chamar `process.exit()` imediatamente no handler — deixar o fluxo async terminar.

**Rationale**: Evita transação abortada mid-flight e conexões penduradas no pool (NFR-009, AC-009).

---

## R9 — Testes automatizados do worker

**Decision**: **Não** adicionar suíte automatizada de testes do worker neste módulo. Validação via checklist manual reproduzível em [quickstart.md](./quickstart.md). Regressão obrigatória: backend **46/46** inalterado.

**Rationale**: Teste de integração Node + MySQL (Testcontainers ou container manual) exigiria dependências extras (`vitest`/`jest`, script de CI, fixture SQL) com custo desproporcional ao escopo do desafio. A spec autoriza explicitamente validação manual (NFR-007). O valor da demo está no fluxo ponta a ponta observável (UI → outbox → log do worker → status no banco).

**Alternatives considered**:
- *Script Node ad-hoc de smoke test*: possível pós-MVP, mas não obrigatório neste plano.
- *Testcontainers Node*: alinhado ao backend, porém alto custo de setup para um worker de ~150 linhas.

---

## R10 — Integração com `dev-up.ps1`

**Decision**: Estender `scripts/dev-up.ps1` para:
1. Definir `$WorkerDir = Join-Path $RepoRoot 'src\TestOrder.OrderProcessor'`.
2. Se `-not (Test-Path (Join-Path $WorkerDir 'node_modules'))` → `npm install` no worker.
3. `Start-ServiceWindow -Title 'TestOrder - Worker' -Command 'node index.js' -WorkingDirectory $WorkerDir`.
4. Imprimir no console principal: `Worker:   see "TestOrder - Worker" window`.

**Rationale**: Mesmo padrão das janelas MySQL/API/Web (módulo 004 + limpeza recente). Um comando sobe o ambiente completo para demo (SC-004).

**Alternatives considered**:
- *Worker no mesmo terminal do script*: perde visibilidade de logs em tempo real — rejeitado.
- *Forever/nodemon*: dependência extra desnecessária.

---

## R11 — Módulos ES vs CommonJS

**Decision**: `"type": "module"` no `package.json` do worker; `import`/`export` nativos (mesmo padrão de `src/TestOrder.Web`).

**Rationale**: Consistência com o frontend já entregue; Node 18+ suporta ESM nativamente.

---

## R12 — Preservação do backend e frontend

**Decision**: Nenhuma alteração em `src/TestOrder.Api/` ou `src/TestOrder.Web/` neste módulo. Alterações permitidas: `src/TestOrder.OrderProcessor/**`, `scripts/dev-up.ps1`, documentação (`README.md`, `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md`), `.gitignore` (append pontual para `node_modules/` do worker se necessário).

**Rationale**: FR-012, FR-013, FR-014 da spec. O outbox `pending` já é escrito pelo módulo 002; o worker apenas consome.

---

## Referências

- [Scaling inventory reservations — Shopify Engineering](https://shopify.engineering/scaling-inventory-reservations) (mesma família de padrão SKIP LOCKED)
- Módulo 002: [research.md R3/R6](../002-criacao-pedido-reservas/research.md), [CreateOrderCommands.cs](../../src/TestOrder.Api/Controllers/CreateOrderCommands.cs)
- Índice existente: `IX_order_processing_events_status_created` em [data-model módulo 002](../002-criacao-pedido-reservas/data-model.md)
