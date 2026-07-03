# Tasks: Módulo 005 — Microserviço Node para Processamento do Outbox

**Input**: Design documents from `specs/005-worker-outbox-node/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/outbox-consumer.md, quickstart.md

**Tests**: Sem suíte automatizada do worker neste módulo ([research.md R9](./research.md)) — validação via smoke `node index.js` + checklist manual. Backend deve permanecer **46/46**.

**Organization**: Worker-only, sem alteração de backend/frontend. Fases: preflight → scaffold → US1 → US2 → US3 → US4 → regressão → **validação manual** → **documentação pós-validação** → validação final.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência de tarefas incompletas)
- **[USn]**: User story de referência (ordem da spec): US1=processar evento OrderCreated, US2=concorrência segura (SKIP LOCKED), US3=integração dev-up (4ª janela), US4=resiliência (fila vazia / MySQL indisponível)
- Caminhos de arquivo explícitos

---

## Phase 1: Preflight

**Goal**: Proteger módulos 001–004 — confirmar build e testes do backend verdes antes de qualquer alteração.

**Independent Test**: `dotnet build` + `.\scripts\test.ps1` passam sem alteração de código.

---

- [X] T001 Validar build e testes do backend (`dotnet build TestOrder.slnx && .\scripts\test.ps1`)

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar qualquer API local; confirmar **46/46** testes e build verde antes de criar o worker. |
| **Permitidos** | Nenhum arquivo alterado |
| **Proibidos** | Alterações de código |
| **Pronto quando** | Build + testes passam |
| **Validação** | `Get-Process TestOrder.Api -ErrorAction SilentlyContinue \| Stop-Process -Force; dotnet build TestOrder.slnx; .\scripts\test.ps1` |
| **Paralelo** | Não — primeiro passo obrigatório |

**Checkpoint Phase 1**: Baseline backend verde confirmada.

---

## Phase 2: Scaffold do worker

**Goal**: Criar o projeto `src/TestOrder.OrderProcessor` com manifest npm, configuração e estrutura mínima de arquivos.

**Independent Test**: `cd src/TestOrder.OrderProcessor && npm install` conclui sem erros; arquivos existem.

---

- [X] T002 Criar `src/TestOrder.OrderProcessor/package.json` com `mysql2`, `"type": "module"` e script `"start": "node index.js"`

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | Dependência de produção: `mysql2` apenas. **Proibido**: TypeScript, BullMQ, amqplib, kafkajs, ioredis, express, nestjs, classes/service layer. Scripts: `"start": "node index.js"`. |
| **Permitidos** | `src/TestOrder.OrderProcessor/package.json` |
| **Proibidos** | Alterar `src/TestOrder.Api/`, `src/TestOrder.Web/`, `tests/` |
| **Pronto quando** | `npm install` conclui sem erros |
| **Paralelo** | Não — base do projeto worker |

---

- [X] T003 [P] Criar `src/TestOrder.OrderProcessor/config.js` com env vars e defaults

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | Exportar objeto de config lido de `process.env` com defaults compatíveis com `docker-compose.yml`: `MYSQL_HOST=localhost`, `MYSQL_PORT=3306`, `MYSQL_DATABASE=testorder`, `MYSQL_USER=testorder`, `MYSQL_PASSWORD=testorder`. Constantes: `POLL_INTERVAL_MS=2000`, `BATCH_SIZE=10` (override opcional via env). Sem classes — objeto plain ou export nomeado. |
| **Permitidos** | `src/TestOrder.OrderProcessor/config.js` |
| **Proibidos** | dotenv como dependência obrigatória (env do SO basta) |
| **Paralelo** | Sim — após T002 |

---

- [X] T004 [P] Verificar `.gitignore` na raiz cobre `src/TestOrder.OrderProcessor/node_modules/`

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | Append mínimo ao `.gitignore` existente se faltar — não reescrever o arquivo inteiro. |
| **Permitidos** | `.gitignore` (append apenas) |
| **Proibidos** | Alterar backend/frontend |
| **Pronto quando** | `node_modules/` do worker ignorado |
| **Paralelo** | Sim — arquivo independente |

**Checkpoint Phase 2**: `npm install` no worker OK; `config.js` exporta defaults corretos.

---

## Phase 3: Processar evento OrderCreated (US1 — P1)

**Goal**: Worker consome eventos `pending`/`OrderCreated`, emite log JSON e marca `processed` de forma idempotente.

**Independent Test**: Com MySQL rodando e worker ativo, criar pedido via UI → log JSON no console + `status='processed'` no banco em até ~10s.

---

- [X] T005 [US1] Implementar `processPendingEvents(pool)` com transação, `SELECT ... FOR UPDATE SKIP LOCKED` e `UPDATE` condicional em `src/TestOrder.OrderProcessor/worker.js`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | Função exportada `processPendingEvents(pool)`: `getConnection` → `beginTransaction` → SELECT de `order_processing_events` WHERE `status='pending' AND event_type='OrderCreated'` ORDER BY `created_at ASC, id ASC` LIMIT `BATCH_SIZE` **FOR UPDATE SKIP LOCKED** → para cada linha: UPDATE `SET status='processed' WHERE id=? AND status='pending'` → se `affectedRows === 0`, seguir **sem erro** (outra instância venceu a corrida; idempotência) → `commit` → `release`. Comentário **somente** neste bloco SQL explicando concorrência/idempotência (research.md R4). **Proibido**: classes, repository pattern, ORM. |
| **Permitidos** | `src/TestOrder.OrderProcessor/worker.js` |
| **Proibidos** | Alterar schema/migrations; endpoints HTTP |
| **Paralelo** | Não — base do worker |

---

- [X] T006 [US1] Implementar log estruturado JSON e tratamento de payload inválido em `src/TestOrder.OrderProcessor/worker.js`

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | Após SELECT, para cada evento: tentar `JSON.parse(payload)`. Sucesso → `console.log` JSON `{ level:'info', action:'order-created-processed', eventId, orderId, eventType, processedAt }`. Falha → `console.error` JSON `{ level:'error', eventId, orderId, reason }` e **mesmo assim** marcar `processed` (research.md R6 — evita loop infinito). Eventos de tipo diferente de `OrderCreated` não devem ser retornados pelo SELECT (filtro na query). |
| **Depende de** | T005 |
| **Permitidos** | `src/TestOrder.OrderProcessor/worker.js` |
| **Paralelo** | Não — sequencial no mesmo arquivo |

---

- [X] T007 [US1] Criar bootstrap do pool MySQL em `src/TestOrder.OrderProcessor/index.js`

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | ESM: importar `createPool` de `mysql2/promise`, `config` de `./config.js`, `processPendingEvents` de `./worker.js`. Criar pool com host/port/database/user/password da config. Helper `sleep(ms)` para o loop. |
| **Depende de** | T003, T005 |
| **Permitidos** | `src/TestOrder.OrderProcessor/index.js` |
| **Paralelo** | Não |

---

- [X] T008 [US1] Implementar loop de polling contínuo em `src/TestOrder.OrderProcessor/index.js`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | Loop `while (!shuttingDown)`: `try { await processPendingEvents(pool) }` → `await sleep(POLL_INTERVAL_MS)`. Sem log ruidoso quando fila vazia (research.md R3). `shuttingDown` inicia `false` (handler SIGINT vem em T013). **Nota transitória**: erros de MySQL nesta fase **não** devem chamar `process.exit`; tratamento completo (catch + log JSON + continuar loop) será refinado em T012. |
| **Depende de** | T007 |
| **Permitidos** | `src/TestOrder.OrderProcessor/index.js` |
| **Paralelo** | Não — sequencial |

**Checkpoint Phase 3 (US1)**: `node index.js` conecta ao MySQL e processa pedidos criados pela UI.

---

## Phase 4: Concorrência segura entre instâncias (US2 — P1)

**Goal**: Garantir que duas instâncias do worker não processem o mesmo evento em duplicidade.

**Independent Test**: Duas janelas com `node index.js` + 2–3 pedidos novos → cada evento `processed` uma vez; logs sem duplicidade de `eventId`.

---

- [X] T009 [US2] Checkpoint de revisão — idempotência/concorrência em `src/TestOrder.OrderProcessor/worker.js`

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | **Checkpoint de revisão** (não duplica requisito de T005 — confirma implementação antes da demo de concorrência). Verificar que T005–T006 entregam: (1) `FOR UPDATE SKIP LOCKED` na reserva; (2) `UPDATE ... WHERE id=? AND status='pending'` na marcação; (3) se `affectedRows === 0`, seguir sem erro; (4) comentário no bloco SQL explicando por que duas instâncias não duplicam processamento. Ajustar comentário/SQL se algo estiver incompleto. **Não** adicionar status intermediário `processing`. |
| **Depende de** | T005, T006 |
| **Permitidos** | `src/TestOrder.OrderProcessor/worker.js` |
| **Paralelo** | Não — revisão do mesmo arquivo |

**Checkpoint Phase 4 (US2)**: Código pronto para demo de concorrência (validação manual em T021).

---

## Phase 5: Integração com `dev-up.ps1` (US3 — P2)

**Goal**: Quarta janela CMD `TestOrder - Worker` sobe junto com MySQL/API/Web; `npm install` condicional.

**Independent Test**: `.\scripts\dev-up.ps1` abre 4 janelas; segunda execução não reinstala dependências do worker.

---

- [X] T010 [US3] Adicionar `$WorkerDir` e `npm install` condicional do worker em `scripts/dev-up.ps1`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | Após bloco condicional de `npm install` do frontend e **antes** de `Write-Host 'Opening service windows...'`: definir `$WorkerDir = Join-Path $RepoRoot 'src/TestOrder.OrderProcessor'`; se `-not (Test-Path (Join-Path $WorkerDir 'node_modules'))` → `Push-Location`, `npm install`, `Pop-Location` com mensagem ASCII. Mesmo padrão do frontend (research.md R10). |
| **Permitidos** | `scripts/dev-up.ps1` |
| **Proibidos** | Matar processos automaticamente; alterar backend |
| **Paralelo** | Não |

---

- [X] T011 [US3] Abrir janela `TestOrder - Worker` e atualizar mensagens finais em `scripts/dev-up.ps1`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | `Start-ServiceWindow -Title 'TestOrder - Worker' -Command 'node index.js' -WorkingDirectory $WorkerDir`. Atualizar saída final do script: linha `Worker:   see "TestOrder - Worker" window`. Manter avisos de porta 5069/5173 inalterados. |
| **Depende de** | T010 |
| **Permitidos** | `scripts/dev-up.ps1` |
| **Paralelo** | Não — sequencial (mesmo arquivo) |

**Checkpoint Phase 5 (US3)**: `dev-up.ps1` sobe ambiente completo com 4 janelas.

---

## Phase 6: Resiliência e shutdown limpo (US4 — P3)

**Goal**: Worker não trava sem eventos; sobrevive a MySQL indisponível momentâneo; encerra limpo com `Ctrl+C`.

**Independent Test**: Fila vazia sem spam; `docker compose stop/start mysql` → worker retoma; `Ctrl+C` sem stack trace.

---

- [X] T012 [US4] Implementar tratamento de erro no loop de polling em `src/TestOrder.OrderProcessor/index.js`

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | Envolver `processPendingEvents(pool)` em `try/catch`: em falha de conexão/SQL, `console.error` JSON `{ level:'error', message, ... }` e continuar loop (não `process.exit`). Próximo ciclo tenta novamente após `POLL_INTERVAL_MS` (spec Jornada 4; [contrato §2 Ciclo e concorrência](./contracts/outbox-consumer.md)). |
| **Depende de** | T008 |
| **Permitidos** | `src/TestOrder.OrderProcessor/index.js` |
| **Paralelo** | Não — sequencial |

---

- [X] T013 [US4] Implementar shutdown limpo via `SIGINT` em `src/TestOrder.OrderProcessor/index.js`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | `process.on('SIGINT', () => { shuttingDown = true })` — **não** chamar `process.exit()` no handler. Loop verifica flag antes de novo ciclo; após sair do loop, `await pool.end()` e `process.exit(0)`. Ciclo em andamento conclui commit/rollback antes de encerrar (research.md R8). |
| **Depende de** | T012 |
| **Permitidos** | `src/TestOrder.OrderProcessor/index.js` |
| **Paralelo** | Não — sequencial (mesmo arquivo) |

**Checkpoint Phase 6 (US4)**: Worker resiliente e encerrável sem erro não tratado.

---

## Phase 7: Smoke e regressão do backend

**Goal**: Confirmar worker executável e backend intacto (46/46).

**Independent Test**: `node index.js` smoke OK; `.\scripts\test.ps1` passa 46/46.

---

- [X] T014 Validar smoke do worker (`cd src/TestOrder.OrderProcessor && npm install && node index.js`)

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | Com MySQL rodando (`docker compose up -d mysql`), executar worker manualmente por alguns ciclos — conecta sem crash imediato. Revisar `package.json`: **somente** `mysql2` como dependência de produção (AC-012). Shutdown limpo (`Ctrl+C`) é validado após T013 (T022/T023). |
| **Permitidos** | Nenhuma alteração de código (salvo fixes mínimos se smoke falhar) |
| **Validação** | `cd src/TestOrder.OrderProcessor; npm install; node index.js` |
| **Paralelo** | Não |

---

- [X] T015 Validar regressão do backend (`dotnet build TestOrder.slnx && .\scripts\test.ps1`)

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar API local antes. Confirmar **46/46** testes — nenhum arquivo de `src/TestOrder.Api/` ou `tests/` foi alterado neste módulo. |
| **Permitidos** | Nenhum arquivo alterado |
| **Validação** | `Get-Process TestOrder.Api -ErrorAction SilentlyContinue \| Stop-Process -Force; dotnet build TestOrder.slnx; .\scripts\test.ps1` |
| **Paralelo** | Não — após T014 |

**Checkpoint Phase 7**: Worker smoke OK + backend 46/46 intacto.

---

## Phase 8: Validação manual

**Goal**: Executar checklist de aceite da spec com ambiente completo rodando **antes** de registrar resultados na documentação.

**Independent Test**: Todos os passos de [quickstart.md](./quickstart.md) passam.

---

- [X] T020 [US1] Validação manual E2E — UI → worker → `processed` no MySQL

**Detalhe T020**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar fluxo ponta a ponta conforme [quickstart.md](./quickstart.md): `.\scripts\dev-up.ps1` (4 janelas) → criar pedido pela UI → observar log JSON na janela Worker em ~10s → consultar `order_processing_events` com `status='processed'`. **(AC-008)** Executar seção *Validar npm install condicional do worker* do quickstart (remover `node_modules`, rodar `dev-up.ps1` duas vezes). **(AC-004, opcional)** Executar seção *Validar event_type fora do contrato* — worker não falha; linha permanece `pending`. |
| **Permitidos** | Nenhuma alteração de código (salvo fixes mínimos se checklist falhar) |
| **Pronto quando** | AC-001, AC-002, AC-003, AC-008 passam; AC-004 opcional documentado |
| **Paralelo** | Não |

---

- [X] T021 [US2] Validação manual de concorrência — duas instâncias do worker

**Detalhe T021**
| Campo | Valor |
| --- | --- |
| **Descrição** | Manter worker do `dev-up.ps1`; abrir segunda janela `cd src/TestOrder.OrderProcessor && node index.js`. Criar 2–3 pedidos pela UI. Confirmar: cada evento `processed` uma vez; logs sem duplicidade de `eventId` entre instâncias (AC-005, AC-006, SC-002). |
| **Permitidos** | Nenhuma alteração de código (salvo fixes mínimos) |
| **Pronto quando** | Zero duplicidade observada |
| **Paralelo** | Não — após T020 |

---

- [X] T022 [US4] Validação manual de resiliência e shutdown

**Detalhe T022**
| Campo | Valor |
| --- | --- |
| **Descrição** | (1) Fila vazia: aguardar 2–3 ciclos sem spam de erro. (2) `docker compose stop mysql` → worker loga erro e continua. (3) `docker compose start mysql` → criar pedido → processamento retoma sem reinício manual (SC-006). (4) `Ctrl+C` na janela Worker → encerra sem stack trace (AC-009). (5) Reabrir worker → não reprocessa eventos já `processed`. |
| **Permitidos** | Nenhuma alteração de código (salvo fixes mínimos) |
| **Pronto quando** | AC-009 e cenários Jornada 4 passam |
| **Paralelo** | Não — após T021 |

**Checkpoint Phase 8**: Validação manual completa — resultados prontos para documentação.

---

## Phase 9: Documentação pós-validação

**Goal**: Atualizar artefatos de documentação com fatos reais **após** Phase 8 (checklist manual executado).

**Independent Test**: Revisão humana dos documentos.

---

- [X] T016 [P] Atualizar `AI_NOTES.md` com seção Módulo 005

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Status concluído; decisão schema inalterado; polling 2s/lote 10; SKIP LOCKED + UPDATE condicional; por que não há testes automatizados do worker (R9); **resultados checklist manual (Phase 8)**; erros comuns de IA neste módulo; prompts Spec Kit usados. |
| **Depende de** | T020–T022 |
| **Permitidos** | `AI_NOTES.md` |
| **Paralelo** | Sim — após T022 |

---

- [X] T017 [P] Atualizar `docs/PRESENTATION_GUIDE.md` com seção Módulo 005

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | Marcar módulo 005 na ordem de apresentação. Referências: `src/TestOrder.OrderProcessor/worker.js` (trecho SKIP LOCKED), `index.js`. Roteiro: `dev-up.ps1` (4 janelas) → criar pedido UI → log worker → query SQL. Demo concorrência (2 instâncias). **Tabela pass/fail dos checks manuais (Phase 8)**. Nota: item **opcional** do desafio, sem fila externa. |
| **Depende de** | T020–T022 |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Paralelo** | Sim — após T022 |

---

- [X] T018 Atualizar `README.md` na raiz com ambiente de 4 janelas

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | Seção "Subir ambiente completo": `.\scripts\dev-up.ps1` abre MySQL, API, Web e **Worker**. Comando manual alternativo do worker: `cd src/TestOrder.OrderProcessor && node index.js`. Link para `docs/PRESENTATION_GUIDE.md`. |
| **Depende de** | T020–T022 |
| **Permitidos** | `README.md` |
| **Paralelo** | Não — após T016/T017 ou em paralelo lógico pós-T022 |

---

- [X] T019 Revisar `specs/005-worker-outbox-node/quickstart.md` com comandos finais validados

**Detalhe T019**
| Campo | Valor |
| --- | --- |
| **Descrição** | Preencher tabela "Resultado esperado da validação" com pass/fail real da Phase 8. Confirmar comandos de 4 janelas, AC-008, E2E, AC-004 opcional, concorrência, resiliência, shutdown e regressão 46/46. |
| **Depende de** | T020–T022 |
| **Permitidos** | `specs/005-worker-outbox-node/quickstart.md` |
| **Paralelo** | Não — após T016–T018 |

**Checkpoint Phase 9**: Documentação pronta para demo.

---

## Phase 10: Validação final

**Goal**: Gates finais de regressão backend, escopo de arquivos e confirmação de entrega.

**Independent Test**: `dotnet build` + `.\scripts\test.ps1` **46/46**; escopo git correto.

---

- [X] T023 Validação final — regressão backend, escopo de arquivos e gates completos

**Detalhe T023**
| Campo | Valor |
| --- | --- |
| **Descrição** | Gates finais obrigatórios + confirmação de escopo. Registrar pass/fail final em `docs/PRESENTATION_GUIDE.md` (se ainda não feito em T017). |
| **Depende de** | T016–T019 |
| **Permitidos** | Ajustes mínimos em docs se necessário |
| **Pronto quando** | Todos os passos abaixo passam |
| **Paralelo** | Não — último passo |

**Validações finais obrigatórias**:

```powershell
# Backend
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1

