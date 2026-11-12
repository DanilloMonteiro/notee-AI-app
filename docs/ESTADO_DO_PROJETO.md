# Estado do projeto Notee — atualizar somente com evidências

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

## Atualização 10/11/2026 — fundação social (parcial)

- Implementados seguidores unilaterais, contagens no perfil, feed cronológico em `/feed`, curtidas de utilidade e reposts. Relações idempotentes com chaves compostas; feed somente de publicações públicas ativas e seus snapshots.
- Migration `AddSocialFoundation` gerada e aplicada no banco isolado `notee_social_test_20261110`. A instância existente e o banco da aplicação não foram alterados.
- Verificado: build frontend; compilação API Release; suíte HTTP social 12/12; suíte HTTP publishing 30/30; `git diff --check` sem erros.
- Escopo ainda parcial: faltam cursor, destaques, atribuição de reposts, biografia, aba de reposts, bloqueios e denúncias; comunidades, discussão, ranking combinado e edição simultânea não implementados. ADR-033 registra continuidade. Nenhum deploy executado.

## Atualizacao 10/11/2026 - comunidades e discussao inicial

- Comunidades abertas: criacao, participacao, vinculo idempotente de publicacoes publicas proprias e remocao pelo criador. Conversas vinculadas a publicacao com referencia a revisao e exclusao pelo autor do comentario.
- Frontend: catalogo, pagina da comunidade, compartilhamento de publicacoes e comentarios. Ordenacoes iniciais por data, curtidas, reposts e soma de curtidas/reposts (ainda nao e o ranking normalizado completo planejado).
- Migration AddCommunities aplicada somente no banco isolado notee_social_test_20261110. Suite HTTP communities: 14/14; build da API Release e frontend aprovados.
- Pendentes: moderadores e suspensoes, respostas nos comentarios, pesquisa e filtros combinados completos, visualizacoes, paginacao da interface da comunidade, colaboracao simultanea. Sem deploy.

## Atualizacao 10/11/2026 - descoberta combinada

- Pagina /discover com busca, selecao de comunidades, periodo e combinacao de curtidas, reposts e recencia. Consulta apenas snapshots publicos ativos; uniao de comunidades sem duplicacao. ADR-034 descreve calculo e limites.
- Verificado: build frontend aprovado; API Release sem avisos/erros; suite HTTP discovery 10/10 contra PostgreSQL isolado.
- Commits desta continuidade: d0803b3 (fundacao social), 36bd493 (comunidades e comentarios). Descoberta registrada em commit separado a seguir.
- O plano maior continua parcial: visualizacoes, cursor estavel, moderacao completa, respostas em comentarios e colaboracao simultanea ainda pendentes. Nao houve deploy ou alteracao do banco principal.

## Atualizacao 10/11/2026 - leituras, respostas e moderacao

- Implementadas leituras unicas por usuario/dia UTC, exclusao do autor da contagem e criterio de mais vistas na descoberta. A leitura publica autenticada recebeu botoes de utilidade/repost e conversa.
- Comentarios com um nivel de resposta, exclusao por marcador preservando respostas e validacao de publicacao/revisao.
- Criador nomeia moderadores e suspende/restaura membros; moderadores removem vinculos de notas da comunidade. Sair/entrar nao contorna suspensao. ADR-035 registra os limites.
- Migration AddCommunityModerationAndReading aplicada no banco isolado notee_social_test_20261110. Testes HTTP: moderation 18/18, communities 14/14, discovery 10/10, social 12/12, publishing 30/30 (84 verificacoes). API Release e build frontend aprovados; lint concluiu com avisos.
- Verificacao de interface no navegador inconclusiva: pagina carregou API_URL localhost:5000, enquanto a instancia isolada usa 5099; nao declarar fluxo visual aprovado. Instancias de teste encerradas.
- Pendentes: cursor estavel, moderacao global/denuncias/bloqueios, refinamentos de perfil/feed, colaboracao simultanea. Nenhum deploy realizado; banco principal preservado.

## Atualização 10/11/2026 — consistência visual das telas sociais

- Feed, descoberta, comunidades, participantes, comentários e ações de leitura alinhados aos tokens existentes: cabeçalhos, cards, avatares, ícones, campos, botões e espaçamentos. Componentes visuais compartilhados em SocialUI.jsx e estilos restritos em social.css.
- Filtros em painel lateral; criação de comunidade com foco no nome; estados de carregamento, vazio e erro com nova tentativa; formulários de conversa com foco ao responder. Navegação com grupos e nomes de páginas reais. Paginação da comunidade utiliza o endpoint já existente.
- Validação executada: build frontend aprovado; lint concluído com avisos, incluindo set-state-in-effect nos fluxos de fetch; diff conferido. Navegador com respostas simuladas (sem API/banco): catálogo, descoberta, moderação, conversa, foco na resposta/criação, erro e recuperação, feed vazio e reação ativa. Sem rolagem horizontal no feed em 1120 px e nas telas inspecionadas em 1440 px.
- Esta rodada não revalidou persistência ou permissões da API; testes HTTP anteriores não foram repetidos. Não houve alteração de backend, migration ou deploy.

## Atualização 12/11/2026 — gráficos estruturados em notas (v1, ADR-036)

