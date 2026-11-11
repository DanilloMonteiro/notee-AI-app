# Estado do projeto Notee — atualizar somente com evidências

## Preparação de entrega contínua (09/11/2026)

- O workflow existente de CI também foi configurado para executar em pushes na branch `deploy`; pull requests continuam cobertos. A configuração ainda precisa ser enviada ao GitHub e executada para ser validada.
- Hospedagem, branch remota, proteção de branch, credenciais e deploy automático ainda não estão configurados. A seleção da plataforma e seus custos dependem da escolha do responsável.
- Antes de publicar, configurar URL do frontend e CORS, secrets de produção, execução de migrations e persistência das chaves de Data Protection; validar o fluxo completo no ambiente escolhido.
- Railway escolhido pelo responsável: imagem da API ajustada para incluir o bundle de migrations e imagem do frontend aceita argumentos de build `VITE_API_URL` e `VITE_ADMIN_PATH`. Os builds Docker locais das duas imagens passaram em 09/11/2026; a execução no Railway ainda não foi verificada.
- Railway, 09/11/2026: PostgreSQL criado com volume persistente e conexão privada configurada na API. Os logs confirmam acesso ao banco novo, mas a inicialização falha porque as migrations ainda não foram aplicadas (`AspNetUsers` ausente). O bundle no Dockerfile local precisa ser publicado antes de configurar o pré-deploy.
- Atualização Railway: branch `deploy` publicada no commit `f0276a3`, CI aprovado; migrations executadas no pré-deploy e API conectada ao PostgreSQL. Deploy `d6a2605a-0cb8-47ec-b4dc-607f81f0c710` observado como SUCCESS, com `/health/ready` retornando 200. Persistência das chaves de Data Protection e bloqueio de deploy por CI pendem de configuração.

**Data de preparação deste pacote:** 09/11/2026.

> O texto original do pacote dizia que nenhum repositório havia sido inspecionado. **Isso mudou:** o repositório real foi inspecionado e evoluído em 09/11/2026; a tabela abaixo reflete o estado verificado.

## Situação atual (09/11/2026, commit `1ff6603` + consolidação)

> Atualizado com evidência do repositório real. "Verificado" = coberto por teste automatizado executado nesta data (CI no GitHub Actions e/ou execução local). Itens não validados estão marcados.

| Marco | Situação | Evidência |
|---|---|---|
| M01 Base reproduzível | Feito | CI (`.github/workflows/ci.yml`): testes, migrations, suítes HTTP, lint/build front, build de imagens |
| M02 Domínio de notas | Feito | arquivar/lixeira/restaurar/excluir só da lixeira; `NoteLifecycleTests`, suíte `notes` |
| M03 API e persistência | Feito (parcial: sem prefixo `/api/v1` e sem cursor) | Problem Details com traceId, concorrência otimista, Idempotency-Key em jobs; suítes HTTP |
| M04 Identidade e recuperação | Feito | refresh rotativo com detecção de reuso, logout, reset revoga sessões, rate limit; suíte `auth` |
| M05 Design system e telas | Feito (JS, sem TS/TanStack — ADR-018) | build + lint no CI; verificação manual no Chrome |
| M06 Editor, revisões | Feito | `NoteRevisions`, histórico e restauração; suíte `revisions` |
| M07 Busca e organização | Feito (ILIKE, não FTS) | filtros, busca no servidor, CRUD de categorias/tags; suíte `notes` |
| M08 Publicação revisada | Feito | snapshots, 3 visibilidades, revogação, perfil, biblioteca; suíte `publishing` |
| M09 Dúvidas e contrato de pesquisa | Feito | 202 + job, Fake determinístico, "Sem fonte"; suíte `research` |
| M10 Sugestões e automação | Feito | análise local, aceite/descarte, precedência conta/nota; suíte `analysis` |
| M11 Outbox + SQS LocalStack | Feito | Outbox/Inbox; CI roda suítes em modo `sqs` com LocalStack |
| M12 Worker recuperável | Feito | lease + fencing, retry/backoff, cancelamento; `ResearchJobTests`; recuperação de lease verificada no banco |
| M13 Pesquisa real com fontes | Implementado, **não validado contra a API real** | servidor Gemini falso (unitário + suíte `provider`); consentimento v2 |
| M14 Segurança e avaliação | Parcial | cabeçalhos, 500 sem detalhes, quotas, URLs http(s), conteúdo publicado como texto; **sem** pentest nem avaliação de qualidade de respostas |
| M15 Observabilidade | Feito | OpenTelemetry (OTLP opcional), métricas/spans por JobId, health checks; suíte `ops` |
| M16 Terraform/AWS full-local | Parcial | Terraform de SQS + DLQ aplicado no LocalStack (local e CI); SES/RDS/ECS/ALB **não** provisionados |
| M17 Entrega/desempenho/restore | Feito (laboratório) | `docs/RUNBOOK.md`; restore verificado; carga local 800 req / 0 erros |
| M18 Evidências e consolidação | Feito | ADRs 011–029, este arquivo, README |
| MAP-01..05 Mapas de Ideias | Feito | suítes `maps` (AC01–AC13) e `graph-ai`; "perguntar ao mapa" com provider real fica pendente (ADR-024) |

**Números da última execução local (09/11/2026):** 89 testes unitários; 11 suítes HTTP com 221 verificações passando (in-process); em modo SQS as mesmas suítes passaram (exceto `provider`, criada depois e coberta no CI).

**Pendências conhecidas:** validar o Gemini com chave real (com consentimento), migrar tokens para cookie HttpOnly (ADR-003), TypeScript/TanStack Query no front (ADR-018), FTS do PostgreSQL quando houver volume, prefixo `/api/v1` e paginação por cursor, provisionar demais serviços AWS no LocalStack, testes automatizados de interface.

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

## Atualização 10/11/2026 — interface de tags

- No editor, o campo de tags recebeu botão de adicionar, foco visível e layout flexível; as tags selecionadas receberam ajustes visuais e de remoção.
- Validação local: `npm run build` em `Notee-Front` concluído; `git diff --check` sem erros. Verificação visual no navegador não executada.

## Atualização 10/11/2026 — seleção e sugestões de tags

- O organizador lista e filtra tags existentes ao focar o campo e permite criar categorias no próprio editor. O botão de análise existente identifica também as sugestões de tags; a análise é local e exige aceite individual.
- `npm run build` em `Notee-Front` e `git diff --check` concluídos. Verificação visual no navegador não executada.

## Atualização 10/11/2026 — apresentação de categorias

- O seletor de categoria recebeu estilo alinhado ao campo de tags, e a criação abre em um card junto ao botão.
- `npm run build` em `Notee-Front` e `git diff --check` concluídos; verificação visual no navegador não executada.

## Atualização 10/11/2026 — conteúdo vazio no editor

- Corrigida a interpretação da lista vazia de blocos para abrir o editor com parágrafo vazio, sem exibir `[]`.
- `npm run build` em `Notee-Front` e `git diff --check` concluídos; teste no navegador não executado.

## Atualização 10/11/2026 — explicação das sugestões

- O painel explica a análise local da versão salva, os tipos de sugestão e o aceite individual; indica quando é preciso salvar antes de analisar.
- `npm run build` em `Notee-Front` e `git diff --check` concluídos; verificação no navegador não executada.
