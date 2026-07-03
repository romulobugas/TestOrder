# Quickstart — Módulo 004

**Objetivo**: Validar a tela React de pedidos (listagem paginada + criação) rodando contra o backend já existente, sem alterar `src/TestOrder.Api`.
**Contratos**: [contracts/ui.md](./contracts/ui.md) | **Modelo**: [data-model.md](./data-model.md)

## Pré-requisitos

- Módulos 001, 002 e 003 operacionais (backend com **46/46** testes passando)
- .NET 10 SDK + Docker (para o backend)
- Node.js 18+ e npm (para o frontend)
- Nenhuma alteração de backend é esperada neste módulo

---

## Subir backend + frontend (um único comando)

```powershell
.\scripts\dev-up.ps1
```

Sobe o MySQL, builda o backend, instala dependências do frontend na primeira execução (se `node_modules` não existir) e abre **três janelas CMD separadas** — `TestOrder - MySQL` (logs), `TestOrder - API` e `TestOrder - Web` — cada uma com seus próprios logs em tempo real. Backend em **`http://localhost:5069`**, frontend em **`http://localhost:5173`** (porta padrão do Vite; se ocupada, o Vite escolhe outra — conferir a janela "TestOrder - Web"). Se a porta `5069` já estiver em uso, o script avisa **antes do `dotnet build`** (o build pode falhar no Windows enquanto o executável antigo da API estiver em uso); os avisos de porta ocupada (`5069`/`5173`) também aparecem novamente antes de abrir as janelas.

O proxy configurado em `vite.config.js` encaminha `/api/*` para `http://localhost:5069`, então nenhuma configuração de CORS é necessária no backend.

---

## Validar listagem de pedidos

1. Abrir a URL do Vite no navegador.
2. **Esperado**: a tela já exibe a listagem de pedidos do seed (id, data, status, total, itens resumidos) — não uma landing page.
3. Clicar em "próxima" — a lista deve trocar para a página seguinte.
4. Clicar em "anterior" — a lista deve voltar para a página anterior.
5. Clicar em "atualizar" — a lista deve ser buscada novamente (observar indicador de carregamento).

## Validar criação de pedido válido

1. No formulário, escolher um produto e uma quantidade, clicar em "adicionar item" (repetir para 1–2 itens).
2. Preencher (ou deixar vazio) o campo de nome do cliente.
3. Clicar em "criar pedido".
4. **Esperado**: mensagem de sucesso, formulário limpo, e o novo pedido aparece na listagem (página 1) sem precisar recarregar a página do navegador.

## Validar erro de validação (400)

1. Tentar enviar o formulário sem nenhum item adicionado.
2. **Esperado**: envio bloqueado no cliente ou mensagem de erro `400` exibida, sem travar a tela.

## Validar conflito de estoque (409)

1. Adicionar um item com uma quantidade muito maior do que o estoque disponível para aquele produto (ex.: `999999`).
2. Clicar em "criar pedido".
3. **Esperado**: mensagem clara de conflito de estoque; os itens preenchidos no formulário **não** são perdidos, permitindo ajustar a quantidade e tentar novamente.

## Validar estados de carregamento e lista vazia

1. Observar o indicador de carregamento ao abrir a tela (antes da listagem/produtos chegarem).
2. (Opcional, se aplicável ao ambiente) Consultar um cenário sem nenhum pedido e confirmar a mensagem "nenhum pedido encontrado" em vez de uma tabela em branco.

## Validar responsividade básica

1. Redimensionar a janela do navegador (ou usar o modo de dispositivo móvel das ferramentas de desenvolvedor) para uma largura estreita (~375px).
2. **Esperado**: formulário e listagem se reorganizam verticalmente, sem rolagem horizontal indevida nem sobreposição de elementos.

---

## Build do frontend

```powershell
cd src/TestOrder.Web
npm run build
```

**Esperado**: build conclui sem erros (saída em `src/TestOrder.Web/dist`).

## Regressão do backend

```powershell
Get-Process TestOrder.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TestOrder.slnx
.\scripts\test.ps1
dotnet test TestOrder.slnx
```

