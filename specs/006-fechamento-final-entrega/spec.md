# Especificação: Módulo 006 — Fechamento Final da Entrega

**Feature Branch**: `006-fechamento-final-entrega`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Preparar o repositório `TestOrder` para envio/apresentação do desafio, sem adicionar novas funcionalidades de negócio: revisar README, `dev-up.ps1`, `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md`, consistência das specs 001–005, arquivos versionados indevidamente, documentação dos dados seedados, validações finais e gerar um checklist final de entrega.

**Depende de**: Módulos 001 (base/listagem), 002 (criação de pedido + outbox), 003 (faturamento), 004 (tela React) e 005 (worker Node/outbox) concluídos. Este módulo **não** implementa funcionalidade nova de negócio — é uma auditoria e consolidação da documentação e do estado do repositório antes do envio.

---

## Objetivo do módulo

Garantir que qualquer pessoa (avaliador do desafio ou terceiro) consiga **clonar o repositório, subir o ambiente completo com poucos comandos, entender o que foi entregue, entender como a IA foi usada com controle, e apresentar o projeto presencialmente** usando apenas os documentos já existentes (`README.md`, `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md`) — sem depender de contexto verbal adicional do candidato.

Este módulo é uma **auditoria de fechamento**, não uma etapa de desenvolvimento: o objetivo é revisar, corrigir inconsistências pequenas e documentar o que já existe (módulos 001–005), não criar comportamento novo do sistema. Qualquer correção de código deve ser mínima, pontual e justificada por um problema real encontrado durante a auditoria (ex.: comando desatualizado, link quebrado, arquivo versionado por engano) — não uma oportunidade para expandir escopo.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Avaliador do desafio** | Clonar o repositório, subir o ambiente com `scripts/dev-up.ps1` e validar as entregas dos módulos 001–005 sem precisar pedir ajuda ao candidato. |
| **Candidato/Apresentador** | Usar `docs/PRESENTATION_GUIDE.md` como roteiro único durante a apresentação presencial, sem precisar consultar código ou memória para lembrar decisões e comandos. |
| **Leitor técnico externo** | Entender, via `AI_NOTES.md`, como a IA foi usada em cada módulo, quais erros ela cometeu, o que foi corrigido por revisão humana e quais decisões foram deliberadamente humanas. |

Não há autenticação nem perfis de acesso neste módulo — o "usuário" é sempre alguém lendo o repositório e/ou executando os comandos documentados.

---

## Jornadas do módulo

### Jornada 1 — Subir e validar o ambiente com poucos comandos (P1)

**Como** avaliador do desafio, **quero** clonar o repositório e conseguir rodar tudo com um número mínimo de comandos claramente documentados, **para** validar a entrega sem precisar investigar a estrutura do projeto por conta própria.

**Por que P1**: Se o setup não for imediato e confiável, a primeira impressão da entrega é negativa, independentemente da qualidade do código.

**Teste independente**: Seguir apenas o `README.md`, do zero (clone limpo), e conseguir subir MySQL + backend + frontend + worker com o comando `\scripts\dev-up.ps1`, sem passos manuais adicionais não documentados.

**Cenários de aceite**:

1. **Dado** um clone limpo do repositório, **quando** o avaliador lê o `README.md`, **então** encontra pré-requisitos, o comando único de subida (`.\scripts\dev-up.ps1`) e a lista das quatro janelas esperadas (MySQL, API, Web, Worker) sem precisar abrir outro arquivo.
2. **Dado** o ambiente no ar, **quando** o avaliador quer rodar os testes do backend, o build do frontend ou o smoke do worker, **então** encontra os comandos exatos no `README.md`.
3. **Dado** dúvidas sobre um módulo específico, **quando** o avaliador precisa de mais detalhes, **então** o `README.md` aponta claramente para `docs/PRESENTATION_GUIDE.md` e para a pasta `specs/<módulo>/quickstart.md` correspondente.

---

### Jornada 2 — Apresentar o projeto com um roteiro único (P1)

**Como** candidato/apresentador, **quero** que `docs/PRESENTATION_GUIDE.md` cubra os cinco módulos entregues em ordem, com comandos prontos para copiar/colar e trechos de código já referenciados, **para** conduzir a apresentação presencial sem depender de memória ou de abrir múltiplos arquivos improvisadamente.

