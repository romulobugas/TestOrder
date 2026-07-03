# Quickstart — Módulo 001

**Objetivo**: Validar a base, seed e endpoints com poucos comandos.  
**Contratos**: [contracts/api.md](./contracts/api.md) | **Modelo**: [data-model.md](./data-model.md)

## Pré-requisitos

- **.NET 10 SDK**
- **Docker** (Docker Desktop ou Docker Engine) — único requisito de infra além do SDK
- Repositório clonado

> **Não é necessário** instalar MySQL na máquina. O banco sobe via **Docker Compose** (MySQL 8).

## Caminho rápido (padrão)

Na raiz do repositório:

```powershell
.\scripts\dev-up.ps1
```

O script:

1. Sobe MySQL 8 com `docker compose up -d mysql`
2. Aguarda o banco aceitar conexão
3. Executa `dotnet build TestOrder.slnx`
4. Executa `dotnet run --project src/TestOrder.Api` em **foreground** (terminal bloqueado até Ctrl+C)

Na **primeira execução**, a API aplica automaticamente:

- **Migrations** (`Database.Migrate()`)
- **Seed** (5000 pedidos em dev; ignorado se o banco já estiver populado)

**Esperado** nos logs:

- Migrations aplicadas sem erro
- Seed concluído ou ignorado
- API escutando em **`http://localhost:5069`** (perfil `http` em `launchSettings.json`)

Connection string padrão (já em `appsettings.Development.json`):

```
Server=localhost;Port=3306;Database=testorder;User=testorder;Password=testorder;
```

### Volume do seed (dev, validado)

| Métrica | Valor |
| --- | --- |
| Produtos | 50 |
| Pedidos | 5000 |
| Itens de pedido | 17499 |
| Média itens/pedido | 3,50 |

## Validar endpoints

Porta padrão: **5069**.

### Produtos

```powershell
curl -s http://localhost:5069/api/products
```

**Esperado**: HTTP 200, array JSON com 50 produtos (`id`, `name`, `unitPrice`).

### Pedidos paginados

```powershell
curl -s "http://localhost:5069/api/orders?page=1&pageSize=5"
```

**Esperado**: HTTP 200, até 5 pedidos em `items`, `totalCount=5000`, metadados de paginação, `total` e itens aninhados. Campo `createdAt` em UTC com sufixo **`Z`** (ex.: `"2026-07-01T00:00:00Z"`).

### Página além do fim

```powershell
curl -s "http://localhost:5069/api/orders?page=999&pageSize=20"
```

**Esperado**: HTTP 200, `items: []`.

### Paginação página 2

```powershell
curl -s "http://localhost:5069/api/orders?page=2&pageSize=20"
```

**Esperado**: IDs diferentes da página 1 (ordenacao `createdAt DESC`, desempate `id DESC`).

### Parâmetro inválido

```powershell
curl -s -w "`n%{http_code}" "http://localhost:5069/api/orders?page=0&pageSize=20"
```

**Esperado**: HTTP 400, corpo com `"error"`.

### Detalhe por id

```powershell
curl -s http://localhost:5069/api/orders/1
curl -s -w "`n%{http_code}" http://localhost:5069/api/orders/99999999
```

**Esperado**: 200 para id existente (com itens e `createdAt` em UTC/`Z`); 404 para id inexistente.

## Testes automatizados

Requer Docker (Testcontainers sobe MySQL efêmero nos testes).

```powershell
# Parar a API se estiver rodando (Windows — evita lock do TestOrder.Api.exe)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force

.\scripts\test.ps1
```

Equivalente a `dotnet test TestOrder.slnx`.

**Esperado**: **17/17** testes de integração passando, incluindo:

- Seed ≥ 3.000 pedidos no perfil de teste (`appsettings.Test.json`)
- Endpoints 200/400/404
- Total = soma dos itens
- Paginação sem overlap entre páginas 1 e 2
- `createdAt` serializado com sufixo `Z`

## Build isolado

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
```

## Parar o ambiente

- **API**: Ctrl+C no terminal do `dev-up.ps1`
- **MySQL Docker**: `docker compose down` (dados persistem no volume) ou `docker compose down -v` (apaga volume)

## Fallback opcional: MySQL instalado localmente

Use apenas se **não** quiser Docker para o banco de desenvolvimento:

1. Tenha MySQL 8 rodando localmente
2. Crie manualmente database e usuário:

```sql
CREATE DATABASE testorder CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'testorder'@'localhost' IDENTIFIED BY 'testorder';
GRANT ALL ON testorder.* TO 'testorder'@'localhost';
FLUSH PRIVILEGES;
```

3. Ajuste `ConnectionStrings:Default` se porta/usuário diferirem
4. `dotnet build TestOrder.slnx` e `dotnet run --project src/TestOrder.Api`

Migrations e seed continuam automáticos no startup da API.

> Este caminho **não** é o recomendado para demonstração ao avaliador.

## Inspeção SQL opcional

Se quiser confirmar volume diretamente no banco (Docker ou local):

```sql
SELECT COUNT(*) FROM products;      -- 50
SELECT COUNT(*) FROM orders;        -- 5000 (dev)
SELECT COUNT(*) FROM order_items;   -- 17499 (dev)
SELECT ROUND(COUNT(*) / (SELECT COUNT(*) FROM orders), 2) AS avg_items
FROM order_items;                     -- ~3.50
```

Conexão Docker: `localhost:3306`, user `testorder`, password `testorder`, database `testorder`.

## Registrar evidências

Documentação atualizada ao fechar o módulo:

- `AI_NOTES.md` — decisões, seed, testes, uso de IA
- `docs/PRESENTATION_GUIDE.md` — roteiro de demo, referências de código, validações PASS
