# Fonte integral do PDF — páginas 25–30

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 25 — Concorrência, revisões e idempotência

```text
CONFIABILIDADE


Concorrência, revisões e idempotência
A base já prevê expectedVersion e conflito HTTP 409. Preservar essa semântica e implementá-la na condição
de escrita do banco, não apenas em um if antes de SaveChanges. O EF Core oferece tokens de concorrência e
sinaliza o conflito quando a linha esperada não pode ser atualizada. [B0, p. 5; R09]
  Cliente le v7.
  A envia expectedVersion=7 -> grava v8 + snapshot v8.
  B envia expectedVersion=7 -> nenhuma linha corresponde -> 409.
  A interface preserva o rascunho de B e oferece comparacao.

No PostgreSQL, usar Version BIGINT controlada pela aplicação e configurada como concurrency token; não
transportar automaticamente o rowversion específico de SQL Server. Nota e nova NoteRevision são gravadas
na mesma transação. Salvar conteúdo idêntico pode ser no-op; evitar versões por tecla. [R09]

Autosave seguro, em um recorte posterior do editor
Começar com Salvar explícito. Depois serializar as requisições de autosave por nota, consolidar mudanças
durante uma gravação em andamento e usar a versão devolvida pelo servidor na próxima requisição. Nunca
deixar uma resposta antiga do servidor substituir texto que o usuário digitou depois.

   Situação                            Regra para Idempotency-Key

   Mesma chave + mesmo payload         Devolver a mesma criação/aceite dentro da janela de retenção.

   Mesma chave + payload diferente     409 com código de chave incompatível.

   Duas requisições simultâneas        Constraint única + transação arbitram; memória do processo não basta.

   Recurso já apagado ou acesso        Revalidar autorização; não reproduzir conteúdo privado antigo por cache
   revogado                            de idempotência.


 Não confundir garantias
 Idempotência HTTP evita duplicar a mesma intenção no período configurado. Deduplicação de mensagens evita
 repetir efeitos locais. Nenhuma delas garante, sozinha, cobrança externa exatamente uma vez.
```

## Página 26 — Outbox, SQS e Worker durável

```text
CONFIABILIDADE


Outbox, SQS e Worker durável
A Outbox resolve a falha de duas escritas independentes: Job + evento são confirmados no PostgreSQL;
publicar no SQS ocorre depois. O SQS Standard pode entregar mensagens repetidas, portanto o consumo
deve tolerar duplicatas. Essas são propriedades documentadas; o desenho abaixo é a aplicação proposta ao
Notee. [R10, R11]

   Componente               Responsabilidade e fronteira da transação

                            Reivindicar lote com lease, publicar EventId estável e marcar publicado. Crash após
   OutboxPublisher
                            enviar pode reenviar: comportamento esperado.

                            Consultar inbox; adquirir lease do job por compare-and-swap. Não manter transação
   ResearchConsumer
                            SQL aberta durante chamadas externas.

                            Persistir etapas concluídas. Antes de repetir uma integração, verificar se existe
   Checkpoints
                            resultado persistido ou request remoto consultável.

                            Resposta + status terminal + Inbox(Consumer, EventId) em commit. Somente depois
   Finalização
                            executar DeleteMessage no SQS.

                            Renovar a concessão e o visibility timeout enquanto houver trabalho. Toda gravação
   Lease / fencing
                            tardia valida o fencing token; um Worker antigo não sobrepõe o novo.



Uma autoridade de retry
Proposta para o marco avançado: em falha transitória conhecida, gravar RetryScheduled + NextAttemptAt e
concluir o consumo daquele evento. Um dispatcher persistente cria, quando devido, um novo evento de
tentativa via Outbox. Não combinar isso com republicações independentes em outros componentes.
Redelivery do SQS continua protegendo crashes antes do commit.
Usar backoff com jitter e limites de tentativas/tempo; retries de HttpClient devem ser pequenos e restritos a
operações seguras. DLQ trata mensagens venenosas ou falhas repetidas de consumo; falha funcional do job
permanece visível no banco. Recebimentos SQS e tentativas de negócio são contadores distintos.

 Por que isso não ocupa a API?
 A API apenas valida e confirma o agendamento. O Worker é outro host/processo e executa I/O assíncrono com
 concorrência limitada. Job, checkpoint, lease e resultado ficam no PostgreSQL; SQS é transporte, não o banco interno
 de progresso. Não usar Task.Run no request para trabalho durável.
```

## Página 27 — Laboratório de falhas e garantias

