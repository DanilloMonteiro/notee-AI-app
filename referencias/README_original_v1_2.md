# Notee

> Anote agora. Entenda depois.

Pacote de **planejamento e protótipo desktop**, interface versão 1.2, 05/11/2026 (plano técnico v1.1). O produto-alvo será implementado pelo autor com mentoria, não foi entregue como aplicação de produção.

## Comece por aqui

1. Abra `Notee_Prototipo.html` no Chrome, Edge ou Firefox no computador. Não precisa de npm, servidor, conta ou chave de IA. Alternativa equivalente: abrir `prototipo/index.html`, mantendo CSS e JS na mesma pasta.
2. Use **Explorar o protótipo** ou **Telas** para navegar. O design é desktop-first; responsividade foi adiada por decisão de escopo.
3. Leia `Notee_Planejamento.pdf`. São 60 páginas: 2 páginas de atualização, 51 páginas do plano original e 7 de especificação técnica, com navegação interna, requisitos, arquitetura, regras, 18 marcos sem cronograma, mentoria e fontes oficiais.
4. Para estudar com outra IA, anexe o PDF ou `docs/PLANEJAMENTO_NOTEE.md`, acrescente `docs/ESTADO_DO_PROJETO.md` e copie o prompt de `docs/MENTOR_IA.md`.

## Conteúdo

```text
Notee/
  Notee_Planejamento.pdf
  Notee_Prototipo.html             # arquivo unico para abrir
  README.md
  docs/
    PLANEJAMENTO_NOTEE.md          # plano expandido com a secao v1.1
    MAPAS_DE_IDEIAS.md             # spec de grafos, contexto IA e aceite
    MAPAS_DE_IDEIAS_preview.png    # captura do canvas demonstrativo
    MENTOR_IA.md                   # contrato e prompt mestre
    ESTADO_DO_PROJETO.md           # progresso real entre sessoes
    VALIDACAO_DA_ENTREGA.md        # o que foi e nao foi verificado
  prototipo/
    index.html                    # mesma experiencia, arquivos separados
    styles.css                    # tokens e componentes visuais
    app.js                        # dados ficticios e interacoes
    README.md                     # mapa de telas e guia de migracao
```

## Um roteiro para experimentar

Na visão geral, crie uma nota e escreva uma pergunta. Salve. Selecione **uma palavra ou uma frase** dentro de um bloco e clique em **Marcar como dúvida**. Ajuste a pergunta e salve-a. Inicie a pesquisa simulada; volte depois para ver fontes ou **Sem fonte**.

No editor, **Analisar texto (simulação)** sugere dúvidas e organização. Nada é aplicado sem aceitar. Ajuste o padrão da conta em Configurações e o comportamento da nota em Herdar/Ativar/Desativar. Sugestões não aceitas não disparam pesquisas.

Depois revise uma resposta, publique um snapshot e abra a biblioteca pública. Compare o rascunho com a publicação; veja histórico, arquivo e lixeira. Não há curtidas, comentários, seguidores ou feed social.

## O que é simulado

Autenticação, envio/recuperação de senha, pesquisa, fontes consultadas durante uma execução, métricas, publicação externa e backend. As fontes do exemplo de Outbox são links de referência; este arquivo não consulta a web nem um modelo.

Os dados ficam no armazenamento deste navegador, sem criptografia de aplicativo. Se `localStorage` estiver indisponível, a demonstração funciona em memória e pode não persistir. Use apenas conteúdo fictício. Senhas digitadas nos formulários demonstrativos não são armazenadas.

As tarefas são temporizadores locais e não sobrevivem ao fechamento da página como um Worker durável. O histórico é simplificado. Um link copiado continua sendo local, não uma URL hospedada. A aplicação real deve seguir as regras de segurança, fontes, snapshots e tentativas do planejamento.

Para recomeçar: **Configurações > Privacidade e dados > Reiniciar dados**. O reset remove alterações locais desta demonstração.

## Infraestrutura e aprendizado

A meta é AWS **somente emulada no LocalStack**, com Terraform. O PDF separa etapas com containers locais do perfil full-local e explicita que acesso/serviços dependem de versão/plano do LocalStack. Não há Terraform, backend ou deploy implementados neste pacote; eles fazem parte dos marcos que você vai construir.

A IA deve explicar os motivos, dar pistas, aguardar sua implementação e revisar. Não deve exigir perguntas pedagógicas antes de explicar nem escrever o sistema inteiro por padrão. Testes automatizados de frontend e responsividade não fazem parte da etapa atual.

## Novidade v1.1 — Mapas de ideias (05/11/2026)

