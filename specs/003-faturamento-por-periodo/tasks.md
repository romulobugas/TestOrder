# Tasks: Módulo 003 — Faturamento por Período

**Input**: Design documents from `specs/003-faturamento-por-periodo/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, quickstart.md

**Tests**: Incluídos — módulo exige suíte de integração com Testcontainers + MySQL real.

**Organization**: Leitura pura, sem schema novo. Fases enxutas: preflight → implementação → testes → docs → validação final.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência de tarefas incompletas)
- **[USn]**: User story de referência (ordem da spec): US1=consultar intervalo com pedidos, US2=intervalo sem pedidos, US3=rejeitar parâmetros inválidos
- Caminhos de arquivo explícitos

---

## Phase 1: Preflight

**Goal**: Proteger módulos 001/002 — confirmar build e testes verdes antes de qualquer alteração.

**Independent Test**: `dotnet build` + `dotnet test` passam sem alteração de código.

---

- [x] T001 Validar build e testes dos módulos 001/002 (`dotnet build TestOrder.slnx && dotnet test TestOrder.slnx`)

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar qualquer API local; confirmar 32/32 testes e build verde antes de qualquer mudança de código. |
| **Permitidos** | Nenhum arquivo alterado |
| **Proibidos** | Alterações de código |
| **Pronto quando** | Build + testes passam |
| **Validação** | `Get-Process TestOrder.Api -ErrorAction SilentlyContinue \| Stop-Process -Force; dotnet build TestOrder.slnx; dotnet test TestOrder.slnx` |
| **Paralelo** | Não — primeiro passo obrigatório |

**Checkpoint Phase 1**: Baseline verde confirmada.

---

## Phase 2: Contratos de resposta e consulta agregada (Foundational)

**Goal**: Criar os tipos e o SQL necessários para o endpoint, sem endpoint ainda.

**Independent Test**: `dotnet build TestOrder.slnx` compila com os novos tipos.

---

- [x] T002 [P] Adicionar `RevenueDayResponse` e `DailyRevenueResponse` em `src/TestOrder.Api/Models/Responses/ApiResponses.cs`

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | `public record RevenueDayResponse(string Date, decimal Revenue, int OrderCount);` e `public record DailyRevenueResponse(string StartDate, string EndDate, decimal TotalRevenue, int TotalOrders, IReadOnlyList<RevenueDayResponse> Days);`. Datas como `string` (`yyyy-MM-dd`), não `DateTime` — evita o `UtcDateTimeJsonConverter` global. |
| **Permitidos** | `src/TestOrder.Api/Models/Responses/ApiResponses.cs` |
| **Proibidos** | Alterar `OrderResponse`, `PagedOrdersResponse`, `ErrorResponse` existentes |
| **Pronto quando** | Compila |
| **Paralelo** | Sim — independente de T003 |

---

- [x] T003 [P] Criar `RevenueQueries.cs` com SQL agregado em `src/TestOrder.Api/Controllers/RevenueQueries.cs`

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | Classe estática com SQL const `DailyRevenueByRange` e record interno `RevenueDayRow(DateTime Date, decimal Revenue, long OrderCount)`. Query: `SELECT DATE(o.created_at) AS Date, SUM(oi.quantity * oi.unit_price) AS Revenue, COUNT(DISTINCT o.id) AS OrderCount FROM orders o INNER JOIN order_items oi ON oi.order_id = o.id WHERE o.status = 'created' AND o.created_at >= @StartUtc AND o.created_at < @EndExclusiveUtc GROUP BY DATE(o.created_at)`. Retorna somente dias com pedidos — comentário explicando o intervalo semiaberto e por que dias zerados são preenchidos depois em C# (ver research.md R2/R3). **Nota da implementação**: `OrderCount` usa `long` (não `int`) porque `COUNT(DISTINCT o.id)` retorna `BIGINT` no MySqlConnector/Dapper; o cast para `int` acontece só na resposta (`RevenueDayResponse`). |
| **Permitidos** | `src/TestOrder.Api/Controllers/RevenueQueries.cs` |
| **Proibidos** | EF Core; migrations; alterar `OrdersQueries.cs` |
| **Pronto quando** | Compila; SQL documentado no const |
| **Paralelo** | Sim — independente de T002 |

**Checkpoint Phase 2**: Tipos e SQL prontos; build verde; nenhum endpoint exposto ainda.

---

## Phase 3: Endpoint `GET /api/revenue/daily` (US1)

**Goal**: Expor o endpoint completo — parse de datas, validações 400, agregação, preenchimento de dias zerados e totais.

**Independent Test**: `GET /api/revenue/daily` com intervalo válido → 200; parâmetros inválidos → 400 (via `curl` manual nesta fase).

---

- [x] T004 [US1] Criar `RevenueController.cs` com `GET /api/revenue/daily` em `src/TestOrder.Api/Controllers/RevenueController.cs`

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | `[ApiController] [Route("api/revenue")]`. `[HttpGet("daily")]` recebe `[FromQuery] string? startDate, string? endDate`. Fluxo: `startDate`/`endDate` ausente/vazio → 400; `DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)` para ambos → 400 se falhar; `startDate > endDate` → 400; `(endDate.DayNumber - startDate.DayNumber) > 365` → 400 (máx. 366 dias); abrir `MySqlConnection`, executar `RevenueQueries.DailyRevenueByRange` com `StartUtc`/`EndExclusiveUtc` (UTC meia-noite); montar dicionário `Date → RevenueDayRow`; iterar de `startDate` a `endDate` inclusive preenchendo `RevenueDayResponse` (zero quando ausente no dicionário); `totalRevenue`/`totalOrders` somados a partir da lista final de `days`; `return Ok(new DailyRevenueResponse(...))`. |
| **Permitidos** | `src/TestOrder.Api/Controllers/RevenueController.cs` |
| **Proibidos** | Minimal APIs; alterar `OrdersController.cs`, `CreateOrderCommands.cs`, `ProductsController.cs`; repositories/services/interfaces genéricas |
| **Pronto quando** | Compila; `curl` manual retorna 200/400 conforme contrato |
| **Validação** | `dotnet build TestOrder.slnx` |
| **Paralelo** | Não — depende de T002 e T003 |

**Checkpoint Phase 3**: Endpoint funcional; build verde; módulos 001/002 intocados.

---

## Phase 4: Testes de integração

**Goal**: Suíte de **10 métodos de teste** (14 casos, incluindo `Theory`) com Testcontainers + MySQL real cobrindo US1–US3 e regressão dos módulos 001/002. Número final ajustado de 9 para 10 após o `/speckit-analyze` do módulo 003 apontar 3 lacunas de cobertura (C1, C2, C3 — ver notas em T005, T012 e T013).

**Independent Test**: `dotnet test TestOrder.slnx` — todos passam.

---

- [x] T005 [US1] Teste `GetDailyRevenue_ValidRange_ReturnsAggregatedDays` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | Adicionado `RevenueDayDto`/`DailyRevenueDto` em `tests/TestOrder.Api.Tests/Integration/ApiDtos.cs` (mesmo shape de `RevenueDayResponse`/`DailyRevenueResponse`). Inseridos via SQL direto (Dapper) 2 pedidos com `created_at` fixo em dias distintos dentro de um intervalo de 3 dias isolado (produto exclusivo deste teste): um pedido exatamente às `00:00:00` UTC do primeiro dia do intervalo e outro às `23:59:59` do último dia (cobre U1 — pedido na borda do intervalo, sem testar o microssegundo final, já coberto pelo desenho semiaberto `< end+1dia`). `GET /api/revenue/daily?startDate=...&endDate=...` → 200; `days` cobre cada data do intervalo; dias com pedido têm `revenue`/`orderCount` corretos; dia do meio sem pedido tem `0`/`0`. **Cobertura C1** (`startDate == endDate`): implementada em teste irmão dedicado `GetDailyRevenue_SingleDayRange_ReturnsExactlyOneDay` no mesmo arquivo — 200 com `days.Count == 1`, valores corretos quando há pedido no dia e zeros quando não há. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`, `tests/TestOrder.Api.Tests/Integration/ApiDtos.cs` |
| **Proibidos** | SQLite/InMemory |
| **Pronto quando** | Teste passa contra MySQL real Testcontainers |
| **Paralelo** | Não — mesmo arquivo que T006–T013; implementar em sequência |

