# Research: Módulo 007 — Tela de Faturamento por Período

**Input**: [spec.md](./spec.md) | **Contexto adicional**: prompt técnico do usuário para `/speckit-plan`

Este módulo adiciona **somente visualização React** para o endpoint já existente `GET /api/revenue/daily` (módulo 003). Não há alteração de backend, worker, schema ou contratos HTTP. As decisões abaixo resolvem escolhas de implementação do frontend que a spec deixa abertas.

---

## R1 — Extrair `RevenuePanel.jsx` vs. manter tudo em `App.jsx`

- **Decision**: **Extrair** um componente de apresentação pequeno `RevenuePanel.jsx` para a área de faturamento; `App.jsx` mantém `activeTab`, estado de datas/resultado/loading/erro e o handler `handleConsultRevenue`.
- **Rationale**: `App.jsx` já tem **361 linhas** (listagem paginada + formulário de criação + três fluxos de loading/erro). Adicionar navegação por abas, formulário de datas, totais, tabela por dia e tratamento de consulta pendente aumentaria o arquivo em ~80–120 linhas — difícil de ler e revisar em entrevista. A extração é **por legibilidade**, não por arquitetura: um único arquivo JSX adicional, sem pasta `components/`, hooks genéricos ou camada de serviço. Validação local e HTTP permanecem em `App.jsx`; o painel só renderiza props (ver [contracts/ui.md](./contracts/ui.md) §6).
- **Alternatives considered**: Manter tudo em `App.jsx` — rejeitado porque o arquivo resultante (~450+ linhas) mistura duas áreas funcionais distintas (pedidos vs. faturamento) sem ganho de simplicidade; criar múltiplos subcomponentes (`RevenueTable`, `DateRangeForm`, etc.) — rejeitado por cerimônia desnecessária para uma tela operacional pequena.

## R2 — Navegação entre áreas sem roteador

- **Decision**: Estado local `activeTab` em `App.jsx` com valores `'orders'` | `'revenue'`; controles visuais (abas ou segmento) alternam o JSX renderizado. URL do navegador **não muda**.
- **Rationale**: Atende FR-001 e a restrição explícita de não introduzir `react-router`. Duas áreas na mesma SPA, padrão já usado em apps operacionais simples.
- **Alternatives considered**: `react-router` com rotas `/` e `/faturamento` — rejeitado por dependência e escopo proibidos; duas páginas HTML separadas — rejeitado por quebrar a experiência de alternância sem reload.

## R3 — Intervalo padrão de datas ao abrir `Faturamento`

- **Decision**: Preencher `startDate` com o **primeiro dia do mês corrente** e `endDate` com a **data local de hoje**, ambos como strings `YYYY-MM-DD` compatíveis com `<input type="date">`. **Não** disparar consulta automática ao abrir a aba — o usuário clica em `Consultar` (FR-004: campos preenchidos ao abrir; consulta HTTP somente no clique).
- **Rationale**: Alinhado às Assumptions da spec. Para demo com dados do seed, o quickstart documenta intervalos que cobrem o período real do seed (`2025-07-02` a `2026-07-01`, ver `AI_NOTES.md` módulo 003); o operador ajusta manualmente se o mês corrente não tiver pedidos.
- **Alternatives considered**: Defaults fixos alinhados ao seed (`2025-07-02` / `2026-07-01`) — rejeitado porque acopla a UI a dados de teste e confunde ambiente real vs. demo; auto-consulta ao montar a aba — rejeitado por adicionar chamada de rede não exigida e complicar cancelamento ao trocar de aba rapidamente.

## R4 — Formatação de moeda e datas

- **Decision**:
  - **Moeda**: extrair `formatCurrency(value)` para um helper local mínimo `src/formatters.js` (ou equivalente), movendo o `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })` hoje inline em `App.jsx`. `App.jsx` e `RevenuePanel.jsx` importam a mesma função — **sem** camada genérica de formatação.
  - **Datas de faturamento (`date: YYYY-MM-DD`)**: exibir como `DD/MM/YYYY` via **manipulação de string** (ex.: `2026-01-15` → `15/01/2026`), **sem** `new Date(dateString)` — mesmo dia calendário do backend, sem deslocamento de fuso (FR-008).
  - **`createdAt` de pedidos**: manter `formatDate(isoString)` existente inalterado em comportamento.
- **Rationale**: Reaproveita o formatador BRL (FR-009) sem duplicar `Intl.NumberFormat`; datas calendário do backend são **date-only**, não instantes UTC.
- **Alternatives considered**: Duplicar `currencyFormatter` dentro de `RevenuePanel.jsx` — rejeitado por duplicação desnecessária; `toLocaleDateString` sobre `new Date(`${date}T00:00:00`)` — rejeitado por risco de off-by-one em fusos negativos.

## R5 — Helper HTTP: `fetchDailyRevenue` em `api.js`

