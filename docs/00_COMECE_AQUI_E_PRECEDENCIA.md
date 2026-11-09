# Notee — contrato de contexto e hierarquia de fontes

**Finalidade:** dar a uma IA desenvolvedora ou mentora contexto suficiente para evoluir o Notee sem inventar código, status de implementação ou decisões não aprovadas.

## Fontes efetivamente recebidas

1. `../referencias/Notee_Planejamento_v1_0.pdf`: **51 páginas**, documento de produto e engenharia, datado de 30/09/2026. É a base de requisitos RF01–RF14 e marcos M01–M18.
2. `../referencias/Notee_Prototipo_v1_2.html`: arquivo HTML autocontido, contendo CSS e JS, que demonstra as telas e funcionalidades. **Não** é backend nem aplicação React; persistência via `localStorage` no navegador e IA simulada.
3. `../referencias/README_original_v1_2.md`: descrição do pacote de 05/11/2026, com Mapas de Ideias v1.1 e refinamentos visuais v1.2.
4. `../referencias/PROTOTIPO_V12_LISTA_MAPAS.png` e `../referencias/PROTOTIPO_V12_EDITOR.png`: referência visual, não prova de execução em produção.
5. `../fontes_pdf/*.md`: transcrição por páginas do PDF anexado para conferência de detalhes, tabelas e fontes.

## Atenção ao conflito de versões

O README recebido **afirma** que outro PDF do pacote teria 60 páginas e conteria um suplemento v1.1. O PDF **efetivamente anexado** nesta conversa tem 51 páginas e identifica-se como v1.0. Portanto **não** afirme que o suplemento está no PDF recebido. Para Mapas de Ideias e UX v1.2, baseie-se no README, HTML e capturas; as propostas técnicas complementares deste pacote são explicitamente identificadas como propostas.

## Níveis de autoridade

- **Decisão consolidada:** presente no PDF original ou descrita como comportamento intencional no README/protótipo fornecido.
- **Comportamento demonstrado:** observável no HTML, mas sem garantia de persistência real, API ou controle de acesso.
- **Proposta de engenharia:** schema, API, regras de concorrência ou teste sugeridos neste pacote para implementar a decisão; exige validação no código real/ADR.
- **Lacuna:** requisito ainda não fechado ou implementação não verificada. Não preencher por inferência silenciosa.

## Regras operacionais da IA

1. **Antes de alterar código**, inspecione repositório, branch, arquivos, migrations e testes reais; leia `ESTADO_DO_PROJETO.md`. Não assuma que código já existe porque aparece no HTML.
2. Preserve o nome **Notee**, a proposta *Anote agora. Entenda depois.*, o caráter privado por padrão e a autonomia do texto escrito pela pessoa.
3. Planejamento-base: **React + TypeScript + Vite + React Router + TanStack Query**, tokens Notee/Tailwind; **ASP.NET Core/.NET 10 com monólito modular**, PostgreSQL e EF Core, Worker + SQS via LocalStack, Terraform local, OpenTelemetry.
4. Não invente fontes, pesquisa web, status de deploy, testes executados, certificação legal ou garantias `exactly-once`.
5. Autorizações: sugestões não alteram notas/mapas sem aceite; dados só são enviados a provider externo com consentimento e escopo explícito; publicar depende de snapshot revisado.
6. Requisitos técnicos propostos pelos arquivos de `docs/` que não constam das fontes devem ser registrados em ADR se aceitos.

## Arquivos para cada tarefa

| Tarefa | Contexto principal |
|---|---|
| Entender produto/escopo | `01_PRODUTO_E_ESCOPO.md`, `02_REQUISITOS_E_CASOS_DE_USO.md` |
| Fazer tela React | `03_UX_DESIGN_SYSTEM.md`, `UX_V12.md`, HTML de referência |
| Backend e persistência | `04_ARQUITETURA_E_STACK.md`, `05_MODELO_DE_DADOS.md`, `06_API_E_CONTRATOS.md` |
| Editor e notas | `07_EDITOR_REVISOES_E_ANCORAS.md` |
| Pesquisa e IA | `08_PESQUISA_IA_E_FONTES.md`, `09_JOBS_OUTBOX_IDEMPOTENCIA.md` |
| Mapas e grafo | **`MAPAS_DE_IDEIAS.md`** e **`MAPAS_CONTEXTO_IA.md`** |
| Segurança/entrega | `10_SEGURANCA_E_PRIVACIDADE.md`, `11_INFRA_TESTES_OBSERVABILIDADE.md` |
| Próxima implementação | `12_PLANO_DE_IMPLEMENTACAO.md`, `ESTADO_DO_PROJETO.md` |

**Não carregue todos os arquivos a cada interação.** Leia o índice e apenas os módulos envolvidos. Se um detalhe faltar, consulte a transcrição da página original.