---

- [x] T006 [US1] Teste `GetDailyRevenue_TotalRevenue_MatchesSumOfDays` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | Reaproveitando (ou inserindo novos) pedidos de data controlada, chamar o endpoint e assertar `totalRevenue == days.Sum(d => d.Revenue)` e `totalOrders == days.Sum(d => d.OrderCount)`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T005, T007–T013; implementar em sequência |

---

- [x] T007 [US2] Teste `GetDailyRevenue_EmptyRange_ReturnsZeroedDays` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | Intervalo em data futura sem nenhum pedido (ex.: ano muito à frente do seed) → 200; `totalRevenue == 0`; `totalOrders == 0`; todos os itens de `days` com `revenue == 0` e `orderCount == 0`; contagem de `days` == número de dias do intervalo. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que T005–T006, T008–T013; implementar em sequência |

---

- [x] T008 [US3] Teste `GetDailyRevenue_MissingStartDate_Returns400` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | `GET /api/revenue/daily?endDate=...` (sem `startDate`) → 400 + `ErrorDto` com `error` preenchido. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que os demais testes; implementar em sequência |

---

- [x] T009 [US3] Teste `GetDailyRevenue_MissingEndDate_Returns400` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | `GET /api/revenue/daily?startDate=...` (sem `endDate`) → 400 + `ErrorDto`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que os demais testes; implementar em sequência |

