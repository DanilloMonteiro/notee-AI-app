# Notee — contexto para o projeto no Claude

> Arquivo de conhecimento do projeto. Resume os documentos de `docs/` do repositório (estado em 16/11/2026, commit `8e2d31c`). **O Claude deste projeto não tem acesso ao repositório:** quando precisar de código, peça que eu cole os arquivos. Não presuma que algo existe ou funciona sem eu mostrar.

## 1. Produto

**Notee — Anote agora. Entenda depois.** Espaço para escrever notas, marcar dúvidas no meio do raciocínio sem interromper a escrita, pesquisar depois com fontes e limites declarados, organizar ideias em mapas e publicar snapshots escolhidos. Privado por padrão; o texto do autor nunca é alterado pela IA sem aceite.

**Ciclo principal:** criar conta → escrever nota em blocos → organizar (categoria/tags) → marcar dúvida (manual ou sugestão aceita) → pesquisar (job assíncrono) → revisar resposta separada com fontes ou `Sem fonte` → opcionalmente publicar snapshot.

### Vocabulário

| Termo | Significado |
|---|---|
| Note | Documento canônico do usuário. Nunca copiado para dentro de um mapa. |
| Block | Unidade editável com ID estável dentro da nota. |
| Category / Tag | Zero ou uma categoria por nota; várias tags (N:N). |
| NoteRevision | Snapshot imutável; restaurar cria nova revisão. |
| Doubt | Pergunta ligada a uma nota (e a uma âncora no texto, se veio de seleção). |
| ResearchJob / ResearchAnswer | Execução assíncrona e resposta separadas; status do job ≠ qualidade da evidência. |
| Publication | Snapshot `Private` / `Unlisted` / `Public`; nunca lê o rascunho diretamente. |
| Board | Mapa de ideias privado (título, descrição, layout). |
| BoardPlacement | Posição de uma nota em um board; o mesmo NoteId aparece em vários boards. |
| BoardEdge | Relação dirigida entre duas notas *naquele* board, com tipo e rótulo. |
| NoteReference / Source / Citation | Livros e fontes da nota; biblioteca de fontes com citações e verificação em camadas. |

### Princípios obrigatórios

1. **Privado por padrão** — notas, dúvidas, mapas e relações. Publicar usa snapshot próprio.
2. **Autoridade humana** — sugestões de IA (dúvidas, tags, categoria, organização, arestas do mapa) exigem aceite individual e revalidação; nada é salvo antes do usuário confirmar.
3. **Notas únicas, visões múltiplas** — editar a nota reflete em todos os mapas; mover/ligar nós em um mapa não afeta os outros.
4. **Resultados rastreáveis** — rótulos `Com fontes`, `Evidência parcial`, `Sem fonte`. Nunca inventar fontes.
5. **IA agnóstica de provedor** — Fake determinístico por padrão; provedor real só com consentimento explícito, escopo definido, cota e custo informado.
6. **AWS só emulada** — LocalStack + Terraform; nada de deploy AWS real.
7. **Evidência antes de "pronto"** — só declarar implementado/testado o que tem comando executado e resultado.

## 2. Stack real (o que existe no repositório)

**Backend — `Notee-API/`** (solução `CSharpModernLabs.slnx`, projeto `src/NoteLab`)
- ASP.NET Core / .NET 10, monólito modular, EF Core + PostgreSQL 16, migrations versionadas.
- Módulos em `src/NoteLab/Modules/`: `Identity`, `Notes`, `Research`, `Publishing`, `KnowledgeMaps`, `Social`, `Admin`; código comum em `Shared/`.
- Worker de jobs com lease + fencing, retry/backoff, cancelamento; Outbox transacional + Inbox; SQS via LocalStack (Terraform aplica SQS + DLQ).
- Auth: JWT com access token curto + refresh rotativo com detecção de reuso; rate limit; Problem Details com traceId; concorrência otimista; `Idempotency-Key` em jobs.
- IA: adaptadores ChatGPT, Claude, Grok, DeepSeek, Gemini e Fake; painel admin oculto para escolher o provedor ativo e cadastrar chaves.
- OpenTelemetry (OTLP opcional), health checks.
- Testes: `dotnet test tests/NoteLab.Tests` (xUnit, 195 na última execução) + suítes HTTP em Node (`tests/http/run-all.mjs`) contra API em Postgres descartável.

