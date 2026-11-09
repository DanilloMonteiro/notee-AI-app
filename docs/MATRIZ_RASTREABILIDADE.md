# Rastreabilidade — fontes, requisitos e documentos

Este mapa ajuda a IA a localizar a informação sem carregar todas as páginas do PDF ou repetir texto em cada sessão.

## PDF original para contexto temático

| Páginas PDF | Tópico | Contexto novo |
|---|---|---|
| 4–7 | guia, decisões, produto, RF01–14 | `00_COMECE_AQUI_E_PRECEDENCIA.md`, `01_PRODUTO_E_ESCOPO.md`, `02_REQUISITOS_E_CASOS_DE_USO.md` |
| 8–10 | navegação, tokens, referência visual | `03_UX_DESIGN_SYSTEM.md`, `UX_V12.md` |
| 11–12 | blocos, notas, âncoras | `07_EDITOR_REVISOES_E_ANCORAS.md`, `05_MODELO_DE_DADOS.md` |
| 13–17 | automação, análise, web research, fontes, provider agnóstico | `08_PESQUISA_IA_E_FONTES.md`, `10_SEGURANCA_E_PRIVACIDADE.md` |
| 18 | publicação | `01_PRODUTO_E_ESCOPO.md`, `10_SEGURANCA_E_PRIVACIDADE.md` |
| 19–20 | monólito modular, solution e frontend | `04_ARQUITETURA_E_STACK.md` |
| 21–22 | modelo relacional | `05_MODELO_DE_DADOS.md` |
| 23–24 | API | `06_API_E_CONTRATOS.md` |
| 25–27 | concorrência, Outbox, Worker e laboratório de falhas | `09_JOBS_OUTBOX_IDEMPOTENCIA.md` |
| 28–29 | identidade, IA e conteúdo privado | `10_SEGURANCA_E_PRIVACIDADE.md` |
| 30–36 | testes, LocalStack, Terraform, CI, observabilidade | `11_INFRA_TESTES_OBSERVABILIDADE.md` |
| 37–42 | M01–M18 | `12_PLANO_DE_IMPLEMENTACAO.md` |
| 43 | ADR 001–010 | `13_ADRS_E_DECISOES_ABERTAS.md` |
| 44–46 | mentor, prompt e estado | `MENTOR_IA.md`, `ESTADO_DO_PROJETO.md`, `PROMPT_IA_DESENVOLVEDORA.md` |
| 47–48 | limites do protótipo e aceite final | `UX_V12.md`, `VALIDACAO_DA_ENTREGA.md` |
| 49–51 | referências R01–R30 | `../fontes_pdf/09_REFERENCIAS_DO_PDF.md` + `../fontes_pdf/10_LINKS_ORIGINAIS_DO_PDF.md` |

## Evoluções posteriores documentadas no README/HTML

| Referência | Detalhe | Contexto |
|---|---|---|
| README original §Novidade v1.1 | boards, notas compartilhadas, arestas, dúvida satélite, livros/fontes e IA com aceite | `MAPAS_DE_IDEIAS.md`, `MAPAS_CONTEXTO_IA.md` |
| README original §v1.2 | faixa de dúvidas, catálogo, splitter, sidebar, preferências persistidas | `UX_V12.md` |
| HTML `renderBoardsList()` | cartões, contagens, navegação | `MAPAS_DE_IDEIAS.md` |
| HTML `renderGraph()` | canvas, toolbar, inspector | `MAPAS_DE_IDEIAS.md`, `UX_V12.md` |
| HTML `graphContextForBoard()` | schema `notee-graph-context/1`, filtro de arestas entre notas selecionadas | `MAPAS_CONTEXTO_IA.md` |
| HTML `graphGenerateSuggestions()` | propostas simuladas com aceites individuais | `MAPAS_CONTEXTO_IA.md` |
| HTML `ensureGraphPlacements()` | **todas as notas ativas** em todos os boards | `MAPAS_DE_IDEIAS.md`, `13_ADRS_E_DECISOES_ABERTAS.md` |

## Cobertura de requisitos

| Requisitos | Contexto |
|---|---|
| RF01 | `10_SEGURANCA_E_PRIVACIDADE.md` |
| RF02–RF04, RF11, RF12 | `05_MODELO_DE_DADOS.md`, `07_EDITOR_REVISOES_E_ANCORAS.md` |
| RF05–RF09, RF13 | `08_PESQUISA_IA_E_FONTES.md`, `09_JOBS_OUTBOX_IDEMPOTENCIA.md` |
| RF10 | `10_SEGURANCA_E_PRIVACIDADE.md` |
| RF14 | `11_INFRA_TESTES_OBSERVABILIDADE.md` |
| RF15–RF18 (propostos) | `MAPAS_DE_IDEIAS.md` |
| RF19–RF20 (propostos) | `MAPAS_CONTEXTO_IA.md` |
| RF21 (proposto) | `UX_V12.md` |

## Regra para atualização futura

Se a aplicação real divergir de qualquer contrato proposto, não esconder a divergência: comparar código e requisitos, registrar ADR/alteração aprovada, atualizar documento temático, requisitos e `ESTADO_DO_PROJETO.md`.
