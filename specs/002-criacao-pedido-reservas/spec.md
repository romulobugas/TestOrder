# Especificação: Módulo 002 — Criação de Pedido com Reserva Concorrente

**Feature Branch**: `002-criacao-pedido-reservas`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Implementar `POST /api/orders` com validação de itens, reserva transacional de estoque usando concorrência MySQL 8 (`FOR UPDATE SKIP LOCKED`), e registro de evento pendente em outbox para processamento futuro pelo microserviço Node — sem fila externa.

**Depende de**: Módulo 001 concluído (modelo base, seed, listagens, Docker, testes com Testcontainers).

**Referência de concorrência**: [Scaling inventory reservations (Shopify Engineering)](https://shopify.engineering/scaling-inventory-reservations)

---

## Objetivo do módulo

Permitir a **criação de pedidos** via API com **reserva segura de estoque** sob concorrência, usando exclusivamente o banco relacional MySQL 8 como mecanismo de coordenação. Ao concluir, um consumidor da API deve conseguir enviar um pedido com múltiplos itens, receber confirmação com o pedido criado (201), ou erros claros quando o payload for inválido (400) ou quando não houver unidades disponíveis para reserva (409).

O módulo também **prepara** o processamento assíncrono futuro registrando um evento `pending` em tabela de outbox (`order_processing_events`), sem implementar o microserviço Node nem consumir a fila neste módulo.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Consumidor da API** | Criar pedidos com itens válidos e obter resposta previsível (sucesso, validação ou conflito de estoque). |
| **Avaliador do desafio** | Verificar reserva concorrente real (não apenas decremento cego), transação curta, outbox pendente e testes de concorrência. |
| **Módulo futuro (Node / outbox)** | Encontrar eventos `pending` associados a pedidos recém-criados, prontos para processamento posterior. |
| **Módulos futuros (React, faturamento)** | Reutilizar pedidos criados via API e contrato estável de resposta 201 alinhado à listagem do módulo 001. |

Não há autenticação nem perfis de acesso neste módulo.

---

## Jornadas do módulo

### Jornada 1 — Criar pedido com sucesso (P1)

**Como** consumidor da API, **quero** enviar um pedido com identificação simples do cliente e lista de itens (produto + quantidade), **para** registrar a compra e reservar estoque de forma atômica.

**Por que P1**: Entrega central do desafio (“criar pedido”) e pré-requisito para demo de concorrência.

**Teste independente**: `POST /api/orders` com payload válido retorna 201, pedido persistido com itens, unidades reservadas e evento outbox `pending`.

**Cenários de aceite**:

1. **Dado** produtos existentes com unidades disponíveis suficientes, **quando** envio `POST /api/orders` com cliente e itens válidos, **então** recebo HTTP 201 com o pedido criado (identificador, data de criação, status, itens, total).
2. **Dado** pedido criado com sucesso, **quando** consulto `GET /api/orders/{id}`, **então** o pedido retornado coincide com o criado (itens, quantidades, preços de linha, total).
3. **Dado** pedido criado, **quando** inspeciono reservas, **então** existem registros ligando o pedido às unidades de inventário reservadas, na quantidade solicitada por produto.
4. **Dado** pedido criado, **quando** inspeciono a outbox, **então** existe evento com status `pending` referenciando o pedido, criado na mesma transação.

---

### Jornada 2 — Rejeitar payload inválido (P1)

**Como** consumidor da API, **quero** receber erro claro quando o corpo da requisição estiver incorreto, **para** corrigir o envio sem efeitos colaterais no estoque.

**Por que P1**: Evita pedidos parciais ou reservas incorretas por dados malformados.

**Teste independente**: Diversos payloads inválidos retornam 400 sem criar pedido, reserva ou evento.

**Cenários de aceite**:

1. **Dado** corpo ausente ou JSON inválido, **quando** envio `POST /api/orders`, **então** recebo HTTP 400 com mensagem de erro compreensível.
2. **Dado** lista de itens vazia, **quando** envio a requisição, **então** recebo HTTP 400.
3. **Dado** item com `productId` inexistente, quantidade ≤ 0 ou não inteira, **quando** envio a requisição, **então** recebo HTTP 400.
4. **Dado** payload inválido, **quando** a requisição é rejeitada, **então** nenhum pedido, reserva ou evento outbox é persistido.

---

### Jornada 3 — Conflito por estoque insuficiente (P1)

**Como** consumidor da API, **quero** saber quando não há unidades disponíveis para atender o pedido, **para** tratar o conflito (retry, reduzir quantidade, informar usuário) sem overselling.

**Por que P1**: Demonstra integridade de estoque sob demanda concorrente.

**Teste independente**: Pedido que exige mais unidades do que disponíveis retorna 409; banco permanece consistente (rollback completo).

**Cenários de aceite**:

1. **Dado** produto com N unidades disponíveis, **quando** solicito quantidade > N desse produto em um pedido, **então** recebo HTTP 409 com mensagem indicando indisponibilidade de estoque/reserva.
2. **Dado** pedido com múltiplos produtos e falta de estoque em **qualquer** item, **quando** envio a requisição, **então** recebo HTTP 409 e **nenhuma** reserva parcial permanece (rollback total).
3. **Dado** resposta 409, **quando** inspeciono o banco, **então** contagem de unidades disponíveis e reservas permanece como antes da tentativa.

---

### Jornada 4 — Reserva concorrente (P1)

**Como** avaliador ou sistema sob carga, **quero** que múltiplas requisições simultâneas disputem as mesmas unidades, **para** comprovar que o mecanismo evita double-booking sem fila externa.

**Por que P1**: Diferencial técnico do desafio e motivo da escolha MySQL 8 + `SKIP LOCKED`.

**Teste independente**: Teste automatizado dispara requisições paralelas contra produto com estoque limitado; soma das quantidades reservadas com sucesso não excede unidades disponíveis.

**Cenários de aceite**:

1. **Dado** produto com U unidades disponíveis e M requisições concorrentes cujo total demandado > U, **quando** as requisições completam, **então** algumas retornam 201 e outras 409, e o total de unidades reservadas ≤ U.
2. **Dado** requisições concorrentes, **quando** todas têm sucesso, **então** cada unidade de inventário está reservada para no máximo um pedido.
3. **Dado** contenção no mesmo produto, **quando** o sistema processa reservas, **então** não ocorre deadlock persistente (ordem consistente de produtos na transação).

---

### Casos de borda

- **Mesmo produto repetido no payload**: rejeitar com 400 ou normalizar somando quantidades — preferir **rejeição explícita** se houver `productId` duplicado na mesma requisição (comportamento documentado no plano/contrato).
- **Pedido com um único item vs. vários itens**: ambos devem funcionar; reserva percorre produtos em ordem determinística (`product_id` ascendente).
- **Quantidade exatamente igual ao estoque disponível**: deve retornar 201 e esgotar unidades daquele produto para novos pedidos.
- **Produto sem nenhuma unidade em `inventory_units`**: 409 mesmo que `products.stock_quantity` legado indique valor positivo (fonte de verdade passa a ser unidades bloqueáveis).
- **Falha de banco mid-transaction**: rollback; nenhum pedido órfão, reserva parcial ou evento outbox sem pedido.
- **Preço na linha**: capturar preço unitário do catálogo no momento da criação (snapshot), consistente com pedidos do seed do módulo 001.
- **Cliente ausente ou vazio**: definir no plano se `customerName` é obrigatório ou opcional; se opcional, permitir string vazia ou omitir campo (preferir **opcional** com default documentado).
- **Requisição duplicada (retry idempotente)**: fora de escopo; retries do cliente podem criar pedidos duplicados — documentar como limitação conhecida.

---

## Requisitos funcionais

- **FR-001**: O sistema DEVE expor `POST /api/orders` aceitando corpo JSON com identificação simples de cliente (ex.: `customerName`) e lista `items` (`productId`, `quantity`).
- **FR-002**: O sistema DEVE validar o payload e retornar HTTP **400** quando JSON inválido, itens ausentes/vazios, `productId` inexistente, quantidade inválida ou violação de regras documentadas (ex.: produto duplicado na mesma requisição).
- **FR-003**: O sistema DEVE criar pedido, itens de pedido, reservas de unidades e evento outbox em **uma única transação** de banco.
- **FR-004**: Para cada item, o sistema DEVE reservar exatamente `quantity` unidades disponíveis do produto, usando linhas bloqueáveis em inventário (não decremento cego isolado em `products.stock_quantity`).
- **FR-005**: Se qualquer produto não tiver unidades suficientes, o sistema DEVE abortar a transação e retornar HTTP **409** sem efeitos parciais.
- **FR-006**: Em sucesso, o sistema DEVE retornar HTTP **201** com representação do pedido criado alinhada ao contrato de leitura do módulo 001 (itens aninhados, total calculado, `createdAt` em UTC com sufixo `Z`).
- **FR-007**: Cada item criado DEVE persistir `quantity` e `unitPrice` snapshot do produto no momento da criação.
- **FR-008**: O pedido criado DEVE iniciar com status coerente (ex.: `created`), documentado no plano.
- **FR-009**: O sistema DEVE inserir registro em `order_processing_events` com status **`pending`**, referência ao pedido e payload mínimo para processamento futuro (ex.: `orderId`), na mesma transação do pedido.
- **FR-010**: O sistema NÃO DEVE processar, consumir ou marcar eventos outbox como concluídos neste módulo.
- **FR-011**: Endpoints DEVEM permanecer em controllers MVC; Minimal APIs fora de escopo.
- **FR-012**: Schema e migrações das novas tabelas DEVEM ser aplicáveis sobre base existente do módulo 001 (evolução, não reescrita).

---

## Requisitos não funcionais

- **NFR-001 (Simplicidade)**: Manter código legível em poucos arquivos; evitar Clean Architecture, DDD tático, CQRS, mediator, repositórios genéricos, AutoMapper e interfaces sem necessidade demonstrada.
- **NFR-002 (Transação curta)**: A transação de criação/reserva/outbox DEVE conter apenas operações de banco; nenhuma chamada externa, I/O lento ou processamento assíncrono dentro da transação.
- **NFR-003 (Isolamento)**: Usar isolamento **`READ COMMITTED`** na transação de criação (comportamento padrão MySQL/InnoDB, documentado explicitamente no plano).
- **NFR-004 (Concorrência)**: Reserva DEVE usar `SELECT ... FOR UPDATE SKIP LOCKED` sobre unidades disponíveis; ordem de processamento por **`product_id` ascendente** dentro da transação para reduzir deadlock.
- **NFR-005 (Coordenação)**: Usar **somente** o banco relacional; proibido RabbitMQ, Kafka, Redis ou fila externa neste módulo.
- **NFR-006 (Rastreabilidade)**: Decisões de modelo de reserva, SQL transacional e outbox DEVEM ser documentáveis em `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md` após implementação.
- **NFR-007 (Testabilidade)**: Comportamento DEVE ser verificável com MySQL real via Testcontainers; proibido SQLite/InMemory como substituto do banco principal nos testes de integração.
- **NFR-008 (Compatibilidade)**: Listagens do módulo 001 (`GET /api/products`, `GET /api/orders`) DEVEM continuar funcionando após evolução do schema e seed/migration de unidades.
- **NFR-009 (Explicabilidade)**: A abordagem `inventory_units` + `order_reservation_units` DEVE ser apresentável em demo presencial em poucos minutos, com SQL visível próximo ao controller.

---

## Modelo de dados esperado (alto nível)

Manter entidades existentes: `products`, `orders`, `order_items`.

### Novas estruturas (preferência do projeto)

```text
products (1) ──< inventory_units (N)     [unidade disponível ou reservada]
                      │
                      │ reserva via
                      v
order_reservation_units (N) >── (1) orders
                      │
                      └── liga unidade reservada ao pedido

orders (1) ──< order_processing_events   [outbox, status pending]
```

#### `inventory_units`

| Conceito | Descrição |
| --- | --- |
| Identificador | Chave primária |
| Produto | Referência ao produto |
| Status | Ex.: `available`, `reserved` — valores exatos no plano |
| Controle de concorrência | Linhas `available` selecionáveis com `FOR UPDATE SKIP LOCKED` |

Cada unidade vendável é uma linha bloqueável (modelo inspirado em reservas por unidade, não contador único).

#### `order_reservation_units`

| Conceito | Descrição |
| --- | --- |
| Pedido | Referência ao pedido |
| Unidade de inventário | Referência à unidade reservada |
| Propósito | Auditoria e ligação explícita pedido ↔ unidade |

#### `order_processing_events` (outbox)

| Conceito | Descrição |
| --- | --- |
| Identificador | Chave primária |
| Pedido | Referência ao pedido criado |
| Status | `pending` neste módulo; transições futuras no módulo Node |
| Payload | JSON mínimo (ex.: tipo de evento + `orderId`) |
| Timestamps | Criação (e opcionalmente processamento futuro) |

#### Evolução de `products`

- `stock_quantity` pode permanecer como indicador legado ou derivado, mas **não** como único mecanismo de reserva concorrente.
- Migration/seed do módulo 002 DEVE popular `inventory_units` a partir do estoque existente (backfill), documentado no plano.

#### `orders` (extensão opcional mínima)

- Campo simples de cliente (ex.: `customer_name`) se não aumentar complexidade desproporcional.

**Regra de total**: permanece derivado dos itens (sem coluna `total` persistida como fonte primária).

---

## Contrato HTTP esperado (resumo)

Detalhamento formal ficará em `contracts/api.md` no plano; resumo para alinhamento:

### Request (exemplo)

```json
{
  "customerName": "Cliente Demo",
  "items": [
    { "productId": 1, "quantity": 2 },
    { "productId": 3, "quantity": 1 }
  ]
}
```

### Responses

| Código | Situação | Corpo |
| --- | --- | --- |
| **201** | Pedido criado | Pedido com itens e total (mesmo shape da leitura) |
| **400** | Payload inválido | `{ "error": "..." }` |
| **409** | Estoque/reserva insuficiente | `{ "error": "..." }` |

---

## Fora de escopo deste módulo

- Frontend React e formulário de criação
- Faturamento por período (`GET /api/revenue`)
- Microserviço Node consumindo outbox (apenas preparar eventos `pending`)
- Envio de e-mail, notificação ou pagamento
- Autenticação e autorização
- Dockerfile da API ou compose completo além do já existente
- Refatoração arquitetural ampla do módulo 001
- Idempotência de `POST` / deduplicação por chave de cliente
- RabbitMQ, Kafka, Redis

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | `POST` válido retorna 201 | Payload com estoque suficiente → 201 + corpo com id e itens |
| AC-002 | Pedido persistido | `GET /api/orders/{id}` reflete pedido criado |
| AC-003 | Reserva registrada | Contagem em `order_reservation_units` = soma das quantidades por produto |
| AC-004 | Outbox pendente | Registro em `order_processing_events` com status `pending` para o pedido |
| AC-005 | Payload inválido → 400 | Itens vazios, qty inválida, produto inexistente |
| AC-006 | Sem efeito colateral em 400 | Contagens de pedidos/reservas/unidades inalteradas |
| AC-007 | Estoque insuficiente → 409 | Quantidade > unidades `available` |
| AC-008 | Rollback em 409 | Nenhuma reserva parcial após 409 |
| AC-009 | Concorrência segura | Teste paralelo: reservas totais ≤ unidades iniciais |
| AC-010 | Transação única | Pedido + itens + reservas + outbox commitados juntos ou nenhum |
| AC-011 | SKIP LOCKED usado | SQL de reserva documentado/revisável (não UPDATE cego em contador) |
| AC-012 | Ordem por product_id | Documentado no plano/SQL para reduzir deadlock |
| AC-013 | Listagens 001 intactas | `GET /api/products` e `GET /api/orders` continuam passando |
| AC-014 | Testes integração | Suíte verde com Testcontainers MySQL |
| AC-015 | MVC mantido | Endpoint em controller, não Minimal API |

---

## Checks manuais esperados

1. Subir ambiente: `.\scripts\dev-up.ps1` (após migration do módulo 002).
2. Confirmar backfill: `SELECT COUNT(*) FROM inventory_units WHERE status = 'available'` coerente com estoque esperado.
3. `POST /api/orders` com 1 item válido → 201; anotar `id`.
4. `GET /api/orders/{id}` → mesmo pedido, total = soma dos itens, `createdAt` com `Z`.
5. Inspecionar SQL: reservas em `order_reservation_units` e evento `pending` em `order_processing_events`.
6. `POST` com `quantity` excessiva → 409; confirmar que contagem de unidades disponíveis não mudou.
7. `POST` com itens vazios ou `productId` inválido → 400.
8. (Opcional demo concorrência) Duas janelas/terminais: disparar pedidos simultâneos no último estoque de um produto — apenas um 201, outro 409.
9. `.\scripts\test.ps1` → todos os testes do módulo 002 passando.
10. Registrar comandos e resultados em `docs/PRESENTATION_GUIDE.md`.

---

## Expectativa de suíte de testes (integração)

Todos os testes DEVEM usar **MySQL real via Testcontainers** (mesmo padrão do módulo 001). **Proibido** SQLite/InMemory como banco principal.

| Teste | Objetivo |
| --- | --- |
| **CreateOrder_Success** | Payload válido → 201; pedido consultável; reservas e outbox `pending` existem |
| **CreateOrder_InvalidPayload_Returns400** | Cenários: itens vazios, qty ≤ 0, produto inexistente, JSON malformado |
| **CreateOrder_InsufficientStock_Returns409** | Quantidade maior que unidades disponíveis; sem reserva parcial |
| **CreateOrder_ConcurrentRequests_DoNotOverbook** | N requisições paralelas no mesmo produto com estoque limitado; soma das reservas bem-sucedidas ≤ estoque inicial |
| **CreateOrder_WritesPendingOutboxEvent** | Após 201, evento `pending` ligado ao `orderId` |
| **Regression_Module001_ReadEndpoints** (opcional smoke) | Garantir que listagens existentes não regrediram após migration |

Filtros sugeridos para execução local: `--filter CreateOrder`, `--filter Concurrent`, `--filter Outbox`.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Como a spec limitou escopo (sem Node, sem fila externa, sem decremento cego).
- Decisão humana sobre modelo `inventory_units` vs. alternativas mais simples.
- Onde a IA sugeriu camadas extras (repository, domain services) e o que foi recusado.
- SQL transacional com `SKIP LOCKED` — o que foi escrito à mão vs. EF.
- Estratégia de backfill de unidades a partir do seed do módulo 001.
- Resultados do teste de concorrência (N threads, estoque inicial, 201 vs. 409).
- Erros comuns da IA (deadlock, reserva parcial, esquecer outbox na mesma transação).
- Prompts Spec Kit usados neste módulo.

---

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Referências: controller `POST`, SQL de reserva, migration novas tabelas, seeder/backfill de unidades.
- Diagrama ou frase explicando fluxo: validar → abrir transação → reservar por produto (ordem `product_id`) → inserir pedido/itens/reservas/outbox → commit.
- Trecho ou pseudocódigo do `SELECT ... FOR UPDATE SKIP LOCKED`.
- Link para artigo Shopify e o que foi adaptado em escala reduzida.
- Demo: comando `curl` de criação + query SQL mostrando unidade reservada e evento pending.
- Tabela pass/fail dos checks manuais e testes (incl. concorrência).
- Observação: outbox será consumida no módulo Node (referência forward).

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Um avaliador consegue criar um pedido válido via API e vê-lo na listagem em menos de 3 minutos após subir o ambiente (excluindo primeira migration longa).
- **SC-002**: Em teste automatizado de concorrência com estoque conhecido E, **100%** das execuções respeitam: unidades reservadas ≤ E (zero overbooking).
- **SC-003**: **100%** dos payloads inválidos amostrados (≥ 5 variantes) retornam erro 400 sem alterar contagem de pedidos.
- **SC-004**: **100%** das tentativas com estoque insuficiente retornam 409 sem reserva parcial persistida.
- **SC-005**: **100%** dos pedidos criados com sucesso possuem exatamente um evento outbox `pending` associado.
- **SC-006**: Tempo de resposta de `POST` bem-sucedido em demo local permanece aceitável (< 3 segundos para pedido típico de até 5 itens, observacional).

---

## Assumptions

- Módulo 001 está mergeado em `main` com MySQL 8, Docker, seed e listagens funcionais.
- Unidades de inventário serão materializadas (backfill) a partir do estoque existente em produtos; pedidos históricos do seed **não** consomem unidades retroativamente — apenas novos pedidos via `POST` reservam unidades (comportamento documentado no plano se diferente).
- `customerName` é string simples opcional ou obrigatória conforme contrato final; ausência não bloqueia demo se documentado.
- Status inicial do pedido: `created`.
- Evento outbox: tipo fixo documentado no plano (ex.: `OrderCreated`).
- Não há requisito de autenticação.
- Ambiente de teste continua usando `appsettings.Test.json` com volume reduzido, mas testes de concorrência podem usar estoque controlado inserido no próprio teste.
- Preço na linha segue catálogo atual no momento do `POST`.

---

## Restrições de arquitetura (contexto do desafio)

Limites obrigatórios da entrega, alinhados a `.cursor/rules/testorder.mdc`:

- ASP.NET Core **MVC** com controllers em `src/TestOrder.Api`; **sem Minimal APIs**.
- **EF Core**: evolução de schema/migrations e entidades; seed/backfill de unidades conforme plano.
- **Dapper ou SQL parametrizado** (`MySqlConnector`): transação de criação/reserva/outbox — SQL próximo ao controller.
- **MySQL 8** com `FOR UPDATE SKIP LOCKED`; isolamento `READ COMMITTED`.
- **Sem** RabbitMQ, Kafka, Redis.
- Código simples, sem Clean Architecture/DDD/CQRS/mediator/repositories genéricos/AutoMapper por padrão.
- Comentários apenas onde o comportamento de concorrência/transação não for óbvio.
