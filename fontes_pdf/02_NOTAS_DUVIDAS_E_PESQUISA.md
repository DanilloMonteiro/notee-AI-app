# Fonte integral do PDF — páginas 11–17

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 11 — Notas, blocos e organização

```text
PRODUTO E EXPERIÊNCIA


Notas, blocos e organização
Modelo inicial de edição
Proposta: Tiptap/ProseMirror com extensões básicas e IDs estáveis de bloco; o backend aceita um schema
JSON versionado e validado. Começar com parágrafo, heading, lista, citação e código. Não depender de
serviços pagos de IA, colaboração ou histórico do editor: esses comportamentos pertencem ao Notee. [R06,
R07]

   Conceito             Regra proposta

                        Id, OwnerId, Title, ContentSchemaVersion, ContentJson, PlainText, Version, Status,
   Note
                        CategoryId e timestamps UTC.

                        Id estável, tipo permitido, conteúdo e atributos limitados. Mover o bloco não troca sua
   Block
                        identidade.

                        Pertence a um usuário. Nome normalizado único por dono. Categoria opcional; sem árvore
   Category
                        nesta versão.

                        Também pertence ao usuário. Relação N:N com Note; rejeitar associação com taxonomia de
   Tag
                        outro dono.

   NoteRevision         Snapshot imutável de conteúdo e metadados; restaurar cria uma revisão mais nova.

   NoteStatus           Active, Archived, Trashed. Só Active permite editar, analisar, pesquisar ou publicar.


Limites iniciais sugeridos, não requisitos definitivos: título até 160 caracteres, 200 blocos, 100 mil caracteres
de texto e 12 tags por nota. Validar a mesma política na API; o limite de contexto enviado ao modelo deve ser
muito menor que o tamanho permitido da nota.

Remoção e restauração
Excluir categoria deixa notas sem categoria, sem apagar conteúdo. Excluir tag remove associações. Mover
nota para arquivo/lixeira cancela jobs novos e retira publicações nesta versão. Restaurar a nota não republica
nem reinicia pesquisas automaticamente. Exclusão definitiva exige que a nota já esteja na lixeira.
```

## Página 12 — Ancoragem de palavras e frases

```text
PRODUTO E EXPERIÊNCIA


Ancoragem de palavras e frases
Uma dúvida pode nascer de uma palavra isolada ou de uma frase. Guardar apenas offsets absolutos do
documento é frágil: inserir texto antes desloca a seleção. O objeto Anchor preserva identidade, posição e
evidência do trecho; não depende exclusivamente da aparência do destaque.
  DoubtAnchor
    noteId, noteRevisionId, blockId
    fromUtf16, toUtf16          // fim exclusivo
    exactQuote, prefix, suffix
    contextSnapshotHash, anchorVersion
    status: Attached | Stale | Orphaned

Política proposta
No primeiro recorte, a seleção fica dentro de um único bloco textual; o sistema explica esse limite para
seleções que cruzam blocos. O intervalo usa unidades UTF-16 sobre o texto simples do bloco, igual na API e no
cliente, sem cortar pares substitutos ou grafemas. Não confundir offsets locais de texto com as posições
estruturais do ProseMirror.
Enquanto o editor está aberto, mapear a marca pelas transações de edição. Após recarregar, reancorar por
BlockId + citação + contexto. Se houver uma única correspondência verificável, registrar nova versão da
âncora. Havendo ambiguidade ou remoção, mostrar “Trecho alterado” e pedir remarcação; não adivinhar o
destino.

   Caso                        Comportamento de aceite

                               Usar contexto anterior/posterior; nunca selecionar automaticamente a primeira
   Palavra repetida
                               ocorrência.

   Bloco movido                Preservar o vínculo pelo BlockId; validar se o texto relevante permaneceu igual.

   Nota editada durante        Concluir contra o snapshot de origem. Marcar o resultado como referente à revisão
   pesquisa                    antiga.

   Bloco apagado               Dúvida continua no histórico com âncora órfã. Não mover resposta para outro texto.


 Teste que revela maturidade
 Usar acentos, emojis, duas ocorrências da mesma palavra, edição antes do trecho e remoção do bloco. Validar os
 offsets no backend contra a revisão persistida, não confiar no trecho enviado pelo navegador.
```

## Página 13 — Automação: conta e nota

