# Quickstart — Módulo 006: Fechamento Final da Entrega

**Objetivo**: Guiar a execução reproduzível da auditoria de fechamento — não valida uma feature nova, valida que a documentação e o repositório estão prontos para envio/apresentação.
**Referências**: [spec.md](./spec.md) | [data-model.md](./data-model.md) | [research.md](./research.md)

## Pré-requisitos

- Módulos 001–005 concluídos e validados (46/46 testes, frontend e worker funcionais).
- .NET 10 SDK + Docker (MySQL) + Node.js 18+ e npm.
- Ambiente limpo o suficiente para reexecutar `npm install`/`dotnet build` e observar `git status --porcelain` de forma confiável.
- `rg` (ripgrep) para as buscas de conteúdo sensível — se indisponível, usar `Select-String` do PowerShell como equivalente (não é uma dependência do projeto, apenas ferramenta de auditoria ad-hoc).

---

## 1. Auditar o `README.md` (Setup mínimo)

1. Ler o `README.md` do início ao fim como se fosse a primeira vez.
2. Confirmar que ele lista: pré-requisitos, o comando único `.\scripts\dev-up.ps1`, as janelas esperadas (MySQL/API/Web/Worker) e os comandos de validação final (build, testes, build do frontend, smoke do worker).
3. Confirmar que existe uma seção consolidada com os números do seed (produtos, pedidos, itens/pedido, unidades de inventário) — ver [R4](./research.md).
4. Confirmar que `docs/PRESENTATION_GUIDE.md` e `docs/DELIVERY_CHECKLIST.md` estão referenciados a partir do README.

**Esperado**: nenhuma dúvida de setup exigiria perguntar ao candidato.

---

## 2. Auditar `scripts/dev-up.ps1` como caminho principal

1. Buscar (grep) por comandos alternativos de subida em `README.md`, `docs/PRESENTATION_GUIDE.md` e `specs/*/quickstart.md`.
2. Confirmar que `.\scripts\dev-up.ps1` é sempre apresentado como o caminho recomendado primeiro; comandos manuais (`dotnet run`, `npm run dev`, `node index.js`) aparecem apenas como alternativa para depuração pontual de um serviço.

**Esperado**: nenhum documento sugere um fluxo de subida diferente como principal.

---

## 3. Auditar `AI_NOTES.md` (uso real de IA)

1. Ler a seção de cada módulo (001 a 005).
2. Para cada módulo, confirmar presença de: pelo menos uma decisão humana explícita, pelo menos um erro real de IA identificado/corrigido, e o fluxo Spec Kit usado.
3. Anotar qualquer seção genérica demais para ser verificável (ex. "a IA ajudou bastante" sem exemplo concreto) — se encontrado, ajustar o texto durante a implementação (ver [R6](./research.md)).

**Esperado**: cada módulo é diferenciável dos demais — não é um texto genérico copiado/colado.

---

## 4. Auditar `docs/PRESENTATION_GUIDE.md` (roteiro de apresentação)

1. Ler do início ao fim.
2. Buscar (grep) por "A preencher", "TODO", "TBD" ou placeholders equivalentes nas seções dos módulos 001–005.
3. Conferir, por amostragem, que pelo menos um trecho de código citado por módulo corresponde ao arquivo real atual (copiar o trecho do guia e comparar com o arquivo fonte).
4. Confirmar que a "Ordem de apresentação planejada" no topo do documento reflete os módulos 001–005 como concluídos e referencia o módulo 006 como fechamento.

**Esperado**: zero placeholders pendentes; nenhuma inconsistência entre código citado e código real.

---

## 5. Auditar consistência das specs 001–005

1. Para cada pasta `specs/00N-*` (001 a 005): reler `spec.md`, `plan.md` (quando existir) **e `quickstart.md`**.
2. Buscar (grep, case-insensitive) por termos de risco: `senha`, `password`, `secret`, `token`, `api key`, `apikey`, nomes de empresas/clientes/projetos externos privados que não sejam o próprio desafio, caminhos locais desnecessários (ex. `C:\Users\<nome real>`), menções a screenshots temporários e logs longos de sandbox — mantendo referências técnicas públicas já intencionais (ex. artigo da Shopify). Nomes privados conhecidos devem ser buscados localmente, mas não registrados literalmente no artefato versionado.
3. Confirmar que links internos (para outros arquivos do repositório) não estão quebrados.
4. Confirmar consistência de formatação/nomenclatura entre módulos (títulos, seções obrigatórias presentes: Objetivo, Jornadas, Requisitos, Critérios de aceite).
5. Confirmar que `quickstart.md` de cada módulo não promove um comando de subida alternativo a `.\scripts\dev-up.ps1` como principal (mesmo critério da seção 2 acima, aplicado arquivo a arquivo).

**Esperado**: nenhuma ocorrência de conteúdo sensível; nenhum link quebrado; estrutura comparável entre módulos; `dev-up.ps1` como caminho principal em todos os `quickstart.md`.

---

## 6. Auditar arquivos versionados indevidamente

```powershell
# Backend
dotnet build TestOrder.slnx

# Frontend
cd src/TestOrder.Web
npm install
cd ../..

# Worker
cd src/TestOrder.OrderProcessor
npm install
cd ../..

# Estado do Git
git status --porcelain

# Screenshots temporários versionados por engano
git ls-files -- '*.png' '*.jpg' '*.jpeg' '*.gif'
```

