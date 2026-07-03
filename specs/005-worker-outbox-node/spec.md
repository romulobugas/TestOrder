# Especificação: Módulo 005 — Microserviço Node para Processamento do Outbox

**Feature Branch**: `005-worker-outbox-node`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Implementar o microserviço Node opcional pedido no desafio, responsável por processar os eventos `pending` já gravados pelo backend .NET na tabela `order_processing_events` (outbox), sem qualquer broker externo (RabbitMQ, Kafka, Redis, BullMQ). O worker usa o próprio MySQL como buffer, com `SELECT ... FOR UPDATE SKIP LOCKED` para segurança em concorrência, e roda como processo Node simples em `src/TestOrder.OrderProcessor`.

**Depende de**: Módulos 001 (base/listagem), 002 (criação de pedido + outbox `pending`) e 004 (tela React que dispara criação de pedidos) concluídos e mergeados no `main`. O módulo 005 **não** altera o backend .NET nem o frontend React, exceto se for estritamente impossível viabilizar o worker sem uma alteração mínima (a ser justificada no plano, se ocorrer).

---

## Objetivo do módulo

Entregar o **microserviço de processamento assíncrono** citado no desafio como item opcional: um processo Node independente que lê eventos `OrderCreated` com status `pending` da tabela `order_processing_events` (já escrita pelo backend .NET na mesma transação do `POST /api/orders`), processa cada evento de forma idempotente e o marca como `processed`. Não há fila externa — o próprio MySQL 8 atua como buffer, reaproveitando a mesma tabela e o mesmo padrão de concorrência (`FOR UPDATE SKIP LOCKED`) já usado na reserva de estoque do módulo 002.

O "processamento" em si é deliberadamente simples (log estruturado simulando notificação/enriquecimento do pedido) — o valor demonstrado é o **padrão de outbox + polling seguro sob concorrência**, não um caso de uso de negócio real.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Operador da loja** | Cria pedidos pela tela React (módulo 004) sem precisar saber que existe um worker — o processamento acontece de forma transparente em segundo plano. |
| **Avaliador do desafio** | Confirmar que o item opcional "microserviço Node" existe, funciona de ponta a ponta (UI → API → outbox → worker) e não usa nenhuma fila/broker externo proibido. |
| **Operador do ambiente local** | Subir e encerrar o worker junto com o restante do ambiente usando `scripts/dev-up.ps1`, acompanhando seus logs em janela própria. |

Não há autenticação nem perfis de acesso neste módulo.

---

## Jornadas do módulo

### Jornada 1 — Processar evento de pedido criado (P1)

**Como** operador do ambiente, **quero** que um pedido criado pela tela vire um evento processado automaticamente, **para** confirmar que o fluxo assíncrono ponta a ponta funciona sem intervenção manual.

**Por que P1**: É a entrega central do módulo — sem isso, o "microserviço opcional" do desafio não existe de fato.

**Teste independente**: Criar um pedido pela UI (ou via `POST /api/orders`) e, sem nenhuma ação manual além de observar, ver o evento correspondente mudar de `pending` para `processed` e uma mensagem estruturada aparecer no console do worker.

**Cenários de aceite**:

1. **Dado** o worker rodando e conectado ao MySQL, **quando** um novo evento `OrderCreated` com status `pending` é inserido (via `POST /api/orders`), **então** o worker o encontra no próximo ciclo de polling e inicia o processamento.
2. **Dado** um evento `OrderCreated` sendo processado, **quando** o processamento conclui, **então** o worker atualiza o status do evento para `processed` no banco.
3. **Dado** um evento processado, **quando** consulto `order_processing_events` diretamente no MySQL, **então** vejo `status = 'processed'` para aquele `order_id`.
4. **Dado** um evento processado, **quando** observo o console do worker, **então** vejo uma mensagem estruturada (ex.: JSON) identificando o `orderId`, o tipo de evento e o resultado do processamento.
5. **Dado** um evento de tipo diferente de `OrderCreated` (hipotético, hoje não emitido pelo backend), **quando** o worker o encontra, **então** ele é ignorado sem erro e sem travar o ciclo de polling.