```text
PRODUTO E EXPERIÊNCIA


Automação: conta e nota
Separar três decisões: analisar texto para sugerir, aceitar uma sugestão como dúvida e executar uma
pesquisa. Ativar a pesquisa automática não autoriza transformar toda frase em uma pergunta aceita, nem
compartilhar todo o conteúdo com um fornecedor.

   AutoResearch da conta     Override da nota       Após dúvida aceita e contexto suficiente

   Desativado                Herdar                 Aguardar a ação Pesquisar.

                                                    Enfileirar automaticamente, se as demais proteções
   Ativado                   Herdar
                                                    permitirem.

                                                    Enfileirar automaticamente, respeitando consentimento e
   Qualquer                  Ativar
                                                    limites.

   Qualquer                  Desativar              Somente pesquisa manual; a marcação continua disponível.



Precedência e defaults
Defaults reais: notas privadas, pesquisa automática desligada e nenhum envio externo sem consentimento
específico. A análise externa tem seu próprio controle. O padrão da nota é Herdar. Uma chave global de
bloqueio externo tem precedência sobre todos os overrides; uma nota marcada como “não enviar a serviços
externos” também veta análise e pesquisa externas.
  MayEnqueue = OwnerAuthorized AND Note.Active
    AND Doubt.Accepted AND ContextSufficient
    AND ExternalConsent AND NOT GlobalBlock
    AND NOT Note.ExternalBlock AND WithinBudget
    AND NoEquivalentActiveJob

  Auto = MayEnqueue AND EffectiveAutoResearch

Pesquisa manual independe de EffectiveAutoResearch, mas obedece aos mesmos limites, permissões e
consentimentos. Revalidar esses controles antes de cada chamada externa: um job na fila não congela para
sempre a autorização do usuário. Desativar solicita cancelamento cooperativo; não desfaz uma requisição já
enviada.

 Evitar surpresas e custos
 Mudar a preferência não dispara o acervo antigo. Oferecer, no futuro, “Pesquisar pendentes” com quantidade,
 estimativa e confirmação. Deduplicar por dúvida, revisão e configuração relevante; uma nova tentativa precisa de
 intenção explícita.
```

## Página 14 — Análise e sugestões assistidas

```text
PRODUTO E EXPERIÊNCIA


Análise e sugestões assistidas
Primeiro reconhecer, depois sugerir
A análise observa uma revisão salva, não cada tecla. Começar com o botão Analisar; evoluir para execução
após pausa de digitação e salvamento quando a pessoa habilitar. Proposta inicial: debounce de 3 segundos,
tamanho mínimo de mudança e no máximo uma análise ativa por nota. Esses números devem ser medidos e
ajustados.

   Saída                   Conteúdo mínimo                                  Aplicação

                                                                            Aceitar cria Doubt; ignorar registra
                           Bloco, trecho exato, pergunta sugerida,
   DoubtSuggestion                                                          dismiss; nenhuma pesquisa antes do
                           motivo breve e revisão.
                                                                            aceite.

                           CategoryId existente do dono ou proposta         Confirmar troca de categoria. Criar
   CategorySuggestion
                           de nome, com justificativa.                      categoria nova exige intenção do usuário.

                           Tags normalizadas; distinguir existentes e       Aceitar individualmente ou em grupo
   TagSuggestion
                           novas.                                           selecionado, sem duplicar associações.


Persistir AnalysisRun com NoteRevisionId, modelo/provider, versão de prompt, custo reportado e resultado
estruturado. Um retorno de revisão antiga deve aparecer como obsoleto ou ser descartado; não pode
sobrescrever escolhas recentes. Uma sugestão ignorada para o mesmo trecho/contexto não deve reaparecer
a cada salvamento.

Palavra sem pergunta explícita
O contexto próximo ajuda a formular a pergunta, mas não elimina ambiguidade. Selecionar “Java” pode
significar linguagem, ilha ou café. Mostrar a interpretação antes da pesquisa; quando houver mais de uma
leitura plausível, usar NeedsContext e aguardar um complemento mesmo com automação ativa.

 Controle sem uma segunda rede social
 O motor não deve inferir crenças, posicionamento político ou perfis sensíveis para classificar o usuário. Classifica
 conteúdo sob seu controle e com taxonomia da sua conta. Nenhum tema tem status de feature especial.
```

## Página 15 — Pesquisa web: fluxo ponta a ponta

```text
PRODUTO E EXPERIÊNCIA


Pesquisa web: fluxo ponta a ponta
   Etapa            Responsabilidade                               Registro durável

                    Validar dono, pergunta, revisão,               ResearchJob + snapshot do contexto + Outbox na
   1. Preparar
                    consentimento, limite e deduplicação.          mesma transação.

                    Publicador entrega comando ao SQS; API já      EventId estável; JobId e trace context; sem texto
   2. Enfileirar
                    respondeu 202.                                 privado no payload.

                    Confirmar pergunta e escopo; interromper       QueryPlan limitado e versão da política de
   3. Interpretar
                    se faltar contexto.                            pesquisa.

                    Executar consultas web e obter fontes          Consultas minimizadas, IDs do provider, URLs e
   4. Buscar
                    permitidas.                                    metadados.

                    Gerar resposta somente a partir das            Resposta estruturada, afirmações e IDs de
   5. Sintetizar
                    evidências disponíveis.                        fontes.

   6. Validar e     Validar schema, citações, estado da nota e     ResearchAnswer + evidências + status + inbox em
   salvar           cancelamento.                                  commit.

                    Cliente consulta status; exibe resposta para   Notificação interna e status de revisão
   7. Apresentar
                    revisão do autor.                              independentes do job.


Implementar limites de consultas, páginas, tamanho de resposta, tempo e chamadas por job. Começar com
polling de status em intervalos de poucos segundos apenas enquanto houver atividade; parar em estado
terminal e reduzir frequência com a aba oculta. SSE ou SignalR não são necessários neste recorte.

Estados que não devem ser confundidos
  Job: Queued -> Running -> Succeeded
                      |-> RetryScheduled -> Queued
                      |-> Failed
                      |-> CancelRequested -> Cancelled

  Evidence: Sourced | Partial | NoSource
  Review: Pending | Reviewed | Rejected

Succeeded com NoSource é uma execução concluída sem evidência utilizável; não é um erro de infraestrutura.
Cancelamento é cooperativo. O navegador fechar não cancela automaticamente o trabalho real do Worker.
```

