# AI Notes

Este arquivo sera atualizado a cada modulo. O objetivo nao e vender que a IA fez tudo, mas mostrar como ela foi usada com controle, revisao e limites claros.

## Diretrizes iniciais

- Usei IA para estruturar o fluxo de trabalho, organizar prompts e revisar decisoes de arquitetura.
- A decisao humana principal foi manter o sistema simples: ASP.NET Core MVC, poucas pastas, sem Clean Architecture/DDD/CQRS por ritual.
- Minimal APIs foram evitadas por preferencia de organizacao e apresentacao.
- O fluxo de reserva foi inspirado no artigo da Shopify sobre MySQL 8 `FOR UPDATE SKIP LOCKED`, mas sera aplicado de forma menor e didatica.
- O projeto sera construido por modulos com Spec Kit para manter rastreabilidade: spec, plano, tarefas, implementacao e revisao.

## Modulo 001 - Base e listagem de pedidos

Status: planejado.

IA deve ajudar em:
- transformar o desafio em uma especificacao pequena e verificavel;
- lembrar limites de escopo;
- sugerir criterios de aceite e validacoes.

Pontos que precisam de revisao humana:
- se o modelo de dados ficou simples o suficiente;
- se o seed cria volume realista sem deixar o projeto pesado;
- se a listagem retorna totais corretos sem criar camadas artificiais.