**Esperado**: `git status --porcelain` não lista `node_modules/`, `dist/`, `bin/`, `obj/` como arquivos novos/pendentes; nenhum screenshot temporário rastreado. Se listar, revisar e complementar o `.gitignore` (append mínimo).

---

## 7. Validações finais (obrigatórias)

```powershell
# Backend
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1

# Frontend
cd src/TestOrder.Web
npm run build
cd ../..

# Worker (smoke) — SE MySQL disponível; senão, registrar skip justificado
cd src/TestOrder.OrderProcessor
node index.js
# Ctrl+C após alguns ciclos
cd ../..

# Busca final por termos sensíveis (reexecução da seção 5, comando real)
rg -i -g '!node_modules' -g '!dist' -g '!bin' -g '!obj' -g '!.git' `
  "senha|password|secret|token|api[_ -]?key" `
  specs/ docs/ AI_NOTES.md README.md
# Buscar localmente nomes de projetos externos privados conhecidos, sem registrar esses nomes no artefato versionado.
# Ocorrências de "Shopify" são referência pública legítima (Assumptions da spec.md) — não corrigir.
```

**Esperado**: build sem erros; **46/46** testes; `dist/` gerado pelo frontend; worker inicia e conecta sem erro (ou skip justificado); nenhuma ocorrência indevida de termo sensível.

---

## 8. Fluxo manual UI → outbox `processed` (execução real, não apenas documentação herdada)

1. Subir tudo com `.\scripts\dev-up.ps1`.
2. Criar um pedido pela tela React (ou via API).
3. Consultar `order_processing_events` no MySQL e confirmar que o evento nasce com `status = 'pending'`.
4. Observar a janela `TestOrder - Worker` — log JSON referenciando o pedido em poucos segundos.
5. Consultar `order_processing_events` novamente e confirmar `status = 'processed'` para aquele pedido.

**Esperado**: fluxo reexecutado agora, nesta validação de fechamento (não apenas reaproveitado do módulo 005 por citação) — resultado real registrado em `docs/DELIVERY_CHECKLIST.md` (AC-013).

---

## 9. Preencher e revisar `docs/DELIVERY_CHECKLIST.md`

1. Percorrer cada item (um por critério de aceite `AC-001`–`AC-014` da spec deste módulo).
2. Marcar `status` real (`PASS` / `PENDING` / `N/A` com justificativa — convenção única de [data-model.md](./data-model.md)) para cada item, com base nos passos 1–8 acima.
3. Confirmar que nenhum item ficou sem status preenchido.

---

## 10. Registrar o resultado

- Atualizar a seção "Módulo 006" em `AI_NOTES.md` com: correções pontuais aplicadas (ou confirmação de que nenhuma foi necessária), resultado da auditoria de conteúdo sensível, resultado da auditoria de arquivos versionados, e resultados reais das validações finais.
- Atualizar `docs/PRESENTATION_GUIDE.md` com a seção de fechamento (módulo 006) e link para `docs/DELIVERY_CHECKLIST.md`.

---

## Resultado real da validação

| Validação | Resultado |
| --- | --- |
| `README.md` permite subir tudo com um comando | PASS — pré-requisitos, `dev-up.ps1`, 4 janelas, seed e validações documentados |
| `dev-up.ps1` é o caminho principal em toda a documentação | PASS — nenhum comando alternativo apresentado como principal; 1 correção pontual aplicada em `specs/004-tela-web-pedidos/spec.md` |
| `AI_NOTES.md` cobre uso de IA por módulo (001–005) | PASS — decisão humana, erro real corrigido e fluxo Spec Kit presentes em cada módulo |
| `PRESENTATION_GUIDE.md` sem seções pendentes | PASS — nenhum placeholder nos módulos 001–005; roteiro end-to-end e fechamento do módulo 006 adicionados |
| Specs 001–005 consistentes e sem conteúdo sensível | PASS — nenhuma credencial real/dado pessoal/referência externa indevida; apenas credenciais dev `testorder`/`testorder` (aceitáveis) |
| Nenhum artefato de build versionado (`git status --porcelain --ignored`) | PASS — `node_modules/`, `dist/`, `bin/`, `obj/` corretamente ignorados; nenhum screenshot versionado |
| Dados seedados documentados num único lugar | PASS — tabela consolidada adicionada em `README.md` |
| `docs/DELIVERY_CHECKLIST.md` criado e 100% preenchido | PASS — 14 itens, todos `PASS` (nenhum `PENDING`/`N/A`) |
| `dotnet build` + `.\scripts\test.ps1` | PASS — **46/46** (baseline e final) |
| `npm run build` (frontend) | PASS — `dist/` gerado |
| `node index.js` (worker, smoke) | PASS — conectou ao MySQL e processou eventos reais |
| Fluxo manual UI → outbox `processed` | PASS — pedido `#5018` via API, evento `pending` → `processed` em ~4s, confirmado por SQL e log do worker |

Detalhes completos em `docs/DELIVERY_CHECKLIST.md`, `docs/PRESENTATION_GUIDE.md` (seção "Módulo 006") e `AI_NOTES.md` (seção "Módulo 006").
