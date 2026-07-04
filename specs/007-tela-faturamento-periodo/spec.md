# Especificação: Módulo 007 — Tela de Faturamento por Período

**Feature Branch**: `007-tela-faturamento-periodo`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Adicionar, na interface React já existente (`src/TestOrder.Web`), uma área simples para consultar visualmente o endpoint já implementado `GET /api/revenue/daily?startDate=&endDate=` (módulo 003). Não há regra de negócio nova — é apenas uma visualização de um endpoint que hoje só é demonstrável via `curl`/documentação.

**Depende de**: Módulo 003 concluído (`GET /api/revenue/daily`, com testes automatizados) e Módulo 004 concluído (`src/TestOrder.Web` com listagem/criação de pedidos, `api.js`, `App.jsx`, `styles.css`).

---

## Objetivo do módulo

Dar visibilidade visual ao faturamento por período que já existe no backend desde o módulo 003, sem criar nenhuma regra de negócio nova. Hoje o endpoint `GET /api/revenue/daily` só é demonstrado via `curl` ou pela suíte de testes — este módulo adiciona uma segunda área na mesma aplicação React (ao lado da área de pedidos) onde o usuário escolhe um intervalo de datas e vê o resultado já formatado.

A tela continua **simples e operacional** — não é um dashboard com gráficos, comparativos ou exportação. É uma consulta com formulário (duas datas + botão) e uma tabela de resultado, no mesmo estilo visual já usado pela área de pedidos.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Operador da loja** | Consultar quanto foi faturado num intervalo de datas, sem precisar usar `curl` ou abrir documentação técnica. |
| **Avaliador do desafio** | Confirmar que o endpoint de faturamento (requisito obrigatório do módulo 003) tem uma forma visual de ser demonstrado na tela, não só via API. |

Não há autenticação nem perfis de acesso neste módulo (mesma premissa dos módulos 003/004).

---

## Jornadas do módulo

### Jornada 1 — Alternar entre `Pedidos` e `Faturamento` (P1)

**Como** operador, **quero** alternar entre a área de pedidos e a área de faturamento na mesma tela, **para** acessar ambas sem recarregar a aplicação ou navegar por URLs diferentes.

**Por que P1**: Sem essa navegação simples, a nova área fica inacessível a partir da tela existente.

**Teste independente**: Abrir a aplicação exibe a área `Pedidos` (comportamento atual, inalterado); clicar em `Faturamento` troca a área visível sem recarregar a página; clicar em `Pedidos` novamente volta ao estado anterior da listagem (não perde a página em que o usuário estava).

**Cenários de aceite**:

1. **Dado** a aplicação carregada, **quando** nenhuma ação de navegação foi feita, **então** a área `Pedidos` é exibida por padrão, exatamente como hoje.
2. **Dado** a aplicação carregada, **quando** clico no controle `Faturamento`, **então** a área de pedidos é ocultada e a área de faturamento é exibida, sem reload do navegador.
3. **Dado** que estou na área `Faturamento`, **quando** clico em `Pedidos`, **então** volto para a listagem de pedidos, preservando a página em que a listagem estava antes de eu sair (não força voltar para a página 1).

---

### Jornada 2 — Consultar faturamento de um intervalo com dados (P1)

**Como** operador, **quero** informar duas datas e ver o faturamento agregado do período, **para** responder "quanto vendemos nesse intervalo" sem usar `curl`.

**Por que P1**: Entrega central do módulo — sem isso, o endpoint continua invisível na UI.

**Teste independente**: Preencher `startDate`/`endDate` cobrindo um intervalo do seed e clicar em `Consultar` exibe o total de faturamento, o total de pedidos e uma tabela com uma linha por dia do intervalo.

**Cenários de aceite**:

