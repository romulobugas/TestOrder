<!-- Sync Impact Report
  Version change: 0.0.0 (template) → 1.0.0
  Modified principles: All new (template placeholders replaced)
  Added sections: Core Principles (5), Technology Constraints, Quality Gates, Governance
  Removed sections: None (template examples removed)
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ compatible (Constitution Check section exists)
    - .specify/templates/spec-template.md ✅ compatible (no constitution references needed)
    - .specify/templates/tasks-template.md ✅ compatible (phase structure aligns)
  Follow-up TODOs: None
-->

# TestOrder Constitution

## Core Principles

### I. Simplicidade Legível

O sistema DEVE ser o menor conjunto legível de código que satisfaça o desafio.
Cada feature DEVE ser compreensível a partir de poucos arquivos.
Controllers MVC e código direto DEVEM ser preferidos sobre cerimônia arquitetural.

### II. SQL Próximo do Uso

Queries Dapper ou raw SQL DEVEM residir no mesmo namespace do controller que as consome.
EF Core DEVE ser usado exclusivamente para ownership de schema e database setup.
Queries de leitura e transações críticas (reserva de estoque) DEVEM usar Dapper com parâmetros.

### III. Sem Abstração Prematura (NON-NEGOTIABLE)

O projeto NÃO DEVE conter: Repository interfaces, generic services, CQRS, Mediator,
handlers, mappers, factories ou strategies — exceto quando o código demonstrar
necessidade concreta e atual. Clean Architecture por ritual e DDD tactical patterns
são explicitamente proibidos neste domínio.

### IV. Stack Fixa e Mínima

Tecnologias permitidas (sem exceção salvo justificativa documentada em AI_NOTES.md):
- Backend: ASP.NET Core MVC controllers, Dapper, EF Core (migrations).
- Banco: MySQL 8 (FOR UPDATE SKIP LOCKED para concorrência).
- Frontend: React com JavaScript puro, CSS puro, Vite.
- Worker: Node.js microservice para processamento assíncrono pós-criação.
- Proibido: RabbitMQ, Kafka, Redis, bancos extras, Redux, Zustand, React Query,
  react-router, UI kits, CSS-in-JS, Tailwind.

### V. Módulo por Vez

Cada módulo DEVE ser desenvolvido em branch própria com artefatos Spec Kit
na ordem: spec.md → plan.md → tasks.md → implementação.
Módulos futuros NÃO DEVEM ser implementados durante o módulo atual.
Após cada módulo, validações práticas DEVEM ser deixadas em tasks.md e
docs/PRESENTATION_GUIDE.md.

## Technology Constraints

### Frontend
- Nenhuma dependência nova em package.json sem necessidade comprovada.
- styles.css único; sem CSS modules, CSS-in-JS ou frameworks CSS.
- Estado local com hooks React; sem state management externo.
- Duplo clique em campos de filtro/consulta DEVE limpar aquele campo individual.
- Botões "Limpar" DEVEM funcionar com 1 clique.

### Backend
- MVC controllers DEVEM ser preservados; Minimal APIs são proibidas.
- Dapper DEVE ser usado para queries de leitura e transações.
- Testes de integração DEVEM rodar com banco MySQL real (container).
- Validação de entrada DEVE ser feita no controller; sem FluentValidation.

### Worker Node.js
- Alterações SOMENTE quando o módulo exigir processamento assíncrono.
- NÃO DEVE ser modificado para fins de frontend ou relatório.

### Documentação
- AI_NOTES.md DEVE ser atualizado a cada passo assistido por IA.
- docs/PRESENTATION_GUIDE.md DEVE conter decisões, tradeoffs e referências de código.
- Tom DEVE ser profissional e neutro; linguagem defensiva ou autodepreciativa é proibida.

## Quality Gates

Cada step de implementação DEVE satisfazer todos os gates antes de ser considerado completo:

1. `npm run build` no frontend — zero erros.
2. `dotnet build TestOrder.slnx` — zero erros.
3. `.\scripts\test.ps1` — todos os testes passando.
4. `git diff --check` — zero erros de whitespace.
5. Zero referências a termos proibidos no código ou documentação.
6. package.json sem dependências novas não justificadas.
7. Projeto DEVE compilar e rodar, ou documentar exatamente o que falta.

## Governance

- Esta constitution prevalece sobre padrões genéricos da indústria quando houver conflito.
- Alterações à constitution requerem justificativa documentada em AI_NOTES.md.
- Todo PR/review DEVE verificar conformidade com os princípios acima.
- Complexidade adicional DEVE ser justificada por necessidade demonstrável atual.
- Versionamento: MAJOR para remoção/redefinição de princípios; MINOR para adição;
  PATCH para clarificações de texto.

**Version**: 1.0.0 | **Ratified**: 2026-07-04 | **Last Amended**: 2026-07-04
