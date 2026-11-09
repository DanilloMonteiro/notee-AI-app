# Entidades, dados relacionais e invariantes

**Base:** PDF pp. 11–12, 21–22; modelo de mapas **proposto** a partir do HTML/README v1.1/v1.2.

## Domínios consolidados no PDF

| Tabela | Responsabilidade/campos relevantes |
|---|---|
| `identity.users` | ID, e-mail único normalizado, nome público opcional, timestamps; Identity armazena hash, nunca senha em claro |
| `identity.sessions` | ID, user ID, família de refresh, hash de token, expiração, revogação e uso |
| `identity.preferences` | User ID único, padrão de pesquisa, modo de análise, bloqueio externo, consentimento/versionamento |
| `notes.notes` | ID, OwnerId, título, CategoryId?, `ContentJson` JSONB, `PlainText` extraído no servidor, SchemaVersion, Version BIGINT, Status, timestamps |
| `notes.revisions` | ID, NoteId, OwnerId, NoteVersion, snapshot imutável, motivo; UNIQUE(NoteId,NoteVersion) |
| `notes.categories` | ID, OwnerId, Name, NormalizedName; UNIQUE(owner,nome normalizado) |
| `notes.tags`, `notes.note_tags` | tags por owner, N:N com FK segura e UNIQUE(NoteId,TagId) |
| `notes.settings` | NoteId único, `AutoResearchOverride = Inherit / On / Off`, bloqueio externo, versão |
| `research.doubts` | OwnerId, NoteId, âncora (opcional apenas na extensão mapa), pergunta, origem, status e versão |
| `research.analysis_runs`, `suggestions` | NoteRevisionId, provider, propostas com Accepted/Dismissed/Stale; dedup por contexto |
| `research.jobs` | DoubtId, ContextRevisionId, status, lease/fencing/cancelamento, snapshots e timestamps |
| `research.attempts`, `steps` | checkpoints, ID remoto, erro normalizado, custo/uso reportado |
| `research.answers`, `sources`, `citations` | resposta imutável, evidência, claims, fontes obtidas, revisão humana |
| `research.budget_reservations` | reservas atômicas e consumo por conta/período/job |
| `publishing.publications`, `revisions` | snapshot, visibilidade, slug/token, estado, autor, publicação revisável |
| `infra.outbox_messages`, `inbox_messages` | evento durável e UNIQUE(ConsumerName,EventId) |
| `infra.idempotency_records` | OwnerId, Operation, KeyHash, RequestHash, status/resposta; UNIQUE no escopo |

## Extensão proposta: KnowledgeMaps

```text
knowledge_maps.boards
  id UUID PK
  owner_id UUID NOT NULL
  title VARCHAR(80) NOT NULL
  description VARCHAR(240) NULL
  version BIGINT NOT NULL
  created_at, updated_at TIMESTAMPTZ NOT NULL

knowledge_maps.board_placements
  board_id UUID NOT NULL
  note_id UUID NOT NULL
  x DOUBLE PRECISION NOT NULL
  y DOUBLE PRECISION NOT NULL
  position_version BIGINT NOT NULL
  PRIMARY KEY(board_id, note_id)

knowledge_maps.board_edges
  id UUID PK
  board_id UUID NOT NULL
  from_note_id UUID NOT NULL
  to_note_id UUID NOT NULL
  relation_type VARCHAR(32) NOT NULL
  label VARCHAR(80) NULL
  version BIGINT NOT NULL
  created_at, updated_at TIMESTAMPTZ NOT NULL

notes.note_references  # PROPOSTO: pertence à nota, não ao mapa
  id UUID PK
  owner_id UUID NOT NULL
  note_id UUID NOT NULL
  kind ENUM(Book,Source)
  title VARCHAR(220) NOT NULL
  author VARCHAR(140) NULL
  publisher VARCHAR(140) NULL
  publication_year VARCHAR(15) NULL
  url TEXT NULL
  created_at TIMESTAMPTZ NOT NULL

knowledge_maps.graph_suggestions  # opcional, se sugestões persistidas
  id, owner_id, board_id, context_version/hash, kind, payload_json,
  status(Pending,Accepted,Dismissed,Stale), created_at, decided_at
```

### Invariantes propostas para mapas

1. `board.owner_id` é definido pela sessão, nunca do payload público. Toda placement/edge pertence a board desse owner.
2. `note_id`, `from_note_id`, `to_note_id` devem existir, ter **mesmo owner** e status elegível; validar `board_id` na mesma transação. `CHECK(from_note_id <> to_note_id)` (sem auto-conexão, proposta).
3. Coordenadas finitas e dentro de limites documentados; prevenir NaN/Infinity e payloads enormes.
4. Uma nota tem **uma** posição por board; pode ter posições distintas em vários boards. Refs e dúvidas pertencem à nota, não se duplicam por board.
5. `board_edges` referenciam dois nós válidos neste board. Definir política de duplicatas por (`board`,`from`,`to`,`type`,`label`) e validar também no banco se a regra for adotada.
6. Arquivar/lixeira: filtrar nós na exibição/geração de contexto. Na exclusão definitiva, remover placements e arestas vinculadas de modo idempotente; não ressuscitar referências.
7. Edição concorrente de aresta/movimento: `expectedVersion`/ETag ou versão própria; refazer a operação se estiver obsoleta.
8. Atualizar posicionamento ou aresta não cria nova `NoteRevision`. Editar referências da nota, sim, pertence ao histórico da nota conforme política.

### Conflito de regra sobre membros

O protótipo v1.2 chama `ensureGraphPlacements` e inclui **todas as notas ativas em cada mapa**, com posições/arestas particulares. Uma evolução para **selecionar apenas parte das notas** é plausível, mas **não é decisão aprovada**. Implementar primeiro o comportamento da demo ou registrar ADR antes de alterar a regra de membership.

## Índices candidatos

- `notes.notes(owner_id,status,updated_at DESC,id)`; `notes.revisions(note_id,note_version DESC)`.
- `research.jobs(status,next_attempt_at)`, index para lease expirado; Outbox pendente.
- `knowledge_maps.boards(owner_id,updated_at DESC,id)`; `board_placements(board_id,note_id)` único; `board_edges(board_id,from_note_id)` e `(board_id,to_note_id)`.
- Não criar índices adicionais sem plano de consultas/`EXPLAIN ANALYZE`.

**Lembrete:** esses DDLs de mapas são hipóteses de engenharia, não migrations já existentes.
