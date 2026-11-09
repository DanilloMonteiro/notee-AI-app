# Fonte integral do PDF — páginas 04–10

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 04 — Como usar este plano

```text
GUIA DE LEITURA


Como usar este plano
O Notee é um projeto pessoal de aprendizado: um bloco de notas com organização, publicação e pesquisa de
dúvidas. Este documento define o produto-alvo e as decisões de engenharia; não representa um backend já
implementado ou uma infraestrutura já validada.

Três materiais, três funções

   Material                   Como usar

   Planejamento PDF /
                              Fonte de verdade para requisitos, arquitetura, limites, marcos e critérios de aceite.
   Markdown

                              Referência visual e de navegação. Dados fictícios, armazenamento local e pesquisas
   Protótipo HTML
                              simuladas.

   Instruções para a IA       Contrato pedagógico: explicar, orientar, aguardar sua implementação e revisar. Não
   mentora                    construir o sistema por você.



Origem e autoridade das decisões
[B0] identifica o PDF original NoteNest, fornecido pelo autor. Foram preservados .NET, PostgreSQL,
concorrência, idempotência, Outbox, Worker, testes e observabilidade. O documento original usa 12 sprints e
descreve um produto privado; a aprovação desta conversa substitui essas escolhas por marcos sem datas,
publicação e frontend completo. [B0, pp. 3–8]
As regras de produto aqui estabelecidas consolidam suas respostas. Detalhes não especificados, como limites
iniciais, estados e contratos, são propostas técnicas deste plano, ajustáveis por ADR. [Rxx] indica
documentação pública consultada em 30/09/2026; não é evidência de execução local.

 Ordem recomendada
 Leia primeiro o escopo, a automação e o contrato da IA. Navegue no HTML. Depois implemente um marco por vez.
 Ter profundidade significa explicar, testar e operar suas escolhas, não acumular tecnologias.
```

## Página 05 — Decisões que não devem se perder

```text
ESCOPO APROVADO


Decisões que não devem se perder
   Tema                   Decisão consolidada

                          Notee. Slogan proposto: “Anote agora. Entenda depois.” Identidade própria, com
   Identidade
                          referência no AbacatePay.

                          React + TypeScript + Vite + React Router + TanStack Query. Tailwind como utilitário;
   Frontend
                          tokens e componentes Notee.

                          Editor de blocos. Uma categoria principal e várias tags por nota. Sugestões de dúvidas,
   Editor e organização
                          tags e categoria precisam de aceite.

                          Marcação manual de palavra/frase; análise do texto; pesquisa web; resultado separado;
   Pesquisa
                          fontes ou “Sem fonte”.

                          Padrão da conta e override por nota. Análise e execução de pesquisa são controles
   Automação
                          diferentes.

                          Produto generalista, sem viés religioso ou especialização em um tema. Providers/modelos
   Temas e providers
                          substituíveis.

   Conta                  E-mail e senha; recuperação de senha desde cedo. Sem login social nesta etapa.

   Compartilhamento       Privado, não listado e público; perfil e biblioteca somente para leitura. Sem rede social.

                          Monólito modular .NET + API e Worker, PostgreSQL, Redis seletivo, histórico real de
   Backend e dados
                          revisões.

                          Terraform e serviços AWS somente no LocalStack. Nenhuma conta AWS real é requisito
   Infraestrutura
                          do projeto.

                          Testes de backend e CI/CD enxuto. Sem testes automatizados de frontend agora.
   Qualidade
                          Responsividade fica para depois.

                          Marcos sem cronograma. A IA explica os motivos antes; perguntas de verificação apenas
   Aprendizado
                          quando solicitadas.


 Fora do escopo desta etapa
 Curtidas, comentários, seguidores, feed personalizado, colaboração simultânea, aplicativo mobile, modo offline
 completo, pagamentos, Kubernetes e microsserviços. Embeddings entram apenas como evolução opcional.
```

## Página 06 — O produto e seu ciclo principal

