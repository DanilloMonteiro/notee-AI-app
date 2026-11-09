# Consistência, Outbox, SQS, Worker e recuperação

**Base:** PDF pp. 25–27, 35, 40. Princípio: trabalho confirmado precisa sobreviver à falha, mas **não** há garantia de exatamente-uma-vez ponta a ponta em serviços externos.

## Transação de agendamento

```text
POST pesquisar
  -> validar usuário, dúvida, versão e políticas
  -> transação PostgreSQL:
       insert ResearchJob(status=Queued, snapshot=contexto)
       insert OutboxMessage(EventId estável, JobId, tipo, schema, trace)
       reservar orçamento se aplicável
     COMMIT
  -> HTTP 202 + Location

OutboxPublisher:
  -> reivindicar lote pendente (lease)
  -> enviar evento ao SQS Standard (LocalStack)
  -> marcar publicado (pode falhar e reenviar o mesmo EventId)

Worker:
  -> deduplicar via Inbox(ConsumerName, EventId)
  -> obter lease/fencing no job
  -> executar checkpoint sem transação SQL aberta durante I/O externo
  -> validar autorização/cancelamento antes de cada chamada externa
  -> salvar resposta + status + Inbox em um único commit
  -> deletar mensagem do SQS após commit
```

SQS Standard é **at least once**, duplicatas fazem parte do contrato. SQS não é banco de progresso. Não fazer `Task.Run` em request HTTP para simular durabilidade.

## Concorrência e idempotência HTTP

- `expectedVersion` ou ETag deve participar do UPDATE real do PostgreSQL (compare-and-swap); versão divergente resulta `409`, buffer do editor preservado.
- `Idempotency-Key` para comandos criadores de pesquisa e aceites: mesma chave + mesmo payload → mesmo resultado dentro de retenção; mesma chave + payload diferente → `409`.
- Resolver concorrência com UNIQUE/transação, não só `lock` em memória do host.
- Revalidar acesso antes de servir resposta idempotente armazenada; recurso privado revogado não deve ser devolvido por replay.
- Deduplicação de mensagem e idempotência HTTP são mecanismos diferentes. Ambos protegem efeitos locais, não garantem custo único externo.

## Lease, checkpoint e retry

- Worker atualiza job apenas se seu fencing token/lease ainda for válido. Renovar lease e visibility timeout na execução longa.
- Checkpoint registra etapas já concluídas. Após crash, outro worker retoma o último estado confirmado; recuperar remote request quando provider suportar.
- **Uma autoridade para retry**: status `RetryScheduled` + `NextAttemptAt` no banco; dispatcher recria evento por Outbox quando devido. Backoff com jitter e limites; DLQ para poison message.
- `UnknownOutcome`: provider pode ter respondido, mas commit falhou. Sem API remota de consulta/idempotência, não prometer que chamada/custo não se repetirão.
- Cancelamento cooperativo: revogar consentimento ou arquivar nota impede **próxima** chamada; não desfaz chamada já enviada. Resultado atrasado não deve sobrescrever dados de nota cancelada/excluída.

## Falhas obrigatórias de laboratório

| Falha forçada | Garantia/evidência |
|---|---|
| SQS indisponível após COMMIT | Job e Outbox pendentes permanecem; dispatcher retoma depois |
| Crash depois de publish, antes de marcar Outbox | mesma mensagem pode repetir; Inbox elimina efeito local duplicado |
| Crash Worker durante execução | lease expira; worker novo retoma checkpoint com outro fencing |
| ACK ao SQS perdido após salvar resposta | redelivery benigno; uma única resposta local |
| Provider respondeu mas commit falhou | status UnknownOutcome e decisão de retry explícita |
| Nota editada durante pesquisa | Answer aponta revisionId original; interface marca desatualizado |
| Consentimento revogado/nota excluída | bloquear próximas chamadas e impedir resultado tardio indevido |
| 429, timeout, payload inválido | normalizar erro e aplicar retry limitado; nunca rotular `Sem fonte` |

## Relação com mapas

- Movimentar nó e criar ligação são comandos de CRUD transacional; **não** exigem SQS por padrão.
- Ações **IA do mapa** (`perguntar`, análise externa, geração de propostas) reutilizam agendamento durável e as proteções Research/Outbox.
- Aceitar `GraphSuggestion` deve ser atômico: marcar aceita + criar Note/Edge + registrar auditoria; chamadas repetidas de aceite não duplicam recurso.
- Contexto serializado para a IA representa **snapshot versionado**; editar nós/arestas depois da pergunta não altera retroativamente o contexto do job.