**Por que P1**: É o documento usado ao vivo, na frente do avaliador — inconsistência ou desatualização aqui é o risco mais visível da entrega.

**Teste independente**: Ler `docs/PRESENTATION_GUIDE.md` do início ao fim e confirmar que cada módulo (001–005) tem mensagem central, passos de demo, trechos de código referenciados corretamente (arquivo + comportamento) e tabela de validações preenchida — sem seção "a preencher" ou referência quebrada.

**Cenários de aceite**:

1. **Dado** o guia de apresentação, **quando** o apresentador chega à seção de um módulo, **então** encontra: mensagem central, passos numerados de demo, trecho de código central e tabela de validações com status real (não "A preencher").
2. **Dado** um trecho de código citado no guia, **quando** o apresentador confere no arquivo real, **então** o trecho corresponde ao código atual (sem defasagem).
3. **Dado** a ordem de apresentação declarada no topo do documento, **quando** o apresentador chega ao módulo 5, **então** o item está marcado como concluído e o módulo 6 (este) está referenciado como fechamento.

---

### Jornada 3 — Entender o uso real de IA por módulo (P1)

**Como** leitor técnico externo, **quero** que `AI_NOTES.md` explique, por módulo, o que a IA gerou, quais erros ela cometeu, o que foi corrigido por revisão humana e quais decisões foram deliberadamente humanas, **para** avaliar o uso responsável de IA no processo, não apenas o resultado final.

**Por que P1**: É um critério explícito de avaliação do desafio — "mostrar como a IA foi usada com controle, revisão e limites claros" (já é a frase de abertura do próprio `AI_NOTES.md`).

**Teste independente**: Ler `AI_NOTES.md` e confirmar que cada módulo (001–005) tem uma seção com "onde a IA ajudou" e "ajustes de qualidade realizados" (ou equivalente), sem seções vazias ou genéricas demais para serem verificáveis.

**Cenários de aceite**:

1. **Dado** a seção de um módulo em `AI_NOTES.md`, **quando** o leitor procura por decisões humanas, **então** encontra pelo menos uma decisão explicitamente atribuída a revisão/escolha humana (não apenas geração de IA).
2. **Dado** a seção de um módulo, **quando** o leitor procura por erros da IA, **então** encontra pelo menos um exemplo concreto (não uma afirmação vaga como "a IA cometeu alguns erros").
3. **Dado** o arquivo como um todo, **quando** o leitor chega ao final, **então** entende o fluxo Spec Kit usado (`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement`) e como ele se repetiu módulo a módulo.

---

### Jornada 4 — Repositório limpo e specs consistentes (P2)

**Como** avaliador do desafio, **quero** que o repositório não contenha artefatos de build, dependências instaladas, screenshots temporários ou informação sensível/externa, **para** confiar que o que foi versionado é exatamente o código-fonte relevante.

**Por que P2**: Não bloqueia a demonstração funcional, mas afeta a percepção de profissionalismo e organização da entrega.

**Teste independente**: Rodar `git status --porcelain` num clone limpo após seguir o `README.md` (instalar dependências, buildar, rodar testes) e confirmar que nenhum artefato gerado (`node_modules/`, `dist/`, `bin/`, `obj/`, screenshots, arquivos `.env` locais) aparece como rastreado ou pendente de commit indevido.

**Cenários de aceite**:

1. **Dado** o repositório após `npm install` (frontend e worker) e `dotnet build`, **quando** rodo `git status --porcelain`, **então** não vejo `node_modules/`, `dist/`, `bin/` ou `obj/` como arquivos novos rastreáveis.
2. **Dado** as pastas `specs/001-*` a `specs/005-*`, **quando** releio cada `spec.md`/`plan.md`/`AI_NOTES.md` relacionado, **então** não encontro credenciais reais, dados pessoais, nomes de clientes/projetos externos não relacionados ao desafio, nem links quebrados.
3. **Dado** o `.gitignore` na raiz, **quando** confiro os padrões cobertos, **então** `node_modules/`, `dist/`, `bin/`, `obj/` e arquivos temporários de captura de tela/validação já estão cobertos (ou são adicionados, se faltar algum).

---

