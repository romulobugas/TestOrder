# Especificação: Módulo 001 — Base e Listagem de Pedidos

**Feature Branch**: `001-base-listagem-pedidos`

**Criado**: 2026-07-03

**Status**: Rascunho

**Input**: Base do backend MVC, MySQL 8, modelo relacional inicial, seed com volume realista e endpoints de leitura de produtos e pedidos paginados.

---

## Objetivo do módulo

Estabelecer a fundação do sistema de pedidos do desafio TestOrder: backend operacional, banco relacional configurado, dados iniciais em volume que torne performance relevante e consultas de leitura que permitam validar o modelo e os totais antes dos módulos de criação, faturamento, frontend e microserviço.

Ao concluir este módulo, um consumidor da API deve conseguir listar produtos e pedidos paginados com itens e total calculado, sobre uma base populada de forma realista, sem depender de criação manual de registros.

---

## Usuários e personas mínimas

| Persona | Objetivo neste módulo |
| --- | --- |
| **Avaliador do desafio** | Verificar que o projeto sobe, o banco está populado e a listagem retorna dados coerentes com paginação e totais corretos. |
| **Consumidor da API** (desenvolvedor ou cliente HTTP) | Consultar catálogo de produtos e pedidos existentes para integração ou validação manual. |
| **Módulos futuros** (criação, faturamento, React) | Reutilizar modelo, seed e contratos de leitura estáveis sem retrabalho estrutural. |

Não há autenticação nem perfis de acesso neste módulo; todos os endpoints de leitura são públicos no contexto local do desafio.

---

## Jornadas do módulo

### Jornada 1 — Subir o ambiente e ver dados (P1)

**Como** avaliador ou desenvolvedor, **quero** iniciar o backend conectado ao MySQL 8 via Docker Compose com schema e dados já criados, **para** validar a base sem scripts manuais adicionais nem MySQL instalado na máquina.

**Por que P1**: Sem ambiente e seed, nenhuma outra validação do módulo é possível.

**Teste independente**: Subir o MySQL via Docker Compose e iniciar a aplicação; confirmar que tabelas existem e contêm volume significativo de pedidos e itens.

**Cenários de aceite**:

1. **Dado** MySQL 8 acessível pelo serviço `mysql` do Docker Compose e connection string configurada, **quando** a aplicação inicia, **então** o schema inicial é aplicado e os dados de seed estão disponíveis.
2. **Dado** seed concluído, **quando** consulto contagens no banco, **então** existem milhares de pedidos com múltiplos itens cada.

---

### Jornada 2 — Listar produtos (P1)

**Como** consumidor da API, **quero** obter a lista de produtos disponíveis, **para** conhecer o catálogo usado nos pedidos.

**Por que P1**: Produtos são entidade base do domínio e serão necessários nos módulos seguintes.

**Teste independente**: Chamar `GET /api/products` e receber lista não vazia com identificador, nome e preço de cada produto.

**Cenários de aceite**:

1. **Dado** produtos cadastrados no seed, **quando** solicito `GET /api/products`, **então** recebo status de sucesso com lista de produtos.
2. **Dado** a resposta de produtos, **quando** inspeciono cada item, **então** cada produto expõe identificador, nome e preço unitário.

---

### Jornada 3 — Listar pedidos paginados com itens e total (P1)

**Como** consumidor da API, **quero** listar pedidos de forma paginada incluindo itens e total de cada pedido, **para** avaliar o domínio e a performance de leitura sobre volume realista.

**Por que P1**: É o requisito central do desafio para listagem e o principal entregável deste módulo.

**Teste independente**: Chamar `GET /api/orders?page=1&pageSize=20` e validar estrutura, paginação, itens aninhados e total por pedido.

**Cenários de aceite**:

1. **Dado** pedidos no seed, **quando** solicito `GET /api/orders?page=1&pageSize=20`, **então** recebo no máximo 20 pedidos e metadados de paginação (página atual, tamanho da página, total de registros ou equivalente).
2. **Dado** um pedido na resposta, **quando** inspeciono seus itens, **então** cada item referencia produto (identificador e/ou nome), quantidade e preço unitário na linha.
3. **Dado** um pedido na resposta, **quando** comparo o campo de total informado com a soma `quantidade × preço unitário` dos itens, **então** os valores coincidem.
4. **Dado** mais de uma página de pedidos, **quando** solicito `page=2`, **então** recebo o próximo conjunto sem duplicar registros da página anterior.
5. **Dado** parâmetros omitidos, **quando** solicito `GET /api/orders`, **então** a API assume `page=1` e `pageSize=20`.

---

### Jornada 4 — Consultar pedido por identificador (P3, opcional)