---

### Jornada 2 — Processamento seguro com múltiplas instâncias do worker (P1)

**Como** avaliador do desafio, **quero** rodar duas instâncias do worker simultaneamente, **para** confirmar que o mesmo evento não é processado em duplicidade.

**Por que P1**: Demonstra o mesmo princípio de concorrência segura via MySQL já usado no módulo 002 (reserva de estoque), agora aplicado ao consumo do outbox — é o diferencial técnico deste módulo.

**Teste independente**: Rodar duas instâncias do worker apontando para o mesmo banco enquanto pedidos são criados; inspecionar a tabela de eventos e/ou os logs de ambas as instâncias e confirmar que cada evento aparece processado exatamente uma vez (nunca duas).

**Cenários de aceite**:

1. **Dado** duas instâncias do worker rodando ao mesmo tempo contra o mesmo MySQL, **quando** existe um evento `pending` disponível, **então** apenas uma das instâncias o processa (a outra segue para o próximo evento ou aguarda o próximo ciclo).
2. **Dado** múltiplos eventos `pending` simultâneos, **quando** as duas instâncias competem por eles, **então** cada evento é processado por exatamente uma instância, sem duplicidade e sem eventos "perdidos" (todos eventualmente terminam `processed`).
3. **Dado** um evento já `processed`, **quando** qualquer instância do worker o re-lê por engano (ex.: condição de corrida), **então** a atualização condicional impede reprocessamento (o evento permanece `processed`, sem efeito colateral duplicado no log).

---

### Jornada 3 — Subir e encerrar o worker de forma integrada (P2)

**Como** operador do ambiente local, **quero** que o worker suba junto com MySQL, API e frontend usando um único comando, e possa ser encerrado de forma limpa, **para** manter a mesma experiência operacional já estabelecida no módulo 004.

**Por que P2**: Não é o núcleo funcional do módulo, mas é necessário para a demonstração fluida e para manter o padrão de developer experience já entregue.

**Teste independente**: Rodar `.\scripts\dev-up.ps1` e observar uma quarta janela CMD dedicada ao worker, com logs próprios; fechar essa janela (ou `Ctrl+C`) encerra o processo sem stack trace de erro nem conexão pendurada no MySQL.

**Cenários de aceite**:

1. **Dado** `.\scripts\dev-up.ps1` em execução, **quando** o script termina de subir os serviços, **então** uma quarta janela CMD chamada `TestOrder - Worker` está aberta rodando o processo Node do worker, além das já existentes (`TestOrder - MySQL`, `TestOrder - API`, `TestOrder - Web`).
2. **Dado** que `src/TestOrder.OrderProcessor/node_modules` ainda não existe, **quando** `dev-up.ps1` roda, **então** o script executa `npm install` no worker antes de abri-lo (mesmo padrão já usado para o frontend).
3. **Dado** que `node_modules` do worker já existe, **quando** `dev-up.ps1` roda novamente, **então** o `npm install` do worker é pulado.
4. **Dado** o worker rodando em sua janela, **quando** recebo `Ctrl+C` (ou fecho a janela), **então** o processo encerra sem erro não tratado, fechando a conexão com o MySQL de forma limpa.

---

### Jornada 4 — Operar de forma resiliente na ausência de eventos ou de banco (P3)

**Como** operador do ambiente, **quero** que o worker não trave nem produza ruído excessivo quando não há trabalho a fazer ou quando o banco está momentaneamente indisponível, **para** manter o console legível durante a demo.

**Por que P3**: Robustez básica esperada, mas não é o foco do desafio (retry sofisticado e dead-letter queue estão explicitamente fora de escopo).

