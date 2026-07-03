# Desafio Técnico — Full Stack Pleno

Olá! Este é um desafio para a gente conversar sobre código de verdade. Leia tudo antes de começar.

## O que esperamos de você

Tempo sugerido: **2 a 4 horas**. Não passe muito disso. **Não buscamos completude — buscamos boas decisões.** Preferimos uma fatia menor bem-feita, com escolhas claras, do que um sistema grande e atropelado. Se faltar tempo para algo, descreva no README como você faria.

Depois da entrega, faremos uma conversa de ~30–40 min em que você nos guia pelo código e pelas suas decisões.

## O sistema

Construa, **do zero**, um pequeno sistema de **Pedidos**. O domínio é simples de propósito — o interesse está em *como* você constrói, não na complexidade do negócio.

Funcionalidades mínimas:

1. **Listar pedidos** — endpoint paginado que retorna pedidos com seus itens e o total de cada pedido.
2. **Faturamento por período** — endpoint que retorna o faturamento agregado por dia, dado um intervalo de datas.
3. **Criar pedido** — endpoint para criar um pedido com itens.
4. **Tela web** — uma interface que lista os pedidos e permite criar um novo.

Popule a base com **volume realista o suficiente para que performance importe** (pense em milhares de pedidos, vários itens cada). Como você gera esses dados é com você.

## Stack

- **Backend:** .NET. Use **EF Core e/ou Dapper** — a escolha de onde usar cada um é sua e queremos entender o porquê.
- **Frontend:** **React (web)**.
- **Banco:** à sua escolha (relacional).
- **Microserviço (opcional, se sobrar tempo):** um serviço separado em **Node** que processa algo quando um pedido é criado (ex.: enriquecimento, verificação, notificação — você decide). Como o backend .NET conversa com esse serviço é decisão sua. Se não der tempo de implementar, **descreva no README** como você o desenharia e como os dois lados se comunicariam.

## Uso de IA — leia com atenção

**Usar ferramentas de IA é esperado e incentivado.** É assim que trabalhamos; não é trapaça. Use o que você usaria no dia a dia.

Mais do que isso: **um dos pontos centrais deste desafio é como você estrutura o projeto para que ferramentas de IA trabalhem nele de forma produtiva.** Não vamos dizer como fazer isso — queremos ver a *sua* abordagem. Invista energia aqui; pesa bastante na avaliação.

Inclua no repositório um arquivo **`AI_NOTES.md`** contando:
- Onde a IA ajudou.
- Onde a IA errou e você corrigiu (e como percebeu).
- O que você decidiu escrever à mão, e por quê.
- Qualquer artefato do seu fluxo de IA que faça parte de como você trabalha.

## Entrega

- Repositório Git (link ou zip) com **README** explicando: como rodar, decisões que tomou, o que ficou de fora e o que você faria com mais tempo.
- Idealmente, rodar com poucos comandos. Documente o passo a passo.
- O `AI_NOTES.md` descrito acima.

Qualquer dúvida sobre o enunciado, pode perguntar. Boa!
