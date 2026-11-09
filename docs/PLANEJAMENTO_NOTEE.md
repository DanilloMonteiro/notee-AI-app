# Notee — planejamento consolidado para implementação

**Versão documental deste pacote:** compilação de 09/11/2026. **Fontes:** PDF v1.0 de 30/09/2026 com 51 páginas, README de 05/11/2026 e protótipo HTML v1.2. Os conteúdos antigos do PDF não foram substituídos; Mapas de Ideias e layout v1.2 estão integrados como evolução identificada.

## Produto

**Notee — Anote agora. Entenda depois.** Um espaço para escrever, organizar, marcar dúvidas durante o raciocínio, pesquisar depois com fontes/limites e publicar snapshots intencionais. O produto é generalista e privado por padrão. Uma resposta gerada por IA não altera silenciosamente o que o autor escreveu.

### Funcionalidades do escopo

1. Conta com e-mail/senha, recuperação, autenticação e controles de privacidade.
2. Notas com blocos versionados, uma categoria opcional e múltiplas tags.
3. Seleção manual de palavra/frase como dúvida, histórico da revisão e âncora robusta.
4. Análise assistida para **sugerir** dúvidas/tags/categoria com aceite.
5. Pesquisa web com jobs duráveis, providers substituíveis e resultados separados, citando evidências ou `Sem fonte`.
6. Pesquisa automática configurável por conta e nota, obedecendo consentimento e bloqueios.
7. Busca privada, arquivo/lixeira, revisões reais, conflito de edição e restauração.
8. Publicações de snapshot (`Private`, `Unlisted`, `Public`), perfil e biblioteca de leitura sem rede social.
9. **Mapas de Ideias (extensão v1.1):** coleção de boards privados, mesmos NoteIds em diferentes boards, layouts/conexões independentes, arestas dirigidas com rótulos estruturados, nós satélites de dúvidas, livros/fontes por nota e contexto selecionável de IA. **Aceite manual** de sugestões.
10. **Experiência v1.2:** lista de mapas em cartões, sidebar recolhível, editor/painel do mapa ajustáveis, painel do mapa ocultável e faixa horizontal de dúvidas no editor.

## Pilares de engenharia

| Área | Decisão-base |
|---|---|
| UI | React + TypeScript + Vite + React Router + TanStack Query; design system Notee |
| Editor | Tiptap/ProseMirror proposto, blocos IDs estáveis e schema validado pelo servidor |
| API | ASP.NET Core / .NET 10 baseline; monólito modular (Identity, Notes, Research, Publishing + KnowledgeMaps proposto) |
| Persistência | PostgreSQL/EF Core, notas e revisões imutáveis, transações e concorrência otimista |
| Background | API + Worker, Transactional Outbox, SQS LocalStack, Inbox, lease/fencing, retry e checkpoints |
| IA | Fake determinístico obrigatório; real opt-in mediante capacidades, consentimento, cotas e evidência |
| Infra | Terraform e **AWS somente emulada** no LocalStack; plano/cobertura sujeitos a verificação |
| Qualidade | Testes backend e integração, verificação manual frontend nesta etapa, OpenTelemetry local |

**Sem:** rede social, colaboração simultânea, app mobile, responsividade como requisito atual, pagamento, Kubernetes, microsserviços, embeddings obrigatórios ou deploy AWS real.

## Autoridade das informações

- **PDF v1.0:** requisitos RF01–RF14, decisões originais, marcos M01–M18 e mentoria.
- **README/HTML v1.1 e v1.2:** funcionalidades de mapas e comportamento visual demonstrado.
- **Documentos de contexto novos:** endpoints, banco e testes para mapas são propostas que devem ser confrontadas com o repositório e, quando aceitas, registradas em ADR.
- **Estado real da aplicação:** desconhecido, pois o repositório de produção não foi anexado. Ver `ESTADO_DO_PROJETO.md`.

## Mapa dos documentos detalhados

| Questão | Documento |
|---|---|
| Onde começar e qual versão vale? | `00_COMECE_AQUI_E_PRECEDENCIA.md` |
| Por quê e o quê construir? | `01_PRODUTO_E_ESCOPO.md` + `02_REQUISITOS_E_CASOS_DE_USO.md` |
| Como deve parecer e navegar? | `03_UX_DESIGN_SYSTEM.md` + `UX_V12.md` |
| Como separar os módulos? | `04_ARQUITETURA_E_STACK.md` |
| Qual modelo de dados? | `05_MODELO_DE_DADOS.md` |
| Quais rotas e contratos? | `06_API_E_CONTRATOS.md` |
| Como editar, revisar e ancorar dúvidas? | `07_EDITOR_REVISOES_E_ANCORAS.md` |
| Como pesquisar com IA sem inventar fontes? | `08_PESQUISA_IA_E_FONTES.md` |
| Como não perder jobs nem duplicar efeitos? | `09_JOBS_OUTBOX_IDEMPOTENCIA.md` |
| Quais controles de segurança? | `10_SEGURANCA_E_PRIVACIDADE.md` |
| Como testar e rodar local? | `11_INFRA_TESTES_OBSERVABILIDADE.md` |
| Em qual ordem executar? | `12_PLANO_DE_IMPLEMENTACAO.md` |
| Quais decisões seguem abertas? | `13_ADRS_E_DECISOES_ABERTAS.md` |
| O que exige o módulo de Mapas? | **`MAPAS_DE_IDEIAS.md`** |
| Como preparar contexto e aprovar sugestões de IA no mapa? | **`MAPAS_CONTEXTO_IA.md`** |
| Como instruir uma IA para implementar? | `PROMPT_IA_DESENVOLVEDORA.md` |
| Como instruir uma IA mentora? | `MENTOR_IA.md` |
| O que existe de verdade? | `ESTADO_DO_PROJETO.md` |
| O que foi ou não validado no pacote? | `VALIDACAO_DA_ENTREGA.md` |
| De onde vem cada requisito? | `MATRIZ_RASTREABILIDADE.md` e `../fontes_pdf/` |

## Execução priorizada do módulo de Mapas

1. **MAP-01:** modelar domínio, regras de mesmo owner, Board/Placement/Edge/NoteReference, contratos e testes.
2. **MAP-02:** catálogo + canvas React, com interações equivalentes à referência v1.2.
3. **MAP-03:** ligação real a Notes/Research para referências/dúvidas únicas entre boards.
4. **MAP-04:** selecionar escopo `all/manual`, construir snapshot de contexto validado no backend e exibir prévia.
5. **MAP-05:** propostas de IA com aceite humano, pesquisa/questões via worker durável quando necessário.

Não é necessário atrasar o CRUD de mapas até implementar toda infraestrutura de pesquisa. Em compensação, não enviar contexto a serviços externos antes de identidade, consentimento e proteções adequadas.

## Critério transversal de conclusão

Cada tarefa concluída tem **código real, comandos e evidência de execução**, critérios de aceite satisfeitos, segurança preservada e `ESTADO_DO_PROJETO.md` atualizado. O HTML demonstra UI, mas não é comprovação do backend ou de uso real de IA.
