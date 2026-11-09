# AGENTS.md — instruções de contexto do Notee

Este arquivo é adequado para ferramentas de programação que leem automaticamente `AGENTS.md`. **Antes de copiá-lo para um repositório existente, mescle-o com as instruções locais em vez de sobrescrevê-las.**

## Fonte de verdade e escopo

- Consulte `docs/00_COMECE_AQUI_E_PRECEDENCIA.md` e `docs/ESTADO_DO_PROJETO.md` no início de cada tarefa relevante.
- Contexto consolidado: `docs/PLANEJAMENTO_NOTEE.md`. Para código de mapas: `docs/MAPAS_DE_IDEIAS.md`, `docs/MAPAS_CONTEXTO_IA.md`, `docs/UX_V12.md`, `docs/05_MODELO_DE_DADOS.md` e `docs/06_API_E_CONTRATOS.md`.
- O PDF incluído é v1.0 com 51 páginas. A feature Mapas de Ideias v1.1 e as telas v1.2 vêm do README e do HTML da demonstração. Não declarar que o backend existe por causa do protótipo.
- As especificações de novos endpoints e DDL de grafo são **propostas**, não migrations ou interfaces já aprovadas.

## Padrões de engenharia

- Frontend React/TS/Vite/Router/TanStack Query, UI fiel aos tokens do Notee.
- Backend ASP.NET Core/.NET 10 baseline, monólito modular, EF Core e PostgreSQL, Worker/Outbox/SQS LocalStack quando necessário.
- Módulos separados por negócio; não copiar conteúdo de Note dentro de Board. Mesmo NoteId em vários boards, com placements e edges independentes.
- Isolar dados por owner, preservar revisão/snapshot, controlar concorrência/idempotência.
- Nenhum dado enviado à IA externa sem consentimento e escopo; sugestões do mapa exigem aceite humano individual e revalidação.
- Não inventar links verificados, resultados de teste, deploys ou status de implementação.

## Processo

1. Inspecionar repositório e dependências realmente presentes.
2. Planejar mudanças menores com critérios de aceite.
3. Modificar arquivos apenas quando autorizado para implementar, preservando alterações existentes.
4. Executar testes pertinentes e relatar **somente os que foram realmente executados**.
5. Registrar decisões novas em ADR e atualizar `docs/ESTADO_DO_PROJETO.md` com evidência de commit/execução.

**Modo de trabalho:** por padrão, o PDF configura IA como mentora (ver `docs/MENTOR_IA.md`); para pedidos explícitos de programação direta, adotar `docs/PROMPT_IA_DESENVOLVEDORA.md`.
