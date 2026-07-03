# Research — Módulo 001

**Data**: 2026-07-03  
**Spec**: [spec.md](./spec.md)

## R1 — ORM vs SQL direto na listagem

**Decision**: EF Core para schema, migrations e seed; Dapper com SQL parametrizado para `GET /api/products`, `GET /api/orders` e `GET /api/orders/{id}`.

**Rationale**: O desafio pede justificativa explícita da divisão EF/Dapper. EF reduz boilerplate de modelo e seed; Dapper deixa visível join, paginação e agregação de total — pontos que o avaliador vai perguntar na apresentação.

**Alternatives considered**:
- *Só EF Core com Include/LINQ*: mais simples, mas esconde SQL e tende a N+1 ou queries pesadas sem controle explícito.
- *Só Dapper + scripts SQL*: menos código EF, mas seed e evolução de schema ficam mais frágeis nos módulos seguintes.

---

## R2 — Estratégia de seed determinístico

**Decision**: Classe `DatabaseSeeder` com `Random(42)` fixo, inserção em lotes via EF (`AddRange` + `SaveChanges` a cada N registros), guard `if (await db.Orders.AnyAsync()) return`.

**Rationale**: Determinismo facilita testes e demo repetível; guard evita duplicação ao reiniciar a API em desenvolvimento; lotes equilibram memória e tempo (~5k pedidos em segundos, não minutos).

**Alternatives considered**:
- *Seed via SQL script embutido*: rápido, mas mistura mecanismo de migração com dados e dificulta explicar distribuição temporal.
- *Bogus/Faker*: dependência extra; `Random` + arrays fixos de nomes bastam para o desafio.

**Volumes adotados**:

| Ambiente | Produtos | Pedidos | Itens/pedido |
| --- | --- | --- | --- |
| Desenvolvimento (`appsettings.Development`) | 50 | 5.000 | 2–5 (média ~3,5) |
| Testes de integração | 50 | 3.000 | 2–5 |

Datas de pedido: uniformemente distribuídas nos últimos 365 dias a partir de data fixa de referência (`2026-07-01`), para suportar faturamento no módulo 003.

---

## R3 — Montagem de pedidos paginados (itens aninhados)

**Decision**: Três consultas Dapper por requisição de listagem:
1. `COUNT(*)` de pedidos.
2. Página de pedidos (`ORDER BY created_at DESC LIMIT @limit OFFSET @offset`).
3. Itens + nome do produto para `order_id IN (...)` da página atual.

Total do pedido calculado em SQL na query 2 via subselect ou na query 3 agregando em memória — **preferir subselect na query 2** para uma única fonte de verdade:

```sql
SELECT o.id, o.created_at, o.status,
       (SELECT COALESCE(SUM(oi.quantity * oi.unit_price), 0)
        FROM order_items oi WHERE oi.order_id = o.id) AS total
FROM orders o
ORDER BY o.created_at DESC
LIMIT @pageSize OFFSET @offset
```

**Rationale**: Evita N+1, mantém SQL legível, total sempre consistente com itens.

**Alternatives considered**:
- *JSON_ARRAYAGG no MySQL*: uma query só, porém SQL mais opaco na apresentação.
- *LEFT JOIN + GROUP BY na listagem*: duplica linhas e complica paginação; rejeitado.

---

## R4 — Paginação e validação de parâmetros

**Decision**: `page` padrão 1, `pageSize` padrão 20, máximo 100. Valores `page < 1`, `pageSize < 1` ou `pageSize > 100` retornam **400** com corpo JSON `{ "error": "mensagem" }` (sem ProblemDetails neste módulo, para manter simplicidade).

**Rationale**: Spec prefere validação explícita a normalização silenciosa; teto de 100 protege contra respostas enormes.

**Alternatives considered**:
- *Normalizar page=0 para 1*: comportamento surpresa; rejeitado.
- *pageSize sem teto*: risco em demo com milhares de itens aninhados.

---

## R5 — Inclusão de `GET /api/orders/{id}`

**Decision**: **Incluir** no módulo 001.