**Teste independente**: Deixar o worker rodando sem nenhum pedido novo por alguns ciclos e confirmar que ele não emite erros nem trava; derrubar o container do MySQL momentaneamente e confirmar que o worker loga o problema e continua tentando nos ciclos seguintes (sem crash irrecuperável), voltando a processar normalmente quando o banco retorna.

**Cenários de aceite**:

1. **Dado** nenhum evento `pending`, **quando** o worker executa um ciclo de polling, **então** ele aguarda o próximo ciclo sem logar erro nem consumir CPU de forma perceptível.
2. **Dado** o MySQL indisponível durante um ciclo, **quando** o worker tenta consultar eventos, **então** ele loga o erro de forma clara e tenta novamente no próximo ciclo, sem encerrar o processo.
3. **Dado** o MySQL voltando a ficar disponível, **quando** o próximo ciclo executa, **então** o worker volta a processar eventos pendentes normalmente, sem exigir reinício manual.

---

### Casos de borda

- **Evento de tipo diferente de `OrderCreated`**: ignorado silenciosamente (log informativo opcional), sem impedir o processamento de outros eventos pendentes.
- **Nenhum evento pendente**: ciclo de polling vazio, sem log ruidoso a cada iteração.
- **MySQL indisponível ao iniciar o worker**: o worker deve logar o erro e continuar tentando nos ciclos seguintes, sem encerrar imediatamente (sem retry sofisticado com backoff exponencial — apenas repetição simples no próximo ciclo).
- **Duas ou mais instâncias do worker competindo pelo mesmo evento**: `FOR UPDATE SKIP LOCKED` garante que apenas uma instância bloqueia e processa cada linha; as demais seguem adiante sem esperar o lock.
- **Evento já `processed` selecionado por engano**: a atualização de status DEVE ser condicional (`WHERE status = 'pending'`), tornando o processamento seguro mesmo diante de eventuais condições de corrida residuais.
- **Payload malformado (JSON inválido)**: o worker loga o erro de parsing referenciando o `id`/`order_id` do evento e segue para o próximo, sem derrubar o processo (sem fila de erro dedicada — fora de escopo).
- **Rajada de vários pedidos criados ao mesmo tempo pela UI**: o worker deve conseguir esvaziar a fila de eventos `pending` em poucos ciclos, processando mais de um evento por ciclo se necessário (tamanho de lote definido no plano).
- **Encerramento durante o processamento de um evento**: ao receber `Ctrl+C`, o worker deve preferencialmente concluir o ciclo/transação em andamento antes de sair, evitando deixar um evento "preso" em lock aberto.

---

## Requisitos funcionais