### Jornada 5 — Entender os dados seedados sem precisar rodar nada (P2)

**Como** avaliador do desafio, **quero** que a documentação explique exatamente o que o seed de desenvolvimento cria (produtos, pedidos, itens, inventário), **para** interpretar corretamente os números que vejo na tela e nas consultas SQL durante a demo, sem achar que são dados de produção.

**Por que P2**: Sem essa explicação, os volumes (milhares de pedidos, centenas de milhares de unidades de inventário) podem parecer inconsistentes ou não intencionais para quem não acompanhou o desenvolvimento módulo a módulo.

**Teste independente**: Ler a documentação (README e/ou AI_NOTES) e encontrar, num único lugar, os números do seed (50 produtos, 5000 pedidos, itens realistas por pedido, backfill de `inventory_units`) com uma frase explicando que são dados de desenvolvimento determinísticos, não dados reais.

**Cenários de aceite**:

1. **Dado** a documentação do projeto, **quando** procuro pelos números do seed, **então** encontro uma tabela ou parágrafo consolidado (produtos, pedidos, itens, média de itens/pedido, unidades de inventário) em vez de precisar cruzar informação espalhada em várias seções de `AI_NOTES.md`.
2. **Dado** o número de unidades de `inventory_units` do backfill, **quando** leio a explicação, **então** entendo que ele é gerado automaticamente no primeiro start da API (módulo 002), de forma idempotente (não duplica em execuções seguintes).

---

### Jornada 6 — Checklist final de entrega (P1)

**Como** candidato, **quero** um checklist final único, cobrindo todos os pontos de auditoria deste módulo, **para** confirmar objetivamente que a entrega está pronta antes do envio/apresentação, sem depender de memória sobre o que já foi verificado.

**Por que P1**: É o critério de "pronto" deste módulo — sem ele, o fechamento da entrega não é verificável de forma objetiva.

**Teste independente**: Abrir o checklist final e percorrer cada item, confirmando visualmente que cada um está marcado como concluído (ou com uma justificativa explícita para os que não se aplicam), sem precisar re-executar toda a auditoria do zero.

**Cenários de aceite**:

1. **Dado** o checklist final de entrega, **quando** o percorro item a item, **então** cada item tem um comando ou passo de verificação objetivo associado (não apenas uma afirmação subjetiva).
2. **Dado** um item do checklist relacionado a uma validação técnica (build, testes, smoke do worker), **quando** confirmo o item, **então** o resultado documentado bate com uma execução real e recente dos comandos.

---

### Casos de borda

- **Pequena correção necessária durante a auditoria** (ex.: comando desatualizado no README, link quebrado, referência a arquivo renomeado): deve ser aplicada de forma mínima e pontual, e registrada em `AI_NOTES.md` como parte deste módulo — não deve virar uma refatoração maior.
- **Arquivo sensível versionado por engano** (ex.: `.env` com credencial real, chave de API): deve ser removido do rastreamento do Git e o padrão correspondente adicionado ao `.gitignore`; reescrita de histórico Git **não** faz parte do escopo, salvo pedido explícito.
- **Falha real encontrada durante a validação final** (ex.: um teste do backend passa a falhar por motivo não relacionado a este módulo): deve ser documentada honestamente; um teste novo só é adicionado se for necessário para comprovar a correção de uma falha real encontrada, não de forma especulativa.
- **Referência externa legítima já existente** (ex.: link ao artigo público da Shopify já citado como inspiração de arquitetura em `AI_NOTES.md`/`docs/PRESENTATION_GUIDE.md`): não é considerada "informação sensível" — permanece, pois é uma referência técnica pública e intencional, não um vazamento.
- **Documentação descrevendo algo que não corresponde mais ao código** (drift entre docs e implementação real): tratado como inconsistência a corrigir na documentação, não como justificativa para alterar o código.
- **Item do checklist final que não se aplica** (ex.: alguma validação específica de um ambiente que o avaliador não tem): deve ser marcado explicitamente com a justificativa, não deixado em branco.

---

## Requisitos funcionais