1. **Dado** a área `Faturamento` aberta, **quando** ela é exibida pela primeira vez, **então** os campos `startDate`/`endDate` já vêm preenchidos com um intervalo padrão razoável (ver Assumptions).
2. **Dado** um intervalo válido com pedidos, **quando** clico em `Consultar`, **então** vejo um indicador de carregamento e, ao concluir, o total de faturamento (formatado em BRL) e o total de pedidos do período.
3. **Dado** o resultado exibido, **quando** observo a tabela por dia, **então** cada linha mostra a data (sem deslocamento de fuso horário), a quantidade de pedidos do dia e o faturamento do dia (formatado em BRL), incluindo dias com valor zero dentro do intervalo consultado.
4. **Dado** um novo intervalo escolhido após uma consulta anterior, **quando** clico em `Consultar` novamente, **então** o resultado anterior é substituído pelo novo (sem acumular resultados na tela).

---

### Jornada 3 — Consultar intervalo sem pedidos (P2)

**Como** operador, **quero** ver claramente que um intervalo não teve vendas, **para** não confundir "zero faturamento" com "erro do sistema".

**Por que P2**: O backend já devolve 200 com dias zerados para intervalos vazios (módulo 003) — a tela deve refletir isso com clareza, sem parecer uma falha.

**Teste independente**: Consultar um intervalo sem nenhum pedido exibe a tabela com todos os dias e valores zerados, junto com totais zerados, sem exibir mensagem de erro.

**Cenários de aceite**:

1. **Dado** um intervalo sem nenhum pedido, **quando** consulto, **então** vejo `total de faturamento = R$ 0,00`, `total de pedidos = 0` e a tabela com uma linha zerada por dia do intervalo — sem mensagem de erro.

---

### Jornada 4 — Tratar parâmetros inválidos e erros de conexão (P1)

**Como** operador, **quero** entender por que uma consulta de faturamento falhou, **para** corrigir as datas ou tentar novamente.

**Por que P1**: O backend responde `400` para datas ausentes/inválidas/invertidas ou intervalo maior que 366 dias (módulo 003) — a tela precisa refletir isso de forma amigável, sem tela em branco ou erro técnico cru.

**Teste independente**: Consultar com `startDate` maior que `endDate` (ou outro parâmetro inválido aceito pelo navegador, como campo de data vazio) exibe uma mensagem de erro amigável, sem quebrar a interface nem exibir o corpo bruto da resposta HTTP.

**Cenários de aceite**:

1. **Dado** `startDate` posterior a `endDate`, **quando** clico em `Consultar`, **então** vejo uma mensagem de erro amigável (não o JSON bruto de erro do backend) e a área de resultado anterior não é substituída por dado inconsistente.
2. **Dado** um campo de data vazio, **quando** clico em `Consultar`, **então** a tela impede o envio ou exibe a mesma mensagem amigável de erro — sem chamar a API com parâmetro ausente.
3. **Dado** qualquer erro de rede ou backend indisponível durante a consulta, **então** a tela exibe uma mensagem de erro genérica compreensível, no mesmo padrão já usado pela área de pedidos.

---

### Casos de borda

- **Intervalo de um único dia** (`startDate == endDate`): válido — tabela com exatamente uma linha.
- **Intervalo maior que 366 dias**: rejeitado pelo backend com `400` — tratado como erro amigável (Jornada 4), não como bug da tela.
- **Troca rápida de aba durante uma consulta em andamento**: se o usuário sair de `Faturamento` enquanto uma consulta está pendente, o resultado (quando chegar) não deve aparecer incorretamente na área de `Pedidos` nem gerar erro de estado em componente desmontado.
- **Consulta repetida com o mesmo intervalo**: permitida livremente (sem cache/deduplicação) — cada clique em `Consultar` refaz a chamada.
- **Datas exibidas**: cada linha usa o mesmo dia calendário da string `date` (`YYYY-MM-DD`) do backend, formatada como `DD/MM/YYYY` via split de string — **sem** `new Date()` — para evitar deslocamento de fuso (mesmo cuidado conceitual de `createdAt` na área de pedidos).
- **Valores monetários**: mesmo formatador BRL (`Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })`) já usado na área de pedidos, aplicado a `totalRevenue` e a `revenue` de cada dia.
- **Viewport mobile**: a área de faturamento deve permanecer utilizável (sem rolagem horizontal do body), reaproveitando o mesmo padrão de tabela com wrapper de rolagem interna já usado na listagem de pedidos.

