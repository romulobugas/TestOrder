# Quickstart — Módulo 007

**Objetivo**: Validar a área **Faturamento** na UI React (consulta visual de `GET /api/revenue/daily`) e, no follow-up, os filtros server-side de `Pedidos`, as datas opcionais do faturamento e a paginação numerada da tabela de dias.
**Contratos**: [contracts/ui.md](./contracts/ui.md) | **Modelo**: [data-model.md](./data-model.md)

## Pré-requisitos

- Módulos 001–006 concluídos; backend com **57/57** testes passando (46 originais + 11 do follow-up de filtros/datas opcionais)
- Módulo 003: endpoint `GET /api/revenue/daily` operacional (agora com `startDate`/`endDate` opcionais)
- Módulo 004: `src/TestOrder.Web` com área de pedidos
- .NET 10 SDK + Docker + Node.js 18+ + npm
- Follow-up deste módulo **altera** `GET /api/orders` (filtros) e `GET /api/revenue/daily` (datas opcionais) — únicas mudanças de backend feitas por este módulo; worker e schema/migrations continuam intocados

---

## Subir ambiente completo

```powershell
.\scripts\dev-up.ps1
```

Backend em `http://localhost:5069`, frontend em `http://localhost:5173` (conferir janela **TestOrder - Web** se a porta Vite divergir). Proxy `/api` → `5069` via `vite.config.js`.

**Intervalo útil para demo com seed** (documentado em `AI_NOTES.md`, módulo 003): `2025-07-02` a `2026-07-01`. Para um mês com dados, usar por exemplo `2026-01-01` a `2026-01-31` (ajustar conforme distribuição real do seed).

---

## Validação automatizada (smoke)

```powershell
# Parar a API local antes de compilar (Windows bloqueia o .exe em uso)
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet build TestOrder.slnx
.\scripts\test.ps1

# Build frontend
cd src\TestOrder.Web
npm run build
cd ..\..
```

**Esperado**: build .NET OK; testes **57/57**; `npm run build` sem erros; `package.json` sem dependências novas.

---

## Checklist manual — navegação

1. Abrir `http://localhost:5173`.
2. **Esperado**: área **Pedidos** visível por padrão (listagem + formulário).
3. Clicar **Faturamento** — área troca **sem reload**; URL não muda de rota.
4. Avançar listagem de pedidos para página 2; ir a **Faturamento**; voltar a **Pedidos**.
5. **Esperado**: listagem ainda na **página 2** (paginação preservada).

---

## Checklist manual — consulta com dados

1. Na aba **Faturamento**, confirmar `startDate` / `endDate` preenchidos (1º dia do mês → hoje).
2. Clicar nos atalhos **Hoje**, **7 dias**, **15 dias**, **30 dias**, **90 dias** e **Último ano**.
3. **Esperado**: os atalhos apenas preenchem as datas; nenhuma consulta automática acontece antes de clicar **Consultar**.
4. Ajustar datas para intervalo com pedidos no seed (ex.: `2026-01-01` a `2026-01-31` ou `2025-07-02` a `2026-07-01`).
5. Clicar **Consultar**.
6. **Esperado**:
   - Indicador de carregamento perceptível;
   - Total de faturamento em **BRL** (`R$ …`);
   - Total de pedidos (inteiro);
   - Tabela com uma linha por dia; dias sem venda com `0` / `R$ 0,00`;
   - Datas na tabela exibem o mesmo dia calendário do backend, formatadas como `DD/MM/YYYY` via split de string (sem `new Date()`).
7. Clicar **Limpar datas** (1 clique limpa as duas datas) e depois **Consultar**.
8. **Esperado**: consulta com as duas datas vazias — backend agrega **todos os dias disponíveis** (seed + qualquer pedido criado na sessão), sem preencher dias zerados (não há intervalo fechado para "explodir" em zeros); resultado aparece como válido, não como erro.
9. Preencher apenas `startDate` (deixar `endDate` vazio) e clicar **Consultar**.
10. **Esperado**: retorna só os dias reais a partir de `startDate` em diante, sem zero-fill; o mesmo vale ao preencher só `endDate`.

---

## Checklist manual — intervalo vazio (sem pedidos)

1. Consultar intervalo futuro sem pedidos, com as duas datas preenchidas (ex.: `2030-01-01` a `2030-01-07`).
2. **Esperado**: totais zerados, tabela com 7 linhas zeradas (zero-fill só ocorre quando as duas datas são informadas), **sem** mensagem de erro.

---

## Checklist manual — paginação numerada da tabela de dias (Faturamento)

1. Consultar um intervalo com mais de 10 dias (ex.: `2025-07-02` a `2026-07-01`, o range do seed).
2. **Esperado**: tabela mostra só os primeiros dias da página 1; abaixo da tabela aparecem os botões **Início**, **Anterior**, números de página, **Próxima**, **Fim** — mesmo padrão visual da paginação de `Pedidos`.
3. Clicar em um número de página, depois em **Próxima**, depois em **Fim**.
4. **Esperado**: a tabela troca de conteúdo local (dias diferentes), sem nova chamada HTTP — a paginação é 100% client-side sobre a resposta já recebida.
5. Fazer uma nova consulta (outro intervalo) e confirmar que a paginação volta para a página 1.

