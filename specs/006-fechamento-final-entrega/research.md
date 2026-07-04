# Fase 0 — Pesquisa e Decisões: Módulo 006 — Fechamento Final da Entrega

**Input**: [spec.md](./spec.md)

Este módulo não introduz tecnologia nova — as decisões aqui são sobre **como conduzir a auditoria e onde registrar seus resultados**, não sobre stack técnica.

---

## R1 — Localização do checklist final de entrega

**Decisão**: Novo arquivo `docs/DELIVERY_CHECKLIST.md`, referenciado a partir do `README.md` e de `docs/PRESENTATION_GUIDE.md`.

**Rationale**: `docs/` já concentra o material de apresentação/entrega (`PRESENTATION_GUIDE.md`). Um arquivo dedicado é rápido de escanear item a item pelo avaliador, sem inflar o `README.md` (cujo foco deve continuar sendo "subir o ambiente com poucos comandos", conforme FR-001/NFR-001 da spec). Nome em inglês mantém consistência com `PRESENTATION_GUIDE.md`, já em inglês no nome de arquivo (conteúdo em português).

**Alternativas consideradas**:
- **Seção dentro do `README.md`**: rejeitada — alongaria o documento de setup com uma tabela de auditoria que não interessa a quem só quer subir o ambiente.
- **Seção dentro do `docs/PRESENTATION_GUIDE.md`**: parcialmente aproveitada (o guia referencia o checklist e resume o status), mas o checklist merece um arquivo próprio, pois seu formato (tabela de itens marcáveis) é diferente de um roteiro de apresentação narrativo.
- **Novo arquivo dentro de `specs/006-fechamento-final-entrega/`**: rejeitada como localização única — ficaria "escondido" para quem não sabe que a pasta `specs/` existe; o avaliador deve encontrá-lo a partir do `README.md` sem precisar conhecer a estrutura interna do Spec Kit.

---

## R2 — Método de auditoria de conteúdo sensível

**Decisão**: Busca textual direcionada (grep) nas pastas `specs/`, `docs/`, `AI_NOTES.md`, `README.md` e `scripts/` por padrões de risco (senha, secret, token, api key, chave privada, nomes de empresas/clientes fora do contexto do desafio), seguida de releitura humana dos trechos encontrados.

**Rationale**: Repositório pequeno (dezenas de arquivos Markdown), sem necessidade de ferramenta dedicada de scanning de segredos. Busca direcionada é rápida, não introduz dependência nova (proibida pela spec) e é auditável (os termos buscados podem ser listados no relatório da auditoria).

**Alternativas consideradas**:
- **Instalar ferramenta de secret scanning (ex. gitleaks, trufflehog)**: rejeitada — adicionaria dependência/ferramenta nova ao processo, contra a restrição explícita do módulo ("não adicionar dependências").
- **Releitura manual sem busca direcionada**: rejeitada como único método — mais lenta e sujeita a esquecimento em arquivos grandes (`AI_NOTES.md` já passa de 300 linhas).

---

## R3 — Método de verificação de arquivos versionados indevidamente

**Decisão**: Rodar `npm install` (frontend e worker) e `dotnet build`, depois `git status --porcelain` na raiz do repositório; qualquer artefato de build/dependência listado como novo/pendente é tratado como falha de higiene a corrigir via `.gitignore`.

**Rationale**: Reaproveita exatamente o padrão já usado no módulo 005 (validação AC-008) e no módulo 001 — nenhuma ferramenta nova, apenas reexecução dos comandos de setup padrão seguida de inspeção do estado do Git.

**Alternativas consideradas**:
- **Inspeção manual de pastas**: rejeitada como único método — `git status --porcelain` é mais confiável e objetivo (reflete exatamente o que o Git rastrearia).

---

## R4 — Consolidação da documentação dos dados seedados

**Decisão**: Nova seção curta no `README.md` ("Dados de desenvolvimento (seed)") com uma tabela única (produtos, pedidos, itens/pedido, unidades de inventário), com nota explícita de que são dados determinísticos de desenvolvimento — mais um link para o detalhe já existente em `AI_NOTES.md` (módulos 001/002) para quem quiser o histórico completo.

