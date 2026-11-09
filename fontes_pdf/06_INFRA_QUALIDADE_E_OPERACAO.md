# Fonte integral do PDF — páginas 31–36

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 31 — Ambiente local e perfis

```text
INFRAESTRUTURA E ENTREGA


Ambiente local e perfis de execução
Tudo que representa um serviço AWS no projeto será executado contra o LocalStack. A meta não é
provisionar uma conta AWS real. Nem todos os componentes são serviços AWS: React, API, Worker,
ferramentas de teste e observabilidade continuam sendo software local.

   Perfil proposto       Composição                                         O que demonstra

                                                                            Regras, SQL, auth e experiência sem
                         API e frontend locais; PostgreSQL em container;
   learn-core                                                               depender de IA comercial ou da
                         FakeResearchProvider.
                                                                            emulação completa.

                                                                            Entrega durável e integrações
                         API/Worker + PostgreSQL/Redis locais; SQS, DLQ e
   async-local                                                              selecionadas, com infraestrutura
                         SES no LocalStack; telemetria local.
                                                                            explicitamente mista.

                         Terraform provisiona SQS, SES, S3, Secrets         Alvo de laboratório para APIs AWS e
   aws-full-local        Manager, RDS, ElastiCache, ECR/ECS e ALB no        deploy local. Não comprova
                         LocalStack conforme plano/cobertura.               comportamento integral da AWS.

   research-live (opt-   Um provider real de busca/IA externo, com          Integração real. Não é emulada pelo
   in)                   consentimento e teto de uso.                       LocalStack e pode gerar custo.


O perfil learn-core é um degrau de aprendizado, não um substituto silencioso do alvo LocalStack. Se o plano
não cobrir RDS/ECS/ALB, documentar o bloqueio e manter o marco correspondente pendente, ou aprovar
explicitamente uma alternativa. Não chamar PostgreSQL de Docker de “RDS validado”.

Convenções operacionais
Versões fixadas de imagens; .env.example sem secrets; volumes de dados separados dos dados descartáveis;
binds apenas em loopback quando possível. Documentar nomes de host: localhost no host não significa
localhost dentro do container. API/Worker usam o endpoint acessível pela rede Docker.

 Reset não pode ser surpresa
 Separar comandos de subir, parar, resetar somente infraestrutura temporária e apagar dados. Qualquer destroy com
 volume de banco ou state deve pedir confirmação e explicar o que será perdido.
```

## Página 32 — LocalStack e limites reais

```text
INFRAESTRUTURA E ENTREGA


LocalStack: capacidade e limites reais
Checagem documental em 30/09/2026: as imagens atuais do LocalStack exigem auth token; desde
23/03/2026 há uma imagem unificada e o acesso a serviços depende da licença associada. Não assumir que
todo o desenho AWS seja gratuito ou disponível no plano utilizado. Validar a cobertura de cada operação
antes de fechar a implementação. [R19, R20]

   Serviço            Uso no Notee                          Limite de interpretação

                      Transporte dos jobs e laboratório     Testar operações usadas; emulação não substitui o
   SQS + DLQ
                      de falhas.                            contrato AWS.

                      Recuperação de senha capturada        Mensagem capturada não comprova entrega externa.
   SES
                      localmente.                           [R28]

                                                            Disponibilidade por plano e limitações de
   RDS PostgreSQL     Banco do perfil full-local.           persistência/gestão; não demonstra Multi-AZ real.
                                                            [R22]

                                                            Plano e cobertura; a documentação lista limites em
   ElastiCache        Cache no perfil full-local.
                                                            failover, TLS e outras operações. [R23]

                                                            ECS local executa containers no Docker; não prova
   ECR + ECS          Registry e tarefas API/Worker.
                                                            isolamento ou capacidade real do Fargate. [R24, R26]

                                                            Validar APIs e integração de targets utilizadas; não
   ALB                Roteamento do deploy local.
                                                            afirmar paridade completa. [R25]

                      Exportações/backups de                Objeto local não prova durabilidade da AWS. Não
   S3
                      laboratório, quando adicionados.      forçar upload como feature. [R27]

                                                            Segredo de laboratório não tem garantia equivalente
   Secrets Manager    Exercitar obtenção de configuração.
                                                            ao serviço gerenciado. [R29]


Planos, entitlements e cobertura podem mudar. Antes do marco full-local, produzir uma matriz com operação
AWS, versão do LocalStack, plano, teste executado e limitação observada. A documentação de RDS e
ElastiCache consultada os coloca nos planos Base/Ultimate; verificar a conta real em vez de inferir acesso.
[R22, R23]

 Honestidade no portfólio
 Escrever “AWS emulada com LocalStack; cenários X/Y validados”. Não escrever “produção AWS validada”, “Multi-AZ
 testado” ou “sem custo” sem evidência.
```