**Esperado**: todos os testes dos módulos 001+002+003 continuam passando — **46/46**, sem nenhuma alteração no backend.

---

## Parar ambiente

- Frontend: fechar a janela `TestOrder - Web` (ou `Ctrl+C` dentro dela).
- Backend: fechar a janela `TestOrder - API` (ou `Ctrl+C` dentro dela).
- Logs do MySQL: fechar a janela `TestOrder - MySQL` (não para o container, só o acompanhamento de logs).
- Container do MySQL: `docker compose down` ou `docker compose down -v`.

## Resultado real da validação (pós-implementação)

| Validação | Resultado |
| --- | --- |
| `npm install` | PASS — 66 pacotes, 0 vulnerabilidades |
| `npm run build` | PASS — `dist/` gerado |
| `package.json` — dependências finais | `react`, `react-dom`, `vite`, `@vitejs/plugin-react` (nenhuma proibida) |
| Backend `.\scripts\test.ps1` (antes e depois do frontend) | PASS — **46/46** |
| `GET /api/products` via proxy Vite | PASS — 200, 50 produtos |
| `GET /api/orders` via proxy Vite | PASS — 200, paginado |
| `POST /api/orders` válido via proxy | PASS — 201 |
| `POST /api/orders` produto duplicado via proxy | PASS — 400 |
| `POST /api/orders` quantidade absurda via proxy | PASS — 409 |
| Quantidade 0/negativa/vazia/não numérica no formulário | Implementado; validado por revisão de código |
| Produto duplicado bloqueado + criar pedido depois sem erro residual | PASS — validado em navegador real (Playwright/Chromium) |
| Overflow horizontal em mobile (~375px) | PASS — `document.documentElement.scrollWidth === 375` (igual à viewport), sem exceder o body |
| `createdAt` exibido em UTC, sem deslocar dia por timezone local | PASS — testado com timezone de navegador `Pacific/Kiritimati` (UTC+14) |
| Console sem erros (desktop e mobile) | PASS — nenhum `console.error`/`pageerror` capturado |

**Nota**: a validação inicial de rede (200/201/400/409) foi feita via chamadas HTTP diretas ao proxy do Vite. Uma correção posterior (overflow mobile, mensagem de erro residual, formatação de data) foi validada com um navegador real headless (Playwright/Chromium), instalado apenas de forma temporária no ambiente de implementação e removido ao final — não consta em `package.json`/`package-lock.json`. Ainda assim, recomenda-se repetir o checklist manual deste arquivo por um humano antes da apresentação para itens de percepção visual subjetiva.

## Atualização — `dev-up.ps1` com janelas separadas por serviço

`.\scripts\dev-up.ps1` passou a abrir uma janela CMD dedicada para cada serviço (MySQL/logs, API, Web), permitindo acompanhar os três em tempo real sem múltiplos terminais manuais. Validado:

| Validação | Resultado |
| --- | --- |
| `.\scripts\dev-up.ps1` executa sem erro (Docker, build, npm install condicional, aviso de porta ocupada) | PASS |
| Aviso de porta `5069` ocupada aparece **antes** do `dotnet build` | PASS |
| `GET http://localhost:5069/api/products` | PASS — 200 |
| Frontend carrega (`<title>TestOrder</title>` presente) | PASS — 200 |
| `dotnet build TestOrder.slnx` | PASS |
| `.\scripts\test.ps1` | PASS — **46/46** |

Em uma sessão PowerShell normal, o script abre três janelas CMD. Antes da apresentação, confirme visualmente que as janelas `TestOrder - MySQL`, `TestOrder - API` e `TestOrder - Web` permaneceram abertas.

## Documentação pós-implementação

- `AI_NOTES.md` — seção "Módulo 004" preenchida com decisões, validações e limitações.
- `docs/PRESENTATION_GUIDE.md` — roteiro de demo e tabela de validações do módulo 004 adicionados.
- `README.md` — criado na raiz com comandos combinados de backend + frontend.