- **FR-001**: O `README.md` DEVE permitir que um avaliador suba o ambiente completo (MySQL, backend, frontend, worker) executando um único comando (`.\scripts\dev-up.ps1`), com pré-requisitos e portas claramente listados.
- **FR-002**: O `README.md` DEVE listar, de forma explícita e testável, os comandos de validação final: `dotnet build TestOrder.slnx`, `.\scripts\test.ps1`, `npm run build` (frontend) e o comando de smoke do worker (`node index.js` em `src/TestOrder.OrderProcessor`).
- **FR-003**: `scripts/dev-up.ps1` DEVE continuar sendo referenciado, em todos os documentos relevantes (`README.md`, `docs/PRESENTATION_GUIDE.md`, `specs/*/quickstart.md`), como o caminho principal e recomendado de demonstração — nenhum caminho alternativo deve ser apresentado como preferencial.
- **FR-004**: `AI_NOTES.md` DEVE conter, para cada módulo 001–005, ao menos uma decisão humana explícita, ao menos um erro real de IA identificado e corrigido, e o fluxo Spec Kit usado (quais comandos `/speckit-*` foram executados).
- **FR-005**: `docs/PRESENTATION_GUIDE.md` DEVE conter um roteiro de demonstração completo e sem seções pendentes ("a preencher") para os módulos 001–005, incluindo passos numerados, trechos de código referenciados corretamente e tabela de validações com resultados reais.
- **FR-006**: As pastas `specs/001-*` a `specs/005-*` DEVEM ser revisadas quanto a: consistência de nomenclatura/formatação entre módulos, ausência de credenciais reais ou dados pessoais, ausência de referências não intencionais a projetos/clientes externos não relacionados ao desafio, e ausência de links quebrados para arquivos do próprio repositório.
- **FR-007**: O repositório NÃO DEVE versionar artefatos gerados por build ou instalação de dependências: `node_modules/`, `dist/` (frontend), `bin/`/`obj/` (backend .NET), nem screenshots temporários ou capturas de validação geradas durante o desenvolvimento.
- **FR-008**: O `.gitignore` na raiz DEVE ser auditado e, se necessário, complementado (append mínimo) para cobrir explicitamente qualquer padrão do FR-007 que não esteja já coberto.
- **FR-009**: A documentação (README e/ou AI_NOTES) DEVE explicar, num ponto único e consolidado, os dados seedados em desenvolvimento: 50 produtos, 5000 pedidos, itens realistas por pedido (média já observada ~3,5 itens/pedido) e o backfill de `inventory_units` (~237 mil unidades), deixando claro que são dados determinísticos de desenvolvimento, não produção.
- **FR-010**: DEVE ser criado um **checklist final de entrega**, cobrindo objetivamente todos os pontos de auditoria deste módulo (README, `dev-up.ps1`, `AI_NOTES.md`, `PRESENTATION_GUIDE.md`, specs 001–005, arquivos versionados indevidamente, dados seedados, validações finais), com cada item marcável e verificável.
- **FR-011**: As validações finais do repositório (build do backend, suíte de testes, build do frontend, smoke do worker e fluxo manual UI → outbox `processed`) DEVEM ser executadas e seus resultados reais registrados na documentação (não apenas descritas como "esperado").
- **FR-012**: Este módulo NÃO DEVE introduzir nenhuma funcionalidade de negócio nova (nenhum novo endpoint, nenhuma nova tela, nenhum novo comportamento do worker).
- **FR-013**: Este módulo NÃO DEVE alterar o schema do banco de dados nem adicionar migrations.
- **FR-014**: Este módulo NÃO DEVE adicionar novas dependências (backend, frontend ou worker).
- **FR-015**: Este módulo NÃO DEVE adicionar testes automatizados novos, salvo se a validação final revelar uma falha real e comprovável que exija um teste de regressão específico — nesse caso, a adição DEVE ser justificada explicitamente na documentação.
- **FR-016**: Qualquer alteração em código de backend, frontend ou worker neste módulo DEVE ser pequena, pontual e justificada por um problema real encontrado durante a auditoria (ex.: comando incorreto na documentação refletindo bug real, link/arquivo referenciado incorretamente) — nunca uma nova feature.

---

## Requisitos não funcionais

