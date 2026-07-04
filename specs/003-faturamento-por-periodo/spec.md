# Especificação: Módulo 003 — Faturamento por Período

**Feature Branch**: `003-faturamento-por-periodo`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Implementar a funcionalidade mínima obrigatória do desafio — consultar o faturamento agregado por dia dentro de um intervalo de datas, via `GET /api/revenue/daily?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD`, somando `quantity * unitPrice` dos itens de pedidos com status `created`.

**Depende de**: Módulo 001 concluído (modelo `orders`/`order_items`, listagens, Docker, testes com Testcontainers) e Módulo 002 concluído (`POST /api/orders` gerando novos pedidos com status `created`).

> **Nota de evolução (follow-up módulo 007, 2026-07-03):** o endpoint `GET /api/revenue/daily` passou a aceitar `startDate` e `endDate` **opcionais**. Ausência de parâmetro deixa de retornar `400` e passa a significar “sem limite” naquele lado; com as duas vazias, agrega todos os dias com pedido. Zero-fill dia a dia continua **somente** quando as duas datas são informadas. Os FR-002/FR-003 e AC-006/AC-007 abaixo descrevem o comportamento **original deste módulo**; o comportamento atual está documentado em `specs/007-tela-faturamento-periodo/` e em `AI_NOTES.md` (seção follow-up).

---

## Objetivo do módulo

Permitir que um consumidor da API **consulte o faturamento bruto agregado por dia** dentro de um intervalo informado, considerando tanto os pedidos históricos do seed quanto os pedidos criados via `POST /api/orders`. "Faturamento" aqui é estritamente a soma de `quantity * unitPrice` dos itens de pedidos com status `created` — **não** é faturamento fiscal, nota fiscal, pagamento ou baixa financeira.

O módulo é uma leitura agregada pura: não altera pedidos, reservas ou estoque, e não introduz novas dependências além do banco relacional já existente.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Consumidor da API** | Consultar quanto foi faturado em um intervalo de datas, dia a dia. |
| **Avaliador do desafio** | Verificar que o requisito obrigatório de faturamento por período foi atendido com agregação SQL correta e validação de entrada. |
| **Módulos futuros (React)** | Consumir o endpoint para exibir gráfico/tabela de faturamento diário. |

Não há autenticação nem perfis de acesso neste módulo.

---

## Jornadas do módulo

### Jornada 1 — Consultar faturamento de um intervalo com pedidos (P1)

**Como** consumidor da API, **quero** informar `startDate` e `endDate`, **para** obter o faturamento total e por dia nesse intervalo.

**Por que P1**: Entrega central do módulo — requisito mínimo obrigatório do desafio.

**Teste independente**: `GET /api/revenue/daily` com intervalo válido contendo pedidos retorna 200 com `totalRevenue`, `totalOrders` e um item em `days` para cada data do intervalo.

**Cenários de aceite**:

1. **Dado** pedidos com status `created` distribuídos em várias datas, **quando** consulto um intervalo que os contém, **então** recebo HTTP 200 com `days` cobrindo cada data do intervalo (inclusive extremidades) e `revenue`/`orderCount` corretos por dia.
2. **Dado** o resultado retornado, **quando** somo `revenue` de todos os itens de `days`, **então** o total é igual a `totalRevenue`.
3. **Dado** o resultado retornado, **quando** somo `orderCount` de todos os itens de `days`, **então** o total é igual a `totalOrders`.
4. **Dado** um pedido com múltiplos itens, **quando** ele é contabilizado, **então** seu `revenue` é a soma de `quantity * unitPrice` de todos os seus itens.

---

### Jornada 2 — Consultar intervalo sem pedidos (P2)

**Como** consumidor da API, **quero** receber uma resposta consistente mesmo quando não há pedidos no intervalo, **para** exibir "sem faturamento" sem tratar como erro.

**Por que P2**: Garante previsibilidade do contrato para intervalos vazios ou parcialmente vazios (dias sem pedido dentro de um intervalo com pedidos).

