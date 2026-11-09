# Mapas de Ideias — contexto estruturado, perguntas e propostas de IA

**Status:** contrato demonstrado no HTML para prévia local; serviços e persistência aqui descritos são **propostas de backend**. Complementa `MAPAS_DE_IDEIAS.md` e `08_PESQUISA_IA_E_FONTES.md`.

## 1. Pergunta central

A função de IA entende relações entre notas selecionadas para ajudar o usuário a **investigar, comparar e conectar ideias**, mantendo rastreabilidade, limites e controle de escopo. Ela não é autorizada a concluir que uma ligação estabelecida pelo usuário é um fato confirmado.

## 2. Escopo selecionável

- **`all` — Mapa inteiro:** incluir notas ativas elegíveis do board e arestas entre elas.
- **`manual` — Selecionar subgrafo:** usuário clica em notas para marcar/desmarcar; incluir só `selectedNoteIds` válidos e **somente arestas cujas duas pontas estão selecionadas**.
- Zero notas => não habilitar `Perguntar com este contexto`.
- Mostrar contagens de notas/relações, prévia JSON, tamanho/limite estimado e dados que serão enviados.
- A seleção é controle de **escopo**, não de **consentimento externo**.

## 3. Schema de prévia **observado** no HTML

O HTML usa `schema: "notee-graph-context/1"`, `{board:{id,title}, scope, noteCount, edgeCount,nodes,edges}`. Cada nó contém `id`, `revision`, `title`, `category`, `tags`, `content`, `references.books/sources`, `doubts`; uma dúvida inclui `id`, `question`, `status`, `evidence`, `reviewed`; apenas resposta concluída **e revisada** aparece como `reviewedAnswer` com `sources`.

Exemplo ilustrativo, com dados fictícios:

```json
{
  "schema": "notee-graph-context/1",
  "board": { "id": "board-a", "title": "Meus estudos" },
  "scope": "manual",
  "noteCount": 2,
  "edgeCount": 1,
  "nodes": [
    {
      "id": "note-a",
      "revision": 3,
      "title": "Tema A",
      "category": "Estudos",
      "tags": ["conceito"],
      "content": "Anotação do autor sobre o tema A.",
      "references": {
        "books": [{"title":"Livro de exemplo","author":"Autora fictícia","year":"2024","url":""}],
        "sources": []
      },
      "doubts": [{"id":"doubt-a","question":"O que ainda falta verificar?","status":"pending","evidence":"pending","reviewed":false}]
    },
    {
      "id": "note-b",
      "revision": 2,
      "title": "Tema B",
      "category": "Estudos",
      "tags": [],
      "content": "Anotação do autor sobre o tema B.",
      "references": {"books":[],"sources":[]},
      "doubts": []
    }
  ],
  "edges": [
    {"id":"edge-ab","from":"note-a","to":"note-b","type":"related","label":null,"displayLabel":"Relacionado a"}
  ]
}
```

**Distinção importante:** o schema acima documenta a **prévia local**; não é autorização para mandar o `content` completo ou todas as dúvidas a um provider. Para backend real, o usuário deve escolher o recorte e a API aplicar limites e política de minimização.

## 4. Como construir contexto de verdade no backend

1. Autenticar e derivar `OwnerId`; verificar `BoardId` e eventual `selectedNoteIds` integralmente no servidor.
2. Carregar apenas notas **ativas** autorizadas do board; resolver `NoteRevisionId` para cada nota, `BoardVersion` e `EdgeVersion`/snapshot hash.
3. Filtrar arestas (`from` **e** `to` no conjunto de nós selecionados); preservar direção, `type` e `label` separado, sem tomar label como evidência.
4. Aplicar allowlist de campos e minimização: título, trechos necessários, tags/categoria úteis, perguntas pertinentes; referências bibliográficas são metadados não verificados.
5. Incluir `reviewedAnswer` apenas sob regra de revisão e permissão explícita; nunca incluir raciocínio interno de modelo, prompt secreto ou respostas descartadas.
6. Contar tokens/bytes estimados e recusar/solicitar recorte se exceder teto; não truncar silenciosamente removendo nota sem avisar.
7. Montar `ContextSnapshot` imutável versionado com `schemaVersion`, `policyVersion`, `boardVersion`, revisões, hash, data e expiração; **revalidar consentimento antes da chamada externa**.
8. Quando pergunta dispara pesquisa externa, criar ResearchJob + Outbox na mesma transação; provider recebe apenas o payload necessário e autorizado.
9. Guardar status, fontes e versão de origem; resposta desatualizada não sobrescreve novas notas/arestas.
10. Em revogação, cancelamento ou exclusão, interromper novas chamadas e impedir vazamento de respostas tardias.

## 5. Contrato **proposto** de prévia e pergunta

```http
POST /api/v1/boards/{boardId}/context-preview
Content-Type: application/json

{"scope":"selected","noteIds":["note-a","note-b"]}

HTTP/1.1 200 OK
{"schema":"notee-graph-context/1","board":{},"scope":"manual","nodes":[],"edges":[],"limits":{},"snapshotVersion":"..."}
```