---

## Checklist manual — filtros de Pedidos (server-side)

1. Ir para a aba **Pedidos**. Acima da tabela, confirmar os campos **Status** (select: Todos/created/processed), **Data inicial**, **Data final**, botões **Filtrar** e **Limpar filtros**, e os atalhos **Hoje**, **7 dias**, **15 dias**, **30 dias**, **90 dias**, **Último ano** (mesmos presets do Faturamento).
2. Clicar em **30 dias** nos atalhos de Pedidos.
3. **Esperado**: preenche `Data inicial` e `Data final` do filtro; **não** dispara busca; `status` permanece como estava.
4. Selecionar `status = processed` e clicar **Filtrar**.
3. **Esperado**: a listagem volta para a página 1, mostra só pedidos `processed`, e o contador de total reflete o filtro (requisição real ao backend, visível na aba Network).
4. Preencher `Data inicial`/`Data final` com um intervalo válido e clicar **Filtrar** novamente.
5. **Esperado**: filtro de status permanece combinado com o filtro de data.
6. Trocar de página com o filtro ativo.
7. **Esperado**: o filtro continua aplicado (não reseta ao trocar de página).
8. Clicar **Limpar filtros** (1 clique).
9. **Esperado**: todos os campos voltam ao padrão (`Todos`/vazio) e a listagem volta a mostrar todos os pedidos, página 1.
10. Preencher `Data inicial` maior que `Data final` e clicar **Filtrar**.
11. **Esperado**: mensagem de erro amigável (validação local, sem round-trip); o mesmo erro aparece via API se testado direto (`400`).

---

## Checklist manual — duplo clique limpa só o campo

1. Em **Pedidos**, preencher os três filtros (status, data inicial, data final).
2. Dar duplo clique no campo **Status**.
3. **Esperado**: só o status volta para `Todos`; as datas preenchidas continuam como estavam (nenhuma busca é refeita automaticamente — só ao clicar **Filtrar**).
4. Repetir o duplo clique isoladamente nos campos de data inicial e final.
5. **Esperado**: cada duplo clique limpa apenas aquele campo.
6. Repetir os passos 1–5 na aba **Faturamento** para os campos `Data inicial`/`Data final`.
7. **Esperado**: mesmo comportamento — duplo clique limpa só aquele campo; o botão **Limpar datas** continua limpando os dois com **1 clique**.

---

## Checklist manual — erros amigáveis

1. Definir `startDate` **posterior** a `endDate`; clicar **Consultar**.
2. **Esperado**: mensagem clara em português; **não** JSON bruto; resultado anterior (se houver) permanece.
3. (Opcional) Intervalo > 366 dias — mensagem do backend exibida de forma legível.
4. (Opcional) Parar a API e consultar — mensagem genérica de conexão (padrão `api.js`).

---

## Checklist manual — regressão pedidos + console

1. Voltar a **Pedidos** — listar, paginar, criar pedido válido, testar 400/409 como no módulo 004.
2. Na listagem, usar os botões numerados de página, **Início** e **Fim**.
3. **Esperado**: a página clicada é carregada, sem perder os contadores do painel.
4. Abrir DevTools → Console — **sem** `React is not defined`, `Unexpected token '<'`, warnings de setState após trocar aba durante loading.
5. Redimensionar para ~375px — área faturamento utilizável; **sem** scroll horizontal do `body`.

---

## Estrutura do frontend (apresentação)

Confirmar que `src/TestOrder.Web/src` está organizado de forma mínima:

```text
App.jsx              # shell: header, abas, renderiza página ativa
pages/orders/        # OrdersPage.jsx — listagem, filtros, criação
pages/revenue/       # RevenuePage.jsx — consulta faturamento
components/          # PageNav.jsx — paginação compartilhada
shared/              # dateRanges, formatters, pagination
api/api.js           # fetch helpers locais
styles.css           # CSS único
```

**Esperado**: sem pastas `hooks/`, `contexts/`, `services/` ou dependências novas; `App.jsx` pequeno e fácil de explicar na entrevista.

---

## Registro pós-validação

Após implementação, registrar resultados em:

- `docs/PRESENTATION_GUIDE.md` — roteiro curto da aba + tabela pass/fail
- `AI_NOTES.md` — escopo só visualização; fora de escopo faturar pedido/baixa estoque
- `README.md` — mencionar aba de faturamento

---

## Referências

| Documento | Conteúdo |
| --- | --- |
| [spec.md](./spec.md) | FR/AC completos |
| [plan.md](./plan.md) | Decisões de implementação |
| [research.md](./research.md) | R1–R10 |
| [../003-faturamento-por-periodo/](../003-faturamento-por-periodo/) | Contrato HTTP do endpoint |
