# Checklist Final de Entrega — TestOrder

Auditoria de fechamento do desafio antes do envio/apresentação (Módulo 006). Este documento **não** descreve funcionalidade nova — consolida o resultado real da auditoria de documentação, higiene do repositório e validações finais dos módulos 001–005.

Convenção de status: **PASS** (verificado com sucesso — inclui casos em que uma correção pontual foi aplicada antes de passar, registrada na coluna Observação e em [`AI_NOTES.md`](../AI_NOTES.md)), **PENDING** (aguardando a execução da validação técnica final), **N/A** (não se aplica, com justificativa).

## Setup

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-001 | `README.md` permite subir tudo com um comando | Clone limpo (ou ambiente resetado) + `.\scripts\dev-up.ps1`, seguindo apenas o README | PASS | `dev-up.ps1` validado com limpeza ativa: encerrou instâncias antigas do TestOrder, liberou `5069`/`5173`, subiu as 4 janelas e deixou API/frontend operacionais |
| AC-002 | Comandos de validação final documentados e corretos | `dotnet build TestOrder.slnx`, `.\scripts\test.ps1`, `npm run build`, `node index.js` do worker — presentes no README e executam com sucesso | PASS | Os 4 comandos executados nesta sessão com sucesso (ver "Resultado real da validação final" abaixo) |

## Apresentação

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-003 | `dev-up.ps1` é o caminho principal em toda a documentação | Busca por comandos alternativos de subida em README/PRESENTATION_GUIDE/quickstarts | PASS | Nenhum comando alternativo apresentado como preferencial; corrigido 1 caso em `specs/004-tela-web-pedidos/spec.md` (ver AI_NOTES) |
| AC-005 | `PRESENTATION_GUIDE.md` sem seções pendentes | Busca por "a preencher"/placeholders nas seções dos módulos 001–005 | PASS | Nenhuma ocorrência nos módulos 001–005; roteiro end-to-end e estimativa de tempo (30–50 min) adicionados |

## IA / AI_NOTES

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-004 | `AI_NOTES.md` cobre uso de IA, ajustes de qualidade e decisões humanas por módulo | Releitura módulo a módulo (001–005) | PASS | Cada módulo tem "onde a IA ajudou", "ajustes de qualidade realizados" e decisões manuais concretas; trechos operacionais foram condensados para leitura mais objetiva |

## Higiene do repositório

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-006 | Specs 001–005 consistentes e sem conteúdo sensível | Releitura das pastas `specs/001-*` a `specs/005-*` | PASS | Nenhuma credencial real, dado pessoal ou referência externa não intencional; credenciais dev `testorder`/`testorder` documentadas como tal (aceitável) |
| AC-007 | Nenhum artefato de build versionado | `git status --porcelain --ignored` não lista `node_modules/`, `dist/`, `bin/`, `obj/` como rastreados | PASS | Todos aparecem corretamente como ignorados (`!!`); nenhum arquivo de build rastreado |
| AC-008 | `.gitignore` cobre os padrões necessários | Revisão manual do `.gitignore` | PASS | `bin/`/`obj/` (genérico), `node_modules/`, `src/TestOrder.Web/dist/` e `src/TestOrder.Web/.vite/` cobertos; cache `.vite` removido do índice |

## Dados seed

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-009 | Dados seedados documentados num único lugar | README ou AI_NOTES com tabela única (50 produtos, 5000 pedidos, itens/pedido, `inventory_units`) | PASS | Tabela adicionada em `README.md` (seção "Dados de desenvolvimento (seed)"), com referência ao histórico em `AI_NOTES.md` |

## Validações finais

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-011 | Sem regressão no backend | `dotnet build TestOrder.slnx` + `.\scripts\test.ps1` → 46/46 | PASS | Executado duas vezes nesta sessão (baseline T001 e final T017): build 0 erros, **46/46** testes em ambas |
| AC-012 | Frontend e worker seguem funcionais | `npm run build` (frontend) e `node index.js` (worker) sem erro | PASS | `npm run build` gerou `dist/` (~151 KB JS/~48,6 KB gzip); tela validada em navegador após correção de `React is not defined`; worker conectou ao MySQL e processou eventos reais sem erro |
| AC-013 | Fluxo manual UI → outbox `processed` documentado e validado | Criar pedido pela tela, observar worker, confirmar `processed` no MySQL | PASS | Pedido `#5018` criado via `POST /api/orders` (mesmo endpoint usado pela tela); evento `id=19` mudou de `pending` para `processed` em ~4s; log do worker confirma `order-created-processed` |

## Escopo

