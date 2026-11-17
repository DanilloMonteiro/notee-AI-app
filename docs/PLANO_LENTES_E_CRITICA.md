# Plano — Lentes e Crítica de Raciocínio

> Status: **F1 implementada** (domínio + Fake, ADR-043). F2–F5 continuam como proposta. Próximo ADR livre: ADR-044.

## 1. Conceito

Ajudar a pessoa a construir raciocínio lógico e pensamento crítico a partir das próprias notas. A IA **não dá respostas nem veredictos**: sugere caminhos, sempre como pergunta/convite, com aceite individual.

Dois tipos de sugestão:

| Tipo | Objetivo | Exemplo (governo Dilma) | Exemplo (derivadas) |
|---|---|---|---|
| **Lente** (`Lens`) | Outro ângulo sobre o tema | Econômica, política, jurídica, histórica, filosófica | Geométrica, física, histórica, formal |
| **Crítica** (`Critique`) | Apontar lacunas no raciocínio da nota | "Que evidência sustenta X?" / "Como alguém que discorda argumentaria?" | "Por que a regra da cadeia vale aqui?" / "Teste um contraexemplo" |

Categorias de crítica: afirmação sem evidência, salto lógico, generalização, um lado só, termo ambíguo, correlação tratada como causa.

## 2. Encaixe no que já existe

- Aceitar sugestão → cria `Doubt` ligada à nota (ancorada no bloco, se crítica) → fluxo de pesquisa com fontes já existente.
- Geração via job assíncrono (Worker, `Idempotency-Key`, estados de UI existentes).
- Fake determinístico por padrão; provedor real só com consentimento e escopo.
- Seguir o padrão das sugestões de categoria/tags (ADR-038) — **ler a implementação antes de decidir o módulo** (`Research` ou `Notes`).
- Futuro (fora do MVP): lente aceita vira nó em um board.

## 3. Regras de produto

1. Temas contestados/políticos: só perguntas, perspectivas equilibradas ("como os defensores dessa visão a veriam"), nunca veredicto.
2. Sugestão ≠ fato: rótulo "Caminho para explorar"; fatos só via pesquisa com fontes.
3. Não interromper a escrita: sob demanda ("Ver outros ângulos") ou após inatividade.
4. Máx. ~4 lentes + 3 críticas por geração.
5. Motivo de descarte guardado só para o owner; nada em logs/telemetria.

## 4. Fatias

**F1 — Domínio + Fake**
`PerspectiveSuggestion`: `Kind` (Lens|Critique), `Category`, `Prompt`, `AnchorBlockId?`, `Status` (Pending|Accepted|Dismissed), `NoteRevisionId` de origem (para estado "desatualizado"). Fake gera por regras (categoria/palavras-chave → templates fixos).

**F2 — API (rascunho, ajustar ao padrão de rotas atual)**
```
POST /notes/{id}/perspectives/jobs            (Idempotency-Key) → 202 + jobId
GET  /notes/{id}/perspectives                 → lista com status e revisão de origem
POST /notes/{id}/perspectives/{sid}/accept    → cria Doubt, retorna doubtId
POST /notes/{id}/perspectives/{sid}/dismiss   { reason? }
```
Outro owner → 404. Aceite de revisão antiga → revalidar ou 409.

**F3 — Migration**: tabela `perspective_suggestions` (FK nota, owner, índice `(note_id, status)`).

**F4 — Front**: painel "Ângulos" ao lado do editor (reusar painel redimensionável); lentes em cartões por categoria; críticas ancoradas com destaque `accent #D8F4A1`; rótulo em `muted`. Estados: vazio, carregando, contexto insuficiente, desatualizado, provedor indisponível, timeout.

**F5 — Provedor real**: saída JSON validada, regras de neutralidade e "só perguntas", consentimento por nota.

## 5. Critérios de aceite

- Nenhuma sugestão vira Doubt sem aceite individual.
- Aceitar crítica cria Doubt ancorada no bloco correto.
- Nota editada após geração → estado "desatualizado".
- Fake determinístico (mesma nota → mesmas sugestões) — teste xUnit.
- Tema político: toda lente é pergunta, sem conclusão.
- Isolamento por owner (HTTP 404).

## 6. Como testar

`dotnet test tests/NoteLab.Tests` · nova suíte em `tests/http/` (gerar → listar → aceitar → conferir Doubt) · `npm test` · `npm run build` · manual com nota de história e de matemática.

## 7. Registro

- ADR-043 "Lentes e crítica de raciocínio" (escopo enviado à IA + regra de neutralidade).
- `docs/ESTADO_DO_PROJETO.md`: "proposta em desenho" até F1 ter evidência.