---

## Requisitos funcionais

- **FR-001**: A aplicação DEVE oferecer uma navegação simples (ex.: abas ou controle segmentado) com duas áreas: `Pedidos` e `Faturamento`, sem introduzir roteamento de URL (sem `react-router` ou biblioteca de rotas).
- **FR-002**: A área `Pedidos` DEVE continuar se comportando exatamente como hoje (listagem paginada, criação de pedido, tratamento de erros 400/409) — nenhuma regressão de comportamento.
- **FR-003**: A área `Faturamento` DEVE conter dois campos de data (`startDate`, `endDate`, tipo `date`) e um botão `Consultar`.
- **FR-004**: Ao carregar a área `Faturamento` pela primeira vez, os campos `startDate` e `endDate` DEVEM vir preenchidos com um intervalo padrão (ver Assumptions). A consulta HTTP **só** ocorre quando o usuário clica em `Consultar` — **não** há auto-fetch ao abrir a aba.
- **FR-005**: Ao clicar em `Consultar`, o sistema DEVE chamar `GET /api/revenue/daily?startDate=&endDate=` com os valores dos campos e exibir um estado de carregamento até a resposta chegar.
- **FR-006**: Em sucesso (`200`), o sistema DEVE exibir: o `totalRevenue` formatado em BRL, o `totalOrders`, e uma tabela com uma linha por item de `days` (`date`, `orderCount`, `revenue` formatado em BRL) — incluindo dias com valor zero.
- **FR-007**: Em erro (`400` ou falha de rede/conexão), o sistema DEVE exibir uma mensagem de erro amigável, sem expor o corpo bruto da resposta HTTP e sem quebrar a interface.
- **FR-008**: Datas exibidas na tabela de faturamento DEVEM representar o **mesmo dia calendário** da string `date` (`YYYY-MM-DD`) recebida do backend, formatadas como `DD/MM/YYYY` via split/manipulação de string — **sem** `new Date()` — para evitar deslocamento de fuso horário local.
- **FR-009**: Valores monetários (`totalRevenue` e `revenue` por dia) DEVEM usar o mesmo formato BRL já usado na área de pedidos.
- **FR-010**: O helper HTTP (`src/TestOrder.Web/src/api.js`) DEVE ganhar exatamente uma função nova, `fetchDailyRevenue(startDate, endDate)`, seguindo o mesmo padrão das funções existentes (`fetchProducts`, `fetchOrders`, `createOrder`) — sem criar uma camada de serviço genérica.
- **FR-011**: O sistema NÃO DEVE alterar o backend (`src/TestOrder.Api`), o worker Node (`src/TestOrder.OrderProcessor`), o schema do banco ou as migrations neste módulo.
- **FR-012**: O sistema NÃO DEVE introduzir edição de pedido, "faturar pedido", baixa de estoque, alteração de status de pedido, novos endpoints ou novas tabelas.
- **FR-013**: O sistema NÃO DEVE introduzir bibliotecas de gerenciamento de estado genérico (Redux, Zustand, React Query), bibliotecas de gráficos, exportação de dados ou filtros avançados além de `startDate`/`endDate`.
- **FR-014**: Se `App.jsx` crescer demais com a nova área, o sistema PODE extrair um componente de apresentação pequeno (ex. `RevenuePanel.jsx`) para a área de faturamento — sem criar biblioteca de componentes, hooks genéricos reutilizáveis ou camada de serviço.

### Key Entities

- **RevenueDay (frontend)**: representação local de cada item de `days` vindo de `GET /api/revenue/daily` — `date` (string `YYYY-MM-DD`), `orderCount` (number), `revenue` (number).
- **RevenueQuery (estado local do formulário)**: `startDate`, `endDate` (strings `YYYY-MM-DD` controladas pelos inputs `type="date"`).
- **RevenueResult (estado local do resultado)**: `startDate`, `endDate`, `totalRevenue`, `totalOrders`, `days: RevenueDay[]` — espelha diretamente a resposta do backend, sem transformação de estrutura.

