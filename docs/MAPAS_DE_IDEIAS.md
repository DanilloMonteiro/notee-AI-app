# ESPECIFICAÇÃO — Mapas de Ideias (Notee)

**Escopo:** feature v1.1/v1.2 descrita no README e demonstrada no HTML, integrada à arquitetura do PDF v1.0. O HTML é prova da **interação visual simulada**, não da existência de API, React, PostgreSQL ou execução de IA. Detalhes de DDL/endpoints são propostas de engenharia.

## 1. Objetivo e limites

Permitir que o usuário organize visualmente a mesma biblioteca de notas em **vários quadros independentes**, sem duplicar conteúdo. Cada quadro mostra cartões de notas, relações direcionadas, dúvidas satélites e inspeção de referências. Há uma função de **contexto de IA** com escopo `Mapa inteiro` ou `Selecionar subgrafo`; nenhuma informação externa é enviada por meramente visualizar/selecionar.

**Proibido:** transformar o grafo em outra fonte de verdade para notas; inventar relações factuais a partir das linhas do usuário; usar a saída de IA para modificar nota, grafo ou publicação sem aceite.

## 2. Experiência aprovada pelo protótipo

### Catálogo `/boards`

- Menu **Mapas de ideias** na sidebar, com contagem.
- Título `Seus mapas de ideias`, descrição e `+ Novo mapa`.
- Aviso: conteúdo das notas é compartilhado com `Minhas notas`, mas posições/arestas são particulares de cada mapa.
- Cartões com mini-ilustração, título, descrição, data da última atualização, número de notas, número de conexões e comandos `Abrir mapa`/`Editar`.
- Criar/editar título e descrição; excluir um mapa **não apaga notas**. O HTML evita excluir o último quadro (regra de demo; uma política de produto pode ser formalizada por ADR).
- Rota interna demonstrativa: `#/boards` e `#/boards/{boardId}`. No React, usar `/boards` e `/boards/:boardId`.

### Canvas `/boards/:boardId`

- Fundo pontilhado com pan ao arrastar fundo, zoom com scroll/botões e **Enquadrar tudo**.
- Nós principais = cartões das **notas ativas**; exibem categoria, título, pequeno resumo, indicadores de dúvidas/referências.
- Arrastar um nó atualiza somente sua posição no Board; editar texto continua pelo editor da nota.
- Arestas SVG com direção, tipo, label opcional; clique abre editor da relação, podendo remover sem afetar outros boards.
- Dúvidas = nós satélites menores associados à nota; clicar exibe pergunta/status e link para pesquisa.
- Inspector lateral com duas abas **Nota** e **IA e contexto**; redimensionável, ocultável; ao selecionar nota mostra resumo, tags, livros, fontes, dúvidas e conexões locais.
- Botões `Conectar` (escolher origem e destino), `Nova nota`, `Layout automático`, zoom, enquadrar e alternar painel; use o HTML para localizar controles exatos.

### Referências e dúvidas

- Livros: `title`, `author`, `year`, `url` (opcional); fontes: `title`, `publisher`, `url` (opcional).
- Referências pertencem à **nota**. Ao inspecionar a mesma nota em outro board, mesmas referências devem aparecer.
- Criar dúvida no mapa não exige selecionar trecho do editor: sua origem é `BoardQuestion`, âncora opcional nula, mas `noteId` sempre existente.
- Uma mesma dúvida pode aparecer em todos os mapas em que sua nota esteja visível; não gerar cópias por board.

## 3. Tipos de relação e direção

O HTML define exatamente:

| Código | Rótulo de interface |
|---|---|
| `none` | Sem rótulo |
| `related` | Relacionado a |
| `causes` | Causa |
| `example` | Exemplo de |
| `contradicts` | Contradiz |
| `depends` | Depende de |
| `influences` | Influencia |
| `custom` | Personalizado |

