# Fonte integral do PDF — páginas 21–24

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 21 — Modelo relacional: conta e notas

```text
DADOS E CONTRATOS


Modelo relacional: conta e notas
Schemas por módulo ajudam a visualizar propriedade. Começar com uma instância PostgreSQL e migrations
versionadas do EF Core. Schema JSON define o documento do editor; tabelas relacionais mantêm identidade,
relações e invariantes pesquisáveis.

   Tabela / responsabilidade     Campos e restrições relevantes

                                 Identity user, e-mail normalizado único, nome público opcional e timestamps. Não
   identity.users
                                 armazenar senha em claro.

                                 SessionId, UserId, família de refresh, hash do token, expiração, revogação e último
   identity.sessions
                                 uso.

                                 UserId único, AutoResearchDefault, AnalysisMode, bloqueio externo,
   identity.preferences
                                 consentimento e versão da política aceita.

                                 Id, OwnerId, Title, CategoryId opcional, ContentJson JSONB, PlainText,
   notes.notes
                                 SchemaVersion, Version BIGINT, Status e timestamps.

                                 Id, NoteId, OwnerId, NoteVersion, snapshot e motivo. UNIQUE(NoteId,
   notes.revisions
                                 NoteVersion). Imutável.

   notes.categories              Id, OwnerId, Name e NormalizedName. UNIQUE(OwnerId, NormalizedName).

                                 Taxonomia por dono; UNIQUE(NoteId, TagId). Impedir vínculos com tags de outro
   notes.tags / note_tags
                                 dono.

                                 NoteId único, AutoResearchOverride: Inherit/On/Off, ExternalBlock e versão da
   notes.settings
                                 preferência.



Índices guiados pelas consultas
Listagem privada: (OwnerId, Status, UpdatedAt DESC, Id). Revisões: (NoteId, NoteVersion DESC).
Categorias/tags: unicidade normalizada por OwnerId. Normalização de espaços e caixa deve ser definida; não
presumir que todas as collation rules sejam idênticas entre ambientes.
Version de concorrência e NoteRevision não são a mesma coisa: uma protege a escrita, a outra preserva
história. Gerar PlainText a partir do JSON validado no servidor para busca e contexto; não aceitar texto
extraído do navegador como representação autoritativa.

 Defesa em camadas
 Filtrar todas as operações privadas por dono. Onde for viável, usar FKs compostas com OwnerId para proteger
 associações. Testes devem provar que IDs de outra conta não permitem ler, associar, restaurar ou publicar dados.
```

## Página 22 — Modelo relacional: pesquisa e entrega

```text
DADOS E CONTRATOS


Modelo relacional: pesquisa e entrega
   Tabela                          Propósito e invariante

                                   OwnerId, NoteId, Anchor, Question, origem manual/aceita, status e versão.
   research.doubts
                                   Aceite é explícito.

   research.analysis_runs /        Revisão analisada, propostas e estado Accepted/Dismissed/Stale. Deduplicar
   suggestions                     por trecho/contexto.

                                   JobId, DoubtId, ContextRevisionId, estado, prioridade, configuração congelada,
   research.jobs
                                   lease, fencing token, cancelamento e timestamps.

                                   Tentativas e checkpoints com status, request remoto, erro normalizado e uso
   research.attempts / steps
                                   reportado. Não armazenar raciocínio interno do modelo.

   research.answers / sources /    Resposta imutável por execução, evidência, links e revisão humana. Fonte deve
   citations                       pertencer ao conjunto dessa pesquisa.

                                   Reserva atômica por usuário/período/job; consumo, liberação e eventual
   research.budget_reservations
                                   resultado financeiro desconhecido.

   publishing.publications /       NoteId, autor público, visibilidade, slug/token, estado e snapshot selecionado.
   revisions                       UNIQUE(OwnerId, Slug) para slugs públicos.

   infra.outbox_messages           EventId, tipo, SchemaVersion, payload mínimo, ocorrência, lease e publicação.

                                   UNIQUE(ConsumerName, EventId). Confirma o efeito local concluído, não só o
   infra.inbox_messages
                                   recebimento.

                                   OwnerId, Operation, KeyHash, RequestHash, status e resposta. UNIQUE no
   infra.idempotency_records
                                   escopo da operação.


Índices candidatos: jobs por (Status, NextAttemptAt), lease expirado, e (OwnerId, CreatedAt, Id); Outbox por
pendência/CreatedAt; respostas por DoubtId/CreatedAt; publicações por Visibility/PublishedAt/Id. Medir
planos reais antes de adicionar índices para todo filtro.
Usar migrações de schema e dados reversíveis quando possível, scripts revisados e seed apenas fictício. A
partir de API + Worker simultâneos, migrations passam a ser uma etapa controlada de deploy, não uma
corrida no startup de cada host.
```

