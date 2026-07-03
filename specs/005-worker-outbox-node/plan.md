# Plano Técnico: Módulo 005 — Microserviço Node para Processamento do Outbox

**Branch**: `005-worker-outbox-node` | **Data**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Especificação em `specs/005-worker-outbox-node/spec.md`

---

## Summary

Implementar um **processo Node standalone** em `src/TestOrder.OrderProcessor` que consome eventos `OrderCreated` com status `pending` da tabela `order_processing_events` (já escrita pelo backend .NET no módulo 002), usando **polling + transação curta com `SELECT ... FOR UPDATE SKIP LOCKED`**, marca cada evento como `processed` de forma idempotente e emite **log estruturado JSON** simulando processamento assíncrono. Sem RabbitMQ/Kafka/Redis/BullMQ; sem HTTP entre backend e worker; **sem alteração de schema** nem de `src/TestOrder.Api`/`src/TestOrder.Web`. Estender `scripts/dev-up.ps1` para abrir a **quarta janela** `TestOrder - Worker`. Regressão obrigatória: backend **46/46**; validação do worker principalmente **manual** (checklist em [quickstart.md](./quickstart.md)).

---

## Technical Context

| Item | Valor |
| --- | --- |
| **Language/Version** | JavaScript (ES modules) — sem TypeScript |
| **Primary Dependencies** | `mysql2` apenas (driver MySQL com pool e transações) |
| **Storage** | MySQL 8 (mesma instância `docker-compose.yml`; tabela `order_processing_events` existente) |
| **Testing** | Backend: xUnit + Testcontainers **46/46** (inalterado). Worker: **sem suíte automatizada** neste módulo — validação manual reproduzível ([research.md R9](./research.md)) |
| **Target Platform** | Processo Node local (Windows dev), janela CMD via `dev-up.ps1` |
| **Project Type** | Worker/polling consumer — terceira fatia vertical Node ao lado de `TestOrder.Api` e `TestOrder.Web` |
| **Performance Goals** | Evento `processed` em até 10s após criação do pedido (SC-001); polling 2s; lote até 10 eventos/ciclo |
| **Constraints** | Sem broker externo; sem classes/service layer; SQL raw em `worker.js`; shutdown limpo SIGINT; não alterar backend/frontend |
| **Scale/Scope** | ~3 arquivos JS + `package.json`; demo local; suporte a 2+ instâncias do worker sem duplicidade |

---

## Constitution Check

*GATES: `.cursor/rules/testorder.mdc` + spec módulo 005 (`.specify/memory/constitution.md` permanece template genérico — gates efetivos vêm das regras do workspace).*

| Gate | Status | Notas |
| --- | --- | --- |
| Microserviço Node só para processamento pós-criação | ✅ PASS | Worker consome outbox; não participa do POST |
| Sem RabbitMQ/Kafka/Redis/BullMQ | ✅ PASS | MySQL como único buffer |
| MySQL 8 + SKIP LOCKED | ✅ PASS | Mesmo padrão do módulo 002 |
| Sem Clean Architecture/DDD/CQRS/repositories | ✅ PASS | Funções + SQL em poucos arquivos |
| Backend MVC + EF/Dapper inalterados | ✅ PASS | Nenhum arquivo em `src/TestOrder.Api/` |
| Frontend React inalterado | ✅ PASS | Nenhum arquivo em `src/TestOrder.Web/` |
| Comunicação via tabela, não HTTP | ✅ PASS | Sem endpoints no worker; sem chamada .NET→Node |
| `dev-up.ps1` como DX principal | ✅ PASS | Quarta janela CMD + npm install condicional |
| Preservar 46 testes backend | ✅ PASS | Gate de regressão em todo PR/implementação |
| Schema mínimo (sem migration nova) | ✅ PASS | [research.md R5](./research.md) |

**Pós-design (Phase 1)**: Nenhuma violação. Projeto Node adicional é exigência explícita do desafio (item opcional), não over-engineering.

---

## Project Structure

### Documentação (esta feature)

```text
specs/005-worker-outbox-node/
├── spec.md
├── plan.md                 # este arquivo
├── research.md             # Phase 0
├── data-model.md           # Phase 1 — outbox existente + transições
├── quickstart.md           # Phase 1 — validação manual ponta a ponta
├── contracts/
│   └── outbox-consumer.md  # Phase 1 — contrato produtor/consumidor via tabela
├── checklists/
│   └── requirements.md     # da /speckit-specify
└── tasks.md                # Phase 2 (/speckit-tasks — próximo passo)
```

