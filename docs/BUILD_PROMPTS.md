# TestOrder Modular Build Prompts

Este arquivo organiza o trabalho por modulos pequenos. A regra e: especificar, planejar, quebrar em tarefas, implementar, validar, revisar e so entao passar ao proximo modulo.

## Ordem recomendada dos modulos

1. `001-base-listagem-pedidos`
   - Base do backend MVC, modelo relacional, MySQL, seed realista, `GET /api/products`, `GET /api/orders`.
   - Motivo: cria a base comum, permite ver dados reais cedo e reduz risco antes do fluxo concorrente.

2. `002-criacao-pedido-reservas`
   - `POST /api/orders`, reserva com MySQL 8 `FOR UPDATE SKIP LOCKED`, transacao curta e outbox pendente.
   - Motivo: e o ponto tecnico mais importante; deve ser feito depois do modelo/seed existir.

3. `003-faturamento-periodo`
   - `GET /api/revenue?from=YYYY-MM-DD&to=YYYY-MM-DD`.
   - Motivo: usa dados ja criados e valida agregacao/performance com Dapper.

4. `004-web-react`
   - Tela para listar pedidos, consultar faturamento e criar pedido.
   - Motivo: fica mais simples quando os contratos da API ja estao estaveis.

5. `005-node-order-processor`
   - Microservico Node processando outbox/fila no MySQL com `FOR UPDATE SKIP LOCKED`.
   - Motivo: opcional no desafio, mas entra como diferencial depois do fluxo principal funcionar.

6. `006-entrega-documentacao`
   - Docker, README final, AI_NOTES final, guia de apresentacao e checks finais.

## Contexto fixo para colar em todos os prompts

```text
Repositorio: F:\repository\TestOrder.

Leia antes de agir:
- `desafio-pleno.md`
- `.cursor/rules/testorder.mdc`
- `AI_NOTES.md`, se existir
- `docs/PRESENTATION_GUIDE.md`, se existir
- specs do modulo atual, se existirem

Diretrizes do candidato:
- Usar ASP.NET Core MVC com controllers; nao usar Minimal APIs.
- Evitar Clean Architecture, DDD, CQRS, mediator, repository generico, mappers e interfaces sem necessidade real.
- Manter codigo simples, legivel e facil de explicar olhando poucos arquivos.
- Usar MySQL 8 no fluxo de reserva por causa de `FOR UPDATE SKIP LOCKED`, inspirado no artigo da Shopify:
  https://shopify.engineering/scaling-inventory-reservations
- Nao usar RabbitMQ, Kafka ou Redis.
- Usar Spec Kit por modulo, nao em uma passada unica.
- Atualizar `AI_NOTES.md` a cada modulo com onde IA ajudou, onde poderia errar, decisoes humanas e prompts usados.
- Atualizar `docs/PRESENTATION_GUIDE.md` a cada modulo com decisoes, validacoes e referencias de codigo para apresentacao presencial.
```

## Proximo prompt: Specify do Modulo 001

Use este agora no Spec Kit `specify`.

```text
Crie a especificacao do Modulo 001 do TestOrder: base + listagem de pedidos.

Contexto:
- Este e um desafio Full Stack Pleno de Pedidos.
- O projeto ja foi iniciado em `F:\repository\TestOrder`.
- Backend atual: ASP.NET Core MVC em `src/TestOrder.Api`.
- Solucao atual: `TestOrder.slnx`.
- Pacotes backend ja adicionados: Pomelo EF Core MySQL, Dapper e MySqlConnector.
- Nao implemente codigo neste prompt.

Leia:
- `desafio-pleno.md`
- `.cursor/rules/testorder.mdc`
- `docs/BUILD_PROMPTS.md`

Crie:
- `specs/001-base-listagem-pedidos/spec.md`

Objetivo do modulo:
- Montar a base do backend MVC.
- Configurar MySQL 8.
- Definir modelo relacional inicial.
- Popular a base com volume realista suficiente para performance importar.
- Entregar endpoints de leitura:
  - `GET /api/products`
  - `GET /api/orders?page=1&pageSize=20`
  - opcional neste modulo: `GET /api/orders/{id}`, se nao aumentar muito o escopo.

Fora de escopo deste modulo:
- `POST /api/orders`
- Reserva com `FOR UPDATE SKIP LOCKED`
- Faturamento por periodo
- Frontend React
- Microservico Node
- Docker final

Diretrizes de arquitetura:
- Usar MVC com controllers.
- Nao usar Minimal APIs.
- Nao criar Clean Architecture, DDD, CQRS, mediator, repository generico, mappers ou interfaces sem necessidade real.
- Usar EF Core para schema/modelo/seed.
- Usar Dapper nas consultas de listagem para deixar clara a decisao EF + SQL direto.
- Manter poucas pastas e poucos arquivos.

A especificacao deve incluir:
- Objetivo do modulo.
- Usuarios/personas minimas.
- Jornadas do modulo.
- Requisitos funcionais do modulo.
- Requisitos nao funcionais do modulo.
- Modelo de dados inicial esperado em alto nivel.
- Criterios de aceite verificaveis.
- Checks manuais esperados.
- Pontos que devem entrar no `AI_NOTES.md` depois da implementacao.
- Pontos que devem entrar no `docs/PRESENTATION_GUIDE.md` depois da implementacao.

Nao implemente codigo.
Nao avance para os modulos seguintes.
```

## Depois do Specify 001

Quando `specs/001-base-listagem-pedidos/spec.md` estiver pronto, use outro prompt para `plan`, depois outro para `tasks`, e so depois implemente no Cursor. Nao misture etapas.
