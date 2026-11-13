# Plano — Sugerir categoria/tags e comentários em trechos (privados e públicos)

**Status:** implementação local das três etapas em 13/11/2026; validação automatizada e verificação de interface parcial descritas em `ESTADO_DO_PROJETO.md`.
**Data:** 13/11/2026.

## Contexto

O usuário quer duas frentes novas no Notee:

1. **Botão “Sugerir categoria e tags”.** Usa as tags e categorias existentes e, quando nada se encaixa, propõe criar novas. Pode usar IA.
2. **Comentários em trechos do texto.**
   - Selecionar um trecho e comentar.
   - Outras pessoas veem, curtem e comentam (na nota ou no mesmo trecho).
   - Com muitos comentários, aparecem só os mais curtidos.
   - No editor, alternar entre a visão **Dúvidas** (fundo azul) e a visão **Comentários** (fundo amarelo).

**O que já existe (verificado)**
- `NoteAnalysisEndpoints` (análise local) sugere tags, inclusive novas a partir de termos frequentes, e **apenas categorias existentes**, sem IA. UI em `components/NoteSuggestions.jsx`.
- `PublicationComment` (`Modules/Social/CommunityEndpoints.cs`) tem comentários na publicação inteira, com 1 nível de resposta, sem trecho, sem curtidas em comentários e ordenados por data. Só existe em publicações **Públicas**, exige perfil público. UI em `Discussion` (`features/social/Communities.jsx`).
- Âncoras de dúvida: `DoubtAnchor(BlockId, ExactQuote, StartIndex, EndIndex)` e destaque em `buildHighlightedHtml` (`pages/Note.jsx`). O painel lateral só tem dúvidas.
- Outras pessoas só veem notas por publicação (snapshot `PublicationRevision`). Não há convite nem sistema de avisos (o sino é visual).
- A infraestrutura de IA do “Organizar com IA” serve de base: `IAiService.CompleteAsync`, `AiProviderSelector`, `AiPricing`, cota, padrão estimativa → confirmação → prévia.

**Decisões do usuário**

| Tema | Decisão |
| --- | --- |
| Onde comentar | Na **nota publicada**. |
| Visibilidade | Comentários **privados** (só o autor) e **públicos**. |
| Quem participa | Comentar, curtir e ver comentários **só em publicações Públicas** (o link não listado continua só leitura, sem comentários). |
| Avaliação | **Curtidas**, uma por pessoa. Com muitos comentários, os mais curtidos aparecem primeiro. |
| Sugestões | Regras locais grátis, com **IA opcional** (custo e confirmação). |
| Abas do editor | **Dúvidas \| Comentários**, exclusivas. |
| Nova versão publicada | Os comentários **seguem o trecho**. Se o trecho sumir, vão para “Comentários sobre versões anteriores”. |
| Extras | Autor oculta comentários, desliga comentários por publicação, avisos de novos comentários e respostas, respostas em 1 nível. |

**Entrega em 3 etapas, um commit cada:**
1. Sugestões de categoria e tags.
2. Comentários privados e abas no editor.
3. Comentários públicos com trechos, curtidas, destaque, moderação e avisos.

## Etapa 1 — Sugerir categoria e tags

**Front: `features/notes/suggestTaxonomy.js` (novo, puro, testado com `node --test`)**
- `suggestTaxonomy({ title, blocks, categories, tags, currentCategoryId, currentTagIds })` → `{ category, tags[] }`. Cada item traz `{ name, existingId?, isNew, reason, score }`.
- **Existentes:** nome (sem acento, sem diferenciar maiúsculas, palavra inteira) que aparece no título, nos títulos de seção (peso maior) ou no corpo.
- **Novas tags:** termos e bigramas frequentes, com no mínimo 5 letras e 2 ocorrências, sem stopwords e que não sejam uma tag existente.
- **Nova categoria:** só quando nenhuma existente combina. Usa o termo dominante dos títulos de seção, com o motivo “nenhuma categoria existente combina”.
- Até 5 tags. Ignora as já aplicadas.

**UI**
- Botão **“Sugerir categoria e tags”** em `components/NoteOrganizer.jsx` (barra de categoria e tags). Abre um painel com chips marcados “existente” ou “nova”, cada um com o motivo, e as ações **Aplicar**, **Aplicar todas** e **Ignorar**.
- Aplicar uma existente só altera o estado do editor (`categoryId` e `tagIds`).
- Aplicar uma nova cria a tag ou categoria pelo endpoint que já existe (`taxonomyApi`, em `notes.js`) e então seleciona. Nada é salvo na nota até “Salvar”.
- Botão **“Sugerir com IA”** no mesmo painel.

