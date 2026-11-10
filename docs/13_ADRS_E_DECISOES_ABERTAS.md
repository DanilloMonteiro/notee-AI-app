# Decisões arquiteturais (ADRs) e pendências

**Base:** PDF p. 43 (ADR 001–010). Extensões 011–016 são propostas para Mapas de Ideias.

## ADRs do planejamento original

| ID | Decisão inicial | Critério de revisão |
|---|---|---|
| 001 | Monólito modular, API + Worker | isolamento/equipes independentes demonstrados |
| 002 | SPA React/Vite + API .NET | indexação/SSR de publicações virar requisito central |
| 003 | Identity com política de sessão explicitada | cookies vs JWT: segurança e complexidade |
| 004 | Editor em blocos com schema/IDs estáveis | migração e novos tipos de bloco |
| 005 | Publicação como snapshot revogável | autor aprovar atualização pública automática |
| 006 | Providers com capacidades; resposta/fonte separadas | modos de integração reais e limitações |
| 007 | Outbox + SQS + checkpoints no PostgreSQL | garantias/retry/lease demonstrados |
| 008 | PostgreSQL e FTS; Redis/embeddings após medição | gargalo/qualidade justificados |
| 009 | AWS somente emulada no LocalStack | suporte/plano e ambiente validados |
| 010 | Testes backend; frontend manual agora | evolução de escopo decidida |

## ADRs novos a elaborar

| ID sugerido | Pergunta | Proposta inicial (não aprovada definitivamente) |
|---|---|---|
| 011 | Onde moram mapas? | módulo `KnowledgeMaps`, dados de board/placement/edge sem copiar notas |
| 012 | Todas as notas em todos os mapas? | demo inclui todas as notas ativas; conservar até decisão de membership seletivo |
| 013 | Como representar dúvidas criadas no mapa? | `Doubt` de origem `BoardQuestion`, sem âncora, associada à nota |
| 014 | Que informação vai ao modelo? | snapshot versionado; `all`/`selected`; somente notas do owner e edges internas; consentimento |
| 015 | Como aplicar propostas de IA? | comando de aceite humano versionado/atômico; Fake por padrão |
| 016 | Onde registrar livros/fontes? | metadados `NoteReference` por nota, não por board nem equivalentes a `ResearchSource` verificado |

## Decisões ainda não resolvidas pelos anexos

- **Membership do mapa:** adicionar toda nota ativa automaticamente ou permitir adicionar/remover sem apagar a nota? A demo faz a primeira opção.
- **Escala/canvas:** quantidade máxima de notas/edges, performance, virtualização, layout automático e sincronização de posição em arrasto contínuo.
- **Rótulos de arestas:** permitir duplicatas semânticas, arestas paralelas com mesmo tipo e label, auto-ligação e direção inversa como relação separada.
- **Versão do contexto:** semântica de `boardVersion`, `noteRevision`, referência e status de dúvida quando pergunta em andamento; tratamento de contexto obsoleto.
- **Consumo de IA do mapa:** estrutura exata do comando de pergunta, limite de tokens, redator do plano de consulta, política de fontes, cota, forma de revisar resposta.
- **Consentimento por escopo:** se o usuário permite textos de fontes/autores indicados em nota, respostas revisadas e campos de referência na pesquisa externa.
- **Privacidade e lifecycle:** política de exclusão de grafo, estado de nós em notas arquivadas e revogação de snapshots em jobs ativos.
- **UI v1.2:** valores mínimos/máximos dos painéis, persistência por usuário ou só browser, comportamento de preferência entre dispositivos.
- **Política de testes frontend:** o PDF posterga automação, mas um grafo interativo pode justificar ADR posterior para testes focados, sem alterar silently o escopo original.

## Template de ADR

```md
# ADR-011 — Título
Status: Proposto | Aceito | Substituído
Data: YYYY-MM-DD
Contexto:
Opções consideradas:
Decisão:
Consequências positivas e negativas:
Testes/evidências:
Quando reconsiderar:
```

