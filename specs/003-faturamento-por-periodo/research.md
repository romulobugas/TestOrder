# Research — Módulo 003

**Data**: 2026-07-03
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## R1 — Parse estrito de datas (`yyyy-MM-dd`)

**Decision**: Validar `startDate`/`endDate` no controller com `DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)`. Falha de parse (ausente, formato errado, data inexistente como `2026-13-40`) → 400.

**Rationale**: `DateOnly` expressa a intenção (dia civil, sem hora/timezone) e o parse estrito evita aceitar formatos ambíguos (`MM/dd/yyyy`, `dd-MM-yyyy`) que o `DateTime.Parse` padrão aceitaria silenciosamente.

**Alternatives considered**:
- *`DateTime.TryParse` culture-invariant sem formato exato*: aceita formatos não solicitados pela spec; rejeitado.
- *FluentValidation/DataAnnotations*: cerimônia desnecessária para duas validações de string; validação simples em C# no controller é suficiente e consistente com módulo 002.

---

## R2 — Intervalo inclusivo em UTC via limite semiaberto

**Decision**: Interpretar `startDate`/`endDate` como dias civis em UTC. Consulta filtra `orders.created_at >= @StartUtc AND orders.created_at < @EndExclusiveUtc`, onde `StartUtc = startDate 00:00:00Z` e `EndExclusiveUtc = (endDate + 1 dia) 00:00:00Z`.

**Rationale**: Limite semiaberto (`>= inicio`, `< fim+1dia`) é a forma correta e eficiente de expressar "intervalo inclusivo por dia" em SQL sem truncar `created_at` com funções que impedem uso de índice (`DATE(created_at) BETWEEN ...` força full scan). `created_at` já é armazenado em UTC (mesmo padrão dos módulos 001/002), então não há conversão de fuso horário adicional.

**Alternatives considered**:
- *`DATE(created_at) BETWEEN @Start AND @End`*: correto funcionalmente, mas aplica função à coluna e impede uso de índice em `created_at`; rejeitado por performance em tabelas grandes.
- *Interpretar datas em horário local do servidor*: inconsistente com UTC já usado em todo o sistema; rejeitado.

---

## R3 — Preenchimento de dias sem pedido em C#, não em SQL

**Decision**: A consulta SQL agrega **apenas os dias que têm pedidos** (`GROUP BY DATE(created_at)`), retornando uma linha por dia com dados. A lista completa de dias do intervalo (incluindo os zerados) é gerada em C#, iterando de `startDate` a `endDate` e usando um dicionário `Dictionary<DateOnly, RevenueDayRow>` para preencher `revenue`/`orderCount` reais ou `0`/`0` quando o dia não aparece no resultado.

**Rationale**: Gerar série de datas em MySQL exige CTE recursiva (`WITH RECURSIVE`) ou tabela de calendário auxiliar — complexidade desnecessária para no máximo 366 dias. Preencher em C# é trivial, legível e mantém o SQL simples (uma agregação direta), alinhado à regra do projeto de manter SQL próximo do controller e sem complexidade acidental.

**Alternatives considered**:
- *`WITH RECURSIVE` gerando série de datas no MySQL*: funciona, mas adiciona complexidade de SQL para um requisito trivialmente resolvido em memória (max 366 iterações).
- *Tabela de calendário persistente*: over-engineering para o escopo do desafio.

---

## R4 — `date` da resposta como `string`, não `DateTime`

**Decision**: Os campos `date`, `startDate` e `endDate` da resposta são serializados como `string` no formato `yyyy-MM-dd` (via `DateOnly.ToString("yyyy-MM-dd")` ou equivalente), não como `DateTime`.

**Rationale**: O projeto já registra um `UtcDateTimeJsonConverter` global (`Program.cs`) que formata todo `DateTime` como ISO 8601 UTC com sufixo `Z` (usado em `createdAt` dos módulos 001/002). Como os campos de faturamento representam **dias civis**, não instantes, usar `string` evita ambiguidade/serialização incorreta (`Z` em um campo que é só data) e mantém o contrato exatamente como especificado (`"2026-06-01"`, sem hora).

**Alternatives considered**:
- *`DateOnly` diretamente no record*: `System.Text.Json` no .NET 10 já serializa `DateOnly` como `"yyyy-MM-dd"` nativamente; também seria uma opção válida. Optou-se por `string` explícito para eliminar qualquer dependência de configuração de serialização e deixar o contrato 100% explícito no código do controller/queries.

---

## R5 — Query única, sem N+1

**Decision**: Uma única consulta Dapper agrega `orders` + `order_items` via `INNER JOIN`, filtrando `status = 'created'` e o intervalo de datas, agrupando por `DATE(created_at)`. Nenhuma consulta adicional por dia ou por pedido.

**Rationale**: Atende NFR-002 (consulta única) e AC-016 (sem N+1); mesmo padrão de `OrdersQueries.PageOrders` (subselect único, sem loop de queries).

```sql
SELECT DATE(o.created_at) AS Date,
       SUM(oi.quantity * oi.unit_price) AS Revenue,
       COUNT(DISTINCT o.id) AS OrderCount
FROM orders o
INNER JOIN order_items oi ON oi.order_id = o.id
WHERE o.status = 'created'
  AND o.created_at >= @StartUtc
  AND o.created_at < @EndExclusiveUtc
GROUP BY DATE(o.created_at)
```

**Alternatives considered**:
- *Uma query para totais + outra para detalhe por dia*: duas queries são aceitáveis em geral, mas desnecessárias aqui — `totalRevenue`/`totalOrders` são derivados somando a lista de dias em C# (spec pede explicitamente isso), então uma única query basta.

---

## R6 — Limite de 366 dias

**Decision**: Calcular `diffDays = (endDate - startDate).Days` (ambos `DateOnly`). Se `diffDays < 0` → 400 (`startDate` posterior a `endDate`). Se `diffDays > 365` → 400 (intervalo excede 366 dias, já que a contagem de dias do intervalo é `diffDays + 1`).

**Rationale**: `diffDays = 365` corresponde a exatamente 366 dias no intervalo (contando ambas as extremidades) — limite aceito conforme AC-011. `diffDays = 366` (367 dias) é rejeitado conforme AC-010.

---

## R7 — Testes com pedidos de data controlada

**Decision**: Testes que precisam de pedidos em dias específicos inserem `orders`/`order_items` diretamente via SQL (Dapper/`MySqlConnector`) com `created_at` fixo, em vez de depender do `POST /api/orders` (que sempre usa `DateTime.UtcNow`) ou do volume aleatório do seed. Um teste de regressão usa o fluxo real (`POST` → `GET /api/revenue/daily`) para confirmar que um pedido recém-criado aparece no dia correto.

**Rationale**: Datas determinísticas tornam os testes de agregação (`ValidRange`, `TotalRevenue`, `EmptyRange`) estáveis e independentes de quando a suíte roda, evitando flakiness por causa da data atual do sistema.
