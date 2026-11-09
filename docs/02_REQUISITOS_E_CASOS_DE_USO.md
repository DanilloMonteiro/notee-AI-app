# Requisitos funcionais e critérios verificáveis

**Base:** PDF pp. 6–8, 37–48. `RF01`–`RF14` são do PDF. `RF15`–`RF21` são **acréscimos propostos** para formalizar o comportamento v1.1/v1.2 já descrito no HTML/README.

| ID | Requisito | Critério mínimo de aceite |
|---|---|---|
| RF01 | Conta e recuperação | Login e reset, anti-enumeração, expiração/reuso rejeitados, ownership. |
| RF02 | Notas em blocos | Criar, editar, arquivar, lixeira/restaurar com dono autenticado. |
| RF03 | Categorias e tags | Uma categoria opcional; tags múltiplas, normalizadas e do mesmo dono. |
| RF04 | Dúvida manual | Palavra/frase com `NoteRevisionId`, bloco, quote/contexto/offset; mapa pode criar dúvida não ancorada **como extensão**. |
| RF05 | Sugestões | Propor, aceitar/ignorar; saída desatualizada não escreve dados. |
| RF06 | Automação | Padrão da conta + override da nota; nunca pesquisar sugestão não aceita. |
| RF07 | Pesquisa durável | 202 + job ID/status, recuperação, timeout, retry/cancelamento cooperativo. |
| RF08 | Resposta rastreável | Suporte por afirmação ou `Sem fonte`; contexto, limites, data. |
| RF09 | Provider substituível | Fake determinístico e adaptador real opt-in, sem SDK no Domain. |
| RF10 | Publicação | Snapshot privado/não listado/público, atualização/revogação explícitas. |
| RF11 | Histórico | Revisões imutáveis, conflito 409, restauração cria revisão nova. |
| RF12 | Busca interna | Texto/título/tags/categoria por dono, paginação estável e FTS evolutivo. |
| RF13 | Limites e diagnóstico | Quotas, logs e telemetria sem conteúdo privado. |
| RF14 | Ambiente reprodutível | Terraform/LocalStack, CI leve, backup/restore e runbook. |
| RF15 (novo) | Catálogo de mapas | CRUD privado, lista com título, descrição, contagens, atualização; rotas de lista/canvas. |
| RF16 (novo) | Notas compartilhadas | Mesmo `NoteId` em múltiplos mapas; conteúdo único, posições independentes. |
| RF17 (novo) | Arestas por mapa | Conexões direcionadas com tipo e label opcional; validar endpoints e dono; sem efeito em outros mapas. |
| RF18 (novo) | Dúvidas e referências | Dúvidas satélites vinculadas à nota, livros/fontes estruturados na nota, sem alegar verificação automática. |
| RF19 (novo) | Contexto controlado para IA | Mapa inteiro ou subgrafo; incluir apenas nós autorizados e arestas internas; prévia versionada, consentimento. |
| RF20 (novo) | Sugestões de grafo | Notas/arestas propostas ficam pendentes; aceitar/rejeitar individual; validar revisão e permissão novamente. |
| RF21 (novo) | Layout v1.2 | Menu recolhível; splitter do editor e mapa via mouse/teclado; faixa horizontal da nota; preferências de layout isoladas. |

## Casos de uso por área

### Notas e dúvidas
- Criar nota privada, adicionar/remover/editar blocos e classificar. Editar só `Active`.
- Selecionar uma expressão para formular pergunta; salvar âncora robusta mesmo com acento/emoji.
- Se uma nota mudou durante pesquisa, resposta permanece vinculada à revisão pesquisada.
- Abrir faixa de dúvidas da nota e expandir uma resposta **abaixo** da faixa, sem overlay no editor.

### Pesquisas
- Configurar `AutoResearchDefault` da conta e `Inherit/On/Off` por nota. `GlobalBlock` e `ExternalBlock` vetam requisições.
- Aceitar dúvida/sugestão não implica autorização irrestrita de envio externo.
- Ver status do job, evidência, resposta, fontes, revisão humana e tentativas.
- Jobs falhos/repetidos não reescrevem a nota nem fabricam fontes.

### Mapas
- Catálogo (`/boards`) mostra cartões; `Novo mapa` cria quadro privado.
- Canvas (`/boards/{id}`) permite pan/zoom, arrastar notas, enquadrar tudo, selecionar e inspecionar.
- Salvar aresta dirigida e seus atributos; editar/remover preserva a nota.
- Criar nota no mapa também cria a nota canônica; alteração em qualquer tela aparece nos outros mapas.
- Criar dúvida do mapa vinculada à nota sem inventar âncora textual.
- Visualizar livros/fontes cadastradas por nota e navegar ao editor/pesquisa.
- Escolher contexto all/manual; prévia local em JSON; perguntar/gerar proposta não faz alterações automáticas.

## Critérios transversais

- **Isolamento:** todo recurso privado e qualquer ID do cliente precisa passar por autorização de dono no servidor; não confundir `404` com permissão concedida.
- **Concorrência:** duas atualizações da mesma nota/board com versão antiga não se sobrescrevem.
- **Durabilidade:** fechamento/reload da aba não deve apagar job aceito.
- **Acessibilidade:** focos visíveis, controles por teclado, nome acessível e leitura de status.
- **Honestidade técnica:** separar critérios avaliados no protótipo dos testados no backend real.