- Branch `feature/editor-organizacao-deploy`, base `ad87423`; alterações **não commitadas**. Escopo autorizado: somente gráficos (a importação de conversa de IA continua sugestão pendente).
- Backend: `NoteContent.Validate` (create/update; 400 para gráfico inválido), `NoteText.Extract` inclui metadados do gráfico na busca/contexto, `FindSentence` ignora blocos sem texto. Sem migration.
- Frontend: bloco “Gráfico” no editor (`ChartEditor`: metadados, tabela rótulos × séries, fontes, pré-visualização), `ChartView` em SVG + tabela acessível + fontes declaradas, renderização nas páginas públicas, parser único `features/notes/noteBlocks.js`. Sem dependência nova.
- Comandos executados (12/11/2026, Windows, .NET SDK 10, Node):
  - `dotnet test` (Notee-API) → **138 aprovados, 0 falhas** (inclui `NoteContentTests`).
  - Postgres 16 descartável (porta 5499, removido ao final) + `dotnet ef database update` + API em `:5099` com a configuração do CI (modo `inprocess`) → `node tests/http/run-all.mjs`: **17 suítes, 326 verificações, 0 falhas**, incluindo a nova `charts` (16/16). Modo SQS não executado nesta rodada.
  - `npm run lint` → sem avisos nos arquivos novos (avisos preexistentes permanecem); `npm run build` → ok.
  - Chrome (localhost, contra a API de teste): renderização de linha e barras com ausente/negativo, edição de valor refletida em gráfico e tabela, salvar (v2) e persistência, adicionar/cancelar gráfico novo, validação no editor, link compartilhado mostrando o snapshot publicado; sem erros no console.
- Não verificado: largura estreita (o redimensionamento da janela não teve efeito e o app define `min-width: 1120px` no `body`), leitor de tela real, restauração de revisão pela interface (coberta pela suíte HTTP).

## Atualização 12/11/2026 — colar texto com tabela vira gráfico

- Novo `Notee-Front/src/features/charts/detectTables.js` + integração no `onPaste` do editor (`Note.jsx`), com aviso “Desfazer/OK”. Sem mudança no backend. Alterações **não commitadas**.
- Comandos executados: `npm test` (novo script, runner nativo `node --test`, sem dependência) → **8/8**, incluindo o texto exato fornecido pelo usuário (Produção industrial 2014–2018 → barras `[-3, -8,3, -6,4, 2,5, 1,1]` %); `npm run lint` → sem avisos novos; `npm run build` → ok.
- Chrome (localhost:5174 contra API de teste em Postgres descartável, removido ao final): colagem do texto completo → parágrafo + gráfico + parágrafo; Desfazer → texto original intacto; colar sobre seleção + salvar → v2 aceita pela API (200), persistida após recarregar; colagem de prosa com percentuais não é interceptada; sem erros no console. Evento de colagem simulado via `ClipboardEvent` (não pela área de transferência do sistema).
- Não coberto: colagem real com Ctrl+V do sistema operacional, planilhas coladas com colunas vazias no meio, tabelas com mais de 120 linhas (dividem-se em mais de um gráfico).

## Atualização 12/11/2026 — gráfico interativo/compacto, organizar em tópicos (local e IA) e remoção de faixas vazias

- Front: `ChartView` reescrito (hover/foco com destaque e tooltip, teclado, modo compacto + “Ver dados”); `organizeText.js` + botão “Organizar em tópicos”; `OrganizeAiModal` (estimativa → envio confirmado → prévia → aplicar/descartar); títulos com nível no editor e na página pública; Backspace remove bloco vazio; remover gráfico leva junto parágrafos vazios vizinhos; altura mínima de 80 px dos blocos removida. Back: `IAiService.CompleteAsync` nos adaptadores, `NoteAiOrganizer` (validação de preservação de texto), endpoints `organize-ai`, cota por usuário. Alterações **não commitadas**.
- Comandos executados (12/11/2026):
  - `npm test` (node --test) → **14/14** (inclui o texto de referência completo: 11 seções, destaques, 3 gráficos com fonte adjacente, nenhuma palavra perdida/inventada); `npm run lint` → sem avisos novos; `npm run build` → ok.
  - `dotnet test` (build em pasta separada porque a API de desenvolvimento do usuário na porta 5000 mantinha o binário em uso) → **151 aprovados, 0 falhas**.
  - Postgres descartável + API em :5099 com a configuração do CI → `run-all.mjs`: **18 suítes, 338 verificações, 0 falhas** (nova `organize`: 12).
  - Verificação ad hoc (fora do repositório) do caminho externo contra servidor OpenAI-compatível **falso** local: consentimento → provedor externo, tokens reportados, requisição sem ferramentas; resposta que inventa conteúdo → 422. **Nenhuma chamada a provedor real.**
  - Chrome (localhost:5174): colar texto de referência → 3 gráficos; “Organizar em tópicos” → título da nota, 28 títulos, listas e destaques; hover em barras e linhas (6 séries) com destaque e tooltip; teclado; Ver dados/Ocultar; remover gráfico sem blocos vazios; Backspace; modal de IA em simulação (prévia + descartar); Desfazer; salvar (v2) e recarregar; página pública com títulos, gráficos e listas; sem erros no console.
- Não coberto: `CompleteAsync` do adaptador OpenAI-compatível por teste unitário (depende de `SystemSettings`; coberto apenas pela verificação ad hoc), Claude/Gemini reais, tela estreita, leitor de tela real.
