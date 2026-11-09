# UX v1.2 — comportamento visual fiel ao protótipo

**Base:** `../referencias/Notee_Prototipo_v1_2.html`, `../referencias/README_original_v1_2.md`, screenshots anexadas. Todos estes elementos são **demonstração**.

## Capturas de referência

- `../referencias/PROTOTIPO_V12_LISTA_MAPAS.png` — tela **Seus mapas de ideias**, com sidebar, barra de busca, aviso sobre notas compartilhadas e cartões de mapas.
- `../referencias/PROTOTIPO_V12_EDITOR.png` — página **Editor de nota**, faixa horizontal de perguntas logo abaixo do título e área de edição com assistente lateral.

## Navegação e sidebar

- Sidebar expandida com logo Notee, seletor de espaço, menu `Visão geral`, `Minhas notas`, `Mapas de ideias`, `Categorias`, `Dúvidas e pesquisas`, `Buscar nas notas`, `Publicações`, `Biblioteca pública`, `Arquivo`, `Lixeira`, `Configurações`, `Design system`.
- Botão de recolher ao lado do logo. No estado recolhido, barra estreita com ícones e nome acessível via `aria-label`/`title` e suporte de foco.
- Sidebar não deve ocultar conteúdo ou deixar navegação inacessível.

## Página do editor de nota

- Título principal e metadados de revisão no topo; histórico e publicar à direita.
- Faixa `Dúvidas e respostas` da **nota atualmente aberta**. Cards horizontais têm resumo legível e truncamento gracioso; setas rolam horizontalmente.
- A pessoa abre uma resposta e vê o conteúdo **imediatamente abaixo da faixa**; editor e painel lateral descem no fluxo normal, sem modal e sem sticky.
- Editor de blocos com título, categoria, tags, seleção `Marcar como dúvida`, botões de salvar/arquivo/lixeira.
- Separador entre editor e assistente **arrastável** e operável por teclado (`Tab` → setas, `Shift` para ajuste maior). Preferência compartilhada entre notas.

## Catálogo de mapas

- Cabeçalho `Seus mapas de ideias`, descrição, ação `+ Novo mapa`.
- Aviso sobre notas da biblioteca compartilhadas e posições/conexões independentes.
- Cartões: imagem esquemática de nós e linhas, rótulo `Mapa de ideias`, data, título, descrição, contagem de notas/conexões, `Abrir mapa` e `Editar`.
- O desenho em miniatura do HTML é **decorativo**, não uma captura garantida do estado real do grafo.
- A tela mostra 2 cartões na captura, por exemplo `Política e sociedade` e `Meu aprendizado` — dados fictícios, não hardcode do produto.

## Canvas e inspector

- `BoardCanvas` com nós posicionáveis, conexões, dúvidas satélites, pan/zoom.
- `Enquadrar tudo` centraliza nós considerando extensão do grafo; toolbar distingue `Selecionar`/`Conectar`.
- Painel lateral com tabs `Nota` e `IA e contexto`; arrastar divisor para ajustar largura, ou ocultar/mostrar painel, preservando desenho da área principal.
- Inspector: dados da nota escolhida, resumo, tags, livros, fontes, dúvidas, arestas. Tab de IA: sugestões com aceitar/rejeitar, seleção de subgrafo e JSON preview.
- Sem seleção, apresentar orientação neutra (`Selecione uma nota no mapa...`).

## Preferências visuais

No protótipo:

```json
{
  "storageKey": "notee-ui-layout-v1",
  "sidebarCollapsed": false,
  "editorAsideWidth": 316,
  "graphAsideWidth": 330,
  "graphAsideHidden": false
}
```

Os dados da demo ficam separadamente sob `notee-prototype-v1`. `Configurações > Layout e acessibilidade > Restaurar layout padrão` restaura somente preferências; `Privacidade e dados > Reiniciar dados` restaura os dados da demo. Na aplicação real, usar storage do navegador apenas para preferências não sensíveis (ou API de preferências, se decidida). Nunca salvar JWT ou conteúdo de notas como fonte de verdade.

## Verificação manual mínima

- Sidebar recolher/expandir sem perder rótulos/foco.
- Splitters com mouse, `Tab`, setas e `Shift+setas`; largura permanece dentro de limites utilizáveis.
- Faixa de dúvidas: várias perguntas, scroll/controles, expansão abaixo e ausência de interposição sobre edição.
- Catálogo mostra vazio/nova lista e cards atualizados após mudança.
- No canvas: zoom/pan, mover nó sem criar nova nota/revisão, selecionar edge, alternar abas, esconder/mostrar painel.
- Estado de contexto selecionado não é confundido com permissão de saída para provider.
- Redução de movimento, foco visível, labels acessíveis e contraste de texto secundário.

## Quando o protótipo divergir da stack final

É permitido reimplementar a experiência com componentes React e primitives acessíveis, **não** copiar a arquitetura monolítica de `app.js`. Deixe interações equivalentes com o mesmo propósito e sinalize qualquer diferença de UX escolhida por desempenho/acessibilidade, em ADR.
