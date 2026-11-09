# Editor, notas, revisões e âncoras

**Base:** PDF pp. 10–12, 25, 38; UX v1.2 do README/HTML. Valores quantitativos do PDF são sugestões, não limites definitivos.

## Editor e conteúdo

- Produto-alvo: editor de blocos baseado em Tiptap/ProseMirror, schema JSON versionado e **validado no servidor**; elementos iniciais: parágrafo, heading, lista, citação e código.
- Cada bloco tem `blockId` estável, tipo permitido, conteúdo e atributos validados. Reordenar bloco não muda seu ID.
- `notes.notes` guarda JSONB, título, `PlainText` gerado do JSON *no servidor*, `Version` de concorrência, `Status` e timestamps UTC.
- Limites iniciais sugeridos: título até 160 caracteres, até 200 blocos, até 100 mil caracteres e até 12 tags; limites reais devem ser centralizados na API e anotados em ADR.
- Status: `Active`, `Archived`, `Trashed`; só `Active` permite editar, analisar, pesquisar ou publicar. Excluir permanentemente só a partir da lixeira.

## Revisões

- `NoteRevision` é snapshot imutável de conteúdo + metadados, UNIQUE(NoteId,NoteVersion).
- Toda alteração válida aumenta `Version` e salva Note+Revision na mesma transação. Restauração cria **revisão nova**, nunca apaga a anterior.
- Update usa `expectedVersion`/token de concorrência na condição de escrita SQL; versão antiga gera `409`. A tela preserva o buffer local e oferece comparação.
- Começar com **Salvar** explícito. Autosave posterior serializa chamadas por nota, consolida mudanças pendentes e não aceita resposta antiga que sobrescreva digitação nova.
- Tags/categoria de outra conta são inválidas; remoção de categoria não apaga notas; retirada de tag remove associações.

## Âncora de dúvida selecionada

Estrutura do plano (PDF p. 12):

```text
DoubtAnchor:
  noteId, noteRevisionId, blockId
  fromUtf16, toUtf16          # fim exclusivo
  exactQuote, prefix, suffix
  contextSnapshotHash, anchorVersion
  status: Attached | Stale | Orphaned
```

### Regras críticas

1. Primeira versão: seleção dentro de **um único bloco textual**. Seleção que cruza blocos recebe orientação, não cria uma âncora quebrada.
2. UTF-16 deve refletir texto simples do bloco e não posições estruturais de ProseMirror. Não cortar pares substitutos/grafemas.
3. Valide `exactQuote` e intervalo no snapshot **persistido**, não apenas nos valores do navegador.
4. Enquanto editor aberto, remapear transações; ao recarregar, reancorar por `BlockId + quote + prefix/suffix`, somente se correspondência inequívoca.
5. Editar/remover trecho marca âncora `Stale/Orphaned`, mantendo dúvida e histórico. Não selecionar a primeira ocorrência de palavra repetida por adivinhação.
6. Job iniciado na revisão N produz resposta da N, mesmo se a nota avançou para N+1.

## Extensão: dúvida criada pelo mapa

O HTML permite `Nova dúvida` no mapa sem seleção textual. É uma dúvida pertencente à nota, porém **sem âncora**. Proposta: `Doubt.origin = NoteSelection | BoardQuestion` e `anchor = null` somente para perguntas do mapa, com validação de tipo e sem inventar offsets. A UI pode oferecer vincular a trecho depois, se isso for desenhado e aprovado.

## UX v1.2: faixa e divisória

- Colocar faixa horizontal de perguntas da **nota aberta** logo abaixo do cabeçalho principal. Cada cartão mostra a pergunta e um pequeno resumo de resultado/estado.
- Rolagem horizontal e setas; clique expande o conteúdo abaixo da barra, deslocando o editor. A barra não fica fixa.
- Editor e painel direito com divisor arrastável; acessível por `Tab`, setas e `Shift+setas`; largura compartilhada entre notas no estado visual do navegador.
- Manter separadas as mensagens de `Salvo` da nota e o progresso/status do Job.

## Testes que importam

- Texto com `á`, emoji, símbolos UTF-16, duas ocorrências da mesma palavra, alteração antes do trecho, mover/excluir bloco.
- Save concorrente `v7`/`v7` ⇒ um sucesso e um `409`, sem perda de texto no frontend.
- Restauração cria nova revisão; arquivo/lixeira impedem novas pesquisas; restaurar não republica nem reinicia jobs.
- Editar enquanto pesquisa roda mostra resposta como originada da revisão anterior.