**Backend IA**
- `POST /notes/{id}/taxonomy-ai/estimate` e `POST /notes/{id}/taxonomy-ai` (`confirm:true`). Envia o título, o texto (até 12.000 caracteres) e os nomes das categorias e tags existentes.
- A resposta é JSON `{category:{name, reason}, tags:[{name, reason}]}`.
- O servidor normaliza:
  - nome igual a um existente → `existingId`;
  - senão → `isNew`;
  - limites de nome iguais a `Tag`/`Category`;
  - até 5 tags;
  - descarta nomes vazios ou repetidos.
- Reaproveita `CompleteAsync`, consentimento (ADR-019), `AiPricing` e a cota (`Ai:TaxonomyMaxPerHour`, padrão 20).
- O Fake aplica as mesmas regras locais no servidor.
- **Nada é aplicado no servidor.**
- Arquivos: `Modules/Notes/Application/TaxonomyAiEndpoints.cs` e `TaxonomyAi.cs`, reaproveitando o padrão de `OrganizeAiEndpoints.cs` e `NoteAiOrganizer.cs`.

## Etapa 2 — Comentários privados e abas no editor

**Modelo (migration `AddNoteComments`)**
- `NoteComments`:
  - campos: `Id`, `NoteId` (cascade), `OwnerId`, `BlockId`, `ExactQuote` (≤ 500), `StartIndex`, `EndIndex`, `Text` (1–2000), `CreatedAt`, `UpdatedAt`;
  - índice `(NoteId, OwnerId)`;
  - só o dono da nota vê.

**API (dono)**
- `GET /notes/{id}/comments`
- `POST /notes/{id}/comments` (`{blockId, exactQuote, startIndex, endIndex, text}`)
- `PATCH /note-comments/{id}`
- `DELETE /note-comments/{id}`
- Nota na lixeira não aceita comentários novos.

**Editor (`pages/Note.jsx`)**
- O painel lateral ganha abas **Dúvidas | Comentários**. Escolher a aba é estado local, guardado em `localStorage`.
- `buildHighlightedHtml` é generalizado para receber intervalos com classe: `doubt-mark` (azul) **ou** `comment-mark` (amarelo, com `data-count` exibido por CSS `::after`, fora do `innerText`). Só os destaques da aba ativa são pintados.
- O botão da barra muda conforme a aba: “Marcar dúvida no texto” ou **“Comentar trecho”**.
  - “Comentar trecho” abre um modal com o texto e **Visibilidade: Só eu | Público na publicação**.
  - “Público” só fica habilitado se a nota tiver publicação Pública ativa com comentários ligados. Senão, mostra o motivo.
- Clicar num trecho comentado seleciona o fio correspondente no painel.
- A aba Comentários lista os fios agrupados por trecho, com os privados marcados com um cadeado.
- **Trechos movidos ou apagados:** novo `features/notes/anchors.js` com `locateQuote(blocks, anchor)`, que procura o trecho no bloco de mesmo id, perto de `startIndex`, e depois nos outros blocos. Vale para comentários e dúvidas.
  - Comentário sem trecho encontrado vai para a seção “Trecho alterado” (mesmo padrão do aviso das dúvidas).

## Etapa 3 — Comentários públicos com trechos, curtidas, destaque, moderação e avisos

**Modelo (migration `AddAnchoredPublicCommentsLikesNotifications`)**
- `PublicationComments` ganha `AnchorBlockId?`, `AnchorQuote?`, `AnchorStart?`, `AnchorEnd?` (nulos = comentário geral), `IsHidden`, `HiddenAt`. A resposta herda a âncora do pai.
- `Publications.CommentsEnabled` (padrão `true`).
- `PublicationCommentLikes (CommentId, UserId, CreatedAt)` com chave composta, idempotente. Não se curte o próprio comentário.
- `Notifications`: `Id`, `UserId`, `Kind` (`comment` | `reply`), `PublicationId`, `CommentId`, `ActorUserId`, `CreatedAt`, `ReadAt`, com índice `(UserId, ReadAt, CreatedAt)`. Criadas na mesma transação do comentário:
  - para o autor da publicação, se quem comentou for outra pessoa;
  - para o autor do comentário pai, numa resposta.

**API**
- `POST /discussion/{pubId}`: aceita `anchor {blockId, exactQuote, startIndex}`.
  - O servidor valida que o trecho existe naquele bloco da **revisão atual** publicada e grava as posições.
  - Respostas em 1 nível, como hoje.
  - Bloqueado com `CommentsEnabled=false` (409) ou publicação não Pública (404).
