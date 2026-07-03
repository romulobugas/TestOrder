# Quickstart — Módulo 005

**Objetivo**: Validar o worker Node que consome eventos `pending` de `order_processing_events`, processa `OrderCreated` e marca `processed`, em fluxo ponta a ponta com UI + API + MySQL.  
**Contratos**: [contracts/outbox-consumer.md](./contracts/outbox-consumer.md) | **Modelo**: [data-model.md](./data-model.md)

## Pré-requisitos

- Módulos 001, 002 e 004 operacionais (backend **46/46**, frontend funcional)
- .NET 10 SDK + Docker (MySQL)
- Node.js 18+ e npm
- Nenhuma alteração obrigatória em `src/TestOrder.Api/` ou `src/TestOrder.Web/` neste módulo

---

## Subir ambiente completo (4 janelas)

```powershell
.\scripts\dev-up.ps1
```

Sobe MySQL, builda o backend, instala dependências do frontend/worker na primeira execução (se `node_modules` ausente) e abre **quatro** janelas CMD:

| Janela | Título | Comando |
| --- | --- | --- |
| MySQL | `TestOrder - MySQL` | `docker compose logs -f mysql` |
| API | `TestOrder - API` | `dotnet run --project src\TestOrder.Api` |
| Web | `TestOrder - Web` | `npm run dev` |
| Worker | `TestOrder - Worker` | `node index.js` (em `src/TestOrder.OrderProcessor`) |

URLs esperadas no terminal principal:

```
Backend:  http://localhost:5069
Frontend: http://localhost:5173   (ou conferir janela Web se porta ocupada)
MySQL:    localhost:3306
Worker:   see "TestOrder - Worker" window
```

Em uma sessão PowerShell normal, confirme visualmente que as quatro janelas permanecem abertas antes da apresentação.

---

## Validar npm install condicional do worker (AC-008)

1. Fechar todas as janelas CMD abertas pelo `dev-up.ps1` (MySQL logs, API, Web, Worker).
2. Remover dependências instaladas do worker:

```powershell
Remove-Item -Recurse -Force src/TestOrder.OrderProcessor/node_modules -ErrorAction SilentlyContinue
```

3. Rodar `.\scripts\dev-up.ps1` pela **primeira** vez após a remoção.
   - **Esperado**: mensagem indicando instalação de dependências do worker (`npm install`); janela `TestOrder - Worker` abre normalmente.
4. Fechar novamente todas as janelas CMD abertas pelo script.
5. Rodar `.\scripts\dev-up.ps1` **pela segunda** vez (com `node_modules` já presente).
   - **Esperado**: o script **não** executa `npm install` do worker novamente; ambiente sobe com as quatro janelas.

---

## Validar fluxo ponta a ponta (P1)

1. Abrir o frontend no navegador (`http://localhost:5173` ou porta indicada na janela Web).
2. Criar um pedido válido pela UI (produto + quantidade + "Criar pedido").
3. Observar a janela **`TestOrder - Worker`** em até ~10 segundos.
   - **Esperado**: linha JSON no console com `orderId` correspondente ao pedido criado.
4. Consultar o banco:

```powershell
docker compose exec -T mysql mysql -utestorder -ptestorder testorder -e "SELECT id, order_id, event_type, status, created_at FROM order_processing_events ORDER BY id DESC LIMIT 5;"
```

   - **Esperado**: linha do pedido recém-criado com `status = 'processed'`.

---

## Validar event_type fora do contrato (AC-004, opcional)

1. Com worker rodando, inserir manualmente um evento que **não** seja `OrderCreated` (use um `order_id` existente da tabela `orders`):

```powershell
docker compose exec -T mysql mysql -utestorder -ptestorder testorder -e "INSERT INTO order_processing_events (order_id, event_type, status, payload, created_at) VALUES (1, 'OtherEvent', 'pending', '{\"orderId\":1}', UTC_TIMESTAMP(6));"
```

2. Aguardar 2–3 ciclos de polling do worker.
   - **Esperado**: worker **não** falha e **não** emite log de processamento para esse evento.
3. Consultar o banco:

```powershell
docker compose exec -T mysql mysql -utestorder -ptestorder testorder -e "SELECT id, event_type, status FROM order_processing_events WHERE event_type = 'OtherEvent' ORDER BY id DESC LIMIT 1;"
```

   - **Esperado**: linha permanece com `status = 'pending'` — fora do contrato de consumo; dead-letter/alerta fora de escopo.