### Código-fonte — novo worker + delta em scripts/docs

```text
F:\repository\TestOrder\
├── src/
│   ├── TestOrder.Api/              # inalterado neste módulo
│   ├── TestOrder.Web/              # inalterado neste módulo
│   └── TestOrder.OrderProcessor/   # NOVO
│       ├── package.json            # mysql2; "type":"module"; script "start":"node index.js"
│       ├── config.js               # env MYSQL_* + POLL_INTERVAL_MS + BATCH_SIZE
│       ├── worker.js               # SQL claim/process + log estruturado
│       └── index.js                # pool, loop polling, SIGINT shutdown
├── scripts/
│   └── dev-up.ps1                  # + janela Worker + npm install condicional worker
├── .gitignore                      # append pontual: src/TestOrder.OrderProcessor/node_modules/
├── README.md                       # ambiente 4 janelas
├── AI_NOTES.md                     # seção Módulo 005 (pós-implementação)
└── docs/
    └── PRESENTATION_GUIDE.md       # roteiro demo outbox (pós-implementação)
```

**Structure Decision**: Fatia vertical mínima — três arquivos JS + manifest npm, espelhando a simplicidade de `CreateOrderCommands.cs` (SQL colocalizado) e de `api.js` do frontend (funções diretas, sem camada genérica).

---

## Decisões de implementação

| # | Decisão | Detalhe |
| --- | --- | --- |
| 1 | Driver | `mysql2/promise` + `createPool` ([research.md R1](./research.md)) |
| 2 | Arquivos | `config.js`, `worker.js`, `index.js` ([research.md R2](./research.md)) |
| 3 | Polling | 2000 ms; batch 10 ([research.md R3](./research.md)) |
| 4 | Claim SQL | `SELECT ... FOR UPDATE SKIP LOCKED` + `UPDATE ... WHERE status='pending'` ([research.md R4](./research.md)) |
| 5 | Schema | Sem migration/colunas novas ([research.md R5](./research.md)) |
| 6 | Payload inválido | Log erro + marcar `processed` ([research.md R6](./research.md)) |
| 7 | Side effect | Log JSON stdout ([research.md R7](./research.md)) |
| 8 | Shutdown | Flag + concluir ciclo + `pool.end()` ([research.md R8](./research.md)) |
| 9 | Testes worker | Manual only ([research.md R9](./research.md)) |
| 10 | dev-up.ps1 | Quarta janela `TestOrder - Worker` ([research.md R10](./research.md)) |
| 11 | ESM | `"type": "module"` ([research.md R11](./research.md)) |
| 12 | Escopo de arquivos | Só worker + script + docs ([research.md R12](./research.md)) |

---

## Fluxo do worker (sequência)

```text
1. index.js: carregar config; createPool(mysql2); registrar SIGINT -> shuttingDown=true
2. Loop while (!shuttingDown):
     a. try: await processPendingEvents(pool)
     b. catch (err): console.error JSON { level:'error', message, ... }
     c. await sleep(POLL_INTERVAL_MS)
3. pool.end(); process.exit(0)

processPendingEvents(pool):
  conn = await pool.getConnection()
  BEGIN TRANSACTION
  rows = SELECT ... pending + OrderCreated ... FOR UPDATE SKIP LOCKED LIMIT batch
  for each row in rows:
      try parse payload JSON
      if parse OK: console.log JSON info { eventId, orderId, eventType, processedAt }
      if parse fail: console.error JSON error { eventId, orderId, reason }
      UPDATE status='processed' WHERE id=? AND status='pending'
  COMMIT
  conn.release()
```

Comentário no código **somente** no bloco SELECT/UPDATE (concorrência/idempotência).

---

## Alterações em `scripts/dev-up.ps1`

Inserir **após** o bloco condicional de `npm install` do frontend e **antes** de `Write-Host 'Opening service windows...'`:

```powershell
$WorkerDir = Join-Path $RepoRoot 'src\TestOrder.OrderProcessor'
$WorkerNodeModules = Join-Path $WorkerDir 'node_modules'
if (-not (Test-Path $WorkerNodeModules)) {
    Write-Host 'Worker dependencies not found - running npm install (first run)...'
    Push-Location $WorkerDir
    npm install
    ...
    Pop-Location
}
```