- **FR-001**: O sistema DEVE existir como processo Node standalone, organizado em poucos arquivos dentro de `src/TestOrder.OrderProcessor`.
- **FR-002**: O worker DEVE conectar ao MySQL usando variáveis de ambiente (`MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`), com defaults compatíveis com o `docker-compose.yml` atual (`localhost`, `3306`, `testorder`, `testorder`, `testorder`).
- **FR-003**: O worker DEVE consultar `order_processing_events` filtrando `status = 'pending'` e `event_type = 'OrderCreated'`.
- **FR-004**: O worker DEVE usar uma transação curta com `SELECT ... FOR UPDATE SKIP LOCKED` para reservar o(s) evento(s) pendentes antes de processá-los, permitindo múltiplas instâncias simultâneas sem colisão.
- **FR-005**: O worker DEVE marcar o evento como `processed` de forma idempotente (atualização condicionada ao status atual ser `pending`), preferencialmente **sem** alterar o schema atual de `order_processing_events`.
- **FR-006**: O worker DEVE processar apenas eventos do tipo `OrderCreated`; eventos de outros tipos (hoje inexistentes, mas possíveis no futuro) DEVEM ser ignorados sem interromper o processamento dos demais.
- **FR-007**: O worker DEVE simular o processamento por meio de uma mensagem estruturada no console (ex.: JSON com `orderId`, `eventType` e timestamp), representando notificação/enriquecimento do pedido.
- **FR-008**: O worker DEVE operar em ciclo de polling contínuo (sem broker externo), com intervalo fixo simples definido no plano técnico.
- **FR-009**: O worker DEVE encerrar de forma limpa ao receber `SIGINT` (`Ctrl+C`), fechando a conexão/pool com o MySQL sem stack trace de erro não tratado.
- **FR-010**: O worker DEVE suportar múltiplas instâncias simultâneas sem processar o mesmo evento em duplicidade, garantido por `FOR UPDATE SKIP LOCKED` combinado com atualização condicional de status.
- **FR-011**: O worker NÃO DEVE expor nenhum endpoint HTTP nem ser chamado diretamente pelo backend .NET — a única forma de comunicação entre os dois processos é a tabela `order_processing_events`.
- **FR-012**: O sistema NÃO DEVE alterar o fluxo de criação de pedido (`POST /api/orders`) do backend .NET, salvo necessidade mínima e explicitamente justificada no plano técnico.
- **FR-013**: O sistema NÃO DEVE quebrar a suíte de **46 testes** existentes do backend (`dotnet build TestOrder.slnx` e `.\scripts\test.ps1` continuam passando).
- **FR-014**: O sistema NÃO DEVE alterar o frontend React (`src/TestOrder.Web`) neste módulo.
- **FR-015**: `scripts/dev-up.ps1` DEVE abrir uma quarta janela CMD (título `TestOrder - Worker`) executando o processo do worker, além das já existentes (`TestOrder - MySQL`, `TestOrder - API`, `TestOrder - Web`).
- **FR-016**: `scripts/dev-up.ps1` DEVE instalar as dependências do worker (`npm install`) apenas se `src/TestOrder.OrderProcessor/node_modules` ainda não existir, seguindo o mesmo padrão condicional já usado para o frontend no módulo 004.
- **FR-017**: O worker NÃO DEVE usar RabbitMQ, Kafka, Redis, BullMQ ou qualquer broker/fila externa — o MySQL é o único mecanismo de coordenação.
- **FR-018**: O código do worker NÃO DEVE introduzir classes, camada de serviço/repositório genérica ou framework de aplicação pesado — funções simples em poucos arquivos, em JavaScript puro (sem TypeScript).

### Key Entities

- **OrderProcessingEvent** (entidade já existente, escrita pelo backend .NET no módulo 002, apenas **lida e atualizada** pelo worker): `id`, `orderId`, `eventType` (`OrderCreated` neste módulo), `status` (`pending` → `processed`), `payload` (JSON contendo ao menos `orderId`), `createdAt`.
- Nenhuma nova entidade de domínio é introduzida por este módulo — o worker opera exclusivamente sobre a tabela de outbox já existente.

---

## Requisitos não funcionais

