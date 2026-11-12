# API REST — contrato de referência para implementação

**Base:** PDF pp. 23–24; endpoints de mapas são **propostas complementares** (não expostos pelo HTML).

Prefixo: `/api/v1`; autenticação obrigatória em recursos privados; documentação OpenAPI gerada do código. `OwnerId` sempre derivado da identidade autenticada.

## Contratos do plano original

| Módulo | Endpoints |
|---|---|
| Identidade | `POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`; `GET /me` |
| Reset | `POST /auth/forgot-password`, `POST /auth/reset-password` |
| Notas | `GET /notes?query=&categoryId=&tag=&status=&cursor=&limit=`, `POST /notes`, `GET /notes/{id}`, `PUT /notes/{id}` com `expectedVersion` |
| Ciclo de vida | `POST /notes/{id}/archive`, `/trash`, `/restore`; `DELETE /notes/{id}` apenas na lixeira |
| Revisões | `GET /notes/{id}/revisions`, `GET /notes/{id}/revisions/{revisionId}`, `POST /notes/{id}/restore-revision` |
| Categorias/tags | `GET/POST /categories`, `GET/POST /tags`, `PATCH/DELETE /categories/{id}`, `PATCH/DELETE /tags/{id}` |
| Preferências | `GET/PATCH /me/preferences`, `GET/PATCH /notes/{id}/settings` |
| Dúvidas | `POST /notes/{id}/doubts`; `POST /notes/{id}/analysis-runs`; `GET /analysis-runs/{id}`; `POST /suggestions/{id}/accept` e `/dismiss` |
| Pesquisa | `POST /doubts/{id}/research-jobs`, `GET /research-jobs/{id}`, `POST /research-jobs/{id}/cancel`, `GET /doubts/{id}/answers`, `POST /answers/{id}/review` |
| Publicação | `POST /notes/{id}/publications`, `POST /publications/{id}/revisions`, `DELETE /publications/{id}`; leitura `/public/notes/{handle}/{slug}`, `/shared/{token}`, `/public/profiles/{handle}`, `/public/notes` |
| Operação | `GET /health/live`, `/health/ready`, `/openapi/v1.json`; diagnósticos protegidos |

### Job assíncrono

```http
POST /api/v1/doubts/{id}/research-jobs
Idempotency-Key: <chave-da-intencao>

HTTP/1.1 202 Accepted
Location: /api/v1/research-jobs/{jobId}
Content-Type: application/json

{"jobId":"...","status":"queued"}
```

`202` confirma que o job foi aceito/persistido; não significa pesquisa concluída.

## Contratos **propostos** para Mapas de Ideias

| Operação | Método e rota sugeridos | Semântica |
|---|---|---|
| Catálogo | `GET /boards?cursor=&limit=` | somente mapas do owner |
| Criar | `POST /boards` | título/descrição; 201 + Location |
| Consultar | `GET /boards/{boardId}` | metadados, versão, placements e edges; conteúdo de notas via contrato agregado autorizado |
| Editar metadados | `PATCH /boards/{boardId}` | `expectedVersion`; 409 se obsoleto |
| Excluir mapa | `DELETE /boards/{boardId}` | remove apenas mapa, placements, edges e propostas; **não** apaga notas |
| Posicionar | `PUT /boards/{boardId}/placements/{noteId}` | `{x,y,expectedVersion}`; 200 com nova versão |
| Adicionar nó existente | `POST /boards/{boardId}/placements` | **somente se ADR permitir membership selecionável**; na versão da demo, placements são automáticos |
| Criar conexão | `POST /boards/{boardId}/edges` | `{fromNoteId,toNoteId,type,label}`; 201 |
| Editar/remover conexão | `PATCH /boards/{boardId}/edges/{edgeId}`, `DELETE .../edges/{edgeId}` | versões e autorização |
| Referências da nota | `GET/POST /notes/{noteId}/references`, `DELETE /notes/{noteId}/references/{referenceId}` | livro/fonte como metadado, não evidência verificada |
| Dúvida livre do mapa | `POST /notes/{noteId}/doubts` com `origin: "Map"`, `anchor: null` | extensibilidade de RF04 a registrar |
| Prévia contextual | `POST /boards/{boardId}/context-preview` | `scope: all/selected`, `noteIds` e limite; devolve snapshot/sumário autorizado |
| Perguntar com contexto | `POST /boards/{boardId}/questions` | 202 para job durável ou contrato de pesquisa já existente; sem execução no HTML |
| Gerar sugestões | `POST /boards/{boardId}/suggestions` | versão do contexto e sugestões **pendentes** |
| Decidir sugestão | `POST /boards/{boardId}/suggestions/{id}/accept|dismiss` | revalidar versões + permissão; aplicar somente no aceite |

**Escolhas não fechadas:** contrato exato de `questions`/job, limites e quotas por mapa, normalização de URL/referências, política de membros. Antes de fixar DTOs/migrations, registrar proposta e testes em ADR.

## Respostas/erros

- `201 + Location` criado; `200/204` comando concluído; `202` job aceito.
- `400/422` validação (política consistente), `401` não autenticado, `403` sem privilégio global, `404` inexistente **ou recurso privado de outro dono**, `409` conflito/Idempotency-Key incompatível, `429` cota.
- Problem Details com `code` estável, `traceId` sem dados secretos; nunca stack trace, prompts, tokens, corpo de outra conta.
- Paginação por cursor com ordem estável; validação no servidor; ETags/versions e transação nos comandos concorrentes.

## Atualização 12/11/2026 — bloco de gráfico no conteúdo da nota (ADR-036)

`POST /notes` e `PUT /notes/{id}` aceitam, dentro do array JSON de `content`, blocos:

```json
{ "id": "c1", "type": "chart", "schemaVersion": 1, "chartType": "line",
  "title": "Inflação anual", "description": "IPCA por ano", "xAxisLabel": "Ano", "yAxisLabel": "Variação", "unit": "%",
  "labels": ["2019", "2020", "2021"],
  "series": [{ "name": "IPCA", "values": [4.31, null, 10.06] }],
  "sources": [{ "label": "IBGE", "url": "https://www.ibge.gov.br" }] }
```

- `chartType`, `labels` e `series` obrigatórios; demais textos opcionais. `null` = sem dado.
- Limites e regras no ADR-036. Conteúdo inválido → `400` Problem Details (“Regra de negócio violada”, com o motivo em `detail`) e a nota não é alterada.
- Revisões e publicações copiam o conteúdo como antes; o snapshot publicado mantém os dados da versão publicada.

## Atualização 12/11/2026 — organizar com IA (ADR-037)

- `POST /notes/{id}/organize-ai/estimate` `{ text }` → `200 { provider, providerName, external, consentGiven, externalConfigured, estimatedInputTokens, estimatedOutputTokens, estimatedCostUsd|null, maxPerHour }`. Não envia nada.
- `POST /notes/{id}/organize-ai` `{ text, confirm: true }` → `200 { provider, external, title|null, blocks: [{ type: heading|paragraph|list|quote|code|table|chart, text?, level?, ref? }], inputTokens, outputTokens, costUsd|null }`. Gráficos existentes vão no texto como `[[grafico:N]]` e voltam como `{type:"chart", ref:N}`. A nota não é alterada.
- Erros: `400` sem `confirm`, texto vazio, > 50.000 caracteres ou nota na lixeira; `404` nota alheia; `422` resposta que omite/inventa conteúdo (Problem Details com tokens e custo); `429` cota; `502` falha do provedor.
- Bloco `heading` aceita `level` opcional (1–3) no conteúdo da nota.