**Teste independente**: `GET /api/revenue/daily` com intervalo sem nenhum pedido retorna 200 com `totalRevenue = 0`, `totalOrders = 0` e todos os dias em `days` com `revenue = 0` e `orderCount = 0`.

**Cenários de aceite**:

1. **Dado** um intervalo de datas sem nenhum pedido `created`, **quando** consulto esse intervalo, **então** recebo HTTP 200 com `days` preenchido para cada data, todos com `revenue = 0` e `orderCount = 0`.
2. **Dado** um intervalo onde apenas alguns dias têm pedidos, **quando** consulto esse intervalo, **então** os dias sem pedido aparecem em `days` com `revenue = 0` e `orderCount = 0`, e os dias com pedido aparecem com os valores agregados corretos.

---

### Jornada 3 — Rejeitar parâmetros inválidos (P1)

**Como** consumidor da API, **quero** receber erro claro quando os parâmetros de data estiverem incorretos ou ausentes, **para** corrigir a consulta sem ambiguidade.

**Por que P1**: Evita agregações incorretas ou consultas custosas por parâmetros malformados.

**Teste independente**: Diversas combinações de parâmetros inválidos retornam 400 com corpo de erro, sem exceção não tratada.

**Cenários de aceite**:

1. **Dado** `startDate` ausente, **quando** consulto o endpoint, **então** recebo HTTP 400.
2. **Dado** `endDate` ausente, **quando** consulto o endpoint, **então** recebo HTTP 400.
3. **Dado** `startDate` ou `endDate` fora do formato `YYYY-MM-DD` ou data inválida (ex.: `2026-13-40`), **quando** consulto o endpoint, **então** recebo HTTP 400.
4. **Dado** `startDate` posterior a `endDate`, **quando** consulto o endpoint, **então** recebo HTTP 400.
5. **Dado** intervalo maior que 366 dias, **quando** consulto o endpoint, **então** recebo HTTP 400.

---

### Casos de borda

- **`startDate == endDate`**: intervalo de um único dia — válido, retorna exatamente um item em `days`.
- **Intervalo de exatamente 366 dias**: deve ser aceito (limite inclusive); 367 dias ou mais deve ser rejeitado com 400.
- **Pedido criado exatamente na borda do intervalo** (`createdAt` no início do `startDate` ou fim do `endDate` em UTC): deve ser contabilizado no dia correspondente (intervalo inclusive em ambas as extremidades).
- **Pedidos com status diferente de `created`** (não existem ainda no sistema, mas a consulta deve filtrar explicitamente por `status = 'created'` para não quebrar quando cancelamento/pagamento for introduzido em módulo futuro).
- **Fuso horário**: datas de filtro são interpretadas como dias civis em UTC, consistente com `createdAt` armazenado em UTC (mesmo padrão dos módulos 001/002).
- **Muitos pedidos no intervalo**: consulta deve permanecer uma agregação SQL única (sem N+1), independentemente do volume dentro do limite de 366 dias.

---

## Requisitos funcionais

- **FR-001**: O sistema DEVE expor `GET /api/revenue/daily` aceitando `startDate` e `endDate` como query strings no formato `YYYY-MM-DD`.
- **FR-002**: O sistema DEVE retornar HTTP **400** quando `startDate` estiver ausente.
- **FR-003**: O sistema DEVE retornar HTTP **400** quando `endDate` estiver ausente.
- **FR-004**: O sistema DEVE retornar HTTP **400** quando `startDate` ou `endDate` não estiverem no formato `YYYY-MM-DD` ou representarem data inválida.
- **FR-005**: O sistema DEVE retornar HTTP **400** quando `startDate` for posterior a `endDate`.
- **FR-006**: O sistema DEVE retornar HTTP **400** quando o intervalo entre `startDate` e `endDate` exceder **366 dias**.
- **FR-007**: O sistema DEVE calcular faturamento como a soma de `quantity * unitPrice` dos itens de pedidos com status `created`, agrupado por dia de `createdAt` (UTC).
- **FR-008**: Em sucesso, o sistema DEVE retornar HTTP **200** com `startDate`, `endDate`, `totalRevenue`, `totalOrders` e `days` (lista).
- **FR-009**: Cada item de `days` DEVE conter `date`, `revenue` e `orderCount`.
- **FR-010**: O sistema DEVE retornar **todos** os dias do intervalo em `days`, incluindo dias sem pedido (`revenue = 0`, `orderCount = 0`).
- **FR-011**: O intervalo DEVE ser **inclusivo** em ambas as extremidades (`startDate` e `endDate` participam do resultado).
- **FR-012**: O endpoint DEVE considerar tanto pedidos do seed (módulo 001) quanto pedidos criados via `POST /api/orders` (módulo 002).
- **FR-013**: O endpoint DEVE permanecer em controller MVC; Minimal APIs fora de escopo.
- **FR-014**: O endpoint NÃO DEVE alterar pedidos, reservas de estoque ou unidades de inventário — leitura pura.