- **NFR-001 (Simplicidade)**: JavaScript puro (sem TypeScript), sem framework de aplicação pesado, sem classes/service layer/repository; poucos arquivos dentro de `src/TestOrder.OrderProcessor`.
- **NFR-002 (Sem broker externo)**: Apenas MySQL como buffer/coordenação; proibido RabbitMQ, Kafka, Redis, BullMQ ou qualquer fila externa.
- **NFR-003 (Concorrência segura)**: `SELECT ... FOR UPDATE SKIP LOCKED` dentro de transação curta; atualização de status condicional para garantir idempotência sem lock manual externo.
- **NFR-004 (Desacoplamento)**: Comunicação exclusivamente via tabela `order_processing_events`; o backend .NET não deve ter conhecimento do worker (sem chamada HTTP direta em nenhuma direção).
- **NFR-005 (Regressão zero no backend)**: `dotnet build TestOrder.slnx` e `.\scripts\test.ps1` DEVEM continuar passando **46/46** após a introdução deste módulo.
- **NFR-006 (Observabilidade mínima)**: Log estruturado simples via console, sem dashboard, sem métricas externas, sem UI própria para o worker.
- **NFR-007 (Testabilidade pragmática)**: Testes automatizados do worker DEVEM ser adicionados apenas se de baixo custo e claramente úteis; caso o custo/complexidade não se justifique no prazo do desafio, a validação DEVE ser manual e objetiva (passos reproduzíveis e resultado esperado documentado).
- **NFR-008 (Rastreabilidade)**: Decisões de implementação (schema, polling, shutdown, testes) DEVEM ser documentadas em `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md` após a implementação.
- **NFR-009 (Shutdown limpo)**: `SIGINT`/`Ctrl+C` DEVE encerrar o processo sem stack trace de erro não tratado nem conexões de banco penduradas.
- **NFR-010 (Developer experience consistente)**: A experiência de subir/observar o worker DEVE seguir o mesmo padrão já estabelecido para MySQL/API/Web no módulo 004 (`dev-up.ps1`, janela CMD dedicada, `npm install` condicional).

---

## Modelo de dados esperado (alto nível)

Nenhuma nova tabela é criada. O worker opera sobre a estrutura já existente, criada no módulo 002:

```text
orders (1) ──< order_processing_events   [outbox, já existente]
```

### `order_processing_events` (reaproveitada, sem alteração de schema)

| Coluna | Tipo (atual) | Papel neste módulo |
| --- | --- | --- |
| `id` | PK | Identificador do evento; usado na cláusula `WHERE` da atualização condicional |
| `order_id` | referência a `orders` | Correlaciona o evento ao pedido processado |
| `event_type` | string curta | Filtro de leitura (`'OrderCreated'`) |
| `status` | string curta (`pending`/`processed`) | Lido via `FOR UPDATE SKIP LOCKED`; atualizado condicionalmente ao final do processamento |
| `payload` | JSON | Contém ao menos `orderId`; lido para montar a mensagem estruturada de log |
| `created_at` | datetime | Usado para ordenar o processamento (mais antigos primeiro) e para observabilidade |

**Decisão deste módulo**: **não** adicionar colunas novas (`processed_at`, `error_message`, etc.) ao schema existente. Justificativa: a transição de status `pending → processed`, feita de forma condicional dentro da mesma transação do `SELECT ... FOR UPDATE SKIP LOCKED`, já é suficiente para garantir idempotência e visibilidade do resultado (via `status` e via log estruturado no console) dentro do escopo definido — que exclui explicitamente retry sofisticado, dead-letter queue e auditoria de erro persistida. Caso o plano técnico identifique necessidade real de um campo adicional (ex.: para diagnosticar falhas de parsing de payload), essa exceção DEVE ser justificada explicitamente no `plan.md` antes da implementação, mantendo a preferência padrão pelo schema mínimo já existente.

---

## Contrato de comunicação (resumo)

Não há contrato HTTP novo neste módulo — a comunicação entre o backend .NET e o worker Node é **exclusivamente via banco de dados**:

```text
Backend .NET (POST /api/orders)
    └─ INSERT order_processing_events (status='pending', event_type='OrderCreated', payload={orderId})
                                │
                                │ (sem chamada HTTP, sem fila externa)
                                v
Worker Node (src/TestOrder.OrderProcessor)
    └─ SELECT ... FOR UPDATE SKIP LOCKED WHERE status='pending' AND event_type='OrderCreated'
    └─ processa (log estruturado)
    └─ UPDATE status='processed' WHERE id=? AND status='pending'
```

---

## Fora de escopo deste módulo