**Frontend — `Notee-Front/`**
- React 19 + **JavaScript (JSX)** + Vite 8 + React Router 7. **Sem TypeScript e sem TanStack Query** por enquanto (ADR-018) — o plano original prevê ambos.
- Sem bibliotecas de UI ou gráficos: componentes próprios, SVG à mão.
- Pastas: `src/pages`, `src/components`, `src/contexts`, `src/features/{admin,boards,charts,notes,publishing,social,sources}`, `src/api.js`.
- Lint: `oxlint`. Testes: `node --test` (25 na última execução). Layout desktop-first (`body` com `min-width: 1120px`).

**Infra:** `docker-compose.yml`, `infra/` (Terraform/LocalStack), `scripts/` (backup/restore), `tests/load`, CI no GitHub Actions.

## 3. Design system Notee

Tom natural, silencioso, editorial. Desktop-first.

| Token | Valor | Uso |
|---|---|---|
| `brand` | `#21573D` | ações principais |
| `brandStrong` | `#17412D` | hover/ênfase |
| `accent` | `#D8F4A1` | acento com texto escuro |
| `canvas` | `#F7F8F5` | fundo de trabalho |
| `surface` | `#FFFFFF` | painéis/cards |
| `ink` | `#1E2D25` | texto/títulos |
| `muted` | `#6C7A71` | secundário |
| `border` | `#E1E7DE` | contornos |

Espaçamento 4/8/12/16/24/32/48 px · radius 8/10/16 px · UI 14 px, leitura 16 px, títulos 24–36 px · fonte de sistema.

**Layout v1.2:** sidebar 228 px ↔ recolhida 72 px; faixa horizontal de dúvidas abaixo do título da nota; divisor redimensionável editor ↔ painel (acessível por teclado); catálogo de mapas em cartões; canvas pontilhado com pan/zoom, "Enquadrar tudo", painel `Nota / IA e contexto` redimensionável e ocultável.

**Estados que não podem faltar:** vazio, carregando, salvando, salvo, erro, limite atingido, sem permissão, conflito, resposta desatualizada, contexto insuficiente, `Sem fonte`, timeout, provedor indisponível, cancelado.

## 4. O que já está feito (com evidência registrada)

| Área | Situação |
|---|---|
| M01–M12 (base, notas, API, identidade, telas, editor/revisões, busca, publicação, dúvidas/pesquisa, sugestões, Outbox/SQS, Worker) | Feito. Ressalvas: sem prefixo `/api/v1`, sem paginação por cursor, busca com ILIKE (não FTS). |
| M13 pesquisa real com fontes | Implementado, **não validado contra API real** (só servidor falso). |
| M14 segurança | Parcial: cabeçalhos, quotas, 500 sem detalhes; sem pentest. |
| M15 observabilidade, M17 entrega/restore, M18 consolidação | Feito (laboratório). |
| M16 Terraform/LocalStack | Parcial: só SQS + DLQ. |
| Mapas MAP-01..05 | Feito (boards, canvas, conexões, contexto e sugestões com aceite). "Perguntar ao mapa" com provedor real pendente. |
| Social (fora do escopo original, ADR-033–035) | Seguidores, feed, curtidas/reposts, comunidades, descoberta combinada, leituras, respostas, moderação. |
| Gráficos em notas (ADR-036) | Bloco de gráfico editável, colar tabela vira gráfico, gráfico interativo. |
| Organizar em tópicos (ADR-037) | Regras locais + IA opcional com validação de preservação do texto. |
| Sugestão de categoria/tags (ADR-038) | Local + IA opcional, aceite individual ou em lote. |
| Comentários (ADR-039) | Privados ancorados em trechos; públicos ancorados no snapshot, respostas, curtidas, avisos/sino. |
| Fontes e citações (ADR-040–042) | Biblioteca, resolução ISBN/DOI/URL, verificação em camadas, upload privado, snapshot bibliográfico na publicação. Suítes HTTP dessas features **ainda não executadas**. |

**Pendências conhecidas:** validar provedores reais com chave; tokens em cookie HttpOnly (ADR-003); TypeScript + TanStack Query; FTS no PostgreSQL; `/api/v1` + cursor; demais serviços AWS no LocalStack; testes automatizados de interface; chunk JS > 500 kB; moderação global/denúncias/bloqueios; colaboração simultânea (fora do escopo).