---

## Requisitos não funcionais

- **NFR-001 (Simplicidade)**: Implementação próxima do controller, no mesmo estilo de `OrdersQueries.cs`; sem repositories, services genéricos, interfaces, MediatR, CQRS, DDD, Clean Architecture ou AutoMapper.
- **NFR-002 (Consulta única)**: Agregação DEVE ser feita com Dapper/SQL parametrizado em consulta(s) direta(s), sem N+1 por dia ou por pedido.
- **NFR-003 (Compatibilidade)**: `GET /api/products`, `GET /api/orders`, `GET /api/orders/{id}` e `POST /api/orders` DEVEM continuar funcionando sem alteração de contrato.
- **NFR-004 (Testabilidade)**: Comportamento DEVE ser verificável com MySQL real via Testcontainers; proibido SQLite/InMemory.
- **NFR-005 (Performance observacional)**: Consulta para o intervalo máximo (366 dias) sobre o volume de dados de dev (seed + testes) DEVE responder em tempo aceitável para demo local (observacional, sem carga adicional).
- **NFR-006 (Rastreabilidade)**: Decisões de agregação e limites DEVEM ser documentáveis em `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md` após implementação.

---

## Modelo de dados esperado (alto nível)

Nenhuma nova tabela ou migration é esperada. O módulo é uma **leitura agregada** sobre estruturas existentes:

```text
orders (status = 'created') ──< order_items
        │
        └── GROUP BY DATE(orders.created_at)
             SUM(order_items.quantity * order_items.unit_price) AS revenue
             COUNT(DISTINCT orders.id) AS orderCount
```

- **`orders.created_at`**: já existe (UTC); usado para agrupar por dia civil.
- **`orders.status`**: já existe; filtro fixo `= 'created'`.
- **`order_items.quantity`**, **`order_items.unit_price`**: já existem; base do cálculo de faturamento.
- Dias sem pedido não existem na tabela — precisam ser gerados na camada de aplicação ou via série de datas em SQL para completar `days` com zeros.

---

## Contrato HTTP esperado (resumo)

Detalhamento formal ficará em `contracts/api.md` no plano; resumo para alinhamento:

### Request (exemplo)

```text
GET /api/revenue/daily?startDate=2026-06-01&endDate=2026-06-07
```

### Response 200 (exemplo)

```json
{
  "startDate": "2026-06-01",
  "endDate": "2026-06-07",
  "totalRevenue": 1250.50,
  "totalOrders": 18,
  "days": [
    { "date": "2026-06-01", "revenue": 320.00, "orderCount": 5 },
    { "date": "2026-06-02", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-03", "revenue": 930.50, "orderCount": 13 },
    { "date": "2026-06-04", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-05", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-06", "revenue": 0, "orderCount": 0 },
    { "date": "2026-06-07", "revenue": 0, "orderCount": 0 }
  ]
}
```

### Responses

| Código | Situação | Corpo |
| --- | --- | --- |
| **200** | Consulta válida (com ou sem pedidos) | Objeto de faturamento acima |
| **400** | Parâmetro ausente, inválido, `startDate > endDate` ou intervalo > 366 dias | `{ "error": "..." }` |