- `from`/`to` são IDs de notas e expressam **direção**, mesmo para relações descritivas.
- `label` opcional de até 80 caracteres prevalece na apresentação, mas `type` permanece no contrato.
- `none` sem label desenha linha sem texto; com label mostra o texto especificado.
- As conexões são afirmações/associações do **autor**, não fatos que a IA tenha comprovado.
- Validação de self-edge, duplicatas e tamanho máximo do grafo precisa de ADR; recomenda-se rejeitar self-edge e duplicata exata, sem proibir direções distintas automaticamente.

## 4. Modelo de domínio e propriedade

```text
Note (Notes) [fonte canônica de texto, tags, categoria, revisão, referências]
   ^ 0..N (reutilizável em vários boards)
   |
Board (KnowledgeMaps) [ownerId, title, description, version, timestamps]
   |-- BoardPlacement(boardId, noteId, x, y, positionVersion)
   |-- BoardEdge(boardId, fromNoteId, toNoteId, relationType, label, version)
   |-- GraphSuggestion (proposta estruturada, estado pendente/aceita/ignorada/obsoleta)
   |
   `-- BoardContextSnapshot (opcional, versionado e mínimo quando questionado)
Doubt (Research) [pertence a Note; não ao Board]
ResearchAnswer (Research) [pertence ao Job/Doubt, não à aresta]
```

**Invariantes:** (1) proprietário do board, nota, conexão e referência deve ser o mesmo; (2) somente notas ativas no canvas e no contexto; (3) duas pontas da aresta pertencem ao escopo autorizado do board; (4) exclusão de board remove apenas dados do board; (5) exclusão definitiva de nota limpa placements/edges correspondentes, nunca publica texto por engano.

### Política de inclusão das notas: atenção

A implementação visual v1.2 usa `ensureGraphPlacements` para garantir **todas as notas ativas em todos os boards** e, por isso, cartões de mapas exibem mesma contagem de notas. **Não** interpretar como boards de subconjuntos editáveis, a menos que o usuário altere a regra.

**Proposta de implementação eficiente sem side effect de GET:** consultar notas ativas do owner pelo contrato de Notes; combinar posições persistidas com posições determinísticas de fallback para notas ainda sem placement; persistir quando a pessoa arrastar (ou registrar um comando próprio de inicialização). Alternativa: criar placements via evento durável de NoteCreated. Escolher com ADR, pois escrever em schema alheio no GET não é ideal.

## 5. Contrato de persistência (proposto)

Use como base `05_MODELO_DE_DADOS.md`: `boards`, `board_placements`, `board_edges`, `graph_suggestions` no módulo KnowledgeMaps; `note_references` no módulo Notes. Migrations com indices por owner e constraints; coordenadas finitas; `expectedVersion` onde houver conflito.

**API sugerida:** `GET/POST /api/v1/boards`, `GET/PATCH/DELETE /boards/{id}`, `PUT /boards/{id}/placements/{noteId}`, `POST /boards/{id}/edges`, `PATCH/DELETE /boards/{id}/edges/{edgeId}`, `GET/POST/DELETE /notes/{id}/references`, `POST /notes/{id}/doubts`, operações de IA em `MAPAS_CONTEXTO_IA.md`.

- Edição do layout do Board não gera nova `NoteRevision`.
- Adição/remoção de livro/fonte pertence à nota, com política de revisão consistente.
- A criação de `Nova nota` no canvas chama o **mesmo** caso de uso de Notes da listagem/editor, sem criar um “GraphNote” novo.
- No frontend, drag deve ter estado transitório para fluidez e commit ao terminar, com tratamento de 409; evitar PUT a cada pixel.

## 6. Frontend React proposto

```text
web/src/features/boards/
  api/boardsApi.ts
  model/boardTypes.ts
  model/graphSelection.ts
  components/BoardCatalog.tsx
  components/BoardCard.tsx
  components/BoardCanvas.tsx
  components/NoteNode.tsx
  components/DoubtSatelliteNode.tsx
  components/EdgeLayer.tsx
  components/GraphToolbar.tsx
  components/BoardInspector.tsx
  components/GraphAiPanel.tsx
  components/BoardPanelResizer.tsx
  pages/BoardsPage.tsx
  pages/BoardDetailsPage.tsx
