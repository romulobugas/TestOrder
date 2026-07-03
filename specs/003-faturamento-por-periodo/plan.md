# Plano Técnico: Módulo 003 — Faturamento por Período

**Branch**: `003-faturamento-por-periodo` | **Data**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Especificação em `specs/003-faturamento-por-periodo/spec.md`

---

## Summary

Implementar **`GET /api/revenue/daily`** no backend existente (ASP.NET Core MVC + MySQL 8), retornando faturamento bruto (`quantity * unitPrice`) agregado por dia dentro de um intervalo `startDate`/`endDate` inclusivo (`yyyy-MM-dd`, máximo 366 dias), considerando apenas pedidos com `status = 'created'` — tanto do seed (módulo 001) quanto criados via `POST /api/orders` (módulo 002). **Dapper/MySqlConnector** executa uma única consulta agregada por `DATE(created_at)`; dias sem pedido são preenchidos com zero em C#. **Nenhuma migration, entidade EF ou alteração de schema** é necessária — leitura pura sobre `orders`/`order_items` existentes. Endpoints e comportamento dos módulos 001 e 002 permanecem intactos. Suíte de integração estendida com Testcontainers (9 testes em `RevenueEndpointTests.cs`, incluindo regressão).

---

## Technical Context

| Item | Valor |
| --- | --- |
| **Language/Version** | C# / .NET 10 (`net10.0`) |
| **Primary Dependencies** | ASP.NET Core MVC, Dapper 2.x, MySqlConnector (mesmas já usadas nos módulos 001/002) |
| **Storage** | MySQL 8 (Docker Compose dev + Testcontainers testes) — **sem alteração de schema** |
| **Testing** | xUnit, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.MySql |
| **Performance Goals** | Consulta do intervalo máximo (366 dias) responde em tempo aceitável para demo local (< 3s, observacional) |
| **Constraints** | Leitura pura; sem N+1; sem Minimal APIs; sem camadas ceremoniais; não altera criação de pedido/reserva/outbox |
| **Scale/Scope** | Agregação sobre ~5000+ pedidos do seed + pedidos de teste/demo criados via `POST` |

---

## Constitution Check

*GATES: `.cursor/rules/testorder.mdc` + spec módulo 003.*

| Gate | Status | Notas |
| --- | --- | --- |
| MVC controllers (sem Minimal APIs) | ✅ PASS | `GET` em `RevenueController` novo |
| Sem migration/entidade EF nova | ✅ PASS | Leitura pura sobre schema existente |
| Dapper/SQL para consulta agregada | ✅ PASS | `RevenueQueries.cs` adjacente ao controller |
| MySQL 8 real nos testes | ✅ PASS | Testcontainers; proibido SQLite/InMemory |
| Sem Clean Architecture/DDD/CQRS/MediatR | ✅ PASS | Controller + SQL estático, sem camadas |
| Sem repositories/AutoMapper/interfaces genéricas | ✅ PASS | Records de response simples, sem mapper |
| Preservar contratos módulos 001/002 | ✅ PASS | Nenhuma alteração em `OrdersController`/`ProductsController` |
| Não altera criação de pedido/reserva/outbox | ✅ PASS | Módulo 003 é read-only; `CreateOrderCommands.cs` intocado |
| Escopo: sem React/Node/auth/exportação | ✅ PASS | Apenas endpoint JSON de agregação |

**Pós-design (Phase 1)**: Nenhuma violação. Nenhuma tabela nova é necessária — todo o dado já existe em `orders`/`order_items`.

---

## Project Structure

### Documentação (esta feature)

```text
specs/003-faturamento-por-periodo/
├── spec.md
├── plan.md                 # este arquivo
├── research.md             # Phase 0
├── data-model.md           # sem novas entidades — documenta leitura
├── quickstart.md           # validação GET + testes
├── contracts/
│   └── api.md              # GET /api/revenue/daily + referência aos módulos anteriores
├── checklists/
│   └── requirements.md     # da /speckit-specify
└── tasks.md                # (/speckit-tasks — próximo passo)
```

### Código-fonte — delta sobre módulos 001/002

```text
F:\repository\TestOrder\
└── src/TestOrder.Api/
    ├── Controllers/
    │   ├── RevenueController.cs        # NOVO — GET /api/revenue/daily
    │   └── RevenueQueries.cs           # NOVO — SQL agregado + row record
    └── Models/
        └── Responses/ApiResponses.cs   # + DailyRevenueResponse, RevenueDayResponse

tests/TestOrder.Api.Tests/
└── Integration/
    └── RevenueEndpointTests.cs         # NOVO — 9 testes (agregação + validação + regressão)
```