- **NFR-001 (Setup mínimo)**: Um avaliador sem contexto prévio DEVE conseguir subir o ambiente completo lendo apenas o `README.md`, sem precisar abrir código-fonte para descobrir comandos.
- **NFR-002 (Fidelidade)**: Toda validação descrita como "PASS"/"concluído" na documentação DEVE corresponder a uma execução real, verificável e reproduzível — não a uma suposição.
- **NFR-003 (Consistência entre módulos)**: `specs/001-*` a `specs/005-*` DEVEM manter o mesmo padrão de estrutura, nomenclatura e nível de detalhe entre si, permitindo comparação direta entre módulos.
- **NFR-004 (Higiene do repositório)**: Nenhum artefato de build, dependência instalada ou arquivo temporário de validação DEVE estar versionado ou pendente de commit ao final deste módulo.
- **NFR-005 (Sem regressão)**: `dotnet build TestOrder.slnx` e `.\scripts\test.ps1` DEVEM continuar passando **46/46** ao final deste módulo; `npm run build` do frontend e o smoke do worker DEVEM continuar funcionando sem erro.
- **NFR-006 (Escopo mínimo)**: Nenhuma alteração de código além de correções pontuais e justificadas DEVE ocorrer neste módulo — o foco é documentação, auditoria e organização.
- **NFR-007 (Rastreabilidade)**: Toda correção pontual feita durante a auditoria DEVE ser registrada em `AI_NOTES.md`, incluindo o motivo (o que estava incorreto/desatualizado) e o que foi alterado.
- **NFR-008 (Tempo de apresentação)**: O roteiro consolidado de `docs/PRESENTATION_GUIDE.md` DEVE ser executável, do início ao fim (módulos 001–005 + fechamento), dentro de um tempo razoável de apresentação presencial (ordem de 30–50 minutos), sem depender de material externo.

---

## Fora de escopo deste módulo

- Qualquer nova funcionalidade de negócio (novo endpoint, nova tela, novo comportamento do worker, novo relatório).
- Alteração de schema de banco de dados ou novas migrations.
- Adição de novas dependências (backend, frontend, worker).
- Adição de testes automatizados novos, exceto para comprovar a correção de uma falha real encontrada durante a auditoria.
- Refatoração de código existente que não seja estritamente necessária para corrigir uma inconsistência real encontrada.
- Deploy, CI/CD, containerização adicional (Dockerfile do worker continua fora de escopo, como nos módulos anteriores).
- Reescrita de histórico Git (squash, rebase interativo, remoção retroativa de commits).
- Tradução do repositório para outro idioma.
- Criação de novos módulos de funcionalidade (isso seria um módulo 007+, não parte deste fechamento).

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | `README.md` permite subir tudo com um comando | Clone limpo (ou ambiente resetado) + `.\scripts\dev-up.ps1` seguindo apenas o README, sem passos extras não documentados |
| AC-002 | Comandos de validação final documentados e corretos | `dotnet build TestOrder.slnx`, `.\scripts\test.ps1`, `npm run build`, `node index.js` do worker — todos presentes no README e executam com sucesso |
| AC-003 | `dev-up.ps1` é o caminho principal em toda a documentação | Buscar por comandos alternativos de subida em README/PRESENTATION_GUIDE/quickstarts — `dev-up.ps1` é sempre o recomendado primeiro |
| AC-004 | `AI_NOTES.md` cobre uso de IA, ajustes de qualidade e decisões humanas por módulo | Releitura módulo a módulo (001–005): cada um tem "onde a IA ajudou" e "ajustes de qualidade realizados" concretos |
| AC-005 | `PRESENTATION_GUIDE.md` sem seções pendentes | Buscar por "A preencher"/placeholders nas seções dos módulos 001–005 — nenhuma ocorrência |
| AC-006 | Specs 001–005 consistentes e sem conteúdo sensível | Releitura das pastas `specs/001-*` a `specs/005-*`; nenhuma credencial real, dado pessoal ou referência externa não intencional |
| AC-007 | Nenhum artefato de build versionado | `git status --porcelain` após `npm install`/`dotnet build` não lista `node_modules/`, `dist/`, `bin/`, `obj/` |
| AC-008 | `.gitignore` cobre os padrões necessários | Revisão manual do `.gitignore` contra os padrões do FR-007/FR-008 |
| AC-009 | Dados seedados documentados num único lugar | README ou AI_NOTES contém tabela/parágrafo único com 50 produtos, 5000 pedidos, itens/pedido e backfill de `inventory_units` |
| AC-010 | Checklist final de entrega existe e é verificável | Arquivo de checklist criado, com itens objetivos e status preenchido |
| AC-011 | Sem regressão no backend | `dotnet build TestOrder.slnx` + `.\scripts\test.ps1` → **46/46** |
| AC-012 | Frontend e worker seguem funcionais | `npm run build` (frontend) e `node index.js` (worker) executam sem erro |
| AC-013 | Fluxo manual UI → outbox `processed` documentado e validado | Passo a passo replicável: criar pedido pela tela, observar worker, confirmar `processed` no MySQL |
| AC-014 | Nenhuma funcionalidade de negócio nova introduzida | `git diff --name-only` dos módulos 001–005 (código de produção) não mostra alteração de comportamento, apenas documentação/higiene e, no máximo, correções pontuais justificadas |

