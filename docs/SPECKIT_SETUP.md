# Spec Kit Setup

O projeto foi inicializado com GitHub Spec Kit para manter o desenvolvimento guiado por especificacoes e revisoes pequenas.

## Versao local

- `specify 0.12.4`
- Integracao default: `cursor-agent`
- Script type: PowerShell

## Integracoes instaladas

| Ferramenta | Chave Spec Kit | Onde fica |
| --- | --- | --- |
| Cursor | `cursor-agent` | `.cursor/skills` e `.cursor/rules/specify-rules.mdc` |
| Claude Code | `claude` | `.claude/skills` |
| Codex CLI | `codex` | `.agents/skills` |
| Antigravity | `agy` | `.agents/skills` |

## Como usar

No Cursor, use os skills/comandos instalados pelo Spec Kit:

```text
/speckit-constitution
/speckit-specify
/speckit-plan
/speckit-tasks
/speckit-implement
```

No Claude Code, use os skills da pasta `.claude/skills`.

No Codex, use os skills da pasta `.agents/skills`. A convencao documentada pelo Spec Kit para Codex CLI e invocar como `$speckit-<command>`.

No Antigravity, use os skills da pasta `.agents/skills`. A instalacao do Spec Kit avisou que o layout `.agents/` exige Antigravity `1.20.5` ou superior.

## Ressalva Codex + Antigravity

Nesta versao do Spec Kit, Codex e Antigravity instalam os mesmos skills em `.agents/skills`. Por isso o comando `specify integration status` reporta `unsafe-multi-install` e colisao de arquivos gerenciados entre `codex` e `agy`.

Isso nao impede o uso dos skills, mas significa:

- nao rode `specify integration uninstall codex` ou `specify integration uninstall agy` sem revisar o diff;
- nao rode upgrade de uma dessas integracoes sem validar se os arquivos compartilhados continuam corretos;
- mantenha o Cursor como integracao default para alinhar os templates compartilhados.

## Fluxo adotado neste desafio

1. Criar ou revisar o branch do modulo.
2. Rodar `/speckit-specify` para o modulo.
3. Revisar `specs/<modulo>/spec.md`.
4. Rodar `/speckit-plan`.
5. Revisar `plan.md`.
6. Rodar `/speckit-tasks`.
7. Implementar tarefas pequenas.
8. Atualizar `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md`.
9. Revisar diff e validar build/testes.

O modulo inicial recomendado e `001-base-listagem-pedidos`.