## Página 33 — Terraform e deploy local

```text
INFRAESTRUTURA E ENTREGA


Terraform e deploy exclusivamente local
Terraform descreve os recursos; o LocalStack oferece endpoints de emulação. A documentação atual
recomenda o comando lstk terraform ou configuração explícita de endpoints. O wrapper tflocal ainda pode
existir, mas foi sucedido pelo novo fluxo. Fixar a versão escolhida e testar o procedimento no ambiente do
aluno. [R21]

   Etapa de aprendizado     Entrega esperada

                            Subir LocalStack e validar licença. Configurar somente credenciais fictícias AWS e
   1. Bootstrap seguro
                            endpoints locais. O auth token do LocalStack é real e fica fora do Git.

   2. Infraestrutura        Criar fila/DLQ, política de redrive e identidade de e-mail local. Produzir outputs
   mínima                   consumidos pela aplicação.

   3. Imagens               Build local com tag de commit; registrar/push no ECR emulado, conforme cobertura.

                            Adicionar banco/cache emulados, task definitions, serviços e ALB. Executar smoke tests
   4. Full-local
                            em ordem conhecida.

                            Plan revisado, apply controlado, migration em etapa única, health checks e teste de
   5. Evolução
                            rollback de aplicação.

                            Destroy somente do ambiente confirmado. Backup/restore de dados tem procedimento
   6. Limpeza
                            separado.



Proteções contra sair do laboratório
Não fornecer credenciais AWS reais aos scripts. Validar host/endpoint e conta local esperada antes de apply;
uma checagem só do número da conta não substitui a validação de endpoints. Desabilitar busca de
credenciais de metadata e evitar herança de perfis pessoais. Credenciais, tokens e state não entram no
repositório.
Sensitive em Terraform oculta apresentação, mas valores ainda podem constar em state/plan. Preferir
provisionar metadados de secrets e injetar valores de laboratório por etapa controlada. Guardar state em
local protegido; não publicar arquivos de plano como artifacts sem revisão. [R30]

 Não antecipar uma plataforma inteira
 Começar com poucos arquivos legíveis. Extrair módulos Terraform quando houver repetição real. O objetivo é
 conseguir explicar um plan, um output, o state e uma falha de apply, não criar abstrações antes de usar o serviço.
```

## Página 34 — CI/CD enxuto

```text
INFRAESTRUTURA E ENTREGA


CI/CD enxuto e verificável
Iniciar a integração contínua cedo. A primeira versão do pipeline precisa dar feedback rápido e reproduzível,
sem inserir uma ferramenta para cada prática avançada. As etapas abaixo são o contrato do pipeline a
implementar, não arquivos já gerados neste pacote.

   Gatilho                   Etapas obrigatórias

                             Restore com locks; build backend; testes unitários e integração PostgreSQL; npm ci;
   Pull request
                             typecheck/lint/build do frontend.

                             terraform fmt/check + validate sem credenciais reais. Teste de plan no LocalStack em
   Mudanca em infra
                             job dedicado quando configurado.

   Merge / release de        Build de imagens imutáveis; artifact com hashes/versões. Deploy local controlado e
   laboratório               smoke tests.

   Manual / integração       Outbox/SQS/Worker/SES no LocalStack e cenários de falha. Provider de IA Fake, sem
   dedicada                  consumo pago.



O que significa CD neste projeto
Alvo: provisionamento e atualização automatizáveis do ambiente local. Pode haver um job efêmero que cria
LocalStack, aplica infra, sobe a aplicação, testa e destrói tudo. Isso valida a entrega local; não cria um staging
persistente acessível depois que o runner termina.
No computador do aluno, um script ou comando de release aplica a versão aprovada. Não expor runner self-
hosted pessoal a código de PR não confiável. Separar validação sem secrets de operações com token; se
LocalStack exigir CI auth token, usá-lo como secret e respeitar o plano. [R19]

Evoluções posteriores, não bloqueios iniciais
Dependabot, análise de vulnerabilidades, CodeQL, SBOM, assinatura de imagens e ambientes efêmeros por
PR podem entrar quando o pipeline básico estiver compreendido. Não adicionar testes frontend agora.
Segredos fora do Git, permissões mínimas e dependências fixadas continuam sendo baseline, não opcionais.

 Definition of Done de entrega
 Um checkout limpo passa no build/teste, tem instruções executáveis, imagens identificadas por commit e um
 caminho conhecido de rollback. Migration incompatível não é resolvida simplesmente voltando a imagem anterior.
```

## Página 35 — Observabilidade e diagnóstico