**Rationale**: O `README.md` é o primeiro documento lido; uma tabela curta ali evita que o avaliador precise abrir `AI_NOTES.md` só para interpretar os números vistos durante a demo (ex. "por que existem 5000 pedidos e ~237 mil unidades de inventário").

**Alternativas consideradas**:
- **Manter só em `AI_NOTES.md`** (situação atual, espalhada entre módulos 001 e 002): rejeitada — exige cruzar duas seções distintas para montar o quadro completo.
- **Nova seção em `docs/PRESENTATION_GUIDE.md`**: rejeitada como localização primária — é um detalhe de setup/dado de ambiente, não um roteiro de apresentação; o guia pode referenciar a tabela do README em vez de duplicá-la.

---

## R5 — Formato do checklist final de entrega

**Decisão**: Tabela markdown com colunas `Item | Categoria | Como verificar | Status`, uma linha por critério de aceite da spec (AC-001 a AC-014), agrupada visualmente por categoria (Setup, Apresentação, IA/`AI_NOTES`, Higiene do repositório, Dados seed, Validações finais, Escopo).

**Rationale**: Espelha o mesmo padrão de tabela já usado em `docs/PRESENTATION_GUIDE.md` ("Validações — Módulo NNN"), mantendo consistência visual; uma linha por AC facilita rastreabilidade direta spec → checklist.

**Alternativas consideradas**:
- **Checklist em formato de caixas de marcação simples (`- [ ]`) sem tabela**: rejeitada como formato único — perde a coluna "como verificar", que é o que torna cada item objetivamente checável (exigência do FR-010/AC-010 da spec).

---

## R6 — Estratégia para correções pontuais encontradas na auditoria

**Decisão**: Qualquer correção pontual (comando desatualizado, link quebrado, pequeno bug documental) é aplicada diretamente no arquivo correspondente durante a implementação, e listada em uma tabela dedicada dentro da seção "Módulo 006" do `AI_NOTES.md` (arquivo | motivo | o que mudou).

**Rationale**: Mantém rastreabilidade (NFR-007 da spec) sem exigir um processo de aprovação formal para ajustes triviais — consistente com o tom do restante do `AI_NOTES.md`, que já documenta erros e correções por módulo.

**Alternativas consideradas**:
- **Não registrar correções pontuais** (aplicar silenciosamente): rejeitada — contraria a exigência explícita de rastreabilidade (NFR-007) e o espírito do `AI_NOTES.md` ("mostrar como a IA foi usada com controle").

---

## R7 — Ausência de `contracts/` neste módulo

**Decisão**: Este módulo não gera pasta `contracts/`.

**Rationale**: Módulo 006 não expõe nenhuma interface nova (API HTTP, CLI, contrato de mensageria) — é auditoria e consolidação de documentação sobre módulos já existentes. Os "contratos" relevantes (outbox do módulo 005, endpoints dos módulos 001–003) já estão documentados em seus respectivos `contracts/`/specs e não são alterados aqui.

**Alternativas consideradas**:
- **Criar `contracts/checklist-format.md` descrevendo o formato do checklist**: rejeitado como pasta `contracts/` — o formato do checklist já é suficientemente descrito em R5 e será detalhado em `data-model.md`; criar uma pasta `contracts/` para um artefato puramente documental seria over-engineering para o escopo deste módulo.

---

## Resumo das decisões

| # | Decisão | Detalhe |
| --- | --- | --- |
| R1 | Checklist final | Novo arquivo `docs/DELIVERY_CHECKLIST.md` |
| R2 | Auditoria de conteúdo sensível | Busca textual direcionada + releitura humana, sem ferramenta nova |
| R3 | Arquivos versionados indevidamente | `npm install` + `dotnet build` + `git status --porcelain` + revisão do `.gitignore` |
| R4 | Documentação do seed | Nova seção/tabela curta no `README.md`, com link para detalhe em `AI_NOTES.md` |
| R5 | Formato do checklist | Tabela `Item \| Categoria \| Como verificar \| Status`, uma linha por AC da spec |
| R6 | Registro de correções pontuais | Tabela dedicada na seção Módulo 006 do `AI_NOTES.md` |
| R7 | Sem `contracts/` | Módulo não expõe interface nova; pasta omitida |

Todos os pontos identificados como decisão de planejamento na spec (`Assumptions`) foram resolvidos acima — nenhum `NEEDS CLARIFICATION` remanescente.