**Nenhum arquivo de `Data/`, `Migrations/` ou `Data/Entities/` é criado ou alterado.** `Program.cs`, `OrdersController.cs`, `CreateOrderCommands.cs`, `TestOrderDbContext.cs` permanecem intocados.

**Structure Decision**: Mesma fatia vertical dos módulos anteriores. **Um** controller novo (`RevenueController.cs`) + **um** arquivo SQL (`RevenueQueries.cs`) ao lado de `OrdersController.cs`/`OrdersQueries.cs`. Sem pasta `Services/`, `Repositories/` ou `Application/`.

---

## Decisões de implementação

| # | Decisão | Implementação |
| --- | --- | --- |
| 1 | `GET /api/revenue/daily` | `[HttpGet("daily")]` em `RevenueController` novo, rota base `api/revenue` |
| 2 | Parse de data | `DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)` no controller |
| 3 | Ausência de parâmetro | `[FromQuery] string? startDate/endDate` — `null`/vazio → 400 antes de tentar parse |
| 4 | `startDate > endDate` | Comparação de `DateOnly` após parse bem-sucedido → 400 |
| 5 | Limite 366 dias | `(endDate.DayNumber - startDate.DayNumber) <= 365` → senão 400 (ver [research.md](./research.md) R6) |
| 6 | Intervalo inclusivo UTC | SQL usa `created_at >= StartUtc AND created_at < EndExclusiveUtc` (semiaberto) |
| 7 | Filtro de status | SQL fixa `WHERE o.status = 'created'` |
| 8 | Agregação sem N+1 | Uma query `GROUP BY DATE(created_at)`; ver [research.md](./research.md) R5 |
| 9 | Dias zerados | Preenchidos em C# a partir do intervalo, não em SQL (ver [research.md](./research.md) R3) |
| 10 | `totalRevenue`/`totalOrders` | Somados a partir da lista final de `days` (já com zeros), não segunda query |
| 11 | Formato de data na resposta | `string` `yyyy-MM-dd` (não `DateTime`) para não colidir com `UtcDateTimeJsonConverter` global (ver [research.md](./research.md) R4) |

---

## Fluxo GET (sequência)

```text
1. [FromQuery] string? startDate, string? endDate
2. startDate ausente/vazio → 400
3. endDate ausente/vazio → 400
4. Parse estrito yyyy-MM-dd de ambos → 400 se falhar (qualquer um)
5. startDate > endDate → 400
6. (endDate.DayNumber - startDate.DayNumber) > 365 → 400
7. connection.OpenAsync()
8. Query agregada única: SELECT DATE(created_at), SUM(qty*price), COUNT(DISTINCT order_id)
   FROM orders JOIN order_items WHERE status='created' AND created_at >= start AND created_at < end+1dia
   GROUP BY DATE(created_at)
9. Montar dicionário Date → (Revenue, OrderCount) a partir das linhas retornadas
10. Iterar de startDate a endDate (inclusive); para cada dia, buscar no dicionário ou usar (0, 0)
11. totalRevenue = soma dos days[].revenue; totalOrders = soma dos days[].orderCount
12. return 200 Ok(DailyRevenueResponse)
```

Comentários no código **somente** no passo 8 (motivo do intervalo semiaberto e do `GROUP BY` sem preenchimento de zeros em SQL).

---

## Contratos JSON

Detalhamento em [contracts/api.md](./contracts/api.md).

| Endpoint | Novo? | Resumo |
| --- | --- | --- |
| `GET /api/revenue/daily` | **Sim** | Query `startDate`, `endDate` → 200 `DailyRevenueResponse` / 400 |
| `GET /api/products` | Não | Inalterado |
| `GET /api/orders` | Não | Inalterado |
| `GET /api/orders/{id}` | Não | Inalterado |
| `POST /api/orders` | Não | Inalterado |

**Response records** (`Models/Responses/ApiResponses.cs`):

```csharp
public record RevenueDayResponse(string Date, decimal Revenue, int OrderCount);

public record DailyRevenueResponse(
    string StartDate,
    string EndDate,
    decimal TotalRevenue,
    int TotalOrders,
    IReadOnlyList<RevenueDayResponse> Days);
```

**Row de query** (`RevenueQueries.cs`, interno):

```csharp
internal sealed record RevenueDayRow(DateTime Date, decimal Revenue, int OrderCount);
```

---

## Estratégia da suíte de testes