# Worker smoke
cd src/TestOrder.OrderProcessor
node index.js
# Ctrl+C após alguns ciclos
cd ../..

# Escopo — nenhuma alteração indevida
git diff --name-only
# Esperado: SEM mudanças em src/TestOrder.Api/, src/TestOrder.Web/, tests/
# Permitido: src/TestOrder.OrderProcessor/**, scripts/dev-up.ps1, docs, README, AI_NOTES, .gitignore
```

**Checklist final (marcar pass/fail em `docs/PRESENTATION_GUIDE.md`)**:

1. `dev-up.ps1` abre **4** janelas (MySQL, API, Web, Worker) — AC-007
2. `npm install` do worker condicional — AC-008 (procedimento em [quickstart.md](./quickstart.md))
3. Pedido UI → log JSON worker → `processed` no MySQL — AC-002, AC-003, SC-001
4. Duas instâncias worker sem duplicidade — AC-005, SC-002
5. Shutdown limpo `Ctrl+C` — AC-009, SC-005
6. MySQL indisponível → worker retoma — SC-006
7. Backend **46/46** — AC-010, SC-003
8. Nenhum arquivo em `src/TestOrder.Web` alterado — AC-011
9. `package.json` do worker sem broker externo — AC-012
10. Backend não chama worker via HTTP — AC-013
11. **(Opcional)** Evento com `event_type` diferente permanece `pending` — AC-004

**Checkpoint Phase 10**: Módulo 005 completo.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 (preflight)
T002 → T003 [P], T004 [P] (parallel após T002)
T005 → T006 (worker.js, sequencial)
T007 → T008 (index.js loop, sequencial; depende T003+T005)
T009 (US2 checkpoint worker.js, após T005–T006)
T010 → T011 (dev-up.ps1, sequencial — mesmo arquivo)
T012 → T013 (index.js resiliência/shutdown, sequencial)
T014 → T015 (smoke + regressão)
T020 → T021 → T022 (validação manual sequencial — Phase 8)
T016 [P], T017 [P] (docs paralelos, após T022) → T018 → T019 (Phase 9)
T023 (validação final — Phase 10, após T016–T019)
```

