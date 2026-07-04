# Fase 1 — Modelo Conceitual: Módulo 006 — Fechamento Final da Entrega

**Input**: [spec.md](./spec.md), [research.md](./research.md)

Este módulo **não introduz nenhuma tabela, migration ou entidade de banco de dados**. O "modelo de dados" aqui é puramente documental: descreve a estrutura do checklist final de entrega (`docs/DELIVERY_CHECKLIST.md`) e dos documentos auditados, para orientar a geração consistente desses artefatos em `tasks.md`/implementação.

---

## Entidade: Item de Checklist

Representa uma linha da tabela em `docs/DELIVERY_CHECKLIST.md`.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `id` | string | Referência ao critério de aceite da spec (ex. `AC-001`) — rastreabilidade direta spec → checklist |
| `categoria` | enum | Um de: `Setup`, `Apresentação`, `IA / AI_NOTES`, `Higiene do repositório`, `Dados seed`, `Validações finais`, `Escopo` |
| `descricao` | string | Frase curta e objetiva do que está sendo verificado (derivada do critério de aceite correspondente) |
| `como_verificar` | string | Comando exato ou passo reproduzível (ex. `.\scripts\test.ps1`, "reler `AI_NOTES.md` módulo a módulo") |
| `status` | enum | `PASS` (verificado com sucesso — inclui o caso em que uma correção pontual foi aplicada antes de passar, registrada em `observacao`), `PENDING` (ainda não verificado; aguarda a validação final), `N/A` (não se aplica, com justificativa obrigatória em `observacao`) |
| `observacao` | string (opcional) | Detalhe adicional — obrigatório quando `status = N/A`; recomendado quando `status = PASS` só depois de uma correção pontual (referenciando a mudança registrada em `AI_NOTES.md`) |

**Regra de validação**: todo item DEVE terminar com `status = PASS` ou `status = N/A` (com justificativa) ao final da implementação — nenhum item pode permanecer `PENDING` ou com um placeholder tipo "a preencher" no fechamento do módulo (FR-010/AC-010 da spec).

**Origem dos itens**: um item por critério de aceite `AC-001`–`AC-014` da spec deste módulo (14 itens no total), agrupados por `categoria` na renderização da tabela.

---

## Entidade: Documento Auditado

Representa um documento (ou grupo de documentos) revisado durante a auditoria — não é persistido como tabela, é apenas o conceito usado para guiar a Fase de implementação/tasks.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `caminho` | string | Caminho do arquivo ou pasta (ex. `README.md`, `AI_NOTES.md`, `docs/PRESENTATION_GUIDE.md`, `specs/001-base-listagem-pedidos/`) |
| `tipo` | enum | Um de: `Setup` (README), `Apresentação` (PRESENTATION_GUIDE), `Rastro de IA` (AI_NOTES), `Spec de módulo` (specs/NNN-*), `Config` (.gitignore, dev-up.ps1) |
| `resultado_releitura` | enum | `Sem alteração necessária`, `Ajustado` (correção pontual aplicada), `Não aplicável` |
| `observacao` | string (opcional) | O que foi encontrado/corrigido, se houver |

**Relação com o Item de Checklist**: cada `Documento Auditado` alimenta o `status`/`observacao` de um ou mais `Item de Checklist` correspondentes (ex. a releitura de `specs/001-*` a `specs/005-*` alimenta o item `AC-006`).

---

## Entidade: Correção Pontual (registro em `AI_NOTES.md`)

Representa uma linha na tabela de correções da seção "Módulo 006" do `AI_NOTES.md` (ver [research.md R6](./research.md)).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `arquivo` | string | Arquivo alterado |
| `motivo` | string | O que estava incorreto/desatualizado |
| `mudanca` | string | O que foi exatamente alterado |

**Regra**: só existe se uma correção real foi aplicada durante a auditoria (FR-016 da spec); se nenhuma foi necessária, a seção declara isso explicitamente em vez de ficar vazia ou omitida.

---

## Sem transições de estado

Diferente dos módulos 001–005 (que modelam entidades de negócio com ciclo de vida no banco, ex. `order_processing_events.status: pending → processed`), as entidades deste módulo são artefatos documentais estáticos ao final da implementação — não há um "ciclo de vida em produção" a descrever. O único "estado" relevante é o `status` do Item de Checklist, que nasce `PENDING` na criação do arquivo (`docs/DELIVERY_CHECKLIST.md`, T016) e transita para `PASS` ou `N/A` uma única vez, após a validação final (T017/T018), durante a implementação deste módulo.