Estender `tests/TestOrder.Api.Tests` — mesma `MySqlContainerFixture` e collection dos módulos anteriores.

| Teste | Assert principal |
| --- | --- |
| `GetDailyRevenue_ValidRange_ReturnsAggregatedDays` | Pedidos de teste com `created_at` fixo em dias distintos → 200; `days` cobre intervalo; valores corretos nos dias com pedido |
| `GetDailyRevenue_TotalRevenue_MatchesSumOfDays` | `totalRevenue` = Σ `days[].revenue`; `totalOrders` = Σ `days[].orderCount` |
| `GetDailyRevenue_EmptyRange_ReturnsZeroedDays` | Intervalo futuro sem pedidos → 200, todos os dias `revenue=0`/`orderCount=0` |
| `GetDailyRevenue_MissingStartDate_Returns400` | Sem `startDate` → 400 + `ErrorResponse` |
| `GetDailyRevenue_MissingEndDate_Returns400` | Sem `endDate` → 400 + `ErrorResponse` |
| `GetDailyRevenue_InvalidDate_Returns400` | `Theory`: formato errado, data inexistente → 400 |
| `GetDailyRevenue_StartAfterEnd_Returns400` | `startDate` > `endDate` → 400 |
| `GetDailyRevenue_RangeTooLarge_Returns400` | 367+ dias → 400; 366 dias exatos → 200 (caso positivo de borda no mesmo teste ou teste irmão) |
| `Regression_Modules001And002_StillWork` | GET products/orders/orders/{id} → 200; `POST /api/orders` → 201 e aparece no dia correto via `GET /api/revenue/daily` |

**Dados de teste determinísticos**: pedidos com `created_at` específico inseridos via SQL direto (Dapper/`MySqlConnector`) para os testes de agregação, evitando depender do relógio do sistema ou do volume/distribuição aleatória do seed (ver [research.md](./research.md) R7).

---

## Phase 0 & Phase 1 — Artefatos gerados

| Artefato | Status |
| --- | --- |
| [research.md](./research.md) | ✅ |
| [data-model.md](./data-model.md) | ✅ (sem novas entidades) |
| [contracts/api.md](./contracts/api.md) | ✅ |
| [quickstart.md](./quickstart.md) | ✅ |

---

## Documentação pós-implementação (não fazer neste passo)

### `AI_NOTES.md` — seção Módulo 003 (template)

Preencher após implementação:

- Status concluído e dependência dos módulos 001/002
- Por que dias zerados são preenchidos em C# e não com série de datas em SQL
- Por que o intervalo usa limite semiaberto (`>=`/`<`) em vez de `DATE(created_at) BETWEEN`
- Resultado da validação (build, test.ps1, dotnet test, contagem de testes)
- O que a IA sugeriu e foi recusado (CTE recursiva, services/repositories)
- Prompts Spec Kit usados neste módulo

### `docs/PRESENTATION_GUIDE.md` — adições

- Linha na tabela de referências: `RevenueController.cs`, `RevenueQueries.cs`, `RevenueEndpointTests.cs`
- Roteiro demo: `curl GET /api/revenue/daily` com intervalo do seed + intervalo vazio + erro 400
- Explicar que faturamento é bruto (`quantity * unitPrice`), não fiscal
- Tabela pass/fail dos testes do módulo 003

---

## Complexity Tracking

*Nenhuma violação de constitution a justificar. Módulo reduz complexidade em vez de aumentar (sem schema novo).*

---

## Próximos passos

1. **`/speckit-tasks`** — gerar `tasks.md` com fases: controller/query → validação → testes → docs
2. **`/speckit-analyze`** — revisão read-only antes do implement
3. **`/speckit-implement`** — uma fatia por vez
4. Não avançar para React/Node/módulos futuros até fechar T* do módulo 003

---

## Referências cruzadas

| Documento | Uso |
| --- | --- |
| [spec.md](./spec.md) | Requisitos e critérios de aceite |
| [research.md](./research.md) | Decisões R1–R7 |
| [data-model.md](./data-model.md) | Confirma ausência de novo schema |
| [contracts/api.md](./contracts/api.md) | Contrato GET |
| [quickstart.md](./quickstart.md) | Validação manual e testes |
| [../002-criacao-pedido-reservas/plan.md](../002-criacao-pedido-reservas/plan.md) | Padrão de fatia vertical Dapper + controller |
| [../001-base-listagem-pedidos/plan.md](../001-base-listagem-pedidos/plan.md) | Base infra/scripts/testes |