## Página 23 — API: identidade, notas e revisões

```text
DADOS E CONTRATOS


API: identidade, notas e revisões
Contrato proposto REST /api/v1 com OpenAPI gerado do código. O cliente TypeScript pode ser gerado do
contrato quando o fluxo estabilizar; não manter DTOs divergentes manualmente como prática permanente.
Todos os endpoints privados derivam OwnerId da identidade autenticada.

   Grupo                Rotas propostas

   Autenticação         POST /auth/register, /auth/login, /auth/refresh, /auth/logout. GET /me.

                        POST /auth/forgot-password; POST /auth/reset-password. Resposta genérica na
   Recuperação
                        solicitação.

   Notas                GET /notes?query=&categoryId=&tag=&status=&cursor=&limit= ; POST /notes.

   Nota individual      GET /notes/{id}; PUT /notes/{id} com expectedVersion.

   Ciclo de vida        POST /notes/{id}/archive, /trash, /restore; DELETE /notes/{id} apenas quando Trashed.

                        GET /notes/{id}/revisions; GET /notes/{id}/revisions/{revisionId}; POST /notes/{id}/restore-
   Histórico
                        revision.

   Taxonomia            GET/POST /categories e /tags; PATCH/DELETE /categories/{id} e /tags/{id}.

   Preferências         GET/PATCH /me/preferences; GET/PATCH /notes/{id}/settings.

                        GET /health/live, /health/ready e /openapi/v1.json. Diagnóstico protegido por política de
   Operação
                        operador.



Respostas consistentes
201 + Location para recursos criados; 200/204 para comandos concluídos; 202 + Location para trabalho
assíncrono; 400/422 para validação conforme política documentada; 401 sem autenticação; 403 sem
permissão; 404 para recurso ausente ou privado de outra conta; 409 para conflito de versão ou reutilização
incompatível de chave; 429 para limite de requisições.
Erros usam Problem Details, com code estável e traceId seguro. Nunca devolver stack trace, chave, prompt ou
conteúdo de outro usuário. Validar paginação, tamanho de corpo e limites no servidor. Cursor inclui a
ordenação estável, mas não substitui o filtro de dono.
```

## Página 24 — API: dúvidas e publicações

```text
DADOS E CONTRATOS


API: dúvidas e publicações
   Operação                 Contrato proposto

                            POST /notes/{id}/doubts: revisionId, blockId, intervalo, exactQuote e question.
   Marcar dúvida
                            Idempotency-Key.

   Analisar texto           POST /notes/{id}/analysis-runs → 202; GET /analysis-runs/{id}.

                            POST /suggestions/{id}/accept ou /dismiss; revisão obsoleta exige reanálise, não
   Aceitar sugestão
                            aceite cego.

   Iniciar pesquisa         POST /doubts/{id}/research-jobs → 202 + Location. GET /research-jobs/{id}.

   Cancelar / tentar de     POST /research-jobs/{id}/cancel; POST /doubts/{id}/research-jobs com nova chave e
   novo                     motivo de reexecução.

                            GET /doubts/{id}/answers; POST /answers/{id}/review. Revisão não reescreve o
   Revisar resposta
                            resultado.

   Publicar                 POST /notes/{id}/publications: revisionId, visibility, slug e includedAnswerIds.

   Atualizar / retirar      POST /publications/{id}/revisions; DELETE /publications/{id} revoga acesso público.

                            GET /public/notes/{handle}/{slug}; GET /shared/{token}; GET
   Leitura pública
                            /public/profiles/{handle}; GET /public/notes.


  POST /api/v1/doubts/{id}/research-jobs
  Idempotency-Key: <chave-da-intencao-do-cliente>

  202 Accepted
  Location: /api/v1/research-jobs/<jobId>
  { "jobId": "...", "status": "queued" }

A semântica é importante: a API confirma que o trabalho foi aceito de forma durável, não que a resposta já
existe. O cliente consulta o recurso de status e depois a resposta. Campos operacionais do provider não
devem vazar no contrato de tela.

Diferenciar validação e indisponibilidade
Pergunta ambígua fica em NeedsContext antes do job; capacidade não suportada é erro claro de
configuração. Quota de fornecedor e indisponibilidade não viram “Sem fonte”. A interface deve oferecer
refinar, tentar novamente ou aguardar conforme a causa.
```