### User Story Mapping

| Story | Prioridade | Tarefas principais |
| --- | --- | --- |
| US1 — Processar evento OrderCreated | P1 | T005, T006, T007, T008, T020 |
| US2 — Concorrência segura (SKIP LOCKED) | P1 | T005, T009, T021 |
| US3 — Integração dev-up (4ª janela) | P2 | T010, T011, T020 (AC-008) |
| US4 — Resiliência e shutdown | P3 | T012, T013, T022 |

### Parallel Opportunities

- **T003 + T004** — arquivos diferentes após T002 (`config.js` + `.gitignore`)
- **T016 + T017** — docs em arquivos diferentes (**após T022**, Phase 9)
- **T005 → T006 → T009** — edições em `worker.js` devem ser **sequenciais**
- **T007 → T008 → T012 → T013** — edições em `index.js` devem ser **sequenciais**
- **T010 → T011** — edições em `dev-up.ps1` devem ser **sequenciais**
- **T020 → T021 → T022** — validação manual sequencial (Phase 8)

---

## MVP Scope

**MVP mínimo demonstrável**: Phase 1–3 (T001–T008).

Worker processa evento `OrderCreated` e marca `processed` — validar com MySQL rodando + `node index.js` + criar pedido pela UI (sem exigir T014, shutdown ou `dev-up.ps1`).