Nunca marcar ADR proposto como aprovado apenas por estar escrito aqui.

## ADRs registrados na implementação de MAP-01/MAP-02 (09/11/2026)

> Status **Proposto/Implementado**: refletem o código entregue, mas aguardam aceite do responsável pelo produto.

### ADR-011 — Mapas no módulo KnowledgeMaps
Status: Proposto (implementado) · Data: 2026-11-09
Contexto: o repositório usa um único `NoteDbContext` e tabelas no schema `public`, sem prefixo `/api/v1`.
Decisão: entidades `Board`, `BoardPlacement`, `BoardEdge` em `Modules/KnowledgeMaps`; tabelas `Boards`, `BoardPlacements`, `BoardEdges` no schema padrão e rotas `/boards`, seguindo a convenção atual em vez do `knowledge_maps.*`/`/api/v1` sugerido no pacote. Conteúdo de nota nunca é copiado para o board.
Consequências: consistente com o código existente; migrar para schema/prefixo próprios exigirá migration e ajuste de rotas.
Testes: `KnowledgeMapsDomainTests.cs` + teste HTTP (ver `ESTADO_DO_PROJETO.md`).

### ADR-012 — Todas as notas não arquivadas em todos os mapas
Status: Proposto (implementado) · Data: 2026-11-09
Contexto: a demo inclui "todas as notas ativas". No repositório, notas novas nascem `Draft` e não há endpoint que as torne `Active`.
Decisão: exibir notas `Draft` e `Active` (excluir `Archived`). Limite de 500 nós por mapa. O GET **não grava** posições: notas sem placement recebem posição padrão determinística (`positionVersion: null`) e só são persistidas ao arrastar/reorganizar.
Quando reconsiderar: introdução de membership seletivo ou de fluxo Draft→Active.

### ADR-013 — Dúvidas criadas no mapa (adiado)
Status: Aberto. `Doubt.Anchor` é obrigatório hoje e o módulo Research tem alterações em andamento; a criação de dúvida sem âncora (`origin=BoardQuestion`) não foi implementada. O mapa só **exibe** dúvidas existentes e direciona para o editor.

### ADR-016 — Livros/fontes como `NoteReference` da nota
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: tabela `NoteReferences` (FK para `Notes`, cascade), rotas `/notes/{id}/references`. URL aceita apenas http/https. Não altera `Note.Version` (para não gerar 409 em um editor aberto), pois ainda não existe tabela de revisões.
Quando reconsiderar: quando `notes.revisions` existir.

### ADR-017 — Regras de grafo e concorrência
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: rejeitar auto-conexão (domínio + `CHECK`); rejeitar duplicata exata (from, to, tipo, rótulo sem diferenciar maiúsculas) com 409; permitir direção inversa. Limites: 2000 arestas/mapa, coordenadas em [-1000, 7000]. Concorrência otimista: `Board.Version`, `BoardEdge.Version`, `BoardPlacement.PositionVersion` (tokens de concorrência + `expectedVersion`) → 409. Reorganizar usa `PUT /boards/{id}/placements` atômico. Excluir nota remove posições/arestas por cascade; excluir mapa nunca apaga notas.

### ADR-018 — Frontend do mapa sem TanStack Query/TypeScript
Status: Proposto · Data: 2026-11-09
Contexto: o front real é React + JavaScript com `apiFetch`, sem TanStack Query nem TS.
Decisão: implementar `src/features/boards/*.jsx` no padrão existente, sem adicionar dependências. Migração para TS/TanStack Query fica como decisão separada.

### ADR-019 — Consentimento explícito antes de IA externa; Fake por padrão
Status: Proposto (implementado) · Data: 2026-11-09
Contexto: o worker enviava pergunta e trecho ao Gemini sem consentimento, contrariando `10_SEGURANCA_E_PRIVACIDADE.md`.
Decisão: tabela `UserPreferences` (pesquisa ligada/desligada, consentimento versionado). `AiProviderSelector` usa Gemini só se a conta consentiu na versão vigente do termo **e** há chave real configurada; caso contrário, `FakeAiService` determinístico, que declara "Sem fonte". Pesquisa desligada deixa a dúvida pendente. Revogação vale para os próximos processamentos.
Consequências: contas existentes passam a receber respostas simuladas até consentirem em Configurações.

