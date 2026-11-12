# Plano de gráficos em notas do Notee

**Status:** v1 implementada em 12/11/2026 (escopo inicial abaixo, linha e barras, renderização em SVG próprio), sem commit até o momento. Decisões e limites em [ADR-036](13_ADRS_E_DECISOES_ABERTAS.md); evidências em [ESTADO_DO_PROJETO.md](ESTADO_DO_PROJETO.md).  
**Data:** 12/11/2026.

> A seção “Estado atual verificado” abaixo descreve o código **antes** da implementação e foi mantida como registro.

## Objetivo

Adicionar gráficos como blocos estruturados dentro das notas para ajudar a comparar períodos, perceber tendências e relacionar indicadores. O primeiro caso de uso é organizar dados históricos ou políticos — por exemplo, indicadores ao longo de um governo — junto das fontes e do contexto.

O gráfico deve ser parte da nota e continuar editável. Uma imagem estática não é suficiente como representação principal, pois dificulta corrigir dados, consultar valores e adaptar a visualização a diferentes telas.

## Estado atual verificado

- O editor real usa blocos de texto com `contentEditable` em [Note.jsx](../Notee-Front/src/pages/Note.jsx). Os tipos disponíveis são parágrafo, título, lista, citação e código.
- O conteúdo é enviado como uma string em [UpdateNoteRequest.cs](../Notee-API/src/NoteLab/Modules/Notes/Application/UpdateNoteRequest.cs) e armazenado como texto na entidade [Note.cs](../Notee-API/src/NoteLab/Modules/Notes/Domain/Note.cs). A interface serializa os blocos em JSON.
- As páginas de publicação em [PublicPages.jsx](../Notee-Front/src/features/publishing/PublicPages.jsx) só mantêm blocos que tenham texto e não possuem renderização de gráfico.
- O frontend não inclui hoje uma biblioteca de gráficos.

Portanto, gráficos estruturados exigem mudanças no editor, no formato de conteúdo, na validação e nas páginas de leitura e publicação. Eles não existem como recurso nativo no Notee atual.

## Escopo inicial proposto

A primeira versão deve permitir que a pessoa:

- insira um bloco de gráfico numa nota;
- escolha gráfico de linha ou de barras;
- informe título, rótulos dos eixos, unidade, categorias ou períodos e uma ou mais séries;
- informe e consulte fontes declaradas para os dados;
- inclua uma descrição textual do que o gráfico mostra;
- revise e edite a tabela de dados que alimenta o gráfico;
- veja o gráfico na nota e nas publicações que incluam aquele snapshot.

O gráfico deve mostrar também uma tabela acessível com os mesmos valores. Períodos sem dado devem ser distintos de valor zero. A visualização deve indicar unidade, intervalo, categorias e fontes de forma clara.

Ficam fora da primeira versão: buscar dados automaticamente na web, importar diretamente dados de uma conversa compartilhada de IA, gerar interpretações causais, incorporar HTML/JavaScript fornecido pelo usuário e oferecer muitos tipos de gráfico. A sugestão de importar chats é separada; no futuro, ela poderá propor dados para um gráfico, sempre com prévia e aceite humano.

## Modelo de conteúdo candidato

O bloco deve guardar os dados, não uma imagem renderizada. A estrutura abaixo é uma proposta para ADR e validação técnica, não um contrato aprovado.

| Campo | Conteúdo |
| --- | --- |
| `id` | Identificador estável do bloco, como nos demais blocos da nota. |
| `type` | Valor fixo `chart`. |
| `chartType` | `line` ou `bar` na primeira versão. |
| `title` e `description` | Título visível e descrição textual acessível. |
| `xAxisLabel`, `yAxisLabel`, `unit` | Significado dos eixos e unidade de medida. |
| `labels` | Categorias ou períodos na ordem apresentada. |
| `series` | Nome de cada série e valores alinhados aos rótulos; valores ausentes devem ser representados explicitamente, nunca convertidos em zero. |
| `sources` | Rótulo e URL das fontes declaradas; avaliar referência por série quando houver fontes diferentes. |

Os limites de tamanho, quantidade de séries e pontos, formatos numéricos, tipos de unidade e política para URLs devem ser definidos antes da implementação. Um link de fonte informado não significa que o Notee tenha verificado seu conteúdo.