---

## Requisitos não funcionais

- **NFR-001 (Simplicidade)**: Sem Redux/Zustand/React Query/gráficos/exportação; estado local via `useState`/`useEffect`, no mesmo estilo já usado em `App.jsx`.
- **NFR-002 (Sem alteração de backend/worker)**: Nenhuma alteração em `src/TestOrder.Api` ou `src/TestOrder.OrderProcessor` é esperada neste módulo.
- **NFR-003 (Consistência visual)**: A área de faturamento DEVE reutilizar os estilos já existentes (`styles.css`, tokens de tema escuro operacional) — sem introduzir uma segunda linguagem visual.
- **NFR-004 (Build)**: `npm run build` DEVE compilar sem erros.
- **NFR-005 (Regressão do backend)**: A suíte automatizada do backend DEVE continuar passando **46/46** — este módulo não deve introduzir regressão.
- **NFR-006 (Testabilidade manual)**: Este módulo não exige suíte de testes automatizados de frontend; a validação é manual, documentada em checklist reproduzível (mesmo padrão do módulo 004).
- **NFR-007 (Rastreabilidade)**: Decisões de implementação DEVEM ser documentadas em `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md` após a implementação, explicando por que o escopo ficou restrito a uma visualização do endpoint existente.

---

## Modelo de dados esperado (alto nível)

Nenhuma nova entidade de backend, tabela ou migration é criada. O frontend passa a consumir, além dos contratos já existentes, o contrato já implementado no módulo 003:

```text
GET /api/revenue/daily?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
  → RevenueResponse { startDate, endDate, totalRevenue, totalOrders, days: [{ date, revenue, orderCount }] }
  → 400 { error } em parâmetro ausente/inválido/invertido/intervalo > 366 dias
```

Nenhum contrato existente (`/api/products`, `/api/orders`, `/api/revenue/daily`) é alterado — a tela é **somente consumidora**.

---

## Fora de escopo deste módulo

- Alterar o backend .NET (`src/TestOrder.Api`) ou criar novos endpoints/tabelas.
- Alterar o worker Node (`src/TestOrder.OrderProcessor`) — o microserviço continua exclusivamente como worker de outbox (consumo de `order_processing_events`), não passa a ter papel na criação de pedido nem no faturamento.
- Alterar schema de banco de dados ou migrations.
- Edição de pedido existente.
- "Faturar pedido" (mudança de status para algo como `invoiced`/`billed`) ou baixa de estoque a partir da tela de faturamento.
- Bloqueio de edição por status faturado (não existe conceito de "faturado" no sistema).
- Alteração de status de pedido de qualquer forma.
- Gráficos, dashboards visuais complexos, comparativos entre períodos.
- Exportação de dados (CSV/PDF).
- Filtros avançados (por produto, cliente, categoria, agrupamento por semana/mês).
- Autenticação e autorização.
- Testes automatizados de frontend (unitários/E2E) — validação manual apenas.
- Roteamento de URL (`react-router` ou equivalente) — a navegação entre áreas é puramente de estado local.

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | Navegação `Pedidos`/`Faturamento` funciona sem reload | Alternar as duas áreas várias vezes; URL do navegador não muda de página |
| AC-002 | Área `Pedidos` sem regressão | Listar, paginar, criar pedido (200/400/409) continuam funcionando como antes |
| AC-003 | Área `Faturamento` tem os campos esperados | `startDate`, `endDate` (inputs `type="date"`) e botão `Consultar` visíveis |
| AC-004 | Defaults de data preenchidos ao abrir | Campos já vêm com um intervalo válido, sem exigir preenchimento manual |
| AC-005 | Consulta com dados do seed retorna resultado completo | Total de faturamento, total de pedidos e tabela por dia exibidos após `Consultar` |
| AC-006 | Dias zerados aparecem na tabela | Intervalo com dias sem pedido mostra linhas com `orderCount = 0`/`revenue = R$ 0,00` |
| AC-007 | Intervalo totalmente vazio não parece erro | Totais zerados e tabela zerada exibidos, sem mensagem de erro |
| AC-008 | Erro de data invertida é amigável | `startDate > endDate` exibe mensagem clara, não o JSON bruto do backend |
| AC-009 | Estado de carregamento visível | Indicador percebido entre o clique em `Consultar` e a resposta |
| AC-010 | Valores monetários em BRL | `totalRevenue` e `revenue` por dia formatados como `R$ 0.000,00` |
| AC-011 | Datas sem deslocamento de fuso | Cada linha exibe o mesmo dia calendário do backend (`YYYY-MM-DD`), formatado como `DD/MM/YYYY` via split de string — sem `new Date()` |
| AC-012 | `dev-up.ps1` continua caminho principal | Nenhuma documentação nova sugere subir a tela de outra forma |
| AC-013 | `api.js` ganhou exatamente uma função nova | `fetchDailyRevenue` adicionada; `fetchProducts`/`fetchOrders`/`createOrder` inalteradas |
| AC-014 | Sem regressão no backend | `dotnet build` + `.\scripts\test.ps1` continuam **46/46** |
| AC-015 | Build do frontend passa | `npm run build` sem erros |
| AC-016 | Sem bibliotecas novas pesadas | `package.json` não ganha Redux/Zustand/React Query/biblioteca de gráficos/rotas |
| AC-017 | Responsividade mínima mantida | Área de faturamento utilizável em viewport mobile (~375px), sem rolagem horizontal do body |