| Item | Descrição | Como verificar | Status | Observação |
| --- | --- | --- | --- | --- |
| AC-010 | Checklist final de entrega existe e é verificável | Este arquivo, com itens objetivos e status preenchido | PASS | Todos os 14 itens preenchidos com status real (`PASS`/`N/A`, nenhum `PENDING` remanescente) |
| AC-014 | Nenhuma funcionalidade de negócio nova introduzida | `git diff --name-only` dos módulos 001–005 (código de produção) | PASS | Além da documentação de fechamento, há apenas correção pontual de runtime do frontend (`App.jsx`/`api.js`) e higiene de cache Vite (`.gitignore` + remoção de `.vite` do índice). Nenhum arquivo de `src/TestOrder.Api`, `src/TestOrder.OrderProcessor`, `tests/` ou migrations alterado |

---

## Resultado real da validação final (Fase 9 — T017)

Todos os comandos abaixo foram executados nesta sessão, com MySQL disponível (`docker ps` → `testorder-mysql` healthy):

| Comando | Resultado real |
| --- | --- |
| `git status --short --branch` | Apenas arquivos de documentação deste módulo alterados/novos |
| `dotnet build TestOrder.slnx` | PASS — 0 erros, 0 avisos (baseline e final) |
| `.\scripts\test.ps1` | PASS — **46/46** (baseline e final) |
| `npm run build` (`src/TestOrder.Web`) | PASS — `dist/` gerado (~151 KB JS / ~48,6 KB gzip) |
| `node index.js` (`src/TestOrder.OrderProcessor`) | PASS — conectou ao MySQL, processou eventos reais (`order-created-processed`), sem erro |
| Fluxo manual real (API `POST /api/orders` → outbox → worker) | PASS — pedido `#5018` criado, evento `id=19` mudou `pending` → `processed` em ~4s (1 ciclo de polling) |
| Frontend em navegador após correção pontual | PASS — `.app` e `.app-header` presentes, sem erro de console; mensagem de API indisponível tratada sem `Unexpected token`; proxy Vite validado em instância limpa com API temporária |
| `dev-up.ps1` com instâncias antigas abertas | PASS — encerrou Vites/esbuilds antigos do TestOrder, subiu frontend em `5173`; `/api/products` via proxy retornou JSON e a UI exibiu `Sistema operacional` com 20 pedidos |
| `git diff --check` | PASS — nenhum erro de whitespace |
| Busca por termos sensíveis (`rg`) | PASS — únicas ocorrências são credenciais dev `testorder`/`testorder` (documentadas como aceitáveis) e o próprio padrão de busca citado nos artefatos do Módulo 006; nenhuma ocorrência de nomes de projetos externos privados |
| Screenshots/temporários versionados (`git ls-files -- '*.png' '*.jpg' '*.jpeg' '*.gif'`) | PASS — nenhum arquivo encontrado |
| Artefatos de build (`git status --porcelain --ignored`) | PASS — `node_modules/`, `dist/`, `.vite/`, `bin/`, `obj/` aparecem corretamente como ignorados, nenhum rastreado |
| `git diff --name-only` (escopo) | PASS — documentação de fechamento + correção pontual de runtime do frontend e higiene de cache Vite (ver AC-014) |

Comandos usados (reprodutíveis):

```powershell
git status --short --branch
dotnet build TestOrder.slnx
.\scripts\test.ps1
cd src/TestOrder.Web; npm run build; cd ../..
cd src/TestOrder.OrderProcessor; node index.js  # smoke real; Ctrl+C ao final
git diff --check
rg -i -g '!node_modules' -g '!dist' -g '!bin' -g '!obj' -g '!.git' "senha|password|secret|token|api[_ -]?key" specs/ docs/ AI_NOTES.md README.md
# Nomes de projetos externos privados foram buscados localmente, sem registrar esses nomes no artefato versionado.
git ls-files -- '*.png' '*.jpg' '*.jpeg' '*.gif'
git diff --name-only
```

Fluxo manual real executado: API iniciada (`dotnet run --project src\TestOrder.Api`), worker iniciado (`node index.js`), pedido criado via `POST /api/orders` (mesmo endpoint chamado pela tela React) — evento `OrderCreated` nasceu `pending` em `order_processing_events` e o worker o marcou `processed` em ~4 segundos, confirmado por consulta SQL direta e pelo log JSON do worker. Apos a correcao visual/runtime, uma instancia limpa do Vite com API temporaria confirmou `/api/products` retornando JSON via proxy.

## Pontos-chave para demonstrar na apresentação

Ver roteiro completo em [`docs/PRESENTATION_GUIDE.md`](PRESENTATION_GUIDE.md), seção "Roteiro de demonstração end-to-end (ordem recomendada)": `dev-up.ps1` → listar pedidos → criar pedido → estoque insuficiente → faturamento por período → worker/outbox → testes (46/46). Tempo estimado: 30–50 minutos.
