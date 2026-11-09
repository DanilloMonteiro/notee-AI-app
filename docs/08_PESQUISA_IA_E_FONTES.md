# Dúvidas, sugestões, automação, pesquisa e fontes

**Base:** PDF pp. 12–17; inclui os limites originais de autorização, verificação e capacidade dos providers.

## Três comandos distintos

1. **Analisar texto** salvo e sugerir possível dúvida/tags/categoria.
2. **Aceitar/ignorar sugestão** explicitamente; só depois surge uma dúvida aceita.
3. **Pesquisar uma dúvida** manualmente ou por pesquisa automática configurada, respeitando consentimento e limites.

Nunca transformar toda frase sugerida em pergunta pesquisada sem aceite. Nunca deixar IA reescrever o texto autoral ou publicá-lo automaticamente.

## Matriz de automação

| Pesquisa da conta | Override da nota | Resultado após dúvida aceita |
|---|---|---|
| Desativada | Herdar | exige ação `Pesquisar` |
| Ativada | Herdar | enfileira se proteções permitirem |
| Qualquer | Ativar | enfileira se autorizações, limites e contexto permitirem |
| Qualquer | Desativar | somente manual |

Defaults: nota privada, pesquisa automática OFF, `Herdar` na nota, **nenhum envio externo sem consentimento específico**. Análise externa possui controle separado. GlobalBlock e Note.ExternalBlock vetam pesquisa/análise externas, independentemente do override.

```text
MayEnqueue = OwnerAuthorized AND Note.Active AND Doubt.Accepted
             AND ContextSufficient AND ExternalConsent
             AND NOT GlobalBlock AND NOT Note.ExternalBlock
             AND WithinBudget AND NoEquivalentActiveJob
Auto = MayEnqueue AND EffectiveAutoResearch
```

Pesquisa manual ignora apenas EffectiveAutoResearch, **não** as proteções. Antes de cada chamada externa, revalidar permissão, consentimento e orçamento. Alterar preferência não reenvia automaticamente dúvidas antigas.

## Análise e sugestões

- Analisar revisão salva, não cada tecla. Começar botão `Analisar`; debounce de 3 s é hipótese de evolução, não requisito fixo.
- `DoubtSuggestion`: NoteRevisionId, BlockId, quote, pergunta proposta, justificativa curta; aceitar cria `Doubt`.
- `TagSuggestion`: tag do próprio dono ou nova proposta normalizada; aceitar individualmente ou seleção explícita.
- `CategorySuggestion`: existente ou nome de nova; troca/criação exigem intenção do usuário.
- `AnalysisRun` persiste provider/model, promptVersion, custo/uso reportado, revisão, status e propostas.
- Se uma sugestão é de revisão antiga, mostrar `Stale` ou descartar. Não sobrescrever escolhas recentes.
- Palavra ambígua (ex.: Java) pode exigir `NeedsContext`, mesmo com automação ligada.
- Não inferir atributos sensíveis da pessoa para classificação de notas. Organização trata conteúdo controlado pelo usuário.

## Fluxo de pesquisa em sete etapas

| Etapa | Ação | Persistência esperada |
|---|---|---|
| 1 Preparar | Validar dono, pergunta, consentimento, revisão, cota, dedup | ResearchJob + contexto snapshot + Outbox no mesmo commit |
| 2 Enfileirar | Publicar comando mínimo no SQS | EventId, JobId, trace context, sem texto sensível |
| 3 Interpretar | Contexto, pergunta, QueryPlan e limites | plano de consulta + versão da política |
| 4 Buscar | Executar web search autorizada | URLs, IDs e metadados de sources obtidos |
| 5 Sintetizar | Resposta **somente** a partir da evidência recuperada | claims citando sourceIds válidos |
| 6 Validar/salvar | Schema, citações, autorização, cancelamento | Answer, evidência, inbox e status terminal |
| 7 Apresentar | Polling da execução, resposta para revisão humana | sem alterar texto do autor |

Estados independentes:

```text
Job: Queued -> Running -> Succeeded | RetryScheduled -> Queued | Failed
                   \-> CancelRequested -> Cancelled
Evidence: Sourced | Partial | NoSource
Review: Pending | Reviewed | Rejected
```

`Succeeded + NoSource` **não** é erro de servidor. `QuotaExceeded`, `RateLimited`, `Timeout`, `InvalidOutput` e `UnsupportedCapability` **não** podem virar `Sem fonte`.

## Fonte, evidência e citações

- `Source`: URL canônica, título, editor, publicação se houver, data de acesso UTC e status.
- `Evidence`: SourceId, localizador/trecho permitido, hash e método de obtenção.
- `ClaimCitation`: ClaimId, SourceId, relação `support|context|divergence`.
- `Answer`: resumo, desenvolvimento, lacunas/limites, data da pesquisa, snapshot da pergunta e versão do modelo/prompt.
- **Fonte encontrada ≠ fonte utilizada**; verificar se suporta *aquela afirmação*, não apenas se a URL existe.
- Sem evidência utilizável: mostrar literalmente `Sem fonte` e justificar. Evidência incompleta: `Evidência parcial` e sinalizar afirmações não suportadas. Não inferir certeza percentual.
- Fontes inacessíveis ou snippets isolados não contam como leitura integral; modelos não inventam URLs.
- Livros/fontes cadastrados nas notas do grafo são **referências declaradas pelo autor**, distintas de `Source/Evidence` recuperadas numa pesquisa real.

## Providers e capacidades

Portas: `ITextAnalysisProvider`, `IWebSearchProvider`, `IAnswerSynthesisProvider`, fachada opcional `IResearchProvider` e `ProviderCapabilities` (busca real, citações, structured output, cancelamento, idempotência, job remoto, uso reportado).

Usar `FakeResearchProvider` determinístico e pelo menos um fornecedor real *quando autorizado*, sem SDK no Domain. `LocalStack` não é modelo de linguagem nem internet. Registrar `ProviderId`, `ModelId`, `PromptVersion`, `PolicyVersion`, `SearchMode`; não trocar provider silenciosamente em fallback.

## Integração com mapa

Ler `MAPAS_CONTEXTO_IA.md`. Perguntar ao grafo reutiliza **as mesmas regras** de autorização, versionamento, orçamento, fontes e controle humano; não cria endpoint externo desprotegido à parte.
