# Tasks: Módulo 006 — Fechamento Final da Entrega

**Input**: Design documents from `specs/006-fechamento-final-entrega/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md (sem `contracts/` — [research.md R7](./research.md))

**Tests**: Nenhum teste automatizado novo neste módulo, salvo falha real encontrada na validação final (FR-015). Gate obrigatório: suíte existente **46/46** (`dotnet test` / `.\scripts\test.ps1`) deve continuar passando.

**Organization**: Módulo de auditoria/documentação, não de código de produção. Fases: preflight → auditoria fundamental (versionamento + conteúdo sensível) → US1 (README) → US5 (seed) → US3 (AI_NOTES) → US2 (PRESENTATION_GUIDE) → US4 (specs 001–005) → US6 (checklist final) → validação final → fechamento.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência de tarefas incompletas)
- **[USn]**: User story de referência, na ordem da spec — US1=Jornada 1 (setup/README), US2=Jornada 2 (apresentação), US3=Jornada 3 (uso de IA), US4=Jornada 4 (repositório limpo/specs consistentes), US5=Jornada 5 (dados seedados), US6=Jornada 6 (checklist final)
- Caminhos de arquivo explícitos em cada tarefa

---

## Phase 1: Preflight

**Goal**: Registrar o estado real do repositório e das validações **antes** de qualquer alteração de documentação, para servir de baseline honesto no checklist final.

**Independent Test**: Todos os comandos abaixo executam e seus resultados são anotados (não precisam necessariamente estar 100% verdes ainda — o objetivo é ter um ponto de partida real).

---

- [X] T001 Rodar baseline completo (build, testes, frontend, worker) e registrar estado do Git

**Detalhe T001**
| Campo | Valor |
| --- | --- |
| **Descrição** | Executar, nesta ordem, e anotar resultado real de cada um: `git status --short --branch`; `dotnet build TestOrder.slnx`; `.\scripts\test.ps1` (esperado 46/46); `npm run build` em `src/TestOrder.Web`; smoke `node index.js` em `src/TestOrder.OrderProcessor` **se MySQL estiver disponível** (senão, registrar como pulado e o motivo). Nenhuma correção é feita aqui — apenas diagnóstico. |
| **Permitidos** | Nenhum arquivo alterado |
| **Proibidos** | Qualquer correção de código nesta tarefa |
| **Pronto quando** | Os 5 comandos foram executados (ou skip justificado) e o resultado real de cada um está anotado para uso nas tarefas seguintes |
| **Validação** | `git status --short --branch; dotnet build TestOrder.slnx; .\scripts\test.ps1; cd src/TestOrder.Web; npm run build; cd ../..` |
| **Paralelo** | Não — primeiro passo obrigatório |

**Checkpoint Phase 1**: Baseline real conhecida (build/testes/frontend/worker + estado do Git).

---

## Phase 2: Foundational — Auditoria bruta (bloqueante)

**Purpose**: Levantar achados de higiene do repositório e de conteúdo sensível que alimentam **todas** as user stories seguintes (README, AI_NOTES, PRESENTATION_GUIDE, specs 001–005).

**⚠️ CRITICAL**: Nenhuma tarefa de documentação (Phase 3 em diante) deve aplicar correção de conteúdo sensível sem primeiro passar por esta fase.

---

- [X] T002 [P] Auditoria de arquivos versionados indevidamente + revisão do `.gitignore`

**Detalhe T002**
| Campo | Valor |
| --- | --- |
| **Descrição** | Após `npm install` (frontend e worker, já feito/possível em T001) e `dotnet build`, rodar `git status --porcelain` e confirmar que `node_modules/`, `dist/`, `bin/`, `obj/`, screenshots temporários e arquivos locais (`*.local.*`, capturas de validação) não aparecem como novos/pendentes. Revisar `.gitignore` na raiz contra esses padrões — **append mínimo apenas se algo faltar**, sem reescrever o arquivo. |
| **Permitidos** | `.gitignore` (append pontual, só se necessário) |
| **Proibidos** | Reescrever `.gitignore` inteiro; alterar backend/frontend/worker |
| **Pronto quando** | `git status --porcelain` limpo de artefatos de build/dependência (AC-007, AC-008) |
| **Paralelo** | Sim — arquivo independente de T003 |

---

- [X] T003 [P] Auditoria de conteúdo sensível em `specs/`, `docs/`, `AI_NOTES.md`, `README.md`

**Detalhe T003**
| Campo | Valor |
| --- | --- |
| **Descrição** | Busca textual (grep, case-insensitive) por: `senha`, `password`, `secret`, `token`, `api[_ ]?key`, nomes de empresas/clientes/projetos externos privados, caminhos locais desnecessários (ex. `C:\Users\<nome real>`), menções a screenshots temporários e logs de sandbox longos que não agregam valor à leitura. **Manter** referências públicas legítimas já usadas como justificativa técnica (ex. artigo público da Shopify sobre `FOR UPDATE SKIP LOCKED`) — não são consideradas sensíveis (ver Assumptions da [spec.md](./spec.md)). Esta tarefa apenas **lista os achados**; a correção efetiva acontece nas tarefas de cada documento (T004–T015). |
| **Permitidos** | Nenhum arquivo alterado nesta tarefa (apenas levantamento) |
| **Proibidos** | Aplicar correções aqui — isso é feito nas tarefas específicas de cada documento |
| **Pronto quando** | Lista de achados (ou confirmação de que nada foi encontrado) está pronta para orientar T004–T015 |
| **Paralelo** | Sim — independente de T002 |

**Checkpoint Phase 2**: Achados de higiene e de conteúdo sensível conhecidos — pode iniciar a correção por documento.

---

## Phase 3: User Story 1 — Subir e validar com poucos comandos (Priority: P1) 🎯 MVP

**Goal**: `README.md` permite a qualquer avaliador subir o ambiente completo e rodar as validações finais com poucos comandos claros.

**Independent Test**: Ler apenas o `README.md` e conseguir enumerar, sem abrir outro arquivo: pré-requisitos, comando único de subida, as 4 janelas esperadas, e os comandos de validação final.

---

- [X] T004 [P] [US1] Revisar setup mínimo, pré-requisitos e `dev-up.ps1` no `README.md`

**Detalhe T004**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar/ajustar no `README.md`: pré-requisitos (Docker, .NET SDK, Node.js/npm) claramente listados; comando único `.\scripts\dev-up.ps1`; lista das quatro janelas esperadas (MySQL, API, Web, Worker) com portas. Aplicar qualquer correção de conteúdo sensível encontrada em T003 que envolva este arquivo. |
| **Depende de** | T002, T003 (Foundational) |
| **Permitidos** | `README.md` |
| **Proibidos** | Alterar `scripts/dev-up.ps1` (auditoria apenas — ver T009/T010 e Assumptions) |
| **Pronto quando** | AC-001, AC-003 verificáveis lendo só o README |
| **Paralelo** | Sim — arquivo próprio, após Foundational (paralelo a T007 e T009, que editam outros arquivos) |

---

- [X] T005 [US1] Revisar comandos de validação final e endpoints principais no `README.md`

**Detalhe T005**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar/ajustar no `README.md`: comandos exatos e corretos para `dotnet build TestOrder.slnx`, `.\scripts\test.ps1`, `npm run build` (frontend) e smoke do worker (`node index.js` em `src/TestOrder.OrderProcessor`); breve descrição dos endpoints principais da API e do fluxo de validação manual mínimo (criar pedido → ver outbox `processed`). Usar os resultados reais de T001 como referência do que documentar. |
| **Depende de** | T004 (mesmo arquivo, sequencial) |
| **Permitidos** | `README.md` |
| **Pronto quando** | AC-002 — comandos presentes e corretos |
| **Paralelo** | Não — mesmo arquivo que T004 |

**Checkpoint Phase 3 (US1)**: `README.md` cobre setup + validação final sem depender de outro documento.

---

## Phase 4: User Story 5 — Dados seedados documentados (Priority: P2)

**Goal**: Os números do seed de desenvolvimento estão consolidados num único lugar do `README.md`.

**Independent Test**: Ler o `README.md` e encontrar, numa única seção, os números do seed (50 produtos, 5000 pedidos, itens/pedido, `inventory_units`) com nota de que são dados determinísticos de desenvolvimento.

---

- [X] T006 [US5] Consolidar seção "Dados de desenvolvimento (seed)" no `README.md`

**Detalhe T006**
| Campo | Valor |
| --- | --- |
| **Descrição** | Adicionar/consolidar no `README.md` uma tabela ou parágrafo único com: 50 produtos, 5000 pedidos, itens realistas por pedido (média ~3,5 itens/pedido), backfill de `inventory_units` (~237 mil unidades) gerado automaticamente e de forma idempotente no primeiro start da API. Nota explícita: dados determinísticos de desenvolvimento, não produção. Link para o detalhe histórico em `AI_NOTES.md` (módulos 001/002), sem duplicar o conteúdo. |
| **Depende de** | T005 (mesmo arquivo, sequencial) |
| **Permitidos** | `README.md` |
| **Pronto quando** | AC-009 — dados seedados documentados num único lugar |
| **Paralelo** | Não — mesmo arquivo que T004/T005 |

**Checkpoint Phase 4 (US5)**: `README.md` completo (US1 + US5) — pode seguir para revisão de `AI_NOTES.md`/`PRESENTATION_GUIDE.md` em paralelo, já iniciada nas fases seguintes.

---

## Phase 5: User Story 3 — Uso real de IA por módulo (Priority: P1)

**Goal**: `AI_NOTES.md` explica, por módulo (001–005), onde a IA ajudou, onde errou/foi corrigida, e quais decisões foram humanas.

**Independent Test**: Ler `AI_NOTES.md` e confirmar que cada módulo (001–005) tem pelo menos uma decisão humana explícita e pelo menos um erro real de IA identificado, sem trechos genéricos.

---

- [X] T007 [P] [US3] Revisar `AI_NOTES.md` módulo a módulo (001–005)

**Detalhe T007**
| Campo | Valor |
| --- | --- |
| **Descrição** | Reler cada seção de módulo em `AI_NOTES.md`. Confirmar presença de: decisão humana explícita, erro real de IA corrigido, fluxo Spec Kit usado (`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement`). **Critério objetivo de ruído (L3)**: remover transcrições longas de sandbox (logs de processo, tentativas de comando, saídas de terminal extensas) e substituí-las por um resumo objetivo de no máximo 2–3 frases cobrindo o que foi tentado, o que falhou e o impacto real da correção — sem colar blocos de log brutos. Remover qualquer referência a projeto externo privado encontrada em T003. Corrigir apenas o que estiver genérico demais ou incorreto — não reescrever seções que já atendem ao critério. |
| **Depende de** | T002, T003 (Foundational) |
| **Permitidos** | `AI_NOTES.md` |
| **Proibidos** | Remover o registro histórico de decisões/erros reais — só reduzir ruído, não conteúdo relevante |
| **Pronto quando** | AC-004 — cada módulo 001–005 tem conteúdo concreto e verificável |
| **Paralelo** | Sim — arquivo próprio, após Foundational (paralelo a T004 e T009) |

---

- [X] T008 [US3] Criar estrutura da seção "Módulo 006" em `AI_NOTES.md`

**Detalhe T008**
| Campo | Valor |
| --- | --- |
| **Descrição** | Adicionar a seção "Módulo 006" em `AI_NOTES.md` com os cabeçalhos que serão preenchidos com resultados reais na Fase de Fechamento (T019): correções pontuais aplicadas, resultado da auditoria de conteúdo sensível, resultado da auditoria de arquivos versionados, resultados das validações finais, reflexão sobre o processo Spec Kit nos 6 módulos. Não preencher com dados finais ainda — apenas a estrutura. |
| **Depende de** | T007 (mesmo arquivo, sequencial) |
| **Permitidos** | `AI_NOTES.md` |
| **Pronto quando** | Estrutura da seção Módulo 006 existe, pronta para receber dados reais em T019 |
| **Paralelo** | Não — mesmo arquivo que T007 |

**Checkpoint Phase 5 (US3)**: `AI_NOTES.md` revisado (001–005) e com estrutura pronta para o fechamento.

---

## Phase 6: User Story 2 — Roteiro de apresentação único (Priority: P1)

**Goal**: `docs/PRESENTATION_GUIDE.md` cobre os módulos 001–005 em ordem, sem placeholders, com roteiro de demo pronto.

**Independent Test**: Ler `docs/PRESENTATION_GUIDE.md` do início ao fim e confirmar ausência de "a preencher"/placeholders, presença de uma ordem de demo clara, e que `.\scripts\dev-up.ps1` é sempre o comando de subida recomendado (sem alternativa promovida como principal em README/PRESENTATION_GUIDE/quickstarts).

---

- [X] T009 [P] [US2] Revisar roteiro por módulo (001–005), ordem de demonstração e primazia do `dev-up.ps1` em `docs/PRESENTATION_GUIDE.md`

**Detalhe T009**
| Campo | Valor |
| --- | --- |
| **Descrição** | Confirmar que cada módulo (001–005) tem: mensagem central, passos numerados de demo, trecho de código referenciado corretamente (comparar com o arquivo real), tabela de validações com status real. Buscar e eliminar qualquer "A preencher"/placeholder remanescente. Incluir/confirmar a ordem recomendada de demo ponta a ponta: `dev-up.ps1` → listar pedidos → criar pedido → cenário de estoque insuficiente → faturamento por período → worker/outbox → suíte de testes. Manter referências de código úteis, sem excesso (evitar citar trechos que não agregam à explicação). Aplicar correções de conteúdo sensível de T003, se houver. **(C1 — FR-003/AC-003)** Buscar (grep) explicitamente por comandos alternativos de subida em `README.md`, `docs/PRESENTATION_GUIDE.md` (este arquivo) e `specs/*/quickstart.md`: `.\scripts\dev-up.ps1` deve ser sempre apresentado como o caminho recomendado primeiro; qualquer comando alternativo (`dotnet run`, `npm run dev`, `node index.js` isolado) só pode aparecer como opção secundária de depuração pontual, nunca como preferencial. Se algum comando alternativo estiver promovido indevidamente em `README.md` ou em `specs/*/quickstart.md`, registrar o achado (a correção do arquivo específico é feita por sua tarefa dona — T004–T006 para README, T011–T015 para quickstart.md de cada módulo). **(L2)** Ao revisar o roteiro completo, estimar o tempo de execução por módulo e confirmar que a soma fica na faixa de 30–50 min (NFR-008); se exceder, sinalizar oportunidade de corte, sem necessariamente reescrever o conteúdo nesta tarefa. |
| **Depende de** | T002, T003 (Foundational) |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Pronto quando** | AC-005 — nenhuma seção pendente; ordem de demo completa e coerente |
| **Paralelo** | Sim — arquivo próprio, após Foundational (paralelo a T004 e T007) |

---

- [X] T010 [US2] Criar seção de fechamento do Módulo 006 em `docs/PRESENTATION_GUIDE.md`

**Detalhe T010**
| Campo | Valor |
| --- | --- |
| **Descrição** | Adicionar seção explicando que o Módulo 006 é uma auditoria de fechamento (não uma feature nova), com referência a `docs/DELIVERY_CHECKLIST.md` (status real preenchido em T020). Atualizar a "Ordem de apresentação planejada" no topo do documento: marcar módulo 5 como concluído e módulo 6 como fechamento. |
| **Depende de** | T009 (mesmo arquivo, sequencial) |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Pronto quando** | Seção de fechamento existe, com placeholder de status a preencher em T020 |
| **Paralelo** | Não — mesmo arquivo que T009 |

**Checkpoint Phase 6 (US2)**: Roteiro de apresentação completo e pronto para receber o status final do checklist.

---

## Phase 7: User Story 4 — Repositório limpo e specs consistentes (Priority: P2)

**Goal**: `specs/001-*` a `specs/005-*` consistentes entre si, sem conteúdo sensível, sem links quebrados, e com `quickstart.md` de cada módulo confirmando `dev-up.ps1` como caminho principal (FR-003/AC-003).

**Independent Test**: Reler cada pasta de spec (incluindo `quickstart.md`) e confirmar estrutura comparável (Objetivo, Jornadas, Requisitos, Critérios de aceite), ausência de achados de T003 não corrigidos, e que nenhum `quickstart.md` promove um comando de subida alternativo a `.\scripts\dev-up.ps1` como principal.

---

- [X] T011 [P] [US4] Reler `specs/001-base-listagem-pedidos/` (spec.md, plan.md, quickstart.md) quanto a consistência, conteúdo sensível e primazia do `dev-up.ps1`

**Detalhe T011**
| Campo | Valor |
| --- | --- |
| **Descrição** | Reler `spec.md`, `plan.md` (quando existir) **e `quickstart.md`**. Verificar estrutura comparável aos demais módulos, ausência de credenciais/dados pessoais/referências externas privadas (achados de T003), e links internos válidos. **(C1 — FR-003/AC-003)** Confirmar que `quickstart.md` não apresenta nenhum comando de subida alternativo a `.\scripts\dev-up.ps1` como preferencial — comandos manuais (`dotnet run`, `npm run dev`) só podem aparecer como depuração pontual de um serviço isolado. Corrigir **apenas** o que for inconsistência real, texto sensível, link quebrado ou comando alternativo indevidamente promovido — não reescrever a spec. |
| **Depende de** | T003 (Foundational) |
| **Permitidos** | `specs/001-base-listagem-pedidos/**` |
| **Proibidos** | Reescrever requisitos/critérios de aceite já corretos |
| **Pronto quando** | Nenhum achado sensível pendente; nenhum link quebrado; `quickstart.md` não promove comando alternativo a `dev-up.ps1` como principal |
| **Paralelo** | Sim — pasta independente de T012–T015 |

---

- [X] T012 [P] [US4] Reler `specs/002-criacao-pedido-reservas/` (spec.md, plan.md, quickstart.md) quanto a consistência, conteúdo sensível e primazia do `dev-up.ps1`

**Detalhe T012**
| Campo | Valor |
| --- | --- |
| **Descrição** | Mesmo processo de T011 (incluindo verificação de `quickstart.md` e primazia do `dev-up.ps1`), aplicado a `specs/002-criacao-pedido-reservas/`. |
| **Depende de** | T003 (Foundational) |
| **Permitidos** | `specs/002-criacao-pedido-reservas/**` |
| **Pronto quando** | Nenhum achado sensível pendente; nenhum link quebrado; `quickstart.md` não promove comando alternativo a `dev-up.ps1` como principal |
| **Paralelo** | Sim — pasta independente de T011, T013–T015 |

---

- [X] T013 [P] [US4] Reler `specs/003-faturamento-por-periodo/` (spec.md, plan.md, quickstart.md) quanto a consistência, conteúdo sensível e primazia do `dev-up.ps1`

**Detalhe T013**
| Campo | Valor |
| --- | --- |
| **Descrição** | Mesmo processo de T011 (incluindo verificação de `quickstart.md` e primazia do `dev-up.ps1`), aplicado a `specs/003-faturamento-por-periodo/`. |
| **Depende de** | T003 (Foundational) |
| **Permitidos** | `specs/003-faturamento-por-periodo/**` |
| **Pronto quando** | Nenhum achado sensível pendente; nenhum link quebrado; `quickstart.md` não promove comando alternativo a `dev-up.ps1` como principal |
| **Paralelo** | Sim — pasta independente de T011–T012, T014–T015 |

---

- [X] T014 [P] [US4] Reler `specs/004-tela-web-pedidos/` (spec.md, plan.md, quickstart.md) quanto a consistência, conteúdo sensível e primazia do `dev-up.ps1`

**Detalhe T014**
| Campo | Valor |
| --- | --- |
| **Descrição** | Mesmo processo de T011 (incluindo verificação de `quickstart.md` e primazia do `dev-up.ps1`), aplicado a `specs/004-tela-web-pedidos/`. |
| **Depende de** | T003 (Foundational) |
| **Permitidos** | `specs/004-tela-web-pedidos/**` |
| **Pronto quando** | Nenhum achado sensível pendente; nenhum link quebrado; `quickstart.md` não promove comando alternativo a `dev-up.ps1` como principal |
| **Paralelo** | Sim — pasta independente de T011–T013, T015 |

---

- [X] T015 [P] [US4] Reler `specs/005-worker-outbox-node/` (spec.md, plan.md, quickstart.md) quanto a consistência, conteúdo sensível e primazia do `dev-up.ps1`

**Detalhe T015**
| Campo | Valor |
| --- | --- |
| **Descrição** | Mesmo processo de T011 (incluindo verificação de `quickstart.md` e primazia do `dev-up.ps1`), aplicado a `specs/005-worker-outbox-node/`. |
| **Depende de** | T003 (Foundational) |
| **Permitidos** | `specs/005-worker-outbox-node/**` |
| **Pronto quando** | Nenhum achado sensível pendente; nenhum link quebrado; `quickstart.md` não promove comando alternativo a `dev-up.ps1` como principal |
| **Paralelo** | Sim — pasta independente de T011–T014 |

**Checkpoint Phase 7 (US4)**: Specs 001–005 consistentes; higiene de conteúdo sensível confirmada em toda a árvore `specs/`.

---

## Phase 8: User Story 6 — Checklist final de entrega (Priority: P1)

**Goal**: Existe um checklist único, objetivo e verificável cobrindo todos os pontos de auditoria do módulo.

**Independent Test**: Abrir `docs/DELIVERY_CHECKLIST.md` e confirmar que cada um dos 14 critérios de aceite da spec tem uma linha com comando/passo de verificação.

---

- [X] T016 [US6] Criar `docs/DELIVERY_CHECKLIST.md` com os 14 itens de auditoria

**Detalhe T016**
| Campo | Valor |
| --- | --- |
| **Descrição** | Criar o arquivo com tabela `Item \| Categoria \| Como verificar \| Status`, uma linha por critério de aceite `AC-001`–`AC-014` da [spec.md](./spec.md), agrupada por categoria (Setup, Apresentação, IA/AI_NOTES, Higiene do repositório, Dados seed, Validações finais, Escopo) — formato definido em [data-model.md](./data-model.md)/[research.md R5](./research.md). **(M2)** Convenção de status única: `PASS` (verificado com sucesso, incluindo quando uma correção pontual foi aplicada antes de passar — registrar em `observacao`), `PENDING` (aguardando a validação final de T017), `N/A` (não se aplica, com justificativa obrigatória em `observacao`). Preencher `status` com o resultado já conhecido das Phases 2–7 (`PASS` ou `N/A`); os itens que dependem da validação técnica final (build/testes/frontend/worker, fluxo manual outbox) ficam como `PENDING` até T017/T018. Incluir seção com os comandos finais que serão executados, pontos-chave para demonstrar na apresentação, validações complementares e itens fora de escopo (reaproveitando "Fora de escopo" e "Casos de borda" da spec). |
| **Depende de** | T002, T003, T006, T008, T010, T011–T015 |
| **Permitidos** | `docs/DELIVERY_CHECKLIST.md` (novo arquivo) |
| **Pronto quando** | 14 itens presentes; nenhum item sem `como_verificar`; itens que dependem da validação final marcados `PENDING` |
| **Paralelo** | Não — consolida resultado de todas as fases anteriores |

**Checkpoint Phase 8 (US6)**: Checklist criado; falta apenas o resultado real das validações finais.

---

## Phase 9: Validação final

**Goal**: Reexecutar os gates obrigatórios — incluindo o fluxo manual UI → outbox `processed` — e confirmar que nada regrediu antes de fechar o checklist.

**Independent Test**: Todos os comandos abaixo passam (ou têm skip justificado); fluxo manual outbox reexecutado com sucesso; nenhuma alteração fora do escopo permitido.

---

- [X] T017 Rodar validação final completa (build, testes, frontend, worker, fluxo manual outbox, escopo, conteúdo sensível)

**Detalhe T017**
| Campo | Valor |
| --- | --- |
| **Descrição** | Reexecutar os gates finais e registrar resultado real de cada um, incluindo **(C2 — AC-013/NFR-002)** a reexecução real do fluxo manual UI → outbox `processed` (não apenas a documentação herdada do módulo 005): (1) subir o ambiente com `.\scripts\dev-up.ps1`; (2) criar um pedido pela UI ou via API; (3) consultar `order_processing_events` e confirmar que o evento nasce `pending`; (4) aguardar o worker e confirmar que o mesmo evento muda para `processed`; (5) anotar o resultado real (sucesso/falha, tempo observado) para registro em T018/T019. **(L1)** Se o MySQL não estiver disponível no momento desta validação, registrar explicitamente o skip e o motivo — não presumir sucesso. Qualquer correção de código só é aplicada aqui se uma falha **real** for encontrada, e deve ser justificada em `AI_NOTES.md` (T019), nunca especulativa (FR-015/FR-016). |
| **Depende de** | T016 |
| **Permitidos** | Ajustes mínimos e justificados em código/testes **somente** se uma falha real for encontrada (caso raro; exige registro em T019) |
| **Proibidos** | Qualquer alteração de código não motivada por uma falha real observada nesta tarefa |
| **Pronto quando** | Todos os comandos abaixo executam com sucesso (ou falha/skip real documentado e, se aplicável, corrigido) |
| **Validação** | Ver bloco de comandos abaixo |
| **Paralelo** | Não — último gate técnico antes do fechamento |

**Comandos de validação final**:

```powershell
# Backend
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1

# Frontend
cd src/TestOrder.Web
npm run build
cd ../..

# Worker smoke — SE MySQL disponível; senão, registrar skip justificado (L1)
cd src/TestOrder.OrderProcessor
node index.js
# Ctrl+C após alguns ciclos
cd ../..

# (C2) Fluxo manual UI -> outbox "processed" — execução real, não apenas documentada
# 1. .\scripts\dev-up.ps1  (4 janelas: MySQL, API, Web, Worker)
# 2. Criar um pedido pela tela React (ou via POST /api/orders)
# 3. Consultar o evento recem-criado (deve nascer 'pending'):
#    SELECT id, event_type, status, created_at FROM order_processing_events
#    ORDER BY id DESC LIMIT 5;
# 4. Aguardar alguns segundos e confirmar a transicao para 'processed'
#    (mesma query acima) + observar o log JSON na janela "TestOrder - Worker"
# 5. Registrar o resultado real (PASS/PENDING/skip justificado) em T018/T019

# Escopo — nenhuma alteração indevida em código de produção
git diff --check
git diff --name-only
# Esperado: apenas docs/, README.md, AI_NOTES.md, .gitignore, specs/**
# (correções pontuais em src/**/tests/** exigem justificativa registrada em AI_NOTES.md)

# (M1) Busca final por termos sensíveis — reexecução dos padrões de T003, comando real
rg -i -g '!node_modules' -g '!dist' -g '!bin' -g '!obj' -g '!.git' `
  "senha|password|secret|token|api[_ -]?key" `
  specs/ docs/ AI_NOTES.md README.md
# Buscar localmente nomes de projetos externos privados conhecidos, sem registrar esses nomes no artefato versionado.
# Qualquer ocorrência de "Shopify" é referência pública legítima (Assumptions da spec.md) — não corrigir.

# (M1) Confirmar que nenhum screenshot temporário ficou versionado
git ls-files -- '*.png' '*.jpg' '*.jpeg' '*.gif'
```

**Checkpoint Phase 9**: Gates técnicos confirmados (incluindo o fluxo manual outbox reexecutado); pronto para preencher o checklist final com dados reais.

---

## Phase 10: Fechamento

**Goal**: Registrar os resultados reais em todos os artefatos e encerrar o módulo com o repositório pronto para envio.

**Independent Test**: `docs/DELIVERY_CHECKLIST.md` 100% preenchido; `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md` e `quickstart.md` refletem resultados reais; `tasks.md` marcado como concluído.

---

- [X] T018 [US6] Preencher `docs/DELIVERY_CHECKLIST.md` com status reais (`PASS`/`PENDING`/`N/A`) e comandos executados

**Detalhe T018**
| Campo | Valor |
| --- | --- |
| **Descrição** | Substituir os itens `PENDING` pelos resultados reais de T017 (**M2** — convenção única `PASS`/`PENDING`/`N/A`, igual à de [data-model.md](./data-model.md), sem mapeamento para outra nomenclatura). Registrar explicitamente o resultado real do item AC-013 (fluxo manual UI → outbox `processed` reexecutado em T017 — C2), incluindo a lista exata de comandos finais executados, os pontos-chave para demonstrar na apresentação (derivados de T009/T010), validações complementares e itens fora de escopo. |
| **Depende de** | T016, T017 |
| **Permitidos** | `docs/DELIVERY_CHECKLIST.md` |
| **Pronto quando** | AC-010, AC-013, SC-004 — 100% dos itens com status real, nenhum placeholder |
| **Paralelo** | Não — depende do resultado final de T017 |

---

- [X] T019 [US3] Preencher seção "Módulo 006" em `AI_NOTES.md` com resultados reais

**Detalhe T019**
| Campo | Valor |
| --- | --- |
| **Descrição** | Completar a estrutura criada em T008 com: lista real de correções pontuais aplicadas (arquivo, motivo, mudança) ou declaração explícita de que nenhuma foi necessária; resultado real da auditoria de conteúdo sensível (T003) e de arquivos versionados (T002); resultados reais de T017; reflexão curta sobre o processo Spec Kit ao longo dos 6 módulos. |
| **Depende de** | T008, T017, T018 |
| **Permitidos** | `AI_NOTES.md` |
| **Pronto quando** | NFR-007 — toda correção pontual rastreada com motivo e mudança exata |
| **Paralelo** | Sim — arquivo próprio, pode rodar junto com T020 |

---

- [X] T020 [US2] Preencher seção de fechamento em `docs/PRESENTATION_GUIDE.md` com status real

**Detalhe T020**
| Campo | Valor |
| --- | --- |
| **Descrição** | Completar a seção criada em T010 com o status real do checklist (referenciando `docs/DELIVERY_CHECKLIST.md`, já preenchido em T018). |
| **Depende de** | T010, T018 |
| **Permitidos** | `docs/PRESENTATION_GUIDE.md` |
| **Pronto quando** | AC-005, SC-006 — roteiro sem pendências, fechamento com status real |
| **Paralelo** | Sim — arquivo próprio, pode rodar junto com T019 |

---

- [X] T021 Atualizar `specs/006-fechamento-final-entrega/quickstart.md` com resultados reais

**Detalhe T021**
| Campo | Valor |
| --- | --- |
| **Descrição** | Preencher a tabela "Resultado esperado da validação" do `quickstart.md` deste módulo com os resultados reais de T017–T020 (substituir todas as ocorrências de "A preencher"). |
| **Depende de** | T017, T018, T019, T020 |
| **Permitidos** | `specs/006-fechamento-final-entrega/quickstart.md` |
| **Pronto quando** | Tabela sem nenhum "A preencher" |
| **Paralelo** | Não — consolida resultado de todas as tarefas anteriores |

---

- [X] T022 Fechamento formal — marcar `tasks.md` concluído e confirmar escopo final

**Detalhe T022**
| Campo | Valor |
| --- | --- |
| **Descrição** | Marcar todas as caixas `[ ]` deste arquivo como `[X]` (**somente** após a execução real de cada tarefa — nunca marcar preventivamente). Rodar `git status --porcelain` e `git diff --name-only` uma última vez e confirmar que o conjunto de arquivos alterados corresponde exatamente ao esperado por este módulo (documentação, `.gitignore`, `specs/**`, e no máximo correções pontuais justificadas em `src/**`/`tests/**`, se houver). Reportar ao usuário: arquivos alterados, validações executadas com resultado e validações complementares registradas. |
| **Depende de** | T021 |
| **Permitidos** | `specs/006-fechamento-final-entrega/tasks.md` (checkboxes) |
| **Pronto quando** | Todas as tarefas reais concluídas; relatório final entregue ao usuário |
| **Paralelo** | Não — último passo |

**Checkpoint Phase 10**: Módulo 006 completo — repositório pronto para envio/apresentação.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 (preflight)
T002 [P], T003 [P] (Foundational, após T001)
T004 [P] → T005 → T006 (README.md, sequencial; US1 então US5)
T007 [P] → T008 (AI_NOTES.md, sequencial; US3)
T009 [P] → T010 (PRESENTATION_GUIDE.md, sequencial; US2)
T011 [P], T012 [P], T013 [P], T014 [P], T015 [P] (specs 001–005, independentes entre si; US4)
T016 (DELIVERY_CHECKLIST.md — depende de T002, T003, T006, T008, T010, T011–T015)
T017 (validação final — após T016)
T018 (após T016, T017)
T019 [P], T020 [P] (após T018 — arquivos diferentes)
T021 (após T017–T020)
T022 (após T021 — último passo)
```

### User Story Mapping

| Story | Prioridade | Tarefas principais |
| --- | --- | --- |
| US1 — Setup com poucos comandos (README) | P1 | T004, T005 |
| US5 — Dados seedados documentados | P2 | T006 |
| US3 — Uso real de IA (AI_NOTES) | P1 | T007, T008, T019 |
| US2 — Roteiro de apresentação | P1 | T009, T010, T020 |
| US4 — Repositório limpo e specs consistentes | P2 | T002, T003, T011–T015 |
| US6 — Checklist final de entrega | P1 | T016, T018 |

### Parallel Opportunities (paralelismo real)

- **T002 + T003** — arquivos/escopos diferentes (`.gitignore` vs. busca textual), ambos após T001
- **T004 + T007 + T009** — arquivos diferentes (`README.md`, `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md`), todos dependem só da Foundational (T002/T003)
- **T011 + T012 + T013 + T014 + T015** — pastas de specs distintas, sem dependência entre si
- **T019 + T020** — arquivos diferentes (`AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md`), ambos após T018
- **Sequenciais obrigatórios (mesmo arquivo)**: T004→T005→T006 (`README.md`); T007→T008 (`AI_NOTES.md`); T009→T010 (`docs/PRESENTATION_GUIDE.md`)

---

## MVP Scope

**MVP mínimo demonstrável**: Phase 1–3 (T001–T005).

`README.md` já permite subir o ambiente e rodar as validações finais com poucos comandos — suficiente para um avaliador não travar na primeira etapa.

**MVP recomendado para fechamento real**: MVP acima + Phase 4–8 (T006–T016) + Phase 9 (T017).

Cobre todas as user stories P1/P2 e produz o checklist final com status real das validações técnicas.

**Módulo completo** exige Phase 10 (T018–T022): checklist 100% preenchido, `AI_NOTES.md`/`PRESENTATION_GUIDE.md`/`quickstart.md` com resultados reais, e fechamento formal.

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1: Preflight → baseline real conhecida
2. Phase 2: Foundational → achados de higiene e conteúdo sensível
3. Phase 3: README (US1) → setup + validação final documentados
4. **STOP and VALIDATE**: Ler só o README e confirmar que dá para subir o ambiente e rodar as validações

### Incremental Delivery

1. Phase 4: Seed no README (US5)
2. Phase 5: AI_NOTES.md (US3)
3. Phase 6: PRESENTATION_GUIDE.md (US2)
4. Phase 7: Specs 001–005 (US4)
5. Phase 8: Checklist final estruturado (US6)
6. Phase 9: Validação final real
7. Phase 10: Preenchimento com dados reais + fechamento formal

---

## Critérios de pronto por User Story

| Story | Critério | Evidência |
| --- | --- | --- |
| US1 | README permite subir tudo + validar com poucos comandos | T004–T005 |
| US5 | Dados seedados num único lugar | T006 |
| US3 | AI_NOTES cobre IA/erros/decisões por módulo | T007–T008, T019 |
| US2 | Roteiro de apresentação sem pendências | T009–T010, T020 |
| US4 | Specs 001–005 consistentes e sem conteúdo sensível; repositório limpo | T002, T003, T011–T015 |
| US6 | Checklist final 100% preenchido | T016, T018 |

---

## Notes

- Nenhuma alteração de schema, migration, dependência nova ou teste novo é esperada neste módulo (FR-012–FR-015) — qualquer exceção exige justificativa registrada em `AI_NOTES.md` (T019).
- `contracts/` não existe para este módulo — nenhuma interface externa nova ([research.md R7](./research.md)).
- Correções pontuais em `src/**`/`tests/**` só ocorrem se uma falha real for encontrada em T017 — não são esperadas como parte normal do módulo.
- Commit sugerido após cada fase (T001; T002–T003; T004–T006; T007–T008; T009–T010; T011–T015; T016; T017; T018–T020; T021–T022).
- Este `tasks.md` só deve ser marcado como concluído (checkboxes `[X]`) após a execução real de cada tarefa em `/speckit-implement` — nunca preventivamente.
- `rg` (ripgrep) em T003/T017 é uma ferramenta de linha de comando usada ad-hoc para a auditoria — não é adicionada como dependência do projeto (backend/frontend/worker); se indisponível no ambiente, usar `Select-String` do PowerShell como equivalente.
- Convenção de status do checklist final (`docs/DELIVERY_CHECKLIST.md`): sempre `PASS` / `PENDING` / `N/A`, igual em `data-model.md`, `T016` e `T018` — nenhuma outra nomenclatura deve ser introduzida durante a implementação.