## Página 16 — Fontes, limites e qualidade da resposta

```text
PRODUTO E EXPERIÊNCIA


Fontes, limites e qualidade da resposta
Cada afirmação factual relevante precisa apontar para a evidência que a sustenta. Uma lista de links no fim
não basta para demonstrar suporte. Fonte encontrada e fonte utilizada são coleções diferentes; ambas
podem ser registradas, com retenção limitada.

   Registro             Campos propostos

                        Id, URL canônica, título, editor, data de publicação quando disponível, acesso UTC e estado
   Source
                        de acesso.

                        SourceId, trecho curto permitido ou referência recuperável, hash, localizador e método de
   Evidence
                        obtenção.

   ClaimCitation        ClaimId, SourceId e relação: suporte, contexto ou divergência.

                        Resumo, desenvolvimento, limites, lacunas, data da pesquisa, snapshot da pergunta e
   Answer
                        versão do modelo/prompt.



Política de evidência
Priorizar fontes primárias adequadas ao assunto e diversificar quando houver divergências relevantes. Separar
texto documentado, interpretação e hipótese. Para temas atuais, registrar a data do fato e da pesquisa. Não
deduzir que um conteúdo é recente somente pela data do acesso.
Sem suporte utilizável, exibir literalmente “Sem fonte” e explicar o que faltou. Uma fonte inacessível ou um
snippet sem contexto não deve ser apresentado como leitura integral. Havendo suporte somente parcial,
mostrar “Evidência parcial” e marcar os trechos sem suporte. Ausência de evidência não prova falsidade.
Não aceitar URLs inventadas pelo modelo: citações devem referenciar IDs previamente obtidos da busca.
Validar que o link existe no conjunto recuperado não demonstra, sozinho, que a fonte sustenta a conclusão.
Usar avaliação humana e casos de teste de suporte para essa segunda camada. APIs com busca nativa podem
retornar anotações e fontes; o adaptador deve preservá-las. [R08]

 Mesmo produto para todos os assuntos
 Não criar um modo religioso nem um modo de persuasão política. O aplicativo apresenta contexto, fontes e
 divergências com neutralidade, sem orientar crenças ou escolhas eleitorais. Evitar notas percentuais de “confiança”
 não calibradas.
```

## Página 17 — Providers substituíveis

```text
ARQUITETURA DE PESQUISA


Providers substituíveis, sem ficção
Provider-agnostic significa preservar contratos do Notee e isolar integrações; não significa que todos os
modelos tenham as mesmas capacidades ou produzam o mesmo resultado. Começar com
FakeResearchProvider determinístico. Escolher um único fornecedor real quando houver acesso,
consentimento e um teto de custo. Nenhum nome de modelo é fixado neste plano.

   Porta de aplicação             Contrato esperado

   ITextAnalysisProvider          Sugestões estruturadas de dúvida e taxonomia, com trecho exato e revisão.

                                  Consultas limitadas e resultados com IDs, URLs, metadados e evidências
   IWebSearchProvider
                                  recuperadas.

                                  QuestionContext + EvidenceSet → AnswerDraft com ClaimIds/SourceIds; sem
   IAnswerSynthesisProvider
                                  URLs livres.

                                  Fachada opcional para fornecedores com busca e síntese nativas; devolver o
   IResearchProvider
                                  mesmo contrato final.

                                  Busca real, citações, saída estruturada, cancelamento, idempotência, uso
   ProviderCapabilities
                                  reportado e job remoto.


Infrastructure traduz DTOs, SDKs, anotações e erros. Application coordena limites, consentimento, estado e
retomada. Domain desconhece o fornecedor. Registrar ProviderId, ModelId, PromptVersion, PolicyVersion e
SearchMode em cada execução para permitir auditoria.

Erros e capacidades explícitos
Normalizar AuthFailure, QuotaExceeded, RateLimited, Timeout, TransientFailure, InvalidOutput,
UnsupportedCapability e UnknownOutcome. Retry deve obedecer ao tipo de operação e ao efeito externo;
não repetir toda falha cegamente. Se o modelo não tiver busca, combiná-lo com um provedor de busca ou
rejeitar o modo, em vez de chamar conhecimento paramétrico de “pesquisa na internet”.

 Privacidade e dependências externas
 LocalStack não emula ChatGPT, Gemini, Claude nem a internet. O perfil Fake funciona sem APIs comerciais. O perfil
 Live depende de fornecedor externo e pode gerar custos. Não trocar de fornecedor silenciosamente em um fallback;
 isso pode mudar o destinatário dos dados.
```
