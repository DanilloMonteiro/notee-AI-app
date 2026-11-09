# Roadmap — 18 marcos originais + incremento Mapas de Ideias

**Fonte:** PDF pp. 37–42. **Regra:** os marcos são unidades de aprendizado sem datas nem estimativas; avanço por evidência, não pelo calendário. O estado atual real **não foi inspecionado**; consultar `ESTADO_DO_PROJETO.md`.

## Marcos M01–M18 do PDF (sequência e dependências preservadas)

| Marco | Depende de | Escopo e aceite essencial |
|---|---|---|
| M01 — Base reproduzível | nenhum | repositório, .NET/Node, primeira solution, primeiro teste Domain, README e CI mínima; build em checkout limpo |
| M02 — Domínio de notas | M01 | Note/Category/Tag/Active-Archived-Trashed e ownership; testes de regras sem ASP.NET |
| M03 — API e persistência | M02 | ASP.NET Core, EF/Postgres, migrations, Problem Details, concurrency e idempotência; integração real |
| M04 — Identidade e recuperação | M03 | Identity, login/reset, rate-limit, isolamento de contas e tokens expirados |
| M05 — Design system e primeira tela | M04 | React/TS/Vite, shell e componentes; listagem e API real; teclado/foco manual |
| M06 — Editor, revisões e seleção | M05 | editor de blocos, save explícito, IDs, âncoras UTF-16 e restore; conflito não perde buffer |
| M07 — Busca e organização | M06 | categorias, tags, filtros, paginação e busca textual, isoladas por dono |
| M08 — Publicação revisada | M07 | snapshots e três visibilidades, revogação, perfil/biblioteca leitura |
| M09 — Dúvidas e contrato da pesquisa | M06 | dúvida manual, FakeResearchProvider, 202 + job, fontes/Sem fonte, estados |
| M10 — Sugestões e automação | M09 | análise Fake, aceites e precedência conta/nota, sem backfill inesperado |
| M11 — Outbox e SQS no LocalStack | M09 + cobertura validada | job e Outbox atômicos, duplicatas idempotentes e DLQ |
| M12 — Worker recuperável | M10+M11 | leases, checkpoints, retry e cancelamento; falhas injetadas |
| M13 — Pesquisa real com fontes | M12 | provider real opt-in, sourceIds válidos, custos e consentimento |
| M14 — Segurança e avaliação | M13 | SSRF/XSS/injection, quota, revogação, fontes contraditórias/ausentes |
| M15 — Observabilidade e falhas | M12; M13 opcional | trace de JobId, métricas e diagnóstico sem texto privado |
| M16 — Terraform/AWS full-local | M11 + plano validado | SQS/SES/RDS/ECS/ALB etc. no LocalStack conforme licenciamento, sem AWS real |
| M17 — Entrega/desempenho/restore | M15+M16 | CI/CD local, release/rollback, carga documentada, restore real e cache só se medido |
| M18 — Evidências e consolidação | anteriores ou pendências declaradas | ADRs, README, runbook, demo integrada, progresso fiel à realidade |

O PDF detalha em cada marco `Depende de`, `Mão na massa`, `Aceite` e `Orientação da mentoria`. Para preservar **todo o texto**, usar `../fontes_pdf/07_MARCOS_E_ADRS.md`.

## Incrementos propostos para Mapas (sem renumerar os marcos originais)

| Incremento | Inserir após | Entrega e aceite |
|---|---|---|
| MAP-01 — Contrato do grafo | M03 + Notes real | modelar Board/Placement/Edge por dono, regras transacionais e DTOs; testes Domain/Integration |
| MAP-02 — Catálogo e canvas | M05 + MAP-01 | `/boards`, `/boards/:id`, lista de cartões, pan/zoom, drag, arestas e inspector; UX v1.2 fiel |
| MAP-03 — Notas/dúvidas/referências no mapa | M06 + M09 + MAP-02 | reuso dos mesmos NoteIds, dúvidas satélites sem duplicação, livro/fonte por nota e editor conectado |
| MAP-04 — Prévia do contexto | MAP-03 + identidade/consentimento | JSON schema versionado all/manual, only-authorized, só conexões entre nós escolhidos, snapshot e limites |
| MAP-05 — Sugestões e perguntas com IA | M12 + MAP-04 (real externo opcional M13) | propostas pendentes, aceitar/ignorar, job durável quando externo, provider Fake, sem escrita automática |

**Precedência de execução sugerida:** não deixar mapa exigir Worker só para mover um nó. Construir catálogo e CRUD do grafo cedo, e integração de IA apenas quando Research/Outbox estiver apto. Ajustar a ordem ao **repositório inspecionado**, sem afirmar que marco anterior foi concluído.

## Checklist de pronto por incremento

1. Requisito e critérios de aceite visíveis em teste, log ou captura real.
2. API/DTO e alterações no schema reviewados.
3. Ownership cross-user e conflitos concorrentes testados.
4. Estado vazio/erro/loading e foco/teclado verificados.
5. Não existe alteração automática de conteúdo/snapshot por sugestão.
6. Documentado o que foi de fato executado e as limitações.

## Evitar falsos marcos concluídos

Este pacote **não** cria frontend React, API, banco, migrations, Worker ou Terraform. Ele fornece contexto e documentação para implementação posterior. O protótipo é somente a referência demonstrativa.