---

## Validar concorrência — duas instâncias (P1)

1. Manter a instância do worker já aberta pelo `dev-up.ps1`.
2. Abrir **segunda** janela CMD manualmente:

```powershell
cd src\TestOrder.OrderProcessor
node index.js
```

3. Criar 2–3 pedidos novos pela UI (ou via `POST /api/orders`).
4. Observar logs de **ambas** as janelas do worker e consultar:

```sql
SELECT id, order_id, status FROM order_processing_events
WHERE order_id IN (... ids dos pedidos novos ...);
```

   - **Esperado**: cada evento `processed` exatamente uma vez; logs não duplicam o mesmo `eventId` nas duas instâncias.

---

## Validar resiliência básica (P3)

1. Com worker rodando e fila vazia, aguardar 2–3 ciclos de polling.
   - **Esperado**: sem spam de erro no console.
2. Parar MySQL momentaneamente:

```powershell
docker compose stop mysql
```

   - **Esperado**: worker loga erro de conexão e continua tentando.
3. Subir MySQL novamente:

```powershell
docker compose start mysql
```

4. Criar um pedido pela UI.
   - **Esperado**: worker retoma processamento sem reinício manual.

---

## Validar shutdown limpo

1. Na janela `TestOrder - Worker`, pressionar `Ctrl+C`.
   - **Esperado**: processo encerra sem stack trace de erro não tratado.
2. Reabrir manualmente (`node index.js` na pasta do worker).
   - **Esperado**: worker reconecta e não reprocessa eventos já `processed`.

---

## Regressão do backend (obrigatório)

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
```

**Esperado**: **46/46** — nenhuma regressão no backend.

---

## Parar ambiente

- Fechar cada janela CMD (`Ctrl+C` ou fechar janela): MySQL logs, API, Web, Worker.
- Container MySQL: `docker compose down` (ou `docker compose down -v` para reset completo).

---

## Resultado esperado da validação (pós-implementação)

| Validação | Resultado |
| --- | --- |
| `.\scripts\dev-up.ps1` abre 4 janelas (MySQL/API/Web/Worker) | PASS — mensagens finais confirmam as 4 janelas |
| `npm install` condicional do worker (AC-008) | PASS — 1ª execução instala (`node_modules` ausente); 2ª execução pula a instalação |
| Pedido pela UI/API gera log JSON no worker | PASS — pedido `#5013` processado em 1 ciclo de polling (~2s) |
| Evento muda para `processed` no MySQL | PASS — confirmado via SQL |
| Evento com `event_type` diferente permanece `pending` (AC-004, opcional) | PASS — inserido manualmente; worker não falha e não processa |
| Duas instâncias do worker sem duplicidade | PASS — 3 pedidos criados, 3 eventos `processed` únicos, zero `eventId` duplicado entre instâncias |
| Shutdown limpo com `Ctrl+C` | PASS — testado com sinal real `CTRL_C_EVENT` (Windows API); processo encerrou sozinho via `pool.end()` + `process.exit(0)` |
| MySQL indisponível → worker retoma após volta | PASS — `docker compose stop/start mysql`; worker logou erro sem crashar e retomou sozinho |
| `dotnet build` + `.\scripts\test.ps1` | PASS — **46/46** |
| Nenhum arquivo alterado em `src/TestOrder.Web` | PASS — confirmado via `git diff --name-only` |

Detalhes completos em `docs/PRESENTATION_GUIDE.md` (seção "Módulo 005") e `AI_NOTES.md` (seção "Módulo 005").

**Nota sobre o ambiente de implementação**: as validações de E2E, concorrência e resiliência foram feitas com scripts PowerShell auto-contidos (inicia API/worker, testa, encerra tudo em uma única execução), pois o sandbox usado pelo agente de IA não mantém processos em segundo plano de forma confiável entre chamadas de ferramenta separadas. Isso não afeta o funcionamento do `dev-up.ps1` em uma sessão interativa normal do usuário.

---

## Documentação pós-implementação

- `AI_NOTES.md` — seção Módulo 005 (decisões, validação, testes ou ausência deles).
- `docs/PRESENTATION_GUIDE.md` — roteiro de demo outbox + tabela pass/fail.
- `README.md` — ambiente completo com 4 janelas.