O pacote foi ampliado **sem trocar a identidade visual original**. O novo menu **Mapas de ideias** apresenta quadros privados e reutiliza as notas ativas da biblioteca **Minhas notas**. A mesma nota aparece em vários quadros com **posições e conexões independentes**. As linhas podem ter tipos, rótulo personalizado ou nenhum rótulo. Dúvidas aparecem como nós satélites menores; livros e fontes possuem campos estruturados no painel lateral.

**Experimente:** abra `Notee_Prototipo.html`, clique em **Explorar o protótipo** e acesse **Mapas de ideias**; um quadro de exemplo com Política, Capitalismo, Comunismo e Economia está pronto para navegar. Selecione um cartão para ver seu resumo; arraste as notas e o fundo, ajuste zoom ou use **Enquadrar tudo**. Clique em **Conectar** e escolha duas notas para editar o tipo da relação. Crie uma nova nota no mapa e confirme que ela também aparece em **Minhas notas** e nos outros quadros.

A aba **IA e contexto** permite escolher **Mapa inteiro** ou **Selecionar subgrafo**, pré-visualizar JSON e experimentar propostas simuladas de novas notas e ligações que **só são aplicadas após seu aceite**. Nada é enviado a provedores reais. A seleção de contexto também não representa consentimento automático de envio no sistema futuro.

O arquivo [`docs/MAPAS_DE_IDEIAS.md`](docs/MAPAS_DE_IDEIAS.md) contém modelo de dados, relações, endpoints propostos, restrições de privacidade e testes de aceite. A seção 49 do [`docs/PLANEJAMENTO_NOTEE.md`](docs/PLANEJAMENTO_NOTEE.md) atualiza o plano original. `Notee_Planejamento.pdf` contém um suplemento v1.1 e preserva as páginas originais como referência histórica; em caso de conflito sobre os mapas, **v1.1 prevalece**.

A migração demonstrativa preserva a chave `notee-prototype-v1` e acrescenta os mapas aos dados locais já existentes. Ao reiniciar o protótipo, as notas de demonstração são recriadas. Em um navegador sem `localStorage`, as alterações só duram enquanto a página estiver aberta.

## Refinamento de acessibilidade visual v1.2 — 05/11/2026

A interface do protótipo foi atualizada sem modificar as entidades, o conteúdo das notas, a identidade gráfica ou o fluxo de pesquisa simulado da v1.1:

- **Editor:** arraste a divisória entre a área de escrita e o assistente à direita. A divisória aceita foco com `Tab` e ajuste por `←`/`→` (`Shift` acelera). O tamanho é compartilhado entre notas.
- **Dúvidas e respostas:** uma faixa horizontal compacta aparece **abaixo do título da nota** com todas as perguntas daquela nota e um resumo da resposta disponível. Role horizontalmente ou use os botões `‹` e `›`. Clique numa pergunta para expandir **abaixo da faixa**, deslocando o editor para baixo. A faixa não é fixa ao rolar a página.
- **Mapas de ideias:** `#/boards` agora abre uma **lista de mapas em cartões**, com título, descrição, quantidade de notas, conexões e data da última atualização. Um cartão abre `#/boards/{boardId}`.
- **Canvas:** arraste a divisória do painel **Nota / IA e contexto**, ou use **Ocultar painel / Mostrar painel** para aproveitar toda a largura. A função **Enquadrar tudo** permanece disponível.
- **Menu principal:** o botão ao lado do logotipo recolhe o menu até uma faixa de ícones; clique novamente para expandir. Cada ícone mantém nome acessível ao passar o mouse e por leitor de tela.
- **Preferências:** larguras, visibilidade do painel do mapa e estado do menu são guardados na chave separada `notee-ui-layout-v1` do `localStorage`, sem alterar `notee-prototype-v1` (notas/mapas). Para recomeçar, use **Configurações > Layout e acessibilidade > Restaurar layout padrão**.

A versão autocontida `Notee_Prototipo.html` e a versão em `prototipo/index.html` têm as mesmas alterações. O planejamento PDF continua representando o escopo v1.1; as decisões de UX v1.2 foram registradas adicionalmente nos documentos Markdown, sem apresentar uma alteração visual como implementação de backend.

### Capturas reais do protótipo v1.2

Estas imagens foram geradas com o HTML atualizado no Chromium e são apenas exemplos demonstrativos com dados fictícios:

- [Editor com faixa de dúvidas](docs/PROTOTIPO_V12_EDITOR.png)
- [Resposta expandida abaixo da faixa](docs/PROTOTIPO_V12_RESPOSTA_EXPANDIDA.png)
- [Listagem inicial dos mapas](docs/PROTOTIPO_V12_LISTA_MAPAS.png)
- [Canvas com painel ajustável](docs/PROTOTIPO_V12_CANVAS.png)