**Como** consumidor da API, **quero** obter um pedido específico por id com itens e total, **para** inspecionar um registro sem percorrer todas as páginas.

**Por que P3**: Útil para depuração e para o frontend futuro, mas não bloqueia o módulo se omitido.

**Teste independente**: Chamar `GET /api/orders/{id}` com id existente e inexistente.

**Cenários de aceite**:

1. **Dado** um id de pedido existente, **quando** solicito `GET /api/orders/{id}`, **então** recebo o pedido com itens e total corretos.
2. **Dado** um id inexistente, **quando** solicito `GET /api/orders/{id}`, **então** recebo resposta de não encontrado (404 ou equivalente semântico).

---

### Casos de borda

- **Página além do fim**: retornar lista vazia com metadados de paginação coerentes (total de registros inalterado).
- **`page` ou `pageSize` inválidos** (zero, negativo, não numérico): rejeitar com erro de validação claro ou normalizar para valores padrão documentados — preferir validação explícita com mensagem compreensível.
- **`pageSize` muito grande**: limitar a um teto máximo (ex.: 100) para evitar respostas excessivas.
- **Pedido sem itens** (se existir no seed por acidente): total do pedido deve ser zero; comportamento deve ser consistente na listagem.
- **Banco indisponível**: falha de conexão deve resultar em erro HTTP apropriado, não em resposta vazia silenciosa.
- **Seed idempotente ou repetível**: reiniciar a aplicação em desenvolvimento não deve corromper dados nem duplicar seed de forma descontrolada (estratégia documentada no plano de implementação).

---

## Requisitos funcionais

- **FR-001**: O sistema DEVE expor `GET /api/products` retornando todos os produtos do catálogo com identificador, nome e preço unitário.
- **FR-002**: O sistema DEVE expor `GET /api/orders` com parâmetros opcionais `page` (padrão 1) e `pageSize` (padrão 20).
- **FR-003**: A listagem de pedidos DEVE retornar, para cada pedido: identificador, data de criação (ou equivalente ordenável), itens e total do pedido.
- **FR-004**: Cada item de pedido na resposta DEVE incluir identificador do produto, nome do produto (ou referência resolvível), quantidade e preço unitário na linha.
- **FR-005**: O total de cada pedido DEVE ser a soma aritmética de `quantidade × preço unitário` de todos os seus itens.
- **FR-006**: A resposta paginada DEVE incluir metadados que permitam navegar páginas (página atual, tamanho da página e total de pedidos).
- **FR-007**: Os pedidos na listagem DEVEM ser ordenados do mais recente para o mais antigo (por data de criação decrescente), salvo decisão documentada em contrário no plano.
- **FR-008**: O sistema DEVE criar e manter o schema relacional inicial via migração ou equivalente controlado (não scripts manuais soltos como único mecanismo).
- **FR-009**: O sistema DEVE popular o banco com volume realista: ordem de grandeza de **milhares de pedidos**, cada um com **vários itens** (faixa sugerida: 2 a 5 itens por pedido), distribuídos em um catálogo de dezenas de produtos.
- **FR-010** (opcional): O sistema PODE expor `GET /api/orders/{id}` com o mesmo contrato de pedido da listagem; se implementado, DEVE retornar não encontrado para id inexistente.
- **FR-011**: O backend DEVE usar controllers MVC para os endpoints HTTP; Minimal APIs estão fora de escopo.
- **FR-012**: Schema, entidades e seed DEVEM ser responsabilidade do ORM configurado no projeto; consultas de listagem DEVEM usar SQL direto via biblioteca de micro-ORM já adotada no repositório, mantendo a decisão EF + SQL explícita e legível.

---

## Requisitos não funcionais

- **NFR-001 (Simplicidade)**: A solução DEVE permanecer compreensível a partir de poucos arquivos e pastas; evitar camadas ceremoniais (Clean Architecture, DDD tático, CQRS, mediator, repositórios genéricos, mappers ou interfaces sem necessidade demonstrada).
- **NFR-002 (Performance de leitura)**: Com seed realista (≥ 3.000 pedidos), a primeira página de `GET /api/orders` DEVE responder em tempo aceitável para demonstração local (meta: inferior a 2 segundos em máquina de desenvolvimento típica).
- **NFR-003 (Rastreabilidade)**: Decisões de modelo, seed e consultas DEVEM ser documentáveis em `AI_NOTES.md` e `docs/PRESENTATION_GUIDE.md` ao fim da implementação.
- **NFR-004 (Preparação para módulos futuros)**: O modelo DEVE reservar atributos necessários para criação de pedidos e controle de estoque no módulo 002 (ex.: quantidade em estoque por produto), sem implementar reserva ou `FOR UPDATE SKIP LOCKED` neste módulo.
- **NFR-005 (Configuração)**: Connection string e parâmetros de ambiente DEVEM ser configuráveis sem recompilação (ex.: `appsettings` + variáveis de ambiente).
- **NFR-006 (Consistência de contrato)**: Respostas JSON DEVEM usar nomes estáveis e previsíveis para consumo pelo módulo React posterior.
- **NFR-007 (Operação local)**: O módulo DEVE ser validável com Docker Compose subindo MySQL 8; MySQL nativo instalado localmente é apenas fallback opcional. O compose completo com API/frontend/Node fica para módulo 006.