---

## Fora de escopo deste módulo

- Frontend React para exibir o faturamento
- Microserviço Node ou consumo de outbox
- Fila/outbox própria para este endpoint
- Autenticação e autorização
- Relatórios complexos (comparativos, exportação)
- Exportação CSV/PDF
- Agrupamento por mês, semana ou ano
- Filtros por produto, cliente ou categoria
- Alteração de pedidos, reservas de estoque ou unidades de inventário
- Faturamento fiscal, nota fiscal, pagamento ou baixa financeira

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | Intervalo válido com pedidos → 200 | `days` cobre cada data; valores agregados corretos |
| AC-002 | `totalRevenue` consistente | Soma de `revenue` de `days` = `totalRevenue` |
| AC-003 | `totalOrders` consistente | Soma de `orderCount` de `days` = `totalOrders` |
| AC-004 | Intervalo sem pedidos → 200 zerado | Todos os dias com `revenue = 0`, `orderCount = 0` |
| AC-005 | Dias sem pedido dentro de intervalo misto | Aparecem zerados junto aos dias com dados |
| AC-006 | `startDate` ausente → 400 | Query sem `startDate` |
| AC-007 | `endDate` ausente → 400 | Query sem `endDate` |
| AC-008 | Data inválida → 400 | Formato incorreto ou data inexistente |
| AC-009 | `startDate > endDate` → 400 | Datas invertidas |
| AC-010 | Intervalo > 366 dias → 400 | 367+ dias entre `startDate` e `endDate` |
| AC-011 | Intervalo = 366 dias → 200 | Limite aceito |
| AC-012 | Considera seed e `POST` | Pedido criado via `POST /api/orders` aparece no dia correto |
| AC-013 | Filtra por status `created` | Não quebra se status diferente existir futuramente |
| AC-014 | Módulos 001/002 intactos | GET products/orders/orders/{id} e POST orders continuam 200/201 |
| AC-015 | MVC mantido | Endpoint em controller, não Minimal API |
| AC-016 | Sem N+1 | Agregação em consulta(s) SQL direta(s) via Dapper |

---

## Checks manuais esperados

1. Subir ambiente: `.\scripts\dev-up.ps1`.
2. `GET /api/revenue/daily?startDate=2026-01-01&endDate=2026-12-31` (cobrindo período do seed) → 200 com `days` de 365/366 entradas.
3. Conferir que `totalRevenue` bate com soma manual (`SELECT SUM(quantity*unit_price) FROM order_items oi JOIN orders o ON o.id=oi.order_id WHERE o.status='created' AND o.created_at BETWEEN ...`).
4. `GET` com intervalo futuro sem pedidos → 200 com todos os dias zerados.
5. `GET` sem `startDate` → 400.
6. `GET` sem `endDate` → 400.
7. `GET` com `startDate` maior que `endDate` → 400.
8. `GET` com intervalo de 367+ dias → 400.
9. Criar pedido via `POST /api/orders` e confirmar que aparece no dia correto em `GET /api/revenue/daily`.
10. Confirmar regressão: `GET /api/products`, `GET /api/orders`, `GET /api/orders/{id}`, `POST /api/orders` continuam funcionando.
11. `.\scripts\test.ps1` → todos os testes do módulo 003 passando junto aos módulos 001/002.
12. Registrar comandos e resultados em `docs/PRESENTATION_GUIDE.md`.

---

## Expectativa de suíte de testes (integração)

Todos os testes DEVEM usar **MySQL real via Testcontainers** (mesmo padrão dos módulos 001/002). **Proibido** SQLite/InMemory.