---

## Checks manuais esperados

1. Subir tudo com `.\scripts\dev-up.ps1` (backend + frontend + worker).
2. Abrir `http://localhost:5173` — confirmar que a área `Pedidos` aparece por padrão, como hoje.
3. Clicar em `Faturamento` — confirmar troca de área sem reload.
4. Confirmar que `startDate`/`endDate` já vêm preenchidos com um intervalo padrão.
5. Clicar em `Consultar` com o intervalo padrão (dentro do período do seed) — confirmar total de faturamento, total de pedidos e tabela por dia.
6. Consultar um intervalo futuro sem pedidos — confirmar tabela e totais zerados, sem mensagem de erro.
7. Preencher `startDate` maior que `endDate` e consultar — confirmar mensagem de erro amigável.
8. Voltar para `Pedidos` — confirmar que listagem/paginação/criação continuam funcionando normalmente.
9. Redimensionar a janela para largura mobile (~375px) — confirmar que a área de faturamento permanece utilizável, sem rolagem horizontal do body.
10. Confirmar no console do navegador: sem `React is not defined`, sem `Unexpected token '<'`, sem erros de console.
11. Rodar `dotnet build TestOrder.slnx && .\scripts\test.ps1` — confirmar **46/46**, sem regressão.
12. Rodar `npm run build` — confirmar build sem erros.
13. Registrar comandos e resultados em `docs/PRESENTATION_GUIDE.md`.

---

## Expectativa de validação (sem suíte de testes automatizados de frontend)

Mesmo padrão do módulo 004:

- **Automatizada (backend)**: suíte existente `dotnet test TestOrder.slnx` deve continuar **46/46**.
- **Automatizada (frontend, apenas build)**: `npm run build` deve compilar sem erros.
- **Manual (frontend, funcional)**: checklist de 13 passos acima, executado e registrado em `docs/PRESENTATION_GUIDE.md`.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Por que a tela adiciona apenas visualização do endpoint existente, sem regra de negócio nova.
- Por que edição de pedido, "faturar pedido" e baixa de estoque ficaram explicitamente fora de escopo.
- Reforçar que o microserviço Node continua sendo apenas o worker de outbox (consumo de `order_processing_events`), não dono da criação de pedido nem do faturamento.
- Decisão sobre extrair (ou não) `RevenuePanel.jsx` de `App.jsx`.
- Erros comuns de IA a observar (ex.: deslocar data por timezone, tratar dias zerados como erro, esquecer estado de loading).
- Resultados da validação manual e do build do frontend.
- Prompts Spec Kit usados neste módulo.