---

## Modelo de dados inicial (alto nível)

Relacionamento esperado entre entidades principais:

```text
Product (1) ──< (N) OrderItem (N) >── (1) Order
```

### Product (Produto)

| Conceito | Descrição |
| --- | --- |
| Identificador | Chave primária estável |
| Nome | Nome do produto no catálogo |
| Preço unitário | Preço de referência no catálogo |
| Estoque disponível | Quantidade disponível para venda (usado no módulo 002; deve existir no schema desde já) |

### Order (Pedido)

| Conceito | Descrição |
| --- | --- |
| Identificador | Chave primária estável |
| Data de criação | Momento em que o pedido foi registrado |
| Status | Estado do pedido (ex.: criado, processado — valores mínimos aceitáveis para leitura e extensão futura) |

Atributos de cliente ou endereço são opcionais neste módulo; incluir apenas se não aumentarem complexidade.

### OrderItem (Item do pedido)

| Conceito | Descrição |
| --- | --- |
| Identificador | Chave primária ou chave composta order + produto |
| Pedido | Referência ao pedido pai |
| Produto | Referência ao produto |
| Quantidade | Unidades compradas |
| Preço unitário na linha | Preço capturado no momento do pedido (pode diferir do preço atual do catálogo) |

**Regra de total**: o total do pedido é derivado dos itens, não armazenado como fonte primária de verdade neste módulo (pode ser calculado na consulta).

**Volume de seed sugerido** (ajustável no plano, desde que performance continue relevante):

| Entidade | Ordem de grandeza |
| --- | --- |
| Produtos | 30–100 |
| Pedidos | 3.000–10.000 |
| Itens por pedido | 2–5 em média |
| Distribuição temporal | Pedidos espalhados nos últimos 12 meses para suportar faturamento futuro |

---

## Fora de escopo deste módulo

- `POST /api/orders` e qualquer fluxo de criação de pedido
- Reserva de estoque e `FOR UPDATE SKIP LOCKED`
- `GET /api/revenue` (faturamento por período)
- Frontend React
- Microserviço Node e outbox
- Dockerfile da API, compose completo com API/frontend/Node e README de entrega final
- Autenticação e autorização

---

## Critérios de aceite verificáveis

| ID | Critério | Como verificar |
| --- | --- | --- |
| AC-001 | Backend sobe e conecta ao MySQL 8 via Docker | `.\scripts\dev-up.ps1` sobe o banco e inicia a API sem erro de conexão |
| AC-002 | Schema criado automaticamente | Tabelas de produto, pedido e item visíveis no banco após subir a app |
| AC-003 | Seed com volume realista | Contagem de pedidos ≥ 3.000; média de itens por pedido > 1 |
| AC-004 | `GET /api/products` funcional | HTTP 200, JSON array não vazio, campos id/nome/preço presentes |
| AC-005 | `GET /api/orders` paginado | HTTP 200, ≤ `pageSize` pedidos, metadados de paginação presentes |
| AC-006 | Itens e total corretos | Para amostra manual de 3 pedidos, total = soma dos itens |
| AC-007 | Ordenação recente primeiro | Primeiro pedido da página 1 tem data ≥ último da mesma página |
| AC-008 | Página 2 distinta | Nenhum id duplicado entre página 1 e 2 com `pageSize=20` |
| AC-009 | Parâmetros padrão | `GET /api/orders` sem query equivale a `page=1&pageSize=20` |
| AC-010 | Controllers MVC | Endpoints definidos em controllers, não em Minimal API |
| AC-011 | EF para schema/seed | Migrações ou configuração EF evidenciada no código do módulo |
| AC-012 | SQL direto na listagem | Consultas de listagem usam micro-ORM/SQL explícito, não apenas LINQ do ORM |
| AC-013 (opcional) | Detalhe por id | `GET /api/orders/{id}` retorna pedido ou 404 coerente |

---

## Checks manuais esperados