## 5. Regras de engenharia

- Módulos separados por negócio; um módulo não altera tabelas de outro — usa serviços/portas com filtro por owner.
- Domain não depende de ASP.NET/EF/SDKs; Application orquestra; Infrastructure faz I/O; API/Worker são composition roots.
- Todo acesso isolado por owner (recurso de outro usuário → 404).
- Revisões imutáveis, concorrência otimista (409 em conflito), idempotência em jobs e relações.
- Nada de conteúdo privado em logs, telemetria ou `localStorage`.
- Nenhum dado vai para IA externa sem consentimento e escopo; "mapa inteiro / subgrafo" define escopo, não consentimento.
- Novas decisões viram ADR (próximo número livre após ADR-042). Mudanças de escopo exigem ADR.
- Mudanças pequenas e localizadas; não refatorar globalmente nem criar scaffolding vazio.
- Não usar microserviços, Kubernetes, embeddings obrigatórios, app mobile ou deploy AWS real.

## 6. Como quero que você (Claude) trabalhe neste projeto

**Modo padrão: mentora.** Eu implemento, você orienta:
1. Defina um objetivo pequeno, o motivo técnico, os conceitos necessários e como verificar.
2. Espere minha tentativa; dê pistas graduais e revise o código que eu colar.
3. Entregue código pronto **somente quando eu pedir explicitamente** ("modo desenvolvedora" / "escreve o código") — aí entregue dentro do arquivo/escopo pedido, com explicação e teste.

**Sempre:**
- Responda em português.
- Peça o arquivo real antes de propor alteração nele; não invente nomes de classes, rotas ou migrations.
- Diferencie claramente: decisão consolidada × proposta × lacuna.
- Nunca afirme que testes passaram, que algo foi deployado ou que uma fonte existe sem evidência que eu forneça.
- Ao propor uma feature, inclua: tarefas pequenas, arquivos prováveis, contrato da API, migration (se houver), critérios de aceite e como testar (`dotnet test`, suíte HTTP, `npm test`, `npm run build`, verificação manual).
- Respeite os tokens visuais e os estados de interface listados acima.
- Ao final, diga o que eu devo registrar em `docs/ESTADO_DO_PROJETO.md` e se precisa de ADR.

## 7. Documentos completos no repositório (para eu colar quando necessário)

| Assunto | Arquivo |
|---|---|
| Precedência das fontes | `docs/00_COMECE_AQUI_E_PRECEDENCIA.md` |
| Produto, requisitos | `docs/01_PRODUTO_E_ESCOPO.md`, `docs/02_REQUISITOS_E_CASOS_DE_USO.md` |
| UX | `docs/03_UX_DESIGN_SYSTEM.md`, `docs/UX_V12.md` |
| Arquitetura, dados, API | `docs/04_ARQUITETURA_E_STACK.md`, `docs/05_MODELO_DE_DADOS.md`, `docs/06_API_E_CONTRATOS.md` |
| Editor e âncoras | `docs/07_EDITOR_REVISOES_E_ANCORAS.md` |
| Pesquisa/IA, jobs | `docs/08_PESQUISA_IA_E_FONTES.md`, `docs/09_JOBS_OUTBOX_IDEMPOTENCIA.md` |
| Segurança, infra, testes | `docs/10_SEGURANCA_E_PRIVACIDADE.md`, `docs/11_INFRA_TESTES_OBSERVABILIDADE.md`, `docs/RUNBOOK.md` |
| Plano e ADRs | `docs/12_PLANO_DE_IMPLEMENTACAO.md`, `docs/13_ADRS_E_DECISOES_ABERTAS.md` |
| Mapas de Ideias | `docs/MAPAS_DE_IDEIAS.md`, `docs/MAPAS_CONTEXTO_IA.md` |
| Planos de features | `docs/PLANO_GRAFICOS_EM_NOTAS.md`, `docs/PLANO_COMENTARIOS_E_SUGESTOES.md`, `docs/PLANO_FONTES_E_VERIFICACAO.md` |
| Estado real | `docs/ESTADO_DO_PROJETO.md` |

**Ideia exploratória (não aprovada):** importar conversa compartilhada de um serviço de IA como nota, com prévia, seleção de trechos e original preservado como fonte.