```http
POST /api/v1/boards/{boardId}/questions
Idempotency-Key: <intencao>
Content-Type: application/json

{
  "question":"Como estas duas notas se relacionam?",
  "scope":"selected",
  "noteIds":["note-a","note-b"],
  "externalConsentVersion":"..."
}

HTTP/1.1 202 Accepted
Location: /api/v1/research-jobs/{jobId}
{"jobId":"...","status":"queued"}
```

O endpoint da pergunta pode se apoiar na entidade ResearchJob existente ou em subtipo de pergunta contextual; decidir com ADR. Não criar segundo mecanismo de retry/outbox/custos. Campos exatos e limits são **propostos**, não DTO implementado.

## 6. Propostas de IA para nós e conexões

No HTML `Gerar sugestões simuladas` propõe:

- `kind: note`: criar nota temática e vinculá-la a nó existente.
- `kind: edge`: conectar dois nós existentes com tipo indicado.

Em aplicação real, cada sugestão deve ter contrato de comando verificável:

```json
{
  "id": "suggestion-1",
  "boardId": "board-a",
  "kind": "edge",
  "title": "Possível relação",
  "detail": "Hipótese a verificar; não é afirmação factual.",
  "contextHash": "sha256:...",
  "originRevisions": {"note-a":3,"note-b":2},
  "payload": {"from":"note-a","to":"note-b","type":"related","label":""},
  "status": "Pending"
}
```

**Fluxo:** gerada ⇒ visualização `Pendente` ⇒ autor escolhe `Aceitar` ou `Descartar` ⇒ servidor revalida ownership, status e revisões ⇒ comando atômico cria Note/Edge **uma única vez** ⇒ marca proposta como `Accepted`. Se contexto obsoleto, estado `Stale` e nova revisão antes de aceitar. Descarte não cria efeitos.

- Aceitar nova nota **usa o mesmo caso de uso de Notes** e mantém nota disponível em `Minhas notas` e nos mapas segundo a regra v1.2.
- Aceitar edge valida duas pontas e `boardId`; rejeita proposta inválida/incompatível, sem aplicar o restante silenciosamente.
- Se a IA propôs diversas mudanças, exibir cada uma separada, com opção de aceitar individualmente. Um “aceitar todas” futuro exige confirmação explícita com prévia completa.
- Não converter sugestão de relação em citação/evidência científica; para isso disparar pesquisa com fontes reais.

## 7. Segurança específica

- Responder à pergunta sobre `all` não dá acesso às notas fora do board ou de outra conta.
- A prévia pode exibir dados privados **somente ao dono autenticado**, e não pode ser acessível em URLs compartilhadas.
- Contexto selecionado não revoga bloqueio global de IA, bloqueio por nota, limite de orçamento ou exigência de consentimento.
- Paginas web ou LLM não podem injetar comandos `create edge`, `publish`, `delete note`; respostas são dados, propostas são DTOs verificados e comandos passam por Application.
- Só usar `Com fontes/Evidência parcial/Sem fonte` quando houver execução real de busca e suporte verificável. A resposta a um mapa com apenas opiniões/hipóteses precisa indicar que não se verificou fonte externa.

## 8. Casos de teste de contexto e aceite

| ID | Teste | Esperado |
|---|---|---|
| CTX-01 | `scope=all` | todas as notas ativas autorizadas e suas edges internas |
| CTX-02 | `scope=manual`, A e B, edge A→B | 2 nós + edge A→B; nenhuma outra edge |
| CTX-03 | seleção A somente, edge A→B | 1 nó e **0 edges** |
| CTX-04 | seleção vazia | não envia job; erro amigável |
| CTX-05 | noteId de outra conta injetado | 404/validação segura, **nenhum** dado ou preview vazado |
| CTX-06 | nota arquivada/lixeira | não aparece no contexto novo |
| CTX-07 | nota muda após preview | job usa snapshot confirmado ou recusa com conflito; sem troca silenciosa |
| CTX-08 | consentimento ausente | prévia local permitida conforme owner; **nenhuma** chamada externa |
| CTX-09 | resposta sem fonte verificável | rotular `Sem fonte`, nunca simular citação |
| CTX-10 | sugestão descartada | nenhum Note/Edge criado |
| CTX-11 | aceite duplicado/concorrrente | uma única nota/edge, resultado idempotente |
| CTX-12 | aceitação após apagar origem | `Stale`/`409`, nenhum vínculo órfão |
| CTX-13 | livro cadastrado | metadado aparece, mas não vira fonte verificada de Answer |
| CTX-14 | resposta de pesquisa não revisada | `reviewedAnswer` ausente do JSON, conforme HTML |
| CTX-15 | revogação durante Worker | bloquear chamada futura e impedir aplicação de resposta tardia |

## 9. Diferença entre demo e sistema real

No HTML, `graphContextForBoard()` monta o JSON no navegador e `graphAskModal()` apenas simula a pergunta. `graphGenerateSuggestions()` cria duas propostas determinísticas; `Aceitar` altera dados de `localStorage`. **Nenhum** provider externo é chamado. No backend real, usar APIs, persistência, autorização e worker durável; não copiar esse JS como implementação de segurança.