---

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Referência: `RevenuePanel.jsx` (se extraído) ou trecho de `App.jsx`, e a função `fetchDailyRevenue` em `api.js`.
- Roteiro de demo: alternar para `Faturamento`, consultar intervalo com dados, consultar intervalo vazio, mostrar erro de data invertida, voltar para `Pedidos`.
- Tabela pass/fail dos checks manuais.

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Um usuário consegue ver o faturamento de um intervalo do seed em menos de 15 segundos após abrir a aplicação (sem usar `curl` ou documentação técnica).
- **SC-002**: **100%** dos intervalos sem pedidos exibem totais e dias zerados sem serem confundidos com erro (nenhuma mensagem de erro exibida).
- **SC-003**: **100%** das tentativas com `startDate > endDate` resultam em mensagem de erro compreensível, sem tela em branco ou erro técnico cru.
- **SC-004**: A alternância entre `Pedidos` e `Faturamento` não introduz nenhuma regressão observável no fluxo de pedidos (listar, paginar, criar, tratar erros 400/409 continuam idênticos ao módulo 004).
- **SC-005**: A área de faturamento permanece utilizável, sem rolagem horizontal do body, tanto em resolução desktop quanto em uma largura de viewport mobile padrão (~375px).

---

## Assumptions

- O intervalo padrão sugerido ao abrir a área `Faturamento` é do primeiro dia do mês corrente até a data atual do sistema; a consulta só ocorre ao clicar em `Consultar` (sem auto-fetch). Como o ambiente de demo usa dados de seed distribuídos ao longo de um período fixo (ver `AI_NOTES.md`/`specs/001-*`), se esse intervalo padrão não cobrir dados do seed, o usuário troca as datas manualmente para o período documentado do seed — este comportamento é aceitável e não bloqueia a jornada principal (FR-004 exige apenas que os campos venham preenchidos).
- "Faturamento" mantém a mesma definição do módulo 003: soma bruta de `quantity * unitPrice` de pedidos com status `created`, sem impostos, descontos ou baixa financeira — esta tela não reinterpreta o conceito, apenas o exibe.
- Não há requisito de autenticação — mesma premissa dos módulos 003/004.
- A navegação entre `Pedidos` e `Faturamento` é implementada como estado local (ex.: `activeTab`) em `App.jsx`, sem biblioteca de rotas — consistente com a restrição de não introduzir `react-router`.
- O componente `RevenuePanel.jsx` (se extraído) é opcional e uma decisão de implementação, não um requisito rígido — a spec permite tanto extrair quanto manter tudo em `App.jsx`, dependendo do tamanho resultante do arquivo.
- Igual ao módulo 004, JavaScript simples (sem TypeScript) é usado, consistente com o restante do frontend.

---

## Restrições de arquitetura (contexto do desafio)

Limites obrigatórios da entrega, alinhados a `.cursor/rules/testorder.mdc` e ao contexto passado pelo usuário:

- **React + Vite**, dentro de `src/TestOrder.Web/` já existente, JavaScript simples (não TypeScript).
- **Sem alteração do backend .NET, do worker Node, do schema ou de migrations** neste módulo.
- **Sem bibliotecas de estado/dados pesadas**: nada de Redux, Zustand, React Query, CQRS, camada de service genérica.
- **Sem biblioteca de rotas** (`react-router` ou equivalente) — navegação por estado local simples.
- **Sem biblioteca de gráficos, exportação ou filtros avançados**.
- `api.js` ganha **apenas uma função nova** (`fetchDailyRevenue`), seguindo o padrão já existente — sem criar camada de serviço genérica.
- Extração de `RevenuePanel.jsx` é permitida **somente** como componente de apresentação simples, se `App.jsx` ficar grande demais — sem criar biblioteca de componentes, hooks genéricos reutilizáveis ou camada de abstração.
- `.\scripts\dev-up.ps1` continua sendo o caminho principal de demonstração — nenhuma documentação nova deve sugerir um caminho alternativo como preferencial.
- Sem Dockerfile novo, sem deploy, sem autenticação neste módulo.
