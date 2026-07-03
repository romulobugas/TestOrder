# Guia de Apresentacao - TestOrder

Este documento e para apoiar a apresentacao presencial. Ele deve ser atualizado ao fim de cada modulo com decisoes, validacoes e referencias de codigo.

## Mensagem central

O projeto foi construido como uma solucao pequena e explicavel, priorizando boas decisoes em vez de excesso de arquitetura. A regra foi manter o codigo perto do problema: controllers MVC, modelo claro, SQL direto onde performance importa e documentacao viva do uso de IA.

## Decisoes iniciais

- **ASP.NET Core MVC, nao Minimal APIs**: escolha feita para manter uma organizacao familiar por controllers sem cair em camadas artificiais.
- **Sem Clean Architecture/DDD/CQRS por ritual**: o dominio do desafio e pequeno; criar muitas pastas e interfaces atrapalharia a leitura.
- **MySQL 8**: escolhido para demonstrar concorrencia com `FOR UPDATE SKIP LOCKED`, alinhado ao artigo da Shopify sobre reservas de inventario.
- **EF Core + Dapper**: EF Core para modelo, schema e seed; Dapper para consultas e pontos onde SQL explicito ajuda performance e clareza.
- **Microservico Node sem fila externa**: quando entrar, ele processara uma tabela de outbox/fila no proprio MySQL, evitando RabbitMQ/Kafka/Redis.
- **Spec Kit por modulo**: cada modulo tera spec, plano, tarefas, implementacao e revisao.
- **IA com trilha auditavel**: o repositorio inclui Spec Kit para Cursor, Claude, Codex e Antigravity, alem de `AI_NOTES.md` e `docs/SPECKIT_SETUP.md`.

## Ordem de apresentacao planejada

1. Base, modelo e listagem de pedidos.
2. Criacao de pedidos com reserva concorrente.
3. Faturamento por periodo.
4. Tela React.
5. Microservico Node e outbox.
6. README, AI_NOTES e decisoes finais.

## Referencias externas

- Shopify Engineering: https://shopify.engineering/scaling-inventory-reservations
- GitHub Spec Kit: https://github.com/github/spec-kit

## Referencias de codigo

Preencher ao final de cada modulo.

| Modulo | Arquivo | O que explicar |
| --- | --- | --- |
| Setup | `docs/SPECKIT_SETUP.md` | Como a IA foi organizada para trabalhar por especificacao e modulo |
| 001 | A definir | Modelo, seed e listagem paginada |

## Validacoes

Preencher ao final de cada modulo.