**MVP recomendado para demo**: MVP acima + US2 checkpoint (T009) + US3 dev-up (T010–T011) + resiliência/shutdown (T012–T013) + regressão (T015).

Ambiente sobe com um comando, demo ponta a ponta fluida e gates de regressão — esperado pelo avaliador.

**Módulo completo** exige Phase 7–10 (T014–T023): smoke, regressão, validação manual, docs e validação final.

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1: Preflight → baseline backend 46/46
2. Phase 2: Scaffold → `package.json` + `config.js`
3. Phase 3: `worker.js` + `index.js` → polling + processamento
4. **STOP and VALIDATE**: MySQL + `node index.js` + criar pedido pela UI → evento `processed`

### Incremental Delivery

1. Phase 4: Checkpoint concorrência (US2)
2. Phase 5: `dev-up.ps1` 4ª janela (US3)
3. Phase 6: Resiliência + SIGINT (US4)
4. Phase 7: Smoke + regressão backend
5. Phase 8: Validação manual (T020–T022)
6. Phase 9–10: Docs + validação final (T016–T019, T023)

---

## Critérios de pronto por User Story

| Story | Critério | Evidência |
| --- | --- | --- |
| US1 | Evento `pending` → log JSON → `processed` | T005–T008 + T020 |
| US2 | Duas instâncias sem duplicidade | T009 + T021 |
| US3 | `dev-up.ps1` abre 4 janelas; npm install condicional | T010–T011 + T020 (AC-008) + T023 #2 |
| US4 | Fila vazia silenciosa; MySQL down/up; Ctrl+C limpo | T012–T013 + T022 |

---

## Notes

- Parar API local antes de `dotnet build`/`dotnet test` no Windows (exe bloqueado).
- Worker **não** expõe HTTP — comunicação só via tabela `order_processing_events`.
- **Nenhum** arquivo de `src/TestOrder.Api/`, `src/TestOrder.Web/`, `tests/`, migrations EF ou `docker-compose.yml` é tocado neste módulo (salvo impossibilidade justificada no plano — nenhuma prevista).
- Sem suíte automatizada do worker — smoke `node index.js` + checklist manual são os gates do worker.
- Sem Dockerfile do worker neste módulo.
- Commit sugerido após cada fase (T001; T002–T004; T005–T009; T010–T011; T012–T013; T014–T015; T020–T022; T016–T019; T023).
- Testes automatizados opcionais do worker (script ad-hoc) **fora do escopo** deste tasks.md — se adicionados, documentar em `AI_NOTES.md` sem bloquear entrega.
