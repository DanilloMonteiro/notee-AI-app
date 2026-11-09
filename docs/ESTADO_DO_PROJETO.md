# Estado do projeto Notee — atualizar somente com evidências

**Data de preparação deste pacote:** 09/11/2026.

> **Informação confirmada nesta entrega:** recebidos e inspecionados o PDF v1.0 (51 páginas), README do pacote v1.2, HTML de protótipo local v1.2 e duas capturas. **Não foi fornecido/inspecionado repositório de aplicação React, código backend ASP.NET, migrations, testes ou infraestrutura executada.** Logo, os marcos de implementação de produção não estão verificados.

## Tabela de situação

| Item | Estado comprovado | Evidência |
|---|---|---|
| Planejamento Notee v1.0 | Documentado | PDF recebido de 51 páginas |
| Comportamento v1.1 Mapas | Documentado/demonstrado no HTML | README v1.2 + código do protótipo |
| Refinamentos de layout v1.2 | Documentados/demonstrados | README, HTML e duas capturas |
| Arquivos de contexto Markdown | Preparados nesta entrega | `docs/*.md` e `fontes_pdf/*.md` |
| App React | **Não verificado** | repositório real não disponibilizado |
| API/backend .NET/PostgreSQL | **Não verificado** | repositório real não disponibilizado |
| Worker/Outbox/SQS/LocalStack | **Não verificado** | nenhuma execução demonstrada |
| Auth/recuperação reais | **Não verificado** | HTML é simulação |
| Provider IA real | **Não verificado** | HTML não realiza chamadas externas |
| Testes do produto | **Não verificado** | não foram fornecidas saídas reais |

## Atualização 09/11/2026 — repositório inspecionado e incremento de Mapas

- **Repositório:** branch `main`, base `b756867`, com alterações locais do autor ainda não commitadas (Notes/Research/Note.jsx), preservadas.
- **Existe de fato:** API .NET 10 (Identity JWT, Notes, Research com Worker e IA Gemini/OpenAI/Fake), EF Core + PostgreSQL com migrations, front React+JS (Vite, Router), docker-compose.
- **Implementado nesta tarefa (modo: desenvolvedora autorizada):**
  - MAP-01: domínio/API/migration `AddKnowledgeMaps` (Boards, BoardPlacements, BoardEdges, NoteReferences) — ADR-011/012/016/017.
  - MAP-02: `/boards` (catálogo) e `/boards/:boardId` (canvas: pan, zoom, arrastar, enquadrar, reorganizar, conectar, editar/remover relação, painel redimensionável/ocultável).
  - MAP-03 parcial: livros/fontes por nota; dúvidas existentes como satélites. **Não feito:** criar dúvida a partir do mapa (ADR-013).
  - Correções: referência quebrada do projeto de testes (`..\NoteLab` → `..\..\src\NoteLab`); pasta `src/context` → `src/contexts` (imports já alterados pelo autor quebravam o build); connection string configurável por `ConnectionStrings__Default`; `DomainException` → 400.
- **Comandos executados e resultado (09/11/2026, Windows, .NET SDK 10.0.403, Node):**
  - `dotnet test tests/NoteLab.Tests` → **41 aprovados, 0 falhas** (2 de título + 39 novos de domínio/NoteText).
  - `dotnet ef database update` em Postgres 16 descartável (container temporário, já removido) → todas as migrations aplicadas.
  - Teste HTTP (script Node, fora do repo) contra a API nesse banco → **40/40 verificações**: AC01–AC06, AC08, AC10, AC11 (404 entre contas), AC13 (409), cascatas.
  - `vite build` → ok. `oxlint` nos arquivos novos → 0 erros, 3 avisos `set-state-in-effect` (padrão de fetch já usado nas páginas existentes).
  - Verificação manual no Chrome (localhost): catálogo, canvas, reorganizar, arrastar (posição persistida v2), inspector e criação de conexão — sem erros no console.
- **Não executado/pendente:** MAP-AC07 e AC12 sem teste automatizado; AC09 não implementado; MAP-04/MAP-05 (contexto e IA do mapa) não iniciados — a aba "IA e contexto" informa isso e nada é enviado; testes de integração automatizados no repositório (o script HTTP ficou fora do repo).
- **Próxima tarefa pequena sugerida:** endpoint de arquivar/restaurar nota (habilita AC12) ou ADR-013 (dúvida sem âncora).

## Campos que o desenvolvedor/IA deverá manter

```md
Marco atual (M01–M18): [confirmar após inspecionar repositório]
Incremento de mapas (MAP-01..MAP-05): [não verificado]
Tarefa atual: [preencher]
Branch/commit/versão inspecionada: [preencher]
Arquivos realmente presentes: [preencher]
Implementado e verificado: [preencher com evidência]
Comandos executados e resultado (data/ambiente): [preencher]
Testes não executados ou falhos: [preencher]
Componentes simulados/ausentes: [preencher]
Decisões (ADRs): [preencher]
Pendências e riscos: [preencher]
Falha atual + reprodução mínima: [preencher]
Próxima tarefa pequena: [preencher]
Modo atual da IA: [mentora | desenvolvedora autorizada]
```

## Próxima ação sugerida sem assumir progresso

1. Inspecionar repositório real (estrutura, branches, dependências e testes).
2. Marcar os M01–M18 e MAP-01–MAP-05 como concluídos, parciais ou pendentes **somente** com evidência.
3. Se já existe app web/backend, alinhar contratos reais com este pacote sem substituições globais.
4. Se ainda é apenas o protótipo, começar M01 do PDF ou, sob autorização explícita, construir um incremento de UI de mapas demonstrável sem fingir backend.
