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