```text
OPERAÇÃO


Observabilidade e diagnóstico
Instrumentar API e Worker com OpenTelemetry para logs, métricas e traces enviados a um backend local. O
material original já previa OpenTelemetry e otel-lgtm; preservar essa direção e acrescentar sinais do fluxo de
pesquisa. Não confundir uma tela de diagnóstico ilustrativa com instrumentação real. [B0, p. 6; R17]

   Sinal                                             Pergunta operacional

                                                     A API responde rapidamente para aceitar jobs ou está
   HTTP duration / error rate
                                                     esperando o provider?

                                                     Quantas pesquisas foram aceitas, concluídas, canceladas ou
   research_jobs_total{status,trigger}
                                                     falharam?

                                                     Qual o tempo de conclusão? Separar espera em fila de trabalho
   research_job_duration_seconds
                                                     ativo.

                                                     Há aumento de respostas sem evidência ou falhas de
   research_sources_count / no_source_total
                                                     recuperação?

   outbox_pending / oldest_age_seconds               Eventos ficaram presos antes de chegar ao SQS?

                                                     O gargalo é consumo, indisponibilidade externa ou mensagem
   queue_age / retry_total / dlq_depth
                                                     inválida?

                                                     Quanto foi reportado ou estimado? Distinguir estimativa, valor
   provider_tokens / cost_estimated
                                                     real e uso desconhecido.

                                                     A concorrência e a deduplicação estão funcionando como
   lease_expiration / duplicate_events
                                                     esperado?


Correlação via trace context no envelope; JobId e EventId em logs estruturados com acesso controlado, não
como labels de métricas. Evitar cardinalidade alta por usuário, nota, URL e prompt. Registrar códigos de erro e
provider/modelo controlados, sem guardar texto privado nos spans.

Metas de laboratório propostas
Hipóteses iniciais: aceitação de job p95 inferior a 500 ms sob carga declarada; zero job confirmado perdido
nos cenários de falha testados; toda resposta classificada como Sourced com citações estruturalmente válidas.
Tempo de resposta da pesquisa depende da web/provider e deve ser medido, não prometido.

 Health sem efeito cascata
 Liveness indica processo vivo; readiness indica dependências necessárias para atender aquela operação. Uma falha
 de IA não deve tirar do ar o CRUD de notas. Definir degradação parcial e sinalizar claramente o recurso indisponível.
```

## Página 36 — Escala, busca, custo e recuperação

```text
OPERAÇÃO


Escala, busca, custo e recuperação
Melhorar o gargalo que foi medido
API sem estado de sessão em memória permite múltiplas instâncias; persistir chaves de
proteção/configuração de forma consistente. Worker escala por concorrência limitada e lease. Antes de
aumentar consumidores, verificar pool de conexões, limites do provider e orçamento; backlog não autoriza
gasto ilimitado.

   Tema                 Decisão inicial e evolução

                        Adicionar Redis somente após medir. Cache-aside de recursos simples com owner/version
   Cache
                        na chave; não cachear todas as combinações de busca. Autorização não vem do cache.

                        Começar simples; evoluir para tsvector/GIN e configuração de idioma apropriada. Medir
   Busca textual
                        EXPLAIN ANALYZE e ranking no corpus de notas fictícias. [R18]

                        Roadmap opcional: embeddings/pgvector por owner e revisão, custo de reindexação,
   Busca semântica
                        exclusão e comparação de qualidade com FTS. Não necessária para pesquisar na web.

                        Reservar capacidade atomicamente por conta. Concorrência, consultas, tokens e timeout
   Quota e custos
                        limitados. Valores estimados ficam identificados como estimativas.

                        Proposta: lixeira por 30 dias com prazo visível; evidência bruta por 7 dias quando
   Dados e retenção
                        permitido; revisões/respostas até exclusão da nota ou limite comunicado.



Runbooks que precisam ser exercitados
Fila parada: identificar etapa, restaurar dependência, verificar Outbox e backlog. Provider indisponível: abrir
circuito, limitar retries, avisar usuários e manter notas funcionando. Exclusão: revogar publicação, cancelar
trabalhos e limpar dados associados com tarefa idempotente; evitar ressuscitar conteúdo por resposta tardia.
Backup: exportar PostgreSQL e guardar cópia de laboratório fora do volume de origem; testar restore em
banco separado. Snapshot do LocalStack não substitui esse teste. Registrar RPO/RTO medidos e a política de
expiração dos backups; não prometer eliminação imediata de toda cópia histórica.

 Teste de escala honesto
 Usar dados sintéticos e carga documentada, por exemplo 10 usuários concorrentes e 100 mil notas. Reportar
 máquina, configuração e percentis. Resultado local não é capacidade garantida de produção.
```