```

São **nomes propostos**, não arquivos encontrados no repositório. Usar TanStack Query para dados remotos; seleção, pan, scale, drag, modal e splitter como estados locais da view. Não copiar integralmente o `app.js` da demo para um componente gigante.

## 7. Critérios de aceite obrigatórios

| ID | Cenário | Resultado |
|---|---|---|
| MAP-AC01 | Criar dois boards | dois cartões no catálogo; IDs diferentes, ambos privados |
| MAP-AC02 | Mesma nota em dois boards | mesmo NoteId/conteúdo; posições independentes |
| MAP-AC03 | Editar nota no editor | conteúdo refletido em todos os boards, sem duplicar Note |
| MAP-AC04 | Conectar A→B só no Board 1 | Board 2 não recebe edge; ambas notas preservadas |
| MAP-AC05 | `none` sem label | mostra linha sem rótulo, mas preserva direção/IDs |
| MAP-AC06 | Arrastar, zoom, pan, enquadrar | gesto altera viewport/posição esperada, sem gravar texto |
| MAP-AC07 | Abrir dúvida satélite | pergunta e status refletem dúvida do módulo Research |
| MAP-AC08 | Adicionar livro/fonte | persiste na nota; aparece em todos os boards, mas não assume validação externa |
| MAP-AC09 | Criar dúvida no mapa | `origin=BoardQuestion`, sem âncora inventada, ligada à nota |
| MAP-AC10 | Excluir board | notas permanecem; não apaga conexões de outros boards |
| MAP-AC11 | Acessar Board/Note ID de outra conta | 404 e nenhum dado vazado |
| MAP-AC12 | Arquivar/lixeira nota | nó indisponível para contexto; lifecycle e revisões preservados |
| MAP-AC13 | Requisições concorrentes | conflito claro (409), sem perda silenciosa de posição/edge |
| MAP-AC14 | Recarregar UI | dados vêm da API real; preferências visuais restauradas sem usar `localStorage` como base de autorização |
| MAP-AC15 | IA do mapa | somente o fluxo descrito em `MAPAS_CONTEXTO_IA.md`, com aceite |

## 8. Requisitos de UX v1.2

Usar `UX_V12.md` e screenshots. O protótipo usa `notee-prototype-v1` para dados de demonstração e `notee-ui-layout-v1` para preferências; na aplicação real **não** manter notas, senhas ou autoridade de owner no localStorage. Splitter de mapa e editor por teclado; sidebar com ícones acessíveis; painel de mapa ocultável e restaurável.

## 9. Perguntas a resolver por ADR antes de implementar comportamento novo

- Manter regra “todas as notas ativas em todos os mapas” ou introduzir membership por board?
- Qual o limite de nodes/edges e a estratégia de renderização/performance?
- Como gerenciar posições padrão de novas notas sem escrita implícita ao fazer GET?
- Como lidar com concorrência entre movimentos/edições de múltiplas abas?
- Como tratar referências em publicações (por padrão **não** entram sem seleção)?
- Deve existir exportação/compartilhamento de mapas? **Não previsto** na versão atual; não assumir.

## 10. Referências exatas

- README original: seções **Novidade v1.1 — Mapas de ideias** e **Refinamento de acessibilidade visual v1.2**.
- HTML: funções `defaultGraphBoard`, `ensureGraphPlacements`, `graphContextForBoard`, `renderBoardsList`, `renderGraph`, `graphBoardModal`, `graphEdgeModal`, `graphGenerateSuggestions`, `graphPreviewContext`.
- PDF: pp. 5–8, 11–17, 19–24, 28–29 para regras que o grafo não pode violar.