```text
CONFIABILIDADE


Laboratório de falhas e garantias
   Falha provocada              Resultado exigido                                 Evidência a coletar

                                                                                  Linha de Outbox, idade, log
   Fila indisponível após       Job aceito e Outbox pendente; retomar
                                                                                  correlacionado e posterior
   commit                       publicação sem perder o trabalho.
                                                                                  consumo.

   Crash após publicar, antes   Evento pode repetir; um único efeito local        EventId repetido e inbox
   de marcar                    persistido.                                       deduplicada.

   Crash do Worker antes de     Lease expira; outro Worker assume e retoma o      Mudanca de fencing token;
   responder                    último checkpoint confirmado.                     tentativa e etapa registradas.

   Resposta salva, ACK SQS      Redelivery encontra inbox terminal e confirma a   Uma resposta persistida e
   perdido                      mensagem sem recriar resposta.                    redelivery benigno.

                                Resultado externo pode ser desconhecido.
   Provider respondeu;                                                            UnknownOutcome, ID externo
                                Recuperar por request remoto se possível;
   commit falhou                                                                  e decisão de retry auditada.
                                sinalizar risco de repetição/custo.

   Nota editada durante         Resposta ligada ao snapshot original, marcada     NoteRevisionId diferente do
   pesquisa                     como desatualizada.                               atual, sem overwrite.

                                Bloquear próxima chamada; impedir
   Consentimento revogado /                                                       Cancelamento e teste de
                                persistência indevida do resultado; limpeza
   nota apagada                                                                   corrida com resposta tardia.
                                idempotente.

   Provider 429, timeout ou     Erros normalizados; retry limitado ou falha       Tentativas, espera, métrica e
   payload inválido             definitiva. Não rotular como Sem fonte.           status final.



Limites que devem aparecer no README
A garantia pretendida é não perder trabalho confirmado, sob retenção e operação adequadas, e evitar efeitos
locais duplicados. Não há promessa end-to-end de exactly-once em serviços externos. Se um provider não
oferece idempotência ou consulta de resultado, uma falha entre resposta e commit pode exigir repetir a
chamada.
Reprocessar DLQ exige primeiro corrigir a causa e verificar estado/autorização do job. Mover todas as
mensagens de volta à fila sem critério não é um runbook seguro. Guardar limites de retenção e permitir
reconciliação de jobs aceitos que ficaram sem evolução.
```

## Página 28 — Identidade e baseline de segurança

```text
SEGURANÇA E PRIVACIDADE


Identidade e baseline de segurança
Preservar a trilha JWT da base, com ASP.NET Core Identity para usuários, hashing e recuperação. Usar
bibliotecas de token validadas; não escrever criptografia própria. Atenção: os bearer tokens dos endpoints
nativos do Identity não são JWTs padrão. A emissão JWT do laboratório deve ser explícita e separada. [B0, p.
5; R12]

   Risco                    Controle proposto

   Roubo/reuso de           Access token curto em memória; refresh em cookie HttpOnly/Secure, rotativo, hash
   credencial               no banco e família revogável. Nunca localStorage.

                            Validar assinatura, algoritmo permitido, issuer, audience, expiração e chave. Não
   JWT inválido
                            confiar apenas no payload decodificado.

                            Preferir mesma origem local para UI/API; refresh/logout com cookie exigem proteção
   CSRF / origens
                            CSRF. CORS restrito não substitui autorização.

                            Ownership em consultas e comandos, inclusive versões, pesquisas, categorias e
   Acesso por ID alheio
                            publicações.

                            Schema JSON allowlist, renderização segura e sanitização server-side quando houver
   XSS / conteúdo rico
                            HTML. Restringir protocolos de links.

                            Rate limit de login/reset/pesquisa; corpo limitado; secrets fora do Git; diagnóstico por
   Abuso e exposição
                            permissão de operador.



Recuperação desde cedo
Solicitar e-mail com resposta genérica para conta existente ou não. Gerar token de reset curto, específico,
expirable e de uso único efetivo; enviar pelo SES local e nunca devolvê-lo na API pública. Validar
expiração/reuso, alterar senha, invalidar o token e revogar sessões conforme a política. Não logar tokens nem
revelar o cadastro. [R13]
Na trilha inicial, exercitar o fluxo com FakeEmailSender em testes e SES no LocalStack para integração.
Inspecionar a mensagem capturada localmente; isso não demonstra entrega real em uma caixa postal.
Preferir dados fictícios no laboratório. [R28]

 Decisão comparativa para o ADR
 Cookies de sessão com Identity são uma alternativa mais simples para uma SPA de navegador. O JWT aqui preserva o
 objetivo de estudo da base; documente a complexidade adicional, sem apresentar um emissor caseiro como um
 servidor OAuth/OIDC completo. [R12]
```

## Página 29 — IA, dados privados e conteúdo externo