| Teste | Objetivo |
| --- | --- |
| **GetDailyRevenue_ValidRange_ReturnsAggregatedDays** | Intervalo válido com pedidos → 200; `days` cobre intervalo; valores corretos |
| **GetDailyRevenue_TotalRevenue_MatchesSumOfDays** | `totalRevenue` = soma de `revenue` dos dias |
| **GetDailyRevenue_EmptyRange_ReturnsZeroedDays** | Intervalo sem pedidos → 200 com todos os dias zerados |
| **GetDailyRevenue_MissingStartDate_Returns400** | `startDate` ausente → 400 |
| **GetDailyRevenue_MissingEndDate_Returns400** | `endDate` ausente → 400 |
| **GetDailyRevenue_InvalidDate_Returns400** | Data malformada ou inexistente → 400 |
| **GetDailyRevenue_StartAfterEnd_Returns400** | `startDate > endDate` → 400 |
| **GetDailyRevenue_RangeTooLarge_Returns400** | Intervalo > 366 dias → 400 |
| **Regression_Modules001And002_StillWork** (smoke) | GET products/orders/orders/{id} e POST orders continuam funcionando |

Filtros sugeridos para execução local: `--filter GetDailyRevenue`, `--filter Regression`.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Como a spec limitou escopo (sem agrupamento por mês, sem filtros extras, sem exportação).
- Decisão sobre geração dos dias sem pedido (SQL com série de datas vs. preenchimento em C#).
- SQL de agregação escrito à mão (Dapper) vs. tentativa inicial da IA.
- Erros comuns da IA (esquecer dias zerados, N+1 por dia, timezone).
- Resultados da validação: build, test.ps1, dotnet test.
- Prompts Spec Kit usados neste módulo.

---

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Referência: controller e SQL de agregação (`RevenueController.cs` / `RevenueQueries.cs` ou nome equivalente definido no plano).
- Explicar por que faturamento aqui é bruto (`quantity * unitPrice`), não fiscal.
- Demo: `curl` com intervalo real do seed + intervalo vazio + erro 400.
- Tabela pass/fail dos checks manuais e testes.

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Um avaliador consegue consultar o faturamento de um intervalo de 30 dias e conferir o total em menos de 1 minuto.
- **SC-002**: **100%** dos intervalos válidos testados retornam `totalRevenue` e `totalOrders` consistentes com a soma dos dias.
- **SC-003**: **100%** dos parâmetros inválidos amostrados (≥ 5 variantes: ausente, malformado, invertido, excessivo) retornam erro 400.
- **SC-004**: **100%** dos intervalos sem pedidos retornam todos os dias zerados, sem erro.
- **SC-005**: Consulta do intervalo máximo (366 dias) responde em tempo aceitável para demo local (< 3 segundos, observacional).

---

## Assumptions

- Módulos 001 e 002 estão concluídos e funcionais (pedidos do seed + `POST /api/orders` compartilham a mesma tabela `orders`/`order_items`).
- "Faturamento" é estritamente bruto (`quantity * unitPrice`), sem impostos, descontos ou custos — confirmado pelo enunciado do usuário.
- Apenas pedidos com status `created` contam; não há ainda status de cancelamento neste sistema.
- Datas são interpretadas como dias civis em UTC, alinhado ao armazenamento de `createdAt`.
- Limite de 366 dias é suficiente para qualquer consulta razoável de demo (cobre até anos bissextos) e evita consultas descontroladas.
- Não há requisito de autenticação.
- Ambiente de teste continua usando `appsettings.Test.json`; testes podem inserir pedidos de controle diretamente via SQL quando precisarem de datas específicas.

---

## Restrições de arquitetura (contexto do desafio)

Limites obrigatórios da entrega, alinhados a `.cursor/rules/testorder.mdc`:

- ASP.NET Core **MVC** com controllers em `src/TestOrder.Api`; **sem Minimal APIs**.
- **Dapper ou SQL parametrizado** (`MySqlConnector`): consulta agregada de faturamento, SQL próximo ao controller (mesmo estilo de `OrdersQueries.cs`).
- **MySQL 8** real via Testcontainers nos testes; proibido SQLite/InMemory.
- Sem Clean Architecture/DDD/CQRS/mediator/repositories genéricos/AutoMapper por padrão.
- Não alterar a criação de pedido (`POST /api/orders`) nem a reserva concorrente do módulo 002.
- Comentários apenas onde o comportamento de agregação/preenchimento de dias não for óbvio.