- `GET /discussion/{pubId}?view=top|all`:
  - devolve comentários com âncora, `likeCount`, `likedByMe`, respostas e `hidden` (só o autor da publicação vê os ocultos);
  - **`top`**: por trecho, os 3 mais curtidos (desempate: mais recente) mais `moreCount`; nos gerais, os 5 mais curtidos quando houver mais de 10;
  - **`all`**: tudo, paginado.
- `PUT` e `DELETE /discussion/comments/{id}/like`.
- `POST /discussion/comments/{id}/hide|unhide`: só o autor da publicação. Comentário oculto some para os outros e as respostas continuam.
- `PATCH /publications/{id}` aceita `commentsEnabled`.
- `GET /notes/{id}/public-comments`: só o dono. Reúne os comentários das publicações Públicas da nota para a aba Comentários do editor, que os posiciona no texto atual com `locateQuote`.
- `GET /me/notifications` (não lidas mais as últimas 20) e `POST /me/notifications/read` (`{ids?}` ou todas).

**Página pública (`features/publishing/PublicPages.jsx`, `features/social/Communities.jsx`)**
- O componente `Block` passa a renderizar segmentos de texto com `<mark className="comment-mark">` (sem HTML), a partir dos trechos comentados que a API devolve.
- Selecionar texto mostra um botão flutuante **“Comentar trecho”**, apenas para quem está logado com perfil público e com comentários ligados.
- `Discussion` passa a ter duas seções: **“Nos trechos”**, agrupada por citação, e **“Comentários gerais”**, cada uma com “ver todos”.
  - Cada comentário tem curtir (♥ e contador), responder e, para o autor da publicação, ocultar.
  - Clicar num destaque rola até o fio.
- Os trechos não encontrados na revisão atual (depois de uma nova publicação) aparecem em “Comentários sobre versões anteriores”, com a citação original.
- O `SharedNotePage` (link não listado) continua só leitura, sem comentários.

**Avisos**
- O sino do topo (`AppShell`) mostra um contador de não lidas (consulta a cada 60 s e ao focar a janela) e uma lista com links para a publicação ou o trecho. Abrir marca como lido.

**Configurações da publicação**
- O `PublishPanel` ganha uma chave para permitir ou bloquear comentários.

## Segurança e regras

- Todo texto é renderizado como texto pelo React. Nenhum HTML de usuário.
- Isolamento por dono:
  - comentários privados e `public-comments` só para o dono da nota;
  - ocultar só pelo autor da publicação.
- Limites:
  - texto de 1 a 2000 caracteres e citação até 500;
  - limite global da API já existente;
  - cota de comentários por usuário: `Social:CommentsPerHour`, padrão 60 → 429.
- Excluir a nota apaga os comentários privados (cascade). Revogar a publicação esconde a discussão, como hoje.

## Testes e documentação

- **`node --test`:** `suggestTaxonomy`, `locateQuote` (mesmo bloco, bloco movido, ausente) e o agrupamento e ranking de fios no cliente.
- **xUnit:**
  - normalização da resposta de IA de taxonomia;
  - validação de âncora contra o conteúdo da revisão;
  - regras de ranking `top`;
  - entidades novas (curtida no próprio comentário, ocultar).
- **Suítes HTTP:**
  - `taxonomy.test.mjs`: Fake, existentes e novas, 404 para nota alheia;
  - `note-comments.test.mjs`: CRUD e isolamento;
  - `comments.test.mjs`:
    - trecho válido e inválido;
    - resposta herda a âncora;
    - curtida idempotente e no próprio comentário;
    - ordem `top`;
    - ocultar e desligar comentários;
    - link não listado sem comentários;
    - avisos criados e marcados como lidos.
- **Chrome** (API e Postgres descartáveis, porta 5174):
  - sugerir e aplicar categoria e tags (nova e existente);
  - aba Comentários com comentário privado;
  - publicar como Pública;
  - segundo usuário comenta um trecho e curte;
  - ver destaque e fio na página pública e no editor do autor;
  - sino com aviso;
  - ocultar;
  - nova versão publicada com trecho mantido e com trecho removido.
- **Docs:**
  - ADR-038 (sugestões de taxonomia, locais e IA);
  - ADR-039 (comentários privados e públicos em trechos, ranking, moderação e avisos), atualizando o ADR-035;
  - `06_API_E_CONTRATOS.md`;
  - `05_MODELO_DE_DADOS.md`;
  - `ESTADO_DO_PROJETO.md`.
- Um commit por etapa. Push só quando pedido.