**Rationale**: Reutiliza a mesma query de itens da listagem + um `SELECT` por id; útil para testes, debug e módulo React; custo de implementação baixo (~15 linhas no controller + SQL).

**Alternatives considered**:
- *Omitir até módulo 004*: economia mínima; perde contrato estável cedo.

---

## R6 — Testes de integração com MySQL real

**Decision**: Projeto `tests/TestOrder.Api.Tests` com **Testcontainers** (`Testcontainers.MySql`), `WebApplicationFactory`, fixture compartilhada por collection xUnit, sem SQLite/InMemory como substituto principal.

**Rationale**: Projeto depende de sintaxe e comportamento MySQL + Dapper; Testcontainers valida SQL real sem MySQL nativo. Alinha com a decisão R9: **Docker é pré-requisito único de infra** (Compose para dev + Testcontainers para testes).

**Alternatives considered**:
- *MySQL local obrigatório nos testes*: frágil em CI e onboarding; rejeitado.
- *EF InMemory*: não executa SQL real; rejeitado pelo prompt.
- *Reutilizar container do Compose nos testes*: acopla testes ao estado do dev; Testcontainers efêmero é mais isolado.

**Fallback documentado**: se Docker não estiver disponível, testes exibem skip claro (`Skip.If(!DockerAvailable)`); validação manual via `.\scripts\dev-up.ps1` permanece o caminho alternativo.

---

## R7 — Aplicação de schema na inicialização

**Decision**: `Database.Migrate()` no startup em Development e no `WebApplicationFactory` de testes; seed executado logo após migrate quando banco vazio.

**Rationale**: Após `.\scripts\dev-up.ps1`, o avaliador só interage com a API — schema e dados ficam prontos sem `dotnet ef` manual nem SQL de criação de banco.

**Alternatives considered**:
- *EnsureCreated()*: sem histórico de migrations; rejeitado.
- *Migrate manual separado*: passo extra desnecessário para desafio local.
- *Init SQL no Compose*: duplica responsabilidade com EF migrations; rejeitado (Compose só provisiona database/user via env vars).

---

## R8 — Convenção de nomes no banco

**Decision**: Tabelas e colunas em **snake_case** no MySQL (`products`, `orders`, `order_items`, `created_at`, `unit_price`, `stock_quantity`) via `ToTable` / `HasColumnName` no `OnModelCreating`.

**Rationale**: Alinha com SQL explícito em Dapper e leitura natural em queries manuais durante apresentação.

**Alternatives considered**:
- *PascalCase no banco*: funciona com Pomelo default, mas SQL em apresentação fica menos idiomático.

---

## R9 — Infraestrutura local: Docker Compose como padrão

**Decision**: MySQL 8 em desenvolvimento via `docker-compose.yml` (serviço `mysql` apenas). Scripts `scripts/dev-up.ps1` e `scripts/test.ps1` como entrada principal. **Não exigir** MySQL instalado na máquina do avaliador.

**Rationale**: O desafio pede “rodar com poucos comandos”; Docker torna o banco reprodutível sem onboarding de instalação MySQL. A API continua rodando no host (`dotnet run`); apenas o banco fica containerizado neste módulo. Testcontainers nos testes reutiliza a mesma dependência (Docker) sem conflitar com o Compose de dev.

**Alternatives considered**:
- *MySQL nativo como padrão*: exige instalação e SQL manual de setup; rejeitado como caminho principal.
- *Dockerizar API no módulo 001*: aumenta escopo; Dockerfile da API fica para módulo 006.
- *SQLite/InMemory no dev*: incompatível com Dapper/SQL MySQL e com módulo 002 (`SKIP LOCKED`).

**Detalhes**:

| Aspecto | Decisão |
| --- | --- |
| Compose | `docker compose up -d mysql`, imagem `mysql:8`, porta `3306` |
| Credenciais | `testorder` / `testorder`, database `testorder` |
| Persistência | volume Docker nomeado |
| API startup | `Migrate()` + `SeedAsync()` automáticos |
| Demo | `.\scripts\dev-up.ps1` (foreground `dotnet run`) |
| Testes | `.\scripts\test.ps1` → Testcontainers |
| MySQL local | fallback opcional documentado no quickstart |