1. Executar `.\scripts\dev-up.ps1` para subir MySQL 8 via Docker Compose, compilar e iniciar a API em foreground.
2. Confirmar que a API aplica migrations e seed automaticamente na primeira inicialização.
3. Confirmar no cliente SQL: `SELECT COUNT(*) FROM orders` (ou nome de tabela equivalente) retorna milhares.
4. `curl` ou Swagger/browser: `GET /api/products` — validar lista e campos.
5. `GET /api/orders?page=1&pageSize=5` — inspecionar JSON: itens aninhados, total, metadados.
6. Calcular manualmente o total de um pedido retornado e comparar com o campo `total` (ou equivalente).
7. `GET /api/orders?page=2&pageSize=20` — confirmar paginação.
8. `GET /api/orders?page=999&pageSize=20` — confirmar lista vazia sem erro 500.
9. Parar o container MySQL e subir a API — confirmar falha clara (não resposta 200 vazia).
10. (Opcional) `GET /api/orders/{id}` com id válido e inválido.

Registrar resultados e comandos usados em `docs/PRESENTATION_GUIDE.md` após implementação.

---

## Pontos para `AI_NOTES.md` (pós-implementação)

- Como a IA ajudou a derivar esta spec e limitar escopo do módulo 001.
- Decisões humanas sobre formato do JSON de paginação e nomes de campos.
- Escolha do volume exato do seed e tempo de geração aceitável.
- Se a IA sugeriu camadas extras (repositório, DTOs, AutoMapper) e o que foi recusado.
- Onde SQL foi escrito à mão vs. onde EF foi mantido — e por quê.
- Erros comuns da IA neste módulo (ex.: N+1 na montagem de itens, paginação incorreta, total pré-calculado inconsistente).
- Prompts ou comandos Spec Kit usados (`specify`, `plan`, `tasks`, implementação).

---

## Pontos para `docs/PRESENTATION_GUIDE.md` (pós-implementação)

- Referências de arquivo: `DbContext`, entidades, controller de produtos, controller de pedidos, script/classe de seed, queries SQL/Dapper.
- Explicação em 2–3 frases: por que EF no schema/seed e Dapper na listagem.
- Comando exato para subir MySQL via Docker + API e URLs de teste.
- Números do seed (quantos produtos, pedidos, itens) e tempo aproximado da primeira listagem.
- Trecho ou descrição da query de listagem (join pedido-itens-produto, paginação, cálculo de total).
- Validação executada (checks manuais acima) com resultado pass/fail.
- O que ficou de fora de propósito e qual módulo assume cada parte.

---

## Success Criteria (mensuráveis e agnósticos de implementação)

- **SC-001**: Em ambiente local com Docker disponível, um avaliador consegue obter a primeira página de pedidos com itens e totais em menos de 2 minutos desde o clone do repositório (excluindo instalação única do Docker).
- **SC-002**: Sobre base com pelo menos 3.000 pedidos, 95% das requisições de primeira página de listagem completam em menos de 2 segundos em demonstração local.
- **SC-003**: Em amostra de 10 pedidos aleatórios, 100% apresentam total igual à soma dos itens.
- **SC-004**: A listagem paginada não retorna duplicatas entre páginas consecutivas para o mesmo `pageSize` fixo.
- **SC-005**: O catálogo de produtos retorna 100% dos produtos seedados em uma única chamada sem paginação obrigatória neste módulo.

---

## Assumptions

- Docker está disponível localmente; o MySQL 8 sobe via Docker Compose com credenciais de desenvolvimento, sem hardening de produção.
- Não há requisito de autenticação neste módulo.
- Nomes de tabelas/colunas seguem convenção do time (singular ou plural) desde que consistentes e documentados no plano.
- `GET /api/orders/{id}` é desejável mas não obrigatório; omitir não impede aprovação do módulo.
- Preço na linha do item é snapshot; alterações futuras no preço do produto não retroagem em pedidos existentes.
- Status de pedido pode ser valor simples (ex.: string ou enum) sem máquina de estados complexa neste módulo.
- O projeto já contém pacotes Pomelo EF Core MySQL, Dapper e MySqlConnector; a implementação os utiliza conforme diretrizes do repositório.

---

## Restrições de arquitetura (contexto do desafio)

Estas restrições vêm das regras do projeto e do módulo; não são requisitos de negócio do usuário final, mas limites obrigatórios da entrega:

- ASP.NET Core MVC com controllers em `src/TestOrder.Api`.
- Sem Minimal APIs.
- EF Core: modelo, migrations e seed.
- Dapper (ou SQL parametrizado via MySqlConnector): consultas de `GET /api/products` e `GET /api/orders` (e detalhe opcional).
- MySQL 8 como banco relacional.
- Poucas pastas e arquivos; SQL próximo ao ponto de uso.