- Dashboard do worker (interface visual de acompanhamento de eventos).
- Retry sofisticado (backoff exponencial, número máximo de tentativas configurável, etc.).
- Dead-letter queue ou tabela de erros dedicada.
- Autenticação e autorização.
- Deploy (produção, CI/CD, hospedagem).
- Dockerfile do worker.
- Alterações no frontend React (`src/TestOrder.Web`).
- Faturamento por período (módulo 003) — não relacionado a este módulo.
- Cancelamento de pedido.
- Edição de estoque.
- Novos tipos de evento além de `OrderCreated`.
- Reprocessamento manual de eventos com erro via UI ou CLI dedicada.
- Escalonamento horizontal automatizado do worker (múltiplas instâncias são suportadas com segurança, mas orquestração/autoscaling não fazem parte do escopo).

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | Worker conecta ao MySQL usando defaults do `docker-compose.yml` | Subir `.\scripts\dev-up.ps1` sem variáveis de ambiente customizadas; worker conecta com sucesso |
| AC-002 | Pedido criado pela UI gera evento processado | Criar pedido via tela React; consultar `order_processing_events` e confirmar `status = 'processed'` para aquele `order_id` |
| AC-003 | Console do worker mostra mensagem estruturada | Observar log do worker após criação de pedido; mensagem identifica `orderId` e tipo de evento |
| AC-004 | Apenas eventos `OrderCreated` são processados | (Se aplicável) evento de outro tipo inserido manualmente é ignorado sem erro |
| AC-005 | Sem duplicidade com duas instâncias do worker | Rodar duas instâncias simultâneas; cada evento aparece processado por apenas uma, sem duplicidade de log/efeito |
| AC-006 | Idempotência da atualização de status | Consultar `order_processing_events` após processamento; nenhum evento retorna a `pending` nem é processado duas vezes |
| AC-007 | `dev-up.ps1` abre a quarta janela do worker | Rodar o script; confirmar janela `TestOrder - Worker` aberta com logs do processo |
| AC-008 | `npm install` do worker é condicional | Remover `node_modules` do worker e rodar `dev-up.ps1` — instala; rodar de novo — não reinstala |
| AC-009 | Shutdown limpo | `Ctrl+C` na janela do worker encerra sem stack trace de erro não tratado |
| AC-010 | Sem regressão no backend | `dotnet build TestOrder.slnx` e `.\scripts\test.ps1` continuam **46/46** |
| AC-011 | Sem alteração no frontend | Nenhum arquivo em `src/TestOrder.Web` modificado por este módulo |
| AC-012 | Sem broker externo | Nenhuma dependência de RabbitMQ/Kafka/Redis/BullMQ no `package.json` do worker |
| AC-013 | Backend não chama o worker via HTTP | Nenhuma nova chamada de rede adicionada ao backend .NET neste módulo; comunicação só via tabela |

---

## Checks manuais esperados

1. Subir tudo com `.\scripts\dev-up.ps1` — confirmar as **quatro** janelas CMD (`TestOrder - MySQL`, `TestOrder - API`, `TestOrder - Web`, `TestOrder - Worker`).
2. **(AC-008)** Validar `npm install` condicional do worker — seguir a seção *Validar npm install condicional do worker (AC-008)* em [quickstart.md](./quickstart.md).
3. Abrir a tela React e criar um pedido válido.
4. Observar o console da janela `TestOrder - Worker` — confirmar mensagem estruturada referenciando o pedido recém-criado em poucos segundos.
5. Consultar diretamente no MySQL: `SELECT id, order_id, event_type, status, created_at FROM order_processing_events ORDER BY id DESC LIMIT 5;` — confirmar `status = 'processed'` para o evento do pedido criado.
6. **(AC-004, opcional)** Inserir manualmente um evento com `event_type` diferente de `OrderCreated` — confirmar que o worker não falha e que a linha permanece `pending` (fora do contrato de consumo). Procedimento em [quickstart.md](./quickstart.md).
7. Encerrar o worker (`Ctrl+C` na janela) e reabri-lo — confirmar que ele volta a operar normalmente, sem reprocessar eventos já `processed`.
8. Rodar duas instâncias do worker ao mesmo tempo (duas janelas/terminais apontando para o mesmo MySQL) e criar 2-3 pedidos novos — confirmar nos logs de ambas as instâncias e na tabela que cada evento foi processado exatamente uma vez.
9. Derrubar temporariamente o container do MySQL (`docker compose stop mysql`) com o worker rodando, observar o log de erro, subir o MySQL novamente (`docker compose start mysql`) e confirmar que o worker retoma o processamento sem reinício manual.
10. Rodar `dotnet build TestOrder.slnx` e `.\scripts\test.ps1` — confirmar **46/46** sem regressão.
11. Confirmar que nenhum arquivo em `src/TestOrder.Web` ou `src/TestOrder.Api` foi alterado além do estritamente necessário e justificado.
12. Registrar comandos e resultados em `docs/PRESENTATION_GUIDE.md`.

