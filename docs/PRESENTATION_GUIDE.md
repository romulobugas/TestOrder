# Guia de Apresentacao - TestOrder

Este documento e para apoiar a apresentacao presencial. Ele deve ser atualizado ao fim de cada modulo com decisoes, validacoes e referencias de codigo.

## Mensagem central

O projeto foi construido como uma solucao pequena e explicavel, priorizando boas decisoes em vez de excesso de arquitetura. A regra foi manter o codigo perto do problema: controllers MVC, modelo claro, SQL direto onde performance importa e documentacao viva do uso de IA.

## Decisoes iniciais

- **ASP.NET Core MVC, nao Minimal APIs**: escolha feita para manter uma organizacao familiar por controllers sem cair em camadas artificiais.
- **Sem Clean Architecture/DDD/CQRS por ritual**: o dominio do desafio e pequeno; criar muitas pastas e interfaces atrapalharia a leitura.
- **MySQL 8**: escolhido para demonstrar concorrencia com `FOR UPDATE SKIP LOCKED`, alinhado ao artigo da Shopify sobre reservas de inventario.
- **Docker como padrao local (modulo 001)**: MySQL sobe via `docker-compose.yml`; nao exige MySQL instalado na maquina. Comando principal: `.\scripts\dev-up.ps1`. MySQL nativo e apenas fallback opcional.
- **EF Core + Dapper**: EF Core para modelo, schema e seed; Dapper para consultas e pontos onde SQL explicito ajuda performance e clareza.
- **Microservico Node sem fila externa**: quando entrar, ele processara uma tabela de outbox/fila no proprio MySQL, evitando RabbitMQ/Kafka/Redis.
- **Spec Kit por modulo**: cada modulo tera spec, plano, tarefas, implementacao e revisao.
- **IA com trilha auditavel**: o repositorio inclui Spec Kit para Cursor, Claude, Codex e Antigravity, alem de `AI_NOTES.md` e `docs/SPECKIT_SETUP.md`.

## Ordem de apresentacao planejada

1. Base, modelo e listagem de pedidos. **(concluido)**
2. Criacao de pedidos com reserva concorrente.
3. Faturamento por periodo.
4. Tela React.
5. Microservico Node e outbox.
6. README, AI_NOTES e decisoes finais.

## Referencias externas

- Shopify Engineering: https://shopify.engineering/scaling-inventory-reservations
- GitHub Spec Kit: https://github.com/github/spec-kit

## Referencias de codigo

| Modulo | Arquivo | O que explicar |
| --- | --- | --- |
| Setup | `docs/SPECKIT_SETUP.md` | Como a IA foi organizada para trabalhar por especificacao e modulo |
| 001 | `docker-compose.yml` | MySQL 8, volume persistente, credenciais dev |
| 001 | `scripts/dev-up.ps1` | Comando unico de demo: compose + build + run |
| 001 | `scripts/test.ps1` | Valida Docker + `dotnet test` com Testcontainers |
| 001 | `src/TestOrder.Api/Program.cs` | MVC, EF, Dapper, migrate+seed no startup, JSON UTC |
| 001 | `src/TestOrder.Api/Data/TestOrderDbContext.cs` | Schema EF, snake_case, indices |
| 001 | `src/TestOrder.Api/Data/Seed/DatabaseSeeder.cs` | Seed configuravel (`Seed:*`), guard anti-duplicacao |
| 001 | `src/TestOrder.Api/Controllers/ProductsController.cs` | `GET /api/products` com Dapper |
| 001 | `src/TestOrder.Api/Controllers/OrdersController.cs` | Paginacao, validacao 400, detalhe 404 |
| 001 | `src/TestOrder.Api/Controllers/OrdersQueries.cs` | SQL da listagem (3 queries, total por subselect) |
| 001 | `src/TestOrder.Api/Json/UtcDateTimeJsonConverter.cs` | `createdAt` em UTC com sufixo `Z` |
| 001 | `tests/TestOrder.Api.Tests/Integration/` | Fixture Testcontainers, factory, 17 testes |

## Modulo 001 — roteiro de demo (5–10 min)

1. **Subir ambiente**: `.\scripts\dev-up.ps1` — mostrar logs de migration/seed na primeira execucao.
2. **Produtos**: `curl http://localhost:5069/api/products` — 50 itens, campos `id`, `name`, `unitPrice`.
3. **Pedidos paginados**: `curl "http://localhost:5069/api/orders?page=1&pageSize=5"` — `totalCount=5000`, itens aninhados, `total` coerente.
4. **Contrato JSON**: apontar `createdAt` terminando em `Z` (UTC).
5. **Erros**: `page=0` → 400; id inexistente → 404.
6. **Performance observacional**: primeira pagina (`pageSize=20`) responde em < 2s em demo local (meta do modulo).
7. **Testes**: `.\scripts\test.ps1` — 17/17 com MySQL efemero via Testcontainers.
8. **Arquitetura**: EF para schema/seed; Dapper para leituras; SQL visivel em `OrdersQueries.cs`.

### Numeros do seed (dev)

| Metrica | Valor |
| --- | --- |
| Produtos | 50 |
| Pedidos | 5000 |
| Itens | 17499 |
| Media itens/pedido | 3,50 |

## Validacoes — Modulo 001

| Validacao | Status | Evidencia |
| --- | --- | --- |
| `dotnet build TestOrder.slnx` | PASS | Build sem erros |
| `.\scripts\test.ps1` | PASS | 17/17 testes |
| `dotnet test TestOrder.slnx` | PASS | Idem |
| `GET /api/products` | PASS | 200, 50 produtos |
| `GET /api/orders?page=1&pageSize=5` | PASS | 200, totalCount=5000 |
| `GET /api/orders?page=999&pageSize=20` | PASS | 200, lista vazia |
| `GET /api/orders?page=0&pageSize=20` | PASS | 400 + error |
| `GET /api/orders/1` | PASS | 200 com itens |
| `GET /api/orders/99999999` | PASS | 404 |
| `createdAt` com sufixo `Z` | PASS | `UtcDateTimeJsonConverter` + testes |
| Paginacao sem IDs duplicados | PASS | Teste + desempate `id DESC` |
| Primeira pagina < 2s (demo local) | PASS | Observacional com seed 5k |

### Comandos de validacao final

```powershell
# Parar API se estiver rodando (Windows — evita lock do .exe)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

### Observacao operacional

No Windows, encerrar a API antes de `dotnet build`/`dotnet test` quando ela foi iniciada com `dev-up.ps1` ou `dotnet run` — o executavel fica em uso e o build falha.