## Etapas de implementação

| Etapa | Trabalho | Resultado esperado |
| --- | --- | --- |
| 1. Decisões e ADR | Definir escopo dos tipos, forma de edição dos dados, metadados de fonte, limites e estratégia de renderização. Avaliar biblioteca pela acessibilidade, tamanho, responsividade, licença e manutenção. | Modelo versionado e decisões registradas; nenhuma biblioteca é escolhida por pressuposição. |
| 2. Conteúdo e API | Aceitar e validar o novo tipo de bloco sem quebrar notas antigas. Verificar campos, valores finitos, alinhamento entre rótulos e séries, valores ausentes, limites e URLs. Atualizar a extração de texto simples para busca e contexto. | Notas antigas continuam carregando; blocos inválidos são rejeitados pelo servidor. Confirmar se é necessário versionar o schema ou fazer migration. |
| 3. Edição da nota | Adicionar ação “Gráfico”, formulário de título/eixos/unidade/fontes e editor tabular para rótulos e séries. Permitir pré-visualizar, editar e remover o bloco. Preservar o fluxo atual de salvar e a concorrência por versão da nota. | Pessoa cria e revisa dados e gráfico no contexto da nota. |
| 4. Renderização e acessibilidade | Criar um renderer de leitura, responsivo e seguro. Gerar gráfico e tabela a partir da mesma estrutura de dados; adicionar descrição, nomes de séries, valores, unidades e navegação por teclado. | Gráfico compreensível sem depender apenas de cores, tooltips ou ponteiro do mouse. |
| 5. Revisões e publicação | Incluir o bloco nas revisões existentes, permitir restauração e renderizá-lo nos snapshots públicos/compartilhados. Não executar scripts ou buscar dados externos ao abrir uma publicação. | A nota privada, a revisão e a publicação mostram os dados do snapshot correspondente. |
| 6. Verificação de entrega | Conferir compatibilidade com notas antigas, edição, persistência, validação, revisão, publicação, acessibilidade e telas estreitas. Registrar comandos e evidências reais no estado do projeto quando a implementação acontecer. | Critérios de aceite cobertos e status documentado com evidência. |

## Critérios de aceite

- Notas existentes continuam abrindo, salvando, revisando e publicando como antes.
- Uma nota pode conter gráfico de linha e de barras com uma ou mais séries.
- Editar rótulos ou valores atualiza tanto o gráfico quanto a tabela acessível, sem divergência.
- Valores ausentes permanecem visivelmente diferentes de zero.
- A API rejeita tipos de gráfico não suportados, séries desalinhadas, números inválidos e conteúdo acima dos limites acordados.
- O título, a descrição, as unidades e as fontes declaradas ficam acessíveis para leitura e busca.
- Alterar ou restaurar uma revisão mantém o gráfico e os dados da revisão correspondente.
- A publicação mostra a visualização baseada no snapshot publicado e não recebe dados posteriores sem uma nova publicação.
- Layout, rótulos e valores permanecem legíveis em tela estreita e com teclado/leitor de tela.
- O produto não apresenta fonte declarada como fonte verificada nem converte correlação em causa.

## Riscos e decisões em aberto

- Definir a biblioteca pode afetar acessibilidade, tamanho do bundle, manutenção e renderização em publicação.
- Conteúdo JSON sem schema explícito pode deixar blocos inválidos ou incompatíveis; validar no servidor e manter compatibilidade com notas existentes.
- Gráficos com fontes diferentes por série precisam de atribuição clara para cada série.
- Dados fornecidos por IA podem estar errados ou misturar períodos e unidades. A primeira versão deve exigir revisão humana e preservar a fonte original.
- Edição simultânea, importação de CSV e gráficos empilhados podem ser avaliados depois, sem ampliar o primeiro escopo.

## Referências do projeto

- [Editor, notas e revisões](07_EDITOR_REVISOES_E_ANCORAS.md)
- [Modelo de dados](05_MODELO_DE_DADOS.md)
- [API e contratos](06_API_E_CONTRATOS.md)
- [Segurança e privacidade](10_SEGURANCA_E_PRIVACIDADE.md)
- [Estado do projeto](ESTADO_DO_PROJETO.md)