---

## Expectativa de validação (testes automatizados condicionais)

Este módulo **não exige obrigatoriamente** uma suíte de testes automatizados para o worker:

- **Automatizado (backend, obrigatório)**: suíte existente `dotnet test TestOrder.slnx` / `.\scripts\test.ps1` DEVE continuar **46/46**, confirmando ausência de regressão no backend.
- **Automatizado (worker, condicional)**: se, na fase de planejamento/implementação, for identificada uma forma de baixo custo de testar o worker (ex.: um teste de integração simples contra um MySQL real verificando a transição `pending → processed` e a exclusão mútua via `SKIP LOCKED`), ele DEVE ser adicionado. Caso o custo/complexidade não se justifique no prazo do desafio, essa decisão DEVE ser documentada explicitamente em `AI_NOTES.md`.
- **Manual (worker, obrigatório)**: checklist de 12 passos acima, executado e registrado em `docs/PRESENTATION_GUIDE.md`, cobrindo o fluxo ponta a ponta, `npm install` condicional (AC-008), concorrência entre duas instâncias e resiliência a indisponibilidade momentânea do MySQL.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Decisão de não alterar o schema de `order_processing_events` (ou, se alterado, a justificativa específica).
- Estratégia de polling escolhida (intervalo, tamanho de lote) e por que foi considerada suficiente sem broker externo.
- Como a exclusão mútua foi implementada (`FOR UPDATE SKIP LOCKED` + atualização condicional) e evidências de que duas instâncias não processam o mesmo evento em duplicidade.
- Se testes automatizados do worker foram adicionados ou não, e por quê (custo vs. valor no prazo do desafio).
- Erros comuns da IA neste módulo (ex.: sugerir fila externa, sugerir camada de serviço/repositório desnecessária, esquecer shutdown limpo, esquecer atualização condicional de status).
- Resultados da validação manual (ponta a ponta, concorrência, resiliência a indisponibilidade do MySQL).
- Prompts Spec Kit usados neste módulo.

---

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Referências de código: arquivo(s) principais do worker em `src/TestOrder.OrderProcessor`, trecho do `SELECT ... FOR UPDATE SKIP LOCKED` e da atualização condicional de status.
- Diagrama ou frase explicando o fluxo: UI cria pedido → API grava evento `pending` na mesma transação → worker consome via polling seguro → evento `processed` + log estruturado.
- Roteiro de demo: subir tudo com `dev-up.ps1` (4 janelas), criar pedido pela UI, mostrar o log do worker e a mudança de status no banco.
- Demonstração de concorrência: duas instâncias do worker processando a mesma fila sem duplicidade.
- Observação explícita: este é o item **opcional** do desafio, implementado sem nenhuma fila externa, reaproveitando a mesma filosofia de concorrência via MySQL já usada no módulo 002.
- Tabela pass/fail dos checks manuais.

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Um pedido criado pela tela React aparece com o evento correspondente `processed` em até 10 segundos, observável tanto no console do worker quanto por consulta direta ao banco, sem nenhuma ação manual além de criar o pedido.
- **SC-002**: Em um teste com duas instâncias do worker rodando simultaneamente e múltiplos pedidos criados em sequência, **100%** dos eventos são processados exatamente uma vez (zero duplicidade, zero eventos esquecidos).
- **SC-003**: A suíte de 46 testes do backend continua passando (**0 regressões**) após a introdução deste módulo.
- **SC-004**: O ambiente completo (MySQL, API, frontend e worker) sobe com um único comando (`dev-up.ps1`), com o worker visível em janela própria e log legível.
- **SC-005**: O worker encerra sem erro não tratado nem conexão pendurada em **100%** das tentativas observadas de `Ctrl+C`.
- **SC-006**: Após uma indisponibilidade temporária do MySQL, o worker retoma o processamento normal em até um ciclo de polling após o banco voltar, sem exigir reinício manual do processo.