### ADR-020 — Segredos via configuração
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: `Jwt:*`, `ConnectionStrings:Default` e `Cors:Origins` vêm da configuração (appsettings local não versionado, variáveis de ambiente). Na ausência, valores de desenvolvimento com **aviso no log**. A chave JWT antiga deixou de existir no código: tokens emitidos antes da mudança são invalidados. Expiração padrão reduzida de 240h para 12h. Rate limit de 10 req/min por IP em `/auth` (`RateLimit:AuthPerMinute`).

### ADR-021 — Ciclo de vida da nota e busca no servidor
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: estados `Draft`, `Active`, `Archived`, `Trashed` (novo). Rotas `POST /notes/{id}/archive|trash|restore` (com `expectedVersion` opcional) e `DELETE /notes/{id}` **somente na lixeira** (409 caso contrário), apagando também as dúvidas da nota. Nota na lixeira não é editável. `GET /notes` passa a filtrar por `status` (padrão: rascunho+ativa), `query`, `categoryId`, `tagId`, ordenado por `UpdatedAt desc, Id` e com `X-Total-Count`. Busca via `ILIKE` em título e na nova coluna `PlainText` (texto extraído dos blocos no servidor; notas antigas usam `Content` até serem salvas). FTS do PostgreSQL fica para quando houver medição (ADR-008).
`PUT /notes/{id}` passa a substituir categoria (nula = remove) e tags (lista vazia = remove; nula = mantém). Categorias e tags ganharam listagem com contagem, renomear e excluir (nome único por dono, sem diferenciar maiúsculas).

### ADR-022 — Revisões imutáveis de notas
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: tabela `NoteRevisions` (UNIQUE `NoteId, NoteVersion`) com título, conteúdo, categoria e tags de cada versão salva. Criar e editar geram revisão; arquivar/lixeira e mapas não. Notas anteriores ao histórico recebem uma revisão `Baseline` com o estado anterior na primeira edição. `POST /notes/{id}/restore-revision` aplica o conteúdo como **nova** versão (`Restored`, com `RestoredFromVersion`) — o histórico nunca é reescrito. Categoria/tags que não existem mais são ignoradas na restauração. Sem política de retenção por enquanto (reavaliar com volume real).

### ADR-013 (atualização) — Dúvidas criadas no mapa
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: `Doubt.Anchor` passou a ser opcional e `Doubt.Origin` (`Selection` | `BoardQuestion`) foi adicionado. `POST /notes/{noteId}/doubts {question}` cria a dúvida sem âncora, ligada à nota (nunca ao mapa). Dúvidas do editor continuam exigindo âncora.

### ADR-023 — Jobs de pesquisa duráveis com a tabela como fila
Status: Proposto (implementado) · Data: 2026-11-09
Contexto: M09/M11/M12 pedem 202 + job durável, idempotência, lease/fencing, retry e cancelamento. SQS/LocalStack (M11/M16) ainda não está provisionado.
Decisão: `ResearchJobs` é criado **na mesma transação** da dúvida (ou por `POST /doubts/{id}/research-jobs`, que responde `202` + `Location`). O worker reivindica com `UPDATE ... WHERE Id IN (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING`, lease de 2 min e `FencingToken` (token de concorrência do EF: escrita de worker obsoleto é descartada). Lease vencido é reivindicado de novo. Falhas: até 3 tentativas com backoff 10s/20s/40s. Cancelamento: imediato na fila; em execução, sinalizado e respeitado antes de gravar. Índices únicos: um job ativo por dúvida; `Idempotency-Key` por dono. Respostas registram provider e `Evidence = NoSource` (providers atuais não devolvem citações verificáveis).
Consequências: sem broker externo, a tabela é a fila e já funciona como Outbox transacional; publicar em SQS fica para quando LocalStack existir (M11/M16), mantendo este contrato.
Testes: unitários de `ResearchJob`; suíte HTTP `research` (19 verificações); recuperação de lease e migration com dados legados verificadas manualmente no banco de teste.