- **Decision**: Adicionar **exatamente uma** função exportada:

  ```text
  fetchDailyRevenue(startDate, endDate)
    → GET /api/revenue/daily?startDate={startDate}&endDate={endDate}
    → retorna corpo JSON em sucesso; lança Error com mensagem amigável em falha
  ```

  Segue o mesmo padrão de `fetchProducts` / `fetchOrders` / `createOrder`: `fetch` nativo, `parseErrorMessage` para `400`, `GENERIC_ERROR_MESSAGE` para rede ou resposta não-JSON (`readJsonResponse` / content-type check já existente).
- **Rationale**: FR-010; concentra tratamento de HTML/`Unexpected token` no helper, não no componente.
- **Alternatives considered**: Inline `fetch` em `App.jsx` — rejeitado por repetir boilerplate; axios — rejeitado por nova dependência.

## R6 — Validação local antes da chamada HTTP

- **Decision**: No handler de `Consultar`, validar no cliente antes de chamar a API:
  - campos `startDate` / `endDate` não vazios;
  - `startDate <= endDate` (comparação lexicográfica segura para `YYYY-MM-DD`).
  Se inválido, preencher `revenueError` com mensagem amigável em português (ex.: "A data inicial não pode ser posterior à data final.") **sem** substituir `revenue` anterior por dados inconsistentes (Jornada 4).
- **Rationale**: Melhora UX (feedback imediato); reduz round-trips para erros óbvios; backend continua sendo fonte da verdade para intervalo > 366 dias e formatos inválidos que o `<input type="date">` raramente produz.
- **Alternatives considered**: Confiar 100% no backend para `start > end` — aceitável, mas pior UX; bloquear `<input>` vazio via `required` apenas — insuficiente para intervalo invertido.

## R7 — Consulta em andamento e troca de aba

- **Decision**: Usar flag `cancelled` (padrão `useEffect` cleanup) ou contador de requisição (`requestId`) no handler assíncrono: se o usuário sair de `Faturamento` (`activeTab !== 'revenue'`) antes da resposta, **ignorar** o resultado e não chamar `setRevenue` / `setRevenueError`. Opcionalmente desabilitar botão `Consultar` enquanto `loadingRevenue === true`.
- **Rationale**: Caso de borda da spec — evita atualizar estado após "desmontagem lógica" da área e warnings de React sobre setState em componente desmontado.
- **Alternatives considered**: AbortController — válido, mas desnecessário para escopo mínimo; deixar resposta aplicar estado mesmo fora da aba — rejeitado por violar cenário de borda documentado.

## R8 — Preservar paginação de pedidos ao voltar da aba `Faturamento`

- **Decision**: Estado `page` / `orders` / `pagination` de pedidos **permanece em `App.jsx`** e **não é resetado** ao alternar abas. Os `useEffect` de produtos/pedidos continuam ativos apenas quando relevantes (pedidos: sempre montados ou condicionados a `activeTab === 'orders'` — preferir **sempre montados** para preservar cache em memória e evitar refetch desnecessário ao voltar).
- **Rationale**: Jornada 1 cenário 3 — "preservando a página em que a listagem estava".
- **Alternatives considered**: Desmontar completamente a área de pedidos ao ir para Faturamento — rejeitado por perder estado de paginação e forçar reload ao voltar.

## R9 — Estilos e responsividade

- **Decision**: Reutilizar classes/tokens existentes em `styles.css` (tema escuro, `.table-wrapper` com scroll horizontal interno, media query ~640px). Adicionar apenas classes mínimas para abas e bloco de totais (ex.: `.tabs`, `.tab`, `.tab--active`, `.revenue-summary`).
- **Rationale**: NFR-003, AC-017; mesmo padrão da listagem de pedidos — sem segunda linguagem visual.
- **Alternatives considered**: CSS-in-JS ou biblioteca de UI — proibidos.

## R10 — Sem novas dependências ou testes automatizados de frontend

- **Decision**: Nenhuma entrada nova em `package.json`; validação via `npm run build`, regressão backend **46/46** e checklist manual (spec NFR-006).
- **Rationale**: Escopo explícito do módulo e do desafio.
- **Alternatives considered**: Vitest/RTL para componente de faturamento — fora de escopo.

---

## Resumo das decisões já dadas pelo usuário (documentadas, não "pesquisadas")

| Item | Decisão |
| --- | --- |
| Backend / worker / schema | Inalterados |
| Endpoint | Apenas consumir `GET /api/revenue/daily` existente |
| Estado | `startDate`, `endDate`, `revenue`, `loadingRevenue`, `revenueError` + `activeTab` |
| Bibliotecas proibidas | Sem react-router, Redux, Zustand, React Query, UI kit, gráficos |
| Caminho de demo | `.\scripts\dev-up.ps1` |
| Documentação pós-implement | `README.md`, `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md` |