```text
SEGURANÇA E PRIVACIDADE


IA, dados privados e conteúdo externo
Dúvidas podem revelar dados sensíveis. O provider recebe somente o trecho e o contexto necessário, após
consentimento; nunca a conta inteira por conveniência. Uma nota privada no Notee não significa que seja
seguro enviar seu texto a qualquer fornecedor.

   Ameaça                         Controle proposto

   Prompt injection em            Tratar páginas como dados não confiáveis. Ferramentas limitadas; sem acesso a
   página web                     notas alheias, arquivos internos, credenciais ou publicação. [R15]

                                  Bloquear loopback, redes privadas, link-local e metadata endpoints; validar DNS/IP
   SSRF ao buscar URLs
                                  e cada redirecionamento. Restringir esquemas, tamanho e timeout. [R14]

   Exfiltração pelo termo de      Minimizar a consulta, remover identificadores desnecessários, permitir bloqueio
   busca                          externo por nota e revisão do escopo.

   Resposta que executa           Saída estruturada sem ferramentas de escrita. A resposta não pode criar usuário,
   ações                          publicar nota ou consultar secrets.

   Vazamento por                  Logs/traces sem texto, fontes integrais, e-mails, prompts e tokens. Debug com
   observabilidade                conteúdo desabilitado por padrão.

                                  Retenção curta de evidência bruta, respostas/revisões com política explícita,
   Dados persistidos sem fim
                                  exclusão propagada e backups controlados.



Isolamento entre redes
O Worker precisa acessar LocalStack e PostgreSQL, mas a função que recupera páginas não deve poder usar
essa conectividade para ler endpoints internos. Separar cliente/fetcher, aplicar validação de destino e, na
etapa avançada, controles de egress. Um filtro textual na URL não resolve DNS rebinding. [R14]
Antes de habilitar um provider real, registrar quais dados saem, para quem, com qual retenção e quais limites.
Não afirmar conformidade legal ou ausência de retenção sem analisar os termos aplicáveis. A arquitetura
permite trocar o fornecedor, mas a troca precisa respeitar o consentimento anterior.

 Conjunto de avaliação
 Criar casos fictícios com fonte maliciosa, URL interna, afirmação sem suporte, fontes contraditórias e contexto
 insuficiente. Testar o comportamento de contenção; não apenas a qualidade aparente do texto gerado.
```

## Página 30 — Testes de backend e verificação manual

```text
QUALIDADE


Testes de backend e verificação manual
   Suíte              Ferramentas / dependências                 Casos que importam

                                                                 Ciclo de vida, categoria/tag, revisão, âncora e
   Domain             xUnit; sem infraestrutura.
                                                                 matriz de automação.

                                                                 Consentimento, idempotência, limites, contexto,
   Application        Fakes de relógio, providers e portas.
                                                                 cancelamento e roteiro de pesquisa.

                      WebApplicationFactory + PostgreSQL em      Auth, reset, migrations, ownership, concorrência
   Integration
                      Testcontainers.                            real, constraints e contratos HTTP.

                                                                 Outbox, duplicatas, leases, checkpoints, retry,
   Messaging          LocalStack SQS + Worker; suíte dedicada.
                                                                 poison message e DLQ.

                                                                 Domínio sem EF/AWS; acesso entre módulos
   Architecture       Regras simples de dependência.
                                                                 somente por contratos.

   Pesquisa /         Fixtures determinísticas; revisão humana   SourceId inválido, Sem fonte, suporte parcial,
   avaliação          separada.                                  contradição e injection.


Testcontainers permite exercitar integrações contra dependências em containers. Usar PostgreSQL real nos
testes de SQL; um banco in-memory não comprova transações, índices e concorrência do ambiente-alvo.
Isolar bancos/schemas entre testes paralelos. [R16]

Frontend: sem automação de testes nesta etapa
Não adicionar Vitest, React Testing Library ou Playwright ao pipeline do produto agora. Manter build,
typecheck e lint, que não substituem testes de comportamento. Fazer roteiro manual: entrar/recuperar,
editar, marcar palavra/frase, aceitar/ignorar sugestão, alternar automação, visualizar Sem fonte, publicar e
retirar.
Verificar teclado e foco do modal, status anunciados, seleção preservada, erros legíveis, conflito sem perda do
buffer, formulários sem envio duplo e leitura pública sem dados privados. Registrar data, navegador e
resultado do teste manual. A validação visual do HTML entregue não cria uma obrigação de testes
automatizados de frontend no roadmap.

 Sinal de qualidade
 Priorize cenários que protegem dados e garantias. Uma meta arbitrária de 100% de cobertura não substitui testar
 ownership, falhas entre commits e reexecuções concorrentes.
```
