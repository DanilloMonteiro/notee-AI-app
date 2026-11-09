# Runbook operacional — Notee (ambiente local / laboratório)

> Escopo: execução local com Docker Compose. Não há deploy em AWS real (ADR-009). Comandos validados em 09/11/2026 — ver "Evidências".

## 1. Subir, verificar e parar

```bash
docker compose up -d --build              # db + migrate + api + front
curl -s localhost:5000/health/ready       # "Healthy" quando o PostgreSQL responde
docker compose logs -f api                # logs (Problem Details trazem traceId para correlacionar)
docker compose down                       # para tudo, mantém dados
```

Variáveis obrigatórias fora do ambiente local: `Jwt__Key` (≥ 32 bytes), `ConnectionStrings__Default`. Sem elas a API sobe com valores de desenvolvimento e avisa no log.

## 2. Release

1. Garanta CI verde no commit (`.github/workflows/ci.yml`: testes, migrations, suítes HTTP nos modos `inprocess` e `sqs`, lint/build do front e build das imagens).
2. Crie a tag: `git tag vX.Y.Z && git push origin vX.Y.Z`.
3. **Antes** de aplicar migrations, faça backup (seção 4).
4. Publique: `docker compose up -d --build` (o serviço `migrate` aplica as migrations e termina antes de a API subir).
5. Confirme `/health/ready` e um fluxo básico (login, abrir nota, abrir mapa).

## 3. Rollback

1. Volte o código: `git checkout vANTERIOR` e `docker compose up -d --build api front`.
2. Se a release incluiu migration **incompatível** com a versão anterior, restaure o backup feito antes da release (seção 4) em vez de tentar "desfazer" migrations em produção.
3. Migrations deste projeto são aditivas na maior parte; o `Down` existe, mas trate-o como último recurso e só após backup.

## 4. Backup e restore

```bash
PGPORT=5434 scripts/db-backup.sh notee-db notelab_db backups/antes-da-release.dump
PGPORT=5434 scripts/db-restore.sh backups/antes-da-release.dump notelab_restaurado notee-db   # nunca sobrescreve
PGPORT=5434 scripts/db-verify.sh notelab_db notelab_restaurado notee-db                      # compara contagens
```

Para trocar para o banco restaurado, aponte `ConnectionStrings__Default` para `notelab_restaurado` e reinicie a API. Guarde backups fora da máquina.

## 5. Pesquisa / fila

- Jobs travados: o worker recupera leases vencidos sozinho (2 min). Consultar: `SELECT "Status", count(*) FROM "ResearchJobs" GROUP BY 1;`
- Outbox pendente: `SELECT count(*) FROM "OutboxMessages" WHERE "ProcessedAt" IS NULL;` (Attempts ≥ 10 exige investigação do broker).
- SQS (modo `sqs`): mensagens que falharem 5× vão para `notee-research-jobs-dlq`.
- Cota: `Research:MaxJobsPerHour` por conta (429 ao enfileirar acima do limite).

## 6. Incidentes comuns

| Sintoma | Verificar |
|---|---|
| `/health/ready` = Unhealthy | PostgreSQL no ar? connection string? |
| Todos recebem 401 | `Jwt__Key` mudou (tokens antigos invalidados) — usuários precisam entrar de novo |
| Redefinição de senha falha após recriar contêiner | volume `notee-dpkeys` montado? |
| Respostas sempre "[Resposta simulada]" | esperado sem consentimento ou sem `Gemini:ApiKey` (ADR-019) |

## Evidências (09/11/2026, máquina local, Docker Desktop)

- `tests/load/smoke-load.mjs` (20 usuários × 10 rodadas, API de teste local): 800 requisições, 0 erros, ~449 req/s; p95: POST /notes 87 ms, GET /notes 27 ms, busca 32 ms, GET /boards/{id} 54 ms. Número de laboratório, não capacidade de produção.
- Backup do banco de teste (103 KB) restaurado em banco novo; `db-verify.sh` com contagens idênticas em 9 tabelas; restore sobre banco existente recusado.
- Imagens `runtime`, `migrate` e `front` construídas localmente; contêiner da API respondeu `/health/ready = Healthy`.
