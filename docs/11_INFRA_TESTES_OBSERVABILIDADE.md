# Infraestrutura local, testes, CI/CD e observabilidade

**Base:** PDF pp. 30–36 e 42. As referências LocalStack do PDF foram consultadas em 30/09/2026; **reconfirmar** versão/plano/API durante implementação.

## Perfis de execução do plano

| Perfil | Objetivo | Componentes |
|---|---|---|
| `learn-core` | regra e API real sem serviços AWS completos | React/API locais, PostgreSQL em container, `FakeResearchProvider` |
| `async-local` | entrega assíncrona real | API/Worker, PostgreSQL/Redis locais, SQS/DLQ/SES no LocalStack, telemetria |
| `aws-full-local` | emulação AWS mais ampla | Terraform para SQS/SES/S3/Secrets Manager/RDS/ElastiCache/ECR/ECS/ALB quando suportado |
| `research-live` opt-in | consulta externa real | provider de busca/IA com consentimento e limite de custo; não é emulado pelo LocalStack |

LocalStack não é AWS de produção; RDS/ALB/ECS e recursos podem depender de plano, versão e suporte. Não chamar um PostgreSQL simples em Docker de RDS validado. Testar operações realmente usadas antes de fechar marco.

## Terraform seguro

- Credenciais AWS de testes fictícias e endpoints explicitamente locais; auth token LocalStack real guardado fora do Git.
- Evitar herança de credenciais pessoais e chamadas à metadata; checar endpoints antes de `plan/apply/destroy`.
- `terraform state` e `plan` podem conter segredos mesmo com `sensitive`; nunca publicar artefatos sem revisão.
- Fluxo: bootstrap → SQS/DLQ/SES → imagens ECR/ECS quando disponíveis → RDS/ElastiCache/ALB conforme cobertura → smoke tests → rollback → destroy controlado.
- Separar apagar infraestrutura temporária de apagar volumes/dados persistidos. Backup e restore de PostgreSQL em banco separado, não apenas snapshot LocalStack.

## Testes exigidos do backend

| Nível | Proposta |
|---|---|
| Domain (xUnit) | estado da nota, tags/categoria, âncora, matriz de automação, regras do grafo |
| Application (fakes) | permissões, cota, idempotência, contexto, sugestão aceita/ignorada, cancelamento |
| Integration (`WebApplicationFactory` + Testcontainers PostgreSQL) | auth/reset, migrations, SQL/constraints, transações, conflito real, isolamento cross-owner |
| Messaging (LocalStack + Worker) | Outbox, duplicatas, lease/fencing, retry, checkpoints, DLQ, crash e retomada |
| Architecture | Domain sem EF/AWS, módulo `KnowledgeMaps` não escreve direto no schema Notes |
| Pesquisa/avaliação | sourceId inválido, Sem fonte, evidência parcial, contradição, prompt injection/SSRF |

**Frontend:** o PDF opta por não introduzir testes automatizados de frontend nesta fase. Exigir `typecheck`, `lint`, `build` e checklist manual com navegador/data/resultado, incluindo foco/teclado, resizers, faixa horizontal e canvas. Não afirmar que o protótipo equivale a teste funcional React.

## CI/CD

PR: restore/build/test backend; npm ci + typecheck/lint/build frontend. Infra: `terraform fmt`, `validate` e job LocalStack só quando configurado. Release de laboratório: imagem versionada/hash, migrations em etapa controlada, smoke tests e rollback compatível. Não conectar deploy a AWS real sem nova decisão explícita.

## Observabilidade

`HTTP duration/error rate`, `research_jobs_total{status,trigger}`, duração incluindo espera em fila, `sources_count/no_source_total`, idade da Outbox, `queue_age`, retries/DLQ, tokens/custo estimado, expiração de leases e eventos duplicados. Correlacionar JobId/EventId em logs estruturados; métricas com labels de baixa cardinalidade.

Metas do PDF são **hipóteses de laboratório**: aceite de job p95 inferior a 500 ms sob carga declarada, zero jobs confirmados perdidos nos cenários de falha testados, citações estruturalmente válidas em `Sourced`; não são SLA.

## Operação e recuperação

- Liveness de processo separada de readiness; falha de provider não deve derrubar CRUD de notas.
- Fila parada → inspecionar job/outbox/SQS/lease, restaurar dependência e reprocessar com segurança; não “replay all DLQ”.
- Provider indisponível → retries com limite, circuit breaker, status claro e editor operacional.
- Backup/restore em banco distinto, registrar RPO/RTO medidos e política de retenção; dados fictícios para carga.
- Mapas: além dos dados da nota, incluir boards/placements/edges/suggestions/references no plano de backup e restore; testar não gerar ligações órfãs após restauração/exclusão.
