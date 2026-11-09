# Interface, navegação e design system Notee

**Base:** PDF pp. 8–10; HTML e capturas de interface v1.2.

## Identidade

- Tom **natural, silencioso, editorial**, com referência de simplicidade ao AbacatePay; não copiar marca.
- Nome: `Notee`; slogan: `Anote agora. Entenda depois.`
- Layout desktop-first; fundo suave, cartões claros, linhas discretas, verde floresta, lima sutil.

| Token | Valor original | Uso |
|---|---|---|
| `brand` | `#21573D` | ações principais |
| `brandStrong` | `#17412D` | hover e ênfase |
| `accent` | `#D8F4A1` | acento com texto escuro |
| `canvas` | `#F7F8F5` | fundo de trabalho |
| `surface` | `#FFFFFF` | painéis e cards |
| `ink` | `#1E2D25` | títulos/texto |
| `muted` | `#6C7A71` | secundário, validar contraste |
| `border` | `#E1E7DE` | contornos |
| espaçamento | 4/8/12/16/24/32/48 px | escala consistente |
| radius | 8/10/16 px | controles/cards |
| tipografia | UI 14 px, leitura 16 px, títulos 24–36 px | fonte de sistema inicial |

Tokens e visuais no HTML em `referencias/Notee_Prototipo_v1_2.html`, em `:root` e classes `.sidebar`, `.graph-*`, `.editor-*`.

## Rotas de referência

`/welcome`, `/login`, `/register`, `/forgot`, `/reset`, `/home`, `/notes`, `/note/:id`, `/boards`, `/boards/:boardId`, `/categories`, `/research`, `/research/:id`, `/search`, `/history/:noteId`, `/publish/:noteId`, `/publications`, `/public/:publicationId`, `/profile/:handle`, `/explore`, `/archive`, `/trash`, `/settings`, `/admin`, `/design-system`.

Na demo são rotas com hash `#/...`; no React usar React Router. `Telas` é ferramenta de demo, não autorização nem rota de produção necessária.

## Componentes iniciais

`Button`, `TextField`, `Select`, `Checkbox`, `Switch`, `Badge`, `Dialog`, `Toast`, `Tabs`, `Card`, `EmptyState`, `Skeleton`, `SourceCard`, barra superior, sidebar e estados de formulário. Preferir primitivas acessíveis e padronizadas.

## Layout e comportamentos do protótipo v1.2

- Sidebar larga ~228 px ou recolhida ~72 px, mantendo acesso por ícones e rótulos acessíveis.
- Editor de nota tem zona de dúvidas **abaixo do título principal**, horizontal e rolável, com resumo das respostas. Clique expande o detalhe abaixo da faixa, empurrando o editor; não é sticky.
- Editor e painel direito separados por divisor redimensionável. Divisor recebe `Tab`, setas e `Shift + setas` para aceleração.
- Catálogo dos mapas em cartões: ilustração esquemática decorativa, nome, descrição, quantidade de notas, ligações e data. Botões `Abrir mapa` e `Editar`.
- Canvas com fundo pontilhado, cartões de nota, linhas SVG, dúvidas satélites, pan/zoom e botão `Enquadrar tudo`; painel lateral `Nota / IA e contexto` redimensionável e ocultável.
- Estado `notee-ui-layout-v1` no `localStorage` **apenas no protótipo**: `sidebarCollapsed`, `editorAsideWidth`, `graphAsideWidth`, `graphAsideHidden`. Não transportar esse uso para tokens de autenticação ou verdade de dados.

## Estados de interface que não podem faltar

Vazio, carregando, salvando, salvo, erro, limite atingido, sem permissão, conflito, resposta desatualizada, contexto insuficiente, `Sem fonte`, timeout, provider indisponível, cancelamento. Não inventar porcentagem de progresso se o Worker só sabe etapas.

## Referências visuais do pacote

- `../referencias/PROTOTIPO_V12_LISTA_MAPAS.png`: composição do catálogo, dois cartões, listagem lateral e barra superior.
- `../referencias/PROTOTIPO_V12_EDITOR.png`: estrutura do editor, faixa de dúvidas e painel lateral.
- Para detalhes de tela e interação consultar `UX_V12.md`.

**Nota de verificação:** screenshots são referência visual fornecida; não é evidência de que o aplicativo React, API ou serviços estejam implementados.