---

- [x] T010 [US3] Teste `GetDailyRevenue_InvalidDate_Returns400` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | `Theory`/`InlineData`: formato errado (`2026/01/01`, `01-01-2026`), data inexistente (`2026-13-40`, `2026-02-30`), string vazia. Todos → 400 + `ErrorDto`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que os demais testes; implementar em sequência |

---

- [x] T011 [US3] Teste `GetDailyRevenue_StartAfterEnd_Returns400` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | `startDate` posterior a `endDate` (ex.: `startDate=2026-02-01&endDate=2026-01-01`) → 400 + `ErrorDto`. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que os demais testes; implementar em sequência |

---

- [x] T012 [US3] Teste `GetDailyRevenue_RangeBoundary_AcceptsUpTo366AndRejectsOver` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | **Cobertura C2/I2** (`/speckit-analyze`): caso positivo de borda tornado **obrigatório** (não mais opcional) — mesmo teste cobre intervalo de exatamente 366 dias → 200 com `days.Count == 366` **e** intervalo de 367 dias → 400 + `ErrorDto`. Renomeado de `GetDailyRevenue_RangeTooLarge_Returns400` para `GetDailyRevenue_RangeBoundary_AcceptsUpTo366AndRejectsOver` para refletir que cobre os dois lados da borda. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que os demais testes; implementar em sequência |

---

- [x] T013 Teste `Regression_Modules001And002_StillWork` em `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | Smoke: `GET /api/products` → 200, não vazio; `GET /api/orders?page=1&pageSize=5` → 200; `GET /api/orders/{id}` → 200; `POST /api/orders` com payload válido → 201 e, em seguida, `GET /api/revenue/daily` cobrindo a data de hoje reflete esse pedido (`orderCount >= 1` no dia). **Cobertura C3** (`/speckit-analyze`): assert adicional consultando o intervalo real do seed (`2025-07-02` a `2026-07-01`, 365 dias, dentro do limite de 366) e verificando `totalOrders > 0` e `totalRevenue > 0` — confirma que o endpoint agrega também pedidos históricos do seed do módulo 001, não apenas dados de teste isolados inseridos via SQL. |
| **Permitidos** | `tests/TestOrder.Api.Tests/Integration/RevenueEndpointTests.cs` |
| **Paralelo** | Não — mesmo arquivo que os demais testes; implementar em sequência |

---

- [x] T014 Validar build e suíte completa (`dotnet build TestOrder.slnx && .\scripts\test.ps1`)

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | Parar qualquer API local antes. Executar build + `test.ps1`. Todos os testes dos módulos 001+002 (32) + módulo 003 (10 métodos / 14 casos) devem passar — **46/46** no total. |
| **Permitidos** | Nenhum arquivo alterado |
| **Pronto quando** | Exit code 0 |
| **Validação** | `Get-Process TestOrder.Api -ErrorAction SilentlyContinue \| Stop-Process -Force; dotnet build TestOrder.slnx; .\scripts\test.ps1` |
| **Paralelo** | Não — depende de T005–T013 |

**Checkpoint Phase 4**: Suíte completa verde; endpoint funcional; regressão ok.

---

## Phase 5: Documentação pós-implementação

**Goal**: Atualizar artefatos de documentação com fatos reais pós-implementação.

**Independent Test**: Revisão humana dos documentos.

---

- [x] T015 [P] Atualizar `AI_NOTES.md` com seção Módulo 003

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Status concluído; decisão de preencher dias zerados em C# (não CTE recursiva); intervalo semiaberto UTC; resultado da validação (build/test.ps1/dotnet test, contagem de testes); o que a IA sugeriu e foi recusado; prompts Spec Kit usados. |
| **Permitidos** | `AI_NOTES.md` |
| **Proibidos** | Módulos 004+ |
| **Pronto quando** | Seção 003 preenchida com fatos reais |
| **Paralelo** | Sim |

---

- [x] T016 [P] Atualizar `docs/PRESENTATION_GUIDE.md` com seção Módulo 003

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Referências de código: `RevenueController.cs`, `RevenueQueries.cs`, `RevenueEndpointTests.cs`. Roteiro demo: `curl GET /api/revenue/daily` com intervalo do seed + intervalo vazio + erro 400. Explicar que faturamento é bruto (`quantity * unitPrice`), não fiscal. Tabela pass/fail. |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Paralelo** | Sim |

---

- [x] T017 Revisar `quickstart.md` com comandos finais validados em `specs/003-faturamento-por-periodo/quickstart.md`

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar porta, datas de exemplo reais (cobrindo período efetivo do seed), números de testes e resultados de `curl`. |
| **Permitidos** | `specs/003-faturamento-por-periodo/quickstart.md` |
| **Paralelo** | Não — após validação T014 |

---

## Phase 6: Validação final

- [x] T018 Validação final obrigatória do módulo

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar checklist de aceite: build, testes (`dotnet build` + `.\scripts\test.ps1` + `dotnet test` → **46/46**). Validação manual via `curl` (200 com pedidos, 200 vazio, 400 inválido) foi **deliberadamente adiada** nesta passada por restrição de tempo do usuário ("se der tempo") — comportamento equivalente já está coberto por `GetDailyRevenue_ValidRange_ReturnsAggregatedDays`, `GetDailyRevenue_EmptyRange_ReturnsZeroedDays` e `GetDailyRevenue_InvalidDate_Returns400` na suíte automatizada. Comandos de `curl` documentados em `quickstart.md` para execução posterior se desejado. |
| **Permitidos** | Nenhuma alteração de código |
| **Pronto quando** | Build + suíte de testes passam |
| **Paralelo** | Não — último passo |

**Validações finais obrigatórias**:

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

**Validação manual (API local)**:

```powershell
# 200 — intervalo com pedidos (ajustar datas ao período do seed)
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-01-01&endDate=2026-01-31"