```text
PRODUTO E EXPERIÊNCIA


O produto e seu ciclo principal
Problema: perguntas surgem durante a escrita, mas pesquisar imediatamente interrompe o raciocínio; deixar
para depois faz essas perguntas se perderem. O Notee registra a dúvida com seu contexto e organiza uma
pesquisa rastreável, sem modificar silenciosamente o texto do autor.

Vocabulário do produto
Nota é um documento do usuário. Bloco é uma unidade interna do editor, como parágrafo, título ou código.
Categoria organiza notas; tag conecta assuntos. Não haverá uma entidade adicional “caderno” nesta versão.
Dúvida, pesquisa, resposta e publicação são registros diferentes.

   Jornada                      Comportamento esperado

                                Criar conta, recuperar acesso, criar nota, editar blocos e atribuir categoria/tags.
   Escrever e organizar
                                Nada é público por padrão.

                                Selecionar palavra/frase ou aceitar uma sugestão. Confirmar a pergunta
   Capturar a dúvida
                                interpretada e preservar o contexto da revisão.

                                Iniciar manualmente ou seguir a automação. Receber um identificador do trabalho e
   Pesquisar sem bloquear
                                continuar usando a aplicação.

                                Abrir resultado separado, fontes, limitações e data. Refinar ou pesquisar novamente
   Revisitar a resposta
                                sem apagar tentativas anteriores.

   Compartilhar                 Publicar um snapshot revisado. Respostas da IA só acompanham o snapshot quando
   conscientemente              o autor autorizar explicitamente.



O que demonstra sucesso
Uma pessoa consegue registrar uma pergunta sem sair da escrita, reencontrá-la, distinguir informação
sustentada de resposta sem fonte e compartilhar somente o que escolheu. Para o aprendizado, o autor
explica o caminho HTTP → transação → fila → Worker → resposta e reproduz suas falhas.

 Não prometer
 A IA não é um verificador infalível. O aplicativo organiza investigação e evidências, não entrega garantia de verdade,
 aconselhamento especializado ou um índice automático de certeza.
```

## Página 07 — Requisitos verificáveis

```text
PRODUTO E EXPERIÊNCIA


Requisitos verificáveis
   ID       Requisito                 Evidência de aceite

                                      Fluxos completos com resposta anti-enumeração; token
    RF01    Conta e recuperação
                                      expirado/reutilizado rejeitado.

                                      Criar, editar, arquivar, mover para lixeira e restaurar, sempre por
    RF02    Nota em blocos
                                      ownership.

                                      Categoria opcional única por nota; tags múltiplas, normalizadas e do
    RF03    Categoria e tags
                                      mesmo dono.

    RF04    Dúvida manual             Palavra ou frase vira uma dúvida com trecho, bloco, revisão e contexto.

    RF05    Sugestões                 Detectar candidatas; aceitar/ignorar; sugestão obsoleta não altera a nota.

    RF06    Automação                 Matriz conta/nota respeitada; nenhum job para sugestão ainda não aceita.

    RF07    Pesquisa durável          HTTP 202, status persistido, retomada e cancelamento cooperativo.

                                      Fontes por afirmação; data, contexto e limitações; “Sem fonte” sem
    RF08    Resposta rastreável
                                      evidência utilizável.

                                      Fake determinístico e um adaptador real opt-in, sem SDK do fornecedor no
    RF09    Provider substituível
                                      domínio.

                                      Snapshot; privado/não listado/público; retirada de acesso sem expor o
    RF10    Publicação
                                      rascunho.

    RF11    Histórico                 Revisões imutáveis, conflito de edição e restauração como nova revisão.

    RF12    Busca interna             Título/texto/tags/categoria com isolamento, paginação e FTS.

    RF13    Controles e diagnóstico   Limites de utilização e telemetria sem conteúdo privado.

                                      Terraform/LocalStack, pipelines leves e runbook de inicialização e
    RF14    Ambiente reprodutível
                                      restauração.


Requisitos não funcionais transversais: isolamento entre usuários, conteúdo privado por padrão,
acessibilidade básica, processamento assíncrono recuperável, uso limitado de recursos e operação
demonstrável. Metas quantitativas serão hipóteses de laboratório, não SLAs comerciais.
```

## Página 08 — Mapa de navegação e telas