Adicionar na abertura de janelas:

```powershell
Start-ServiceWindow -Title 'TestOrder - Worker' -Command 'node index.js' -WorkingDirectory $WorkerDir
```

Atualizar saída final:

```text
Worker:   see "TestOrder - Worker" window
```

Manter mensagens ASCII-only e avisos de porta existentes (5069/5173) inalterados.

---

## Contrato de outbox

Detalhamento completo em [contracts/outbox-consumer.md](./contracts/outbox-consumer.md).

| Papel | Ação | Tabela |
| --- | --- | --- |
| Produtor (.NET) | INSERT `pending` + `OrderCreated` | `order_processing_events` |
| Consumidor (Node) | SELECT SKIP LOCKED → log → UPDATE `processed` | mesma tabela |

Nenhum endpoint HTTP novo.

---

## Estratégia de validação

| Camada | Método | Critério |
| --- | --- | --- |
| Backend | `dotnet build TestOrder.slnx && .\scripts\test.ps1` | **46/46** |
| Worker smoke | `node index.js` conecta e loop sem crash | Pool OK, sem erro imediato |
| Worker funcional | Checklist [quickstart.md](./quickstart.md) | UI → log worker → `processed` no MySQL |
| Concorrência | 2 instâncias + 3 pedidos | Zero duplicidade |
| Resiliência | `docker compose stop/start mysql` | Worker retoma |
| Regressão escopo | `git diff` | Sem mudanças em Api/Web salvo justificativa |

---

## Phase 0 & Phase 1 — Artefatos gerados

| Artefato | Status |
| --- | --- |
| [research.md](./research.md) | ✅ |
| [data-model.md](./data-model.md) | ✅ |
| [contracts/outbox-consumer.md](./contracts/outbox-consumer.md) | ✅ |
| [quickstart.md](./quickstart.md) | ✅ |

---

## Documentação pós-implementação (não fazer neste passo)

### `AI_NOTES.md` — seção Módulo 005 (template)

- Decisão de manter schema inalterado.
- Intervalo/lote de polling e por quê.
- Evidência de concorrência (2 instâncias, sem duplicidade).
- Por que não há testes automatizados do worker (ou o que foi adicionado, se mudar).
- Resultados do checklist manual.
- Prompts Spec Kit usados.

### `docs/PRESENTATION_GUIDE.md` — adições

- Referências: `src/TestOrder.OrderProcessor/worker.js`, trecho SKIP LOCKED.
- Roteiro: `dev-up.ps1` (4 janelas) → criar pedido na UI → log worker → query SQL status.
- Demo concorrência: duas instâncias do worker.
- Tabela pass/fail dos checks manuais.
- Nota: item **opcional** do desafio, sem fila externa.

### `README.md`

- Atualizar seção "Subir ambiente completo" para **quatro** janelas (MySQL, API, Web, Worker).

---

## Complexity Tracking

*Nenhuma violação de constitution/regras do workspace a justificar. O terceiro projeto Node é exigência do desafio; três arquivos JS + um driver npm mantêm a mesma filosofia de simplicidade dos módulos anteriores.*

---

## Próximos passos

1. **`/speckit-tasks`** — gerar `tasks.md` com tarefas ordenadas para implementação.
2. **`/speckit-implement`** — executar tarefas uma fatia por vez; não avançar para README/AI_NOTES finais até validação passar.

---

## Referências cruzadas

| Documento | Uso |
| --- | --- |
| [spec.md](./spec.md) | Requisitos e critérios de aceite |
| [research.md](./research.md) | Decisões R1–R12 |
| [data-model.md](./data-model.md) | Outbox existente + transições de status |
| [contracts/outbox-consumer.md](./contracts/outbox-consumer.md) | Contrato produtor/consumidor |
| [quickstart.md](./quickstart.md) | Validação manual e comandos |
| [../002-criacao-pedido-reservas/plan.md](../002-criacao-pedido-reservas/plan.md) | Origem do outbox `pending` |
| [../004-tela-web-pedidos/quickstart.md](../004-tela-web-pedidos/quickstart.md) | Padrão dev-up 3 janelas (base para 4ª) |
| [../../src/TestOrder.Api/Controllers/CreateOrderCommands.cs](../../src/TestOrder.Api/Controllers/CreateOrderCommands.cs) | INSERT outbox pelo backend |