# 200 — intervalo sem pedidos (zerado)
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2099-01-01&endDate=2099-01-07"

# 400 — parâmetro inválido
curl -s -w "`nHTTP:%{http_code}`n" "http://localhost:5069/api/revenue/daily?startDate=2026-13-40&endDate=2026-01-31"
```

**Checkpoint Phase 6**: Módulo 003 completo.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 (preflight)
T002, T003 (parallel — responses e queries) → T004
T004 → T005 → T006 → T007 → T008 → T009 → T010 → T011 → T012 → T013 → T014
T014 → T015, T016 (parallel) → T017 → T018
```

### User Story Mapping

| Story | Prioridade | Tarefas principais |
| --- | --- | --- |
| US1 — Consultar intervalo com pedidos | P1 | T004, T005, T006 |
| US2 — Intervalo sem pedidos | P2 | T007 |
| US3 — Rejeitar parâmetros inválidos | P1 | T008–T012 |

### Parallel Opportunities

- **T002 + T003** — responses e SQL em arquivos diferentes, sem dependência mútua
- **T005 → T013** — testes em **sequência** no mesmo arquivo `RevenueEndpointTests.cs` (não paralelizar edições); T005 inclui o teste irmão `GetDailyRevenue_SingleDayRange_ReturnsExactlyOneDay` (cobertura C1)
- **T015 + T016** — docs em paralelo

---

## MVP Scope

**MVP mínimo demonstrável**: Phase 1–3 (T001–T004) + validação manual `curl` (200 com pedidos, 200 vazio, 400 inválido).

Endpoint funcional com 200/400 é a fatia mínima de valor; testes (Phase 4) são obrigatórios para aceite do módulo mas podem ser demonstrados após o `GET` funcionar.

Módulo **completo** exige Phase 4–6 (T005–T018).

---

## Implementation Strategy

### MVP First (US1 + US3 funcionais)

1. Phase 1: Preflight → baseline protegida
2. Phase 2: Responses + Query → tipos prontos, build verde
3. Phase 3: Controller → `GET` funcional, 200/400 via curl manual
4. **STOP and VALIDATE**: curl manual (200 com pedidos, 200 vazio, 400)

### Incremental Delivery

1. Phase 4: Suíte de testes → prova automatizada das 3 jornadas
2. Phase 5: Docs → apresentação pronta
3. Phase 6: Validação final → checklist completo de aceite

---

## Critérios de pronto por User Story

| Story | Critério | Evidência |
| --- | --- | --- |
| US1 | Intervalo com pedidos → 200; `totalRevenue`/`totalOrders` consistentes | T005, T006 |
| US2 | Intervalo sem pedidos → 200 com todos os dias zerados | T007 |
| US3 | 5+ variantes de parâmetro inválido → 400 | T008–T012 |

---

## Notes

- Parar API local antes de `dotnet build`/`dotnet test` no Windows (exe fica bloqueado).
- Testes de agregação (T005–T007) usam pedidos inseridos via SQL direto com `created_at` fixo, independentes do relógio do sistema e do volume do seed.
- Nenhuma migration, entidade EF ou alteração de schema neste módulo.
- `OrdersController.cs`, `CreateOrderCommands.cs`, `ProductsController.cs`, `DatabaseSeeder.cs`, `Program.cs` e migrations **não** são tocados.
- Não avançar para módulo 004+ até T018 passar.
- Commit sugerido após cada fase (T001; T002–T004; T005–T014; T015–T018).