### ADR-024 — Contexto do mapa e sugestões com aceite humano (MAP-04/MAP-05)
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: `POST /boards/{id}/context-preview` monta o snapshot `notee-graph-context/1` com escopo `all` ou `selected`. Só entram notas elegíveis **do dono** (IDs alheios/arquivados são ignorados), arestas com as duas pontas na seleção, texto simples (até 2000 caracteres por nota, 50 notas), referências, perguntas e estado das dúvidas — respostas de IA não revisadas ficam fora. Um `contextHash` (SHA-256 de IDs+versões) identifica o snapshot.
`POST /boards/{id}/suggestions` gera propostas `Pending` com um gerador **local e determinístico** (tags, categoria e termos em comum; não chama provedor externo). Aceite (`/accept`) é individual, revalida dono, elegibilidade e **versões das notas** (mudou → `Stale`, 409) e cria a aresta na mesma transação; `/dismiss` descarta. Gerar nunca altera o grafo.
Pendente: "Perguntar com este contexto" a um provedor real exige definir onde a resposta mora (dúvida de várias notas) — fica para ADR futuro; o envio externo continuará sujeito ao consentimento (ADR-019).

### ADR-025 — Observabilidade e endurecimento básico (M14/M15)
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: OpenTelemetry (ASP.NET Core, HttpClient e fonte própria `Notee.Research`) com exportação OTLP **somente** se `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definido. Span `research.job.process` e métricas `notee.research.jobs.enqueued|finished` e `notee.research.job.duration` carregam só IDs, tentativa, provider e estado — nunca texto. `/health/live` (sem dependências) e `/health/ready` (banco). Problem Details sempre com `traceId`; erros 500 não expõem mensagem interna. Cabeçalhos `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` e CSP restritiva. Limite global por usuário/IP (`RateLimit:ApiPerMinute`, padrão 600/min) e cota de pesquisa por conta (`Research:MaxJobsPerHour`, padrão 60; acima disso a dúvida fica pendente e o enfileiramento manual responde 429). Swagger apenas em Development ou com `Swagger:Enabled=true`; `Properties/launchSettings.json` sobe local em Development na porta 5000.
Fora do escopo agora: SSRF não se aplica (a API não busca URLs de usuários); avaliação de qualidade de respostas com fontes depende de provider com citações (M13).

### ADR-026 — Publicação como snapshot revisado (M08)
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: módulo `Publishing` com `Publications` (slug único por autor entre ativas, `ShareToken` aleatório de 192 bits, visibilidade `Private|Unlisted|Public`, revogação), `PublicationRevisions` (cópia imutável de título+conteúdo de uma versão da nota) e `PublicProfiles` (handle único + nome público). Editar a nota não altera o publicado; o autor publica nova revisão explicitamente. Leitura anônima: `/public/notes/{handle}/{slug}` (só Public), `/shared/{token}` (Unlisted/Public), `/public/profiles/{handle}`, `/public/notes` (biblioteca). Privada só aparece na prévia do autor. Revogar e trocar o link valem imediatamente. Dúvidas, respostas de IA, referências e IDs internos não são publicados. Excluir a nota definitivamente remove as publicações (cascade). Sem comentários, curtidas ou seguidores (sem rede social, conforme escopo).
O front renderiza o conteúdo publicado como texto pelo React (sem HTML do autor), evitando XSS.

### ADR-027 — Análise assistida e precedência de pesquisa automática (M10)
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: `POST /notes/{id}/analysis-runs` executa um analisador **local e determinístico** (termos frequentes e tags já existentes citadas → tag; categoria existente citada → categoria; frases interrogativas → dúvida). Sugestões ficam `Pending` vinculadas à versão da nota; nova análise marca as de versões anteriores como `Stale` e não repete o que foi descartado. Aceite individual (`/suggestions/{id}/accept`) revalida a versão e aplica pelo mesmo caminho do PUT (nova versão + revisão); dúvida aceita recebe âncora real no bloco onde a frase está. Pesquisa automática: `UserPreferences.AutoResearch` (padrão ligado) e `NoteSettings.AutoResearch = inherit|on|off`; precedência pesquisa desligada > nota > conta. Mudar configurações nunca enfileira dúvidas antigas (sem backfill).

### ADR-003 (atualização) — Sessão com access token curto e refresh token rotativo (M04)
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: login devolve JWT de acesso (`Jwt:AccessMinutes`, padrão 30) e refresh token opaco (`Jwt:RefreshDays`, padrão 14) guardado só como hash SHA-256 em `RefreshSessions`. `POST /auth/refresh` rotaciona (o usado é marcado e substituído na mesma família); reutilizar um token já usado revoga **toda a família** (detecção de roubo). `POST /auth/logout` revoga a família e não revela se o token existia. Reset de senha revoga todas as sessões da conta. O token de redefinição só é registrado em log em Development (não há provedor de e-mail). O front renova automaticamente uma vez ao receber 401 e redireciona ao login se falhar.
Trade-off consciente: tokens ficam no `localStorage` (risco em caso de XSS); a mitigação atual é renderizar conteúdo como texto e a CSP da API. Migrar para cookie `HttpOnly` + proteção CSRF fica para revisão deste ADR.

### ADR-028 — Outbox transacional, SQS emulado e Inbox (M11/M16)
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: todo job criado grava `OutboxMessages` (`research.job.enqueued`) na mesma transação. `OutboxDispatcher` publica pendentes (`FOR UPDATE SKIP LOCKED`, até 10 tentativas). `Messaging:Provider=inprocess` (padrão) apenas acorda o worker; `sqs` publica no SQS do **LocalStack** (credenciais fictícias, endpoint local) e `SqsJobListener` consome com dedupe em `InboxMessages (Consumer, MessageId)`. O worker continua sendo a autoridade (lease/fencing na tabela), então mensagem perdida ou duplicada não perde nem duplica pesquisa. Infra em `infra/terraform/localstack`: fila com long polling, visibility 120 s e DLQ após 5 recebimentos.
Evidência (09/11/2026, local): Terraform aplicado no LocalStack 4.4 (2 recursos); dúvida concluída em ~1 s com polling configurado em 30 s (acordada pelo evento SQS); reenvio duplicado do mesmo evento ignorado pelo Inbox; fila drenada; 11 suítes HTTP verdes em modo SQS. O CI roda as suítes nos dois modos.
Fora do escopo: SES, RDS, ECS e ALB do M16 não foram provisionados; equivalência com AWS real não é afirmada.

### ADR-029 — Provider real com fontes e custo (M13)
Status: Proposto (implementado; **não validado contra a API real**) · Data: 2026-11-09
Decisão: `IAiService.AnswerAsync` devolve texto, fontes e uso de tokens. O Gemini é chamado com a ferramenta `google_search` (grounding); somente URIs http/https presentes em `groundingMetadata.groundingChunks` viram `ResearchSources` (sem duplicatas, até 10). Com fontes, `Evidence = Sourced`; sem fontes, `NoSource` ("Sem fonte"). Tokens (`promptTokenCount`, `candidatesTokenCount`) ficam no job. A chave vai no cabeçalho `x-goog-api-key` (antes ia na URL) e o corpo de erros do provider não é propagado. Modelo e URL base configuráveis (`Gemini:Model`, `Gemini:BaseUrl`). Termo de consentimento passou à versão 2 (inclui busca na web); consentimentos v1 deixam de valer.
Evidência: testes unitários com servidor HTTP falso e suíte `provider` de ponta a ponta contra um servidor local que imita o formato documentado. Nenhuma chamada real ao Gemini foi feita nesta sessão (exigiria enviar dados para fora); validar com a chave real é a próxima verificação manual recomendada.

### ADR-030 — Múltiplos provedores de IA e painel administrativo
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: adaptadores para Gemini (grounding com fontes), Claude (SDK oficial `Anthropic` 13.6.0, `claude-opus-5-5` por padrão, ferramenta `web_search_20260209` com fontes vindas dos resultados de busca; recusa do modelo vira falha do job), ChatGPT, Grok e DeepSeek (adaptador HTTP comum no formato chat completions, sem fontes — "Sem fonte"). O antigo `OpenAiService` (pacote `OpenAI`) foi removido. A IA ativa e o modelo por provedor ficam em `SystemSettings` (cache de 10 s); chaves continuam só na configuração e nunca passam pelo painel. Acesso administrativo: conta criada a partir de `Admin:Email`/`Admin:Password` com a claim `notee_role=admin`; só `POST /auth/admin/login` emite token com `Role=admin` (30 min, sem refresh, com limite de tentativas); `/admin/*` exige a política `Admin`. A rota "escondida" do front (`VITE_ADMIN_PATH`) é só conveniência — **não** é controle de segurança. Trocas registram quem e quando. O consentimento dos usuários continua obrigatório: o texto cita o provedor ativo; trocar de provedor **não** pede novo aceite (revisar se a política de privacidade exigir consentimento por provedor).
Evidência: testes unitários dos adaptadores; suíte `admin` de ponta a ponta contra um servidor local que imita OpenAI-compatível e a Messages API (as SDKs atuais enviam a chave como `Authorization: Bearer`). Nenhuma chamada real a OpenAI, Anthropic, xAI ou DeepSeek foi feita nesta sessão.

### ADR-031 — Chaves de IA cadastradas pelo painel
Status: Proposto (implementado) · Data: 2026-11-09
Decisão: o administrador pode cadastrar/remover a chave de um provedor **apenas quando não houver chave na configuração do servidor** (`appsettings.json`/ambiente), que sempre tem prioridade e nunca é alterável pelo painel (409). Chaves do painel ficam na tabela `AiProviderSecrets`, **criptografadas** com ASP.NET Data Protection (propósito `Notee.AiProviderKeys.v1`); o valor nunca é devolvido pela API nem registrado em log — a interface mostra só a origem (`config`, `panel`, `none`). Se as chaves do Data Protection se perderem, a chave cadastrada deixa de ser legível e o provedor aparece como sem chave (basta cadastrar de novo); por isso o compose persiste o volume `notee-dpkeys`.
Evidência: suíte `admin` (31 verificações) — 409 ao tentar sobrescrever/remover chave do servidor, cadastro e remoção pelo painel, valor nunca devolvido, DeepSeek respondendo com a chave do painel; no banco de teste o valor gravado começa com `CfDJ8...` (Data Protection) e não contém o texto original.

## ADR-033 — evolução social incremental (10/11/2026)

Decisão autorizada pelo usuário: evoluir o escopo anterior sem rede social para seguidores unilaterais, feed de publicações e reposts, comunidades públicas criadas pelos usuários, avaliações de utilidade e edição simultânea por convite. Somente o dono publica uma revisão explícita. As categorias pessoais permanecem independentes das comunidades.

A implementação desta execução cobre somente a fundação: seguidores, feed cronológico paginado, curtidas e reposts. O feed consulta apenas publicações públicas ativas e revisões publicadas; nunca usa o rascunho. Chaves compostas e operações idempotentes evitam duplicação de relações e reações.

Pendentes para concluir a primeira etapa: cursor de paginação, destaques, identificação dos autores dos reposts, interações na leitura pública, biografia e aba de reposts no perfil, bloqueio e denúncias. Comunidades, comentários, métricas, ranking combinado e colaboração em tempo real ainda não implementados. A arquitetura Yjs e seu serviço de sincronização permanecem propostas para a etapa de colaboração.