---

## Assumptions

- O MySQL já contém a tabela `order_processing_events` com o schema do módulo 002 (`id`, `order_id`, `event_type`, `status`, `payload`, `created_at`); nenhuma migration EF adicional é necessária, salvo justificativa explícita no plano.
- Node.js e npm já estão disponíveis no ambiente local, no mesmo padrão já assumido para o frontend no módulo 004 (pré-requisito documentado em `README.md`).
- O worker roda apenas localmente (sem Dockerfile próprio nem deploy); subir via `node` diretamente ou via `dev-up.ps1`.
- O intervalo de polling e o tamanho de lote são constantes em código (ordem de poucos segundos e lote modesto), com override opcional via variáveis de ambiente `POLL_INTERVAL_MS` e `BATCH_SIZE` — sem endpoint HTTP de configuração dinâmica.
- "Processamento" do evento é simulado via log estruturado no console — não há integração real com um serviço externo de notificação, e-mail ou enriquecimento de dados.
- Não há requisito de autenticação, multi-tenant ou múltiplos ambientes (dev/staging/prod) neste módulo.
- A criação de pedidos pela tela React (módulo 004) e pela API diretamente já gera o evento `pending` de forma consistente — nenhuma mudança é necessária nesse ponto do backend .NET.
- Testes automatizados do worker são desejáveis mas não obrigatórios; a decisão final (incluir ou não) é tomada na fase de plano/implementação e documentada, não bloqueando a entrega deste módulo.

---

## Restrições de arquitetura (contexto do desafio)

Limites obrigatórios da entrega, alinhados a `.cursor/rules/testorder.mdc` e ao contexto passado pelo usuário:

- **Node.js em JavaScript puro** (sem TypeScript), processo standalone em `src/TestOrder.OrderProcessor/`.
- **Sem framework de aplicação pesado, sem classes/service layer/repository/arquitetura em camadas** — poucos arquivos, código direto.
- **Apenas MySQL como coordenação/buffer** — proibido RabbitMQ, Kafka, Redis, BullMQ ou qualquer broker externo.
- **`SELECT ... FOR UPDATE SKIP LOCKED`** dentro de transação curta para concorrência segura entre múltiplas instâncias do worker.
- **Comunicação exclusivamente via tabela `order_processing_events`** — o backend .NET não deve chamar o worker via HTTP, nem o worker deve expor endpoints.
- **Sem alteração do backend .NET ou do frontend React** neste módulo, exceto necessidade mínima e explicitamente justificada no plano.
- **Sem quebrar os 46 testes existentes do backend.**
- **`scripts/dev-up.ps1`** DEVE ganhar uma quarta janela CMD (`TestOrder - Worker`), com `npm install` condicional à ausência de `node_modules` do worker, seguindo o padrão já estabelecido para o frontend.
- Preferência explícita por **não alterar o schema** de `order_processing_events`; qualquer campo novo exige justificativa no plano.
- Comentários no código apenas onde o comportamento de concorrência/transação/idempotência não for óbvio.