```text
PRODUTO E EXPERIÊNCIA


Mapa de navegação e telas
   Área                 Telas / estados                     Objetivo

   Entrada              Landing, login, cadastro            Apresentar o produto e acessar uma conta local.

                                                            Mensagem genérica; sucesso, token inválido e
   Recuperação          Solicitar link, redefinir senha
                                                            expirado.

   Espaço pessoal       Visão geral, minhas notas           Acesso rápido ao que escrever e ao que revisar.

                                                            Blocos, categoria, tags, sugestões e preferência
   Escrita              Editor, seleção, modal de dúvida
                                                            local.

   Organização          Categorias, busca global            Criar e editar categorias, filtrar e reencontrar ideias.

                                                            Fila, execução, resultado, Sem fonte, falha e
   Pesquisa             Central de dúvidas, detalhe
                                                            cancelamento.

   Versões              Histórico e conflito                Comparar, preservar rascunho e restaurar revisão.

                                                            Escolher snapshot e visibilidade; revisar inclusão de
   Compartilhar         Publicar, minhas publicações
                                                            IA.

                                                            Somente leitura de snapshots autorizados. Não é
   Leitura pública      Nota pública, perfil, biblioteca
                                                            rede social.

                                                            Restaurar ou excluir com confirmação proporcional
   Ciclo de vida        Arquivo, lixeira
                                                            ao risco.

                                                            Conta, automação, privacidade e visão operacional
   Controle             Configurações, diagnóstico
                                                            restrita.

   Referência           Design system, mapa de telas        Tokens, componentes e inventário de navegação.



Estados também são parte da interface
Planejar vazio, carregando, salvando, salvo, falha, sem permissão, conflito, limite atingido, contexto
insuficiente e resultado desatualizado. Nunca simular progresso percentual exato sem informação do
backend; etapas conhecidas são suficientes. Um erro de pesquisa não deve impedir a edição da nota.
O protótipo inclui 24 destinos de navegação contando variantes de pesquisa, além dos modais. No React,
separar os estados da tela da lógica de domínio. O menu “Telas” do HTML é um recurso de demonstração, não
uma permissão de acesso.
```

## Página 09 — Design system Notee

```text
PRODUTO E EXPERIÊNCIA


Design system Notee
Direção proposta: natural, silenciosa e editorial. Referência declarada: a simplicidade da página pública do
AbacatePay; sem copiar marca, textos ou dashboard autenticado. O Notee tem monograma tipográfico
próprio, verde floresta e lima suave como acento. [R01]

   Token                Valor inicial                            Uso

   brand / strong       #21573D / #17412D                        Ação principal, foco e identidade.

   accent               #D8F4A1                                  Ênfase discreta; usar texto escuro.

   canvas / surface     #F7F8F5 / #FFFFFF                        Fundo de trabalho e cards.

                                                                 Hierarquia textual. Validar contraste na combinação
   ink / muted          #1E2D25 / #6C7A71
                                                                 final.

   border               #E1E7DE                                  Divisores e agrupamento visual.

                                                                 Escala comum; sem valores aleatórios por
   spacing              4 / 8 / 12 / 16 / 24 / 32 / 48 px
                                                                 componente.

   radius               8 / 10 / 16 px                           Controles, chamadas e cards.

                        UI 14 px; leitura 16 px; títulos 24–36   Fonte de sistema inicialmente. Ajustar legibilidade,
   type
                        px                                       não apenas densidade.



Construir um sistema pequeno e real
Começar com Button, TextField, Select, Checkbox, Switch, Badge, Dialog, Toast, Tabs, Card, EmptyState,
Skeleton e SourceCard. Definir estados, foco, tamanhos e mensagens antes de espalhar CSS. Adotar primitivas
acessíveis onde necessário; não reinventar gerenciamento de foco só para demonstrar complexidade.
Os tokens são a fonte; Tailwind é uma ferramenta de aplicação. Manter uma página de componentes no
próprio app, sem exigir Storybook neste momento. Validar teclado, nome acessível, contraste e leitura de
mensagens manualmente. Responsividade e testes automatizados do frontend ficam em evolução posterior.

 Regra de linguagem
 Usar “Com fontes”, “Evidência parcial” e “Sem fonte”. Não usar um selo de certeza para uma síntese gerada por IA.
 Mensagens de erro explicam o próximo passo, sem culpar a pessoa.
```

## Página 10 — Referência visual do protótipo

```text
PRODUTO E EXPERIÊNCIA


Referência visual do protótipo
A escrita ocupa o centro; a pesquisa fica em um painel separado. O destaque de dúvida pertence ao texto
selecionado, enquanto a resposta pertence a outro registro. Esta imagem é uma captura do HTML entregue,
com dados de demonstração.




Editor Notee: texto autoral, preferência por nota e painel de pesquisa. Captura do protótipo, não do produto implementado.

Pontos de atenção na migração
O painel lateral não deve roubar o foco do editor a cada atualização de status. Preservar seleção ao abrir o
modal e oferecer atalho de teclado. O status de salvamento pertence à nota; o status de pesquisa pertence ao
job. Não misturar os dois estados.
O HTML usa DOM e dados locais para demonstrar a experiência. No React, o buffer de edição fica no editor, os
dados remotos ficam no TanStack Query e a API decide permissões. Nunca portar o localStorage de
demonstração como armazenamento de autenticação ou fonte de verdade do produto.
```