---

## Checks manuais esperados

1. Clonar (ou simular um clone limpo, resetando artefatos gerados) e seguir apenas o `README.md` até o ambiente completo subir via `.\scripts\dev-up.ps1`.
2. Rodar as validações finais documentadas: `dotnet build TestOrder.slnx`, `.\scripts\test.ps1` (**46/46** esperado), `npm run build` do frontend, `node index.js` do worker (smoke).
3. Criar um pedido pela tela React e confirmar visualmente que o evento correspondente aparece `processed` em `order_processing_events` (fluxo manual UI → outbox).
4. Ler `AI_NOTES.md` do início ao fim e confirmar que cada módulo (001–005) tem conteúdo concreto sobre uso de IA, erros corrigidos e decisões humanas — sem trechos genéricos ou vazios.
5. Ler `docs/PRESENTATION_GUIDE.md` do início ao fim e confirmar ausência de seções "a preencher", trechos de código desatualizados ou passos quebrados.
6. Reler `specs/001-*` a `specs/005-*` (spec.md, plan.md, AI_NOTES relacionado) procurando por credenciais, dados pessoais, referências externas não intencionais e links quebrados.
7. Rodar `git status --porcelain` após instalar dependências e buildar tudo, confirmando ausência de `node_modules/`, `dist/`, `bin/`, `obj/` ou arquivos temporários versionados/pendentes indevidamente.
8. Conferir o `.gitignore` contra os padrões esperados (build artifacts, dependências, arquivos temporários de validação).
9. Confirmar que a documentação explica os números do seed (50 produtos, 5000 pedidos, itens/pedido, backfill de `inventory_units`) num único lugar consolidado.
10. Percorrer o checklist final de entrega item a item, confirmando status real para cada um.
11. Registrar quaisquer correções pontuais feitas durante a auditoria em `AI_NOTES.md`, com motivo e escopo exato da mudança.

---

## Expectativa de validação (testes automatizados condicionais)

Este módulo é predominantemente documental/auditoria, não introduz comportamento novo a testar:

- **Automatizado (obrigatório, sem alteração)**: a suíte existente `dotnet test TestOrder.slnx` / `.\scripts\test.ps1` DEVE continuar **46/46**, confirmando que nenhuma correção pontual quebrou o backend.
- **Automatizado (condicional)**: um teste novo só é adicionado se a validação final revelar uma falha real e reprodutível (ex.: regressão não detectada anteriormente); nesse caso, a adição e o motivo DEVEM ser documentados explicitamente em `AI_NOTES.md`. Sem falha real encontrada, nenhum teste novo é criado.
- **Manual (obrigatório)**: os 11 checks manuais acima, executados e registrados no checklist final de entrega e em `docs/PRESENTATION_GUIDE.md`.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Lista objetiva de toda correção pontual feita durante a auditoria (arquivo, motivo, o que mudou) — ou declaração explícita de que nenhuma foi necessária.
- Resultado real da releitura de `specs/001-*` a `specs/005-*` (inconsistências encontradas e corrigidas, ou confirmação de que nenhuma foi encontrada).
- Resultado real da auditoria de arquivos versionados (`git status --porcelain` pós build/install) e do `.gitignore`.
- Resultados reais das validações finais (build, testes, build do frontend, smoke do worker, fluxo manual).
- Reflexão curta sobre o processo Spec Kit ao longo dos 6 módulos (o que funcionou bem, o que teria sido feito diferente).

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Seção de fechamento explicando que o módulo 006 é uma auditoria de entrega, não uma feature nova.
- Checklist final de entrega referenciado ou embutido, com status real.
- Nota de que a ordem de apresentação (item "6. README, AI_NOTES e decisões finais") está concluída.

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Um avaliador sem contexto prévio consegue subir o ambiente completo (MySQL, backend, frontend, worker) seguindo apenas o `README.md`, executando no máximo um comando principal (`.\scripts\dev-up.ps1`) mais os pré-requisitos de instalação de ferramentas.
- **SC-002**: 100% dos comandos de validação final documentados (build do backend, suíte de testes, build do frontend, smoke do worker) executam com sucesso quando testados na sessão de fechamento.
- **SC-003**: Após instalar dependências e buildar todos os projetos, `git status --porcelain` não lista nenhum artefato de build/dependência (`node_modules/`, `dist/`, `bin/`, `obj/`) como pendente de commit.
- **SC-004**: 100% dos itens do checklist final de entrega estão marcados como concluídos (ou com justificativa explícita de não aplicabilidade) antes do envio.
- **SC-005**: Um leitor externo consegue identificar, só pelo `AI_NOTES.md`, pelo menos uma decisão humana e um erro de IA corrigido por módulo (001–005), sem precisar perguntar ao candidato.
- **SC-006**: O roteiro de `docs/PRESENTATION_GUIDE.md` cobre os módulos 001–005 e o fechamento sem nenhuma seção marcada como pendente/"a preencher".

---

## Assumptions

- Os módulos 001–005 estão funcionalmente completos e validados (46/46 testes, frontend funcional, worker funcional) — este módulo audita e documenta, não corrige funcionalidade quebrada de forma ampla.
- "Texto sensível" neste contexto significa: credenciais reais, dados pessoais, nomes de clientes/empresas ou projetos externos não relacionados ao desafio — não inclui referências técnicas públicas já citadas intencionalmente como inspiração (ex.: artigo público da Shopify sobre `FOR UPDATE SKIP LOCKED`).
- O checklist final de entrega pode ser um novo arquivo (ex.: dentro de `docs/` ou na raiz) ou uma seção dedicada em um documento já existente — a decisão exata de localização e formato é feita no `plan.md`, respeitando a regra do projeto de poucos arquivos e organização simples.
- Nenhuma variável de ambiente, credencial ou segredo real precisa ser removida — o projeto já usa apenas credenciais de desenvolvimento local (`testorder`/`testorder`) documentadas abertamente como tal.
- Pequenas correções pontuais (ex.: comando desatualizado, link quebrado) podem ser necessárias e são esperadas como parte normal de uma auditoria de fechamento — não configuram "nova funcionalidade".
- Este módulo não teve um `plan.md`/`tasks.md` de código de produção como os módulos 001–005; a auditoria é conduzida diretamente a partir desta especificação e de um `plan.md`/`tasks.md` focados em documentação e verificação, não em arquitetura de sistema.

---

## Restrições de arquitetura (contexto do desafio)

Limites obrigatórios desta entrega, alinhados a `.cursor/rules/testorder.mdc` e ao pedido explícito do usuário para este módulo:

- **Nenhuma funcionalidade de negócio nova** — este módulo é auditoria e documentação, não desenvolvimento de feature.
- **Sem alteração de backend, frontend ou worker**, exceto correção pequena e explicitamente justificada por um problema real encontrado na auditoria.
- **Sem alteração de schema ou migrations.**
- **Sem novas dependências** em nenhum dos três projetos (backend, frontend, worker).
- **Sem testes novos**, salvo se a validação final revelar uma falha real que exija um teste de regressão específico — e, nesse caso, com justificativa documentada.
- **Foco em entrega, clareza e apresentação** — qualquer decisão de escopo neste módulo deve priorizar a experiência do avaliador/apresentador, não elegância técnica adicional.
- Mesmo padrão Spec Kit dos módulos anteriores: spec → plan → tasks → (analyze, se aplicável) → implement, adaptado para um módulo de auditoria/documentação em vez de código de produção.
