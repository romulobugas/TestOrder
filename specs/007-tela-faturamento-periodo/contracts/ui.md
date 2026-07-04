# Contrato de UI: Módulo 007 — Tela de Faturamento por Período

Este módulo **não expõe** nova API HTTP — é **consumidor** de `GET /api/revenue/daily` (módulo 003). Este documento formaliza (1) o contrato de consumo desse endpoint do ponto de vista da UI e (2) o contrato de comportamento visível da nova área e da navegação com a área de pedidos (módulo 004).

---

## 1. Endpoint consumido (contrato existente, inalterado)

### `GET /api/revenue/daily?startDate={YYYY-MM-DD}&endDate={YYYY-MM-DD}`

- **Quando é chamado**: ao clicar em `Consultar` na área `Faturamento`, após validação local de `startDate`/`endDate` (R6 em [research.md](../research.md)).
- **Não é chamado**: ao abrir a aba `Faturamento` (apenas preenche defaults); ao alternar para `Pedidos`; em montagem inicial da aplicação.
- **Sucesso (200)** — corpo `DailyRevenueResponse`:

  ```json
  {
    "startDate": "2026-01-01",
    "endDate": "2026-01-31",
    "totalRevenue": 1234.56,
    "totalOrders": 42,
    "days": [
      { "date": "2026-01-01", "revenue": 100.0, "orderCount": 3 },
      { "date": "2026-01-02", "revenue": 0, "orderCount": 0 }
    ]
  }
  ```

  - Armazenado em `revenue` sem remapear campos.
  - UI exibe `totalRevenue` e cada `days[].revenue` com `formatCurrency` (BRL).
  - UI exibe cada `days[].date` formatada por string (sem fuso).
  - Dias com zero **não** são filtrados — tabela lista todos os dias do intervalo.

- **Erro (400)** — corpo `{ "error": "..." }` (exemplos do backend):
  - `startDate is required.` / `endDate is required.`
  - `startDate must be a valid date in yyyy-MM-dd format.`
  - `startDate must not be after endDate.`
  - `Date range must not exceed 366 days.`
  - UI exibe texto de `error` via `fetchDailyRevenue` (mesmo padrão de `api.js` para pedidos) — **não** o JSON bruto.
  - `revenue` anterior **permanece** na tela (não substituir por dado inconsistente).

- **Erro de rede / resposta não-JSON** (ex.: HTML do Vite quando API está down):
  - `fetchDailyRevenue` lança `Error('Não foi possível conectar ao servidor. Tente novamente.')` — mensagem genérica já definida em `api.js`.

### Endpoints **não** alterados neste módulo

| Endpoint | Uso |
| --- | --- |
| `GET /api/products` | Área `Pedidos` — inalterado |
| `GET /api/orders` | Área `Pedidos` — inalterado |
| `POST /api/orders` | Área `Pedidos` — inalterado |
| `GET /api/orders/{id}` | Não consumido |

---

## 2. Contrato de navegação (duas áreas, mesma SPA)

| Situação | Comportamento esperado |
| --- | --- |
| Aplicação recém-aberta | Área `Pedidos` visível; controles `Pedidos` / `Faturamento` presentes |
| Clicar `Faturamento` | Área de pedidos oculta; área de faturamento visível; **sem** reload |
| Clicar `Pedidos` | Volta à listagem; **mesma** `page` de paginação de antes |
| URL do navegador | Permanece `http://localhost:5173/` (sem rotas) |

---

## 3. Contrato de comportamento — área `Faturamento`

| Situação | Comportamento esperado |
| --- | --- |
| Primeira visita à aba | `startDate` = 1º dia do mês corrente; `endDate` = hoje; **sem** auto-consulta |
| `Consultar` com intervalo válido e dados no seed | Loading visível → totais + tabela por dia |
| Intervalo sem pedidos (200, zeros) | Totais `R$ 0,00` / `0` pedidos; tabela com linhas zeradas; **sem** mensagem de erro |
| `startDate > endDate` (local ou 400 backend) | Mensagem amigável; resultado anterior preservado |
| Campo de data vazio | Envio bloqueado ou erro amigável local; **sem** chamar API sem parâmetros |
| Intervalo > 366 dias | Erro amigável com texto do backend |
| Nova consulta após sucesso anterior | `revenue` substituído pelo novo resultado (não acumula) |
| Mesmo intervalo, clique repetido | Nova chamada HTTP (sem cache) |
| `Consultar` em andamento | Botão desabilitado ou loading perceptível (`loadingRevenue`) |
| Usuário troca para `Pedidos` durante loading | Resposta ignorada; nenhum erro de console; estado de pedidos intacto |
| Viewport ~375px | Tabela dentro de wrapper com scroll horizontal **interno**; body sem overflow horizontal |

---

## 4. Contrato de regressão — área `Pedidos`

Comportamento idêntico ao módulo 004 após alternar abas:

| Situação | Comportamento esperado |
| --- | --- |
| Listar / paginar / atualizar | Funciona como antes |
| Criar pedido 201 / erros 400 / 409 | Funciona como antes |
| Formatação `createdAt` / totais BRL | Inalterada (`formatDate` UTC + `formatCurrency`) |

---

## 5. Superfície de arquivos (contrato de implementação)

| Arquivo | Alteração permitida |
| --- | --- |
| `src/TestOrder.Web/src/api.js` | **+** `fetchDailyRevenue` apenas |
| `src/TestOrder.Web/src/App.jsx` | Navegação `activeTab`; estado faturamento; validação local; handler `handleConsultRevenue` (HTTP + guard de race) |
| `src/TestOrder.Web/src/RevenuePanel.jsx` | **Novo** — componente de apresentação controlado por props (sem fetch/validação HTTP) |
| `src/TestOrder.Web/src/formatters.js` | **Novo obrigatório mínimo** — `formatCurrency`, `formatCalendarDate` (+ `formatDate` se extraído de `App.jsx`) |
| `src/TestOrder.Web/src/styles.css` | Classes para abas e bloco faturamento |
| `src/TestOrder.Api/**` | **Proibido** alterar |
| `src/TestOrder.OrderProcessor/**` | **Proibido** alterar |
| `package.json` | **Sem** novas dependências |

---

## 6. Divisão de responsabilidades — `App.jsx` vs `RevenuePanel.jsx`

Escolha de **simplicidade** (estado e HTTP no container; UI no filho controlado por props) — **não** é camada arquitetural nem service layer.

| Responsabilidade | `App.jsx` | `RevenuePanel.jsx` |
| --- | --- | --- |
| Estado `startDate`, `endDate`, `revenue`, `loadingRevenue`, `revenueError` | ✅ dono | ❌ recebe via props |
| Validação local (datas vazias, `startDate > endDate`) | ✅ antes de chamar API | ❌ |
| Chamada HTTP (`fetchDailyRevenue`) | ✅ em `handleConsultRevenue` | ❌ sem `fetch` inline |
| Guard de resposta tardia (troca de aba durante loading) | ✅ flag `cancelled` / `requestId` | ❌ |
| Inputs de data + botão `Consultar` | ❌ | ✅ dispara callbacks (`onStartDateChange`, `onEndDateChange`, `onConsult`) |
| Exibir `loading`, `error`, totais, tabela por dia | ❌ | ✅ renderiza props |
| Formatação BRL / data calendário | ❌ | ✅ via `formatters.js` importado |

---

## 7. Fora do contrato desta tela

- Novos endpoints, mutations, exportação CSV/PDF, gráficos.
- Autenticação.
- Persistência de aba ativa ou último intervalo entre F5.
- Testes automatizados de frontend (validação manual + `npm run build`).
