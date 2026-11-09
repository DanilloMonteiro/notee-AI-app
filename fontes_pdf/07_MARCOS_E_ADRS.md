# Fonte integral do PDF — páginas 37–43

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 37 — Marcos 01 a 03

```text
TRILHA DE IMPLEMENTAÇÃO


Marcos 01 a 03
Sem datas e sem estimativa de horas. Avançar pelo critério de aceite; os marcos são unidades de aprendizado,
não sprints de duração fixa. Cada sessão deve trabalhar apenas uma pequena parte de um marco.

M01 | Base reproduzível
Depende de: Nenhuma. Preparar repositório, .NET, Node, VS Code e convenções. Confirmar acesso, auth
token e cobertura do plano LocalStack antes de assumir o laboratório completo. Registrar versões sem exigir
as mais recentes a cada aula.
Mão na massa: Criar a solution e o primeiro teste de domínio; executar build em checkout limpo. Escrever o
README inicial e uma CI mínima de build/teste backend.
Aceite: O autor explica o papel de cada projeto, executa o teste sem IA e sabe dizer quais serviços locais ainda
não foram validados.

 Orientação da mentoria
 Começar pequeno; não gerar todas as pastas e interfaces de uma vez.



M02 | Domínio de notas
Depende de: M01. Modelar Note, Category, Tag e os estados Active, Archived e Trashed. Definir invariantes
de ownership e o que muda a versão.
Mão na massa: Implementar criação, edição, arquivamento e lixeira com testes. Rejeitar categorias/tags de
outro owner na fronteira apropriada.
Aceite: Os testes mostram transições válidas e inválidas, sem depender de ASP.NET ou banco. RF02 e RF03.

 Orientação da mentoria
 Explicar entidade, value object e serviço de aplicação usando o código que o aluno escreveu.



M03 | API e persistência
Depende de: M02. Introduzir ASP.NET Core, casos de uso, EF Core, PostgreSQL, migrations e Problem Details.
Mão na massa: Persistir notas e taxonomia; testar endpoints com PostgreSQL real em container. Implementar
o compare-and-swap da versão e a primeira chave de idempotência.
Aceite: Duas atualizações simultâneas não se sobrescrevem; erro HTTP é previsível. A API local não é exposta
publicamente antes da autenticação. RF11.

 Orientação da mentoria
 Explicar transação e constraint antes de sugerir abstração de repositório.
```

## Página 38 — Marcos 04 a 06

```text
TRILHA DE IMPLEMENTAÇÃO


Marcos 04 a 06
M04 | Identidade e recuperação
Depende de: M03. Adicionar e-mail/senha e recuperação desde cedo. Separar identidade autenticada de
dados fornecidos pelo cliente.
Mão na massa: Usar Identity para credenciais/reset e implementar a decisão de sessão documentada.
Exercitar reset com FakeEmail e depois caixa local SES; aplicar rate limit e ownership.
Aceite: Usuário A nunca lê/edita dado de B. Resposta de recuperação não enumera contas; token
expirado/reutilizado falha; reset encerra sessões conforme política. RF01.

 Orientação da mentoria
 Não ensinar hashing próprio nem guardar senha/token no frontend.



M05 | Design system e primeira tela
Depende de: M04. Criar React/Vite/TypeScript e transformar tokens e componentes do HTML em
componentes Notee.
Mão na massa: Implementar shell, botões, inputs, badges e listagem de notas. Conectar autenticação e API;
representar loading, vazio, erro e acesso negado.
Aceite: Uma nota criada pela API aparece na interface; foco e navegação por teclado são inspecionados
manualmente. Sem testes automatizados frontend nesta fase.

 Orientação da mentoria
 Distinguir estado de servidor, estado do formulário e estado visual; não copiar app.js inteiro para React.



M06 | Editor, revisões e seleção
Depende de: M05. Adotar editor de blocos com IDs estáveis e revisões completas. Tornar o salvamento
previsível antes de automatizá-lo.
Mão na massa: Começar com salvar explícito; serializar autosave depois. Capturar palavra/frase com posição
UTF-16, quote e contexto. Implementar restauração como nova revisão.
Aceite: Um conflito nunca perde texto local; restaurar não apaga histórico; palavra com emoji/acentos
preserva a seleção. RF04 e RF11.

 Orientação da mentoria
 Explicar por que offsets sozinhos quebram quando o documento muda.
```

## Página 39 — Marcos 07 a 09

```text
TRILHA DE IMPLEMENTAÇÃO


Marcos 07 a 09
M07 | Busca e organização
Depende de: M06. Fechar pesquisa interna, categorias, tags, arquivo e lixeira. Separar essa busca da pesquisa
na internet.
Mão na massa: Implementar filtros, paginação e índices. Evoluir para FTS quando houver corpus para
comparar consultas e relevância.
Aceite: Uma consulta nunca retorna notas privadas de outro owner; filtros combinados funcionam; plano de
execução é registrado com dados sintéticos. RF02, RF03 e RF12.

 Orientação da mentoria
 Não introduzir embeddings nem cache antes de entender a consulta SQL.



M08 | Publicação revisada
Depende de: M07. Implementar snapshot público/não listado independente do rascunho. Manter o produto
sem rede social.
Mão na massa: Criar preview, publicação, atualização explícita e revogação. Renderizar página pública, perfil e
biblioteca de leitura.
Aceite: Editar a nota não altera snapshot; não listada não aparece no catálogo; despublicar/lixeira impede
novas leituras. RF10.

 Orientação da mentoria
 Explicar a diferença entre privado, URL não listada e autorização real.



M09 | Dúvidas e contrato da pesquisa
Depende de: M06. Modelar dúvida, tentativas, fontes, revisão humana e estados sem depender de um
fornecedor de IA.
Mão na massa: Implementar marcação manual, refinamento e contrato 202 + JobId. Usar
FakeResearchProvider e fixtures; manter a aplicação utilizável sem web.
Aceite: O fluxo mostra pendente, concluída, erro, cancelada e Sem fonte; resposta não substitui texto original.
RF04, RF07, RF08 e RF09.

 Orientação da mentoria
 Não usar Task.Run como substituto da etapa durável que vem em seguida.
```

## Página 40 — Marcos 10 a 12

```text
TRILHA DE IMPLEMENTAÇÃO


Marcos 10 a 12
M10 | Sugestões e automação
Depende de: M09. Introduzir análise de texto e sugestões de dúvida, tag e categoria com aceite.
Mão na massa: Implementar precedência conta/nota, bloqueio global e debounce. Começar com análise Fake
versionada; cancelar sugestões obsoletas e evitar duplicatas.
Aceite: Nota Off não executa automaticamente; Inherit segue a conta; sugestão não aceita não vira pesquisa.
Mudança de preferência não dispara backlog antigo. RF05 e RF06.

 Orientação da mentoria
 Explicar diferença entre detectar intenção, confirmar pergunta e autorizar gasto externo.



M11 | Outbox e SQS no LocalStack
Depende de: M09; acesso validado em M01. Tornar persistência do comando e intenção de processamento
atômicas.
Mão na massa: Gravar job e Outbox na mesma transação; publicar no SQS local por dispatcher. Consumir
mensagens duplicadas de forma idempotente e encaminhar falhas permanentes para DLQ.
Aceite: Parar SQS não perde o job confirmado; restaurá-lo esvazia Outbox; duplicata não cria resultado extra.
RF07.

 Orientação da mentoria
 Mostrar primeiro a falha do dual write; depois explicar por que Outbox resolve apenas parte do problema.



M12 | Worker recuperável
Depende de: M10 e M11. Implementar leases, checkpoints, retries, cancelamento e limites de concorrência.
Mão na massa: Executar FakeResearch em etapas duráveis. Injetar falhas após busca, antes do commit e antes
do ACK. Usar fencing token para impedir gravar resultado de lease vencido.
Aceite: Reinício retoma etapa persistida; resultado tardio cancelado não é anexado; timeout externo ambíguo
fica explícito. RF07 e RF13.

 Orientação da mentoria
 Não prometer exactly-once em chamada externa nem manter transação aberta durante a pesquisa.
```

## Página 41 — Marcos 13 a 15

```text
TRILHA DE IMPLEMENTAÇÃO


Marcos 13 a 15
M13 | Pesquisa real com fontes
Depende de: M12. Implementar um provider real por vez atrás dos contratos, com opt-in de dados/custo.
Mão na massa: Buscar na web, validar URLs/citações e sintetizar resposta vinculada a evidências. Registrar
modelo, promptVersion e uso reportado; limitar contextos e orçamento.
Aceite: Sem fonte aparece quando não há evidência; fonte inacessível não é tratada como lida. A troca por
Fake continua possível. RF08 e RF09.

 Orientação da mentoria
 Comparar capacidades dos providers em vez de prometer intercambialidade total.



M14 | Segurança e avaliação
Depende de: M13. Aprofundar a segurança iniciada na autenticação, sem tratar hardening como correção
final de um desenho inseguro.
Mão na massa: Testar SSRF, XSS, prompt injection, acesso cruzado, quotas e revogação. Montar corpus de
dúvidas ambíguas, fontes conflitantes e fontes ausentes.
Aceite: URLs locais/redirecionadas são bloqueadas; nota privada não vaza no prompt/log/público; limitações
da resposta são visíveis.

 Orientação da mentoria
 Separar teste determinístico do pipeline de avaliação de qualidade do modelo.



M15 | Observabilidade e falhas
Depende de: M12; M13 opcional live. Demonstrar o caminho da requisição até o resultado usando traces e
métricas locais.
Mão na massa: Instrumentar API/Worker/HttpClient. Construir painéis de fila, erros, Sem fonte e custo
estimado. Executar os cenários do laboratório de falhas.
Aceite: Um JobId é rastreável sem expor texto privado; idade da fila e Outbox revelam um gargalo; CRUD
segue funcionando com IA indisponível.

 Orientação da mentoria
 Pedir uma hipótese de diagnóstico apenas se o aluno solicitar exercícios em formato de perguntas.
```

## Página 42 — Marcos 16 a 18

```text
TRILHA DE IMPLEMENTAÇÃO


Marcos 16 a 18
M16 | Terraform e AWS full-local
Depende de: M11 e cobertura/plano confirmados. Provisionar somente no LocalStack os serviços AWS
definidos para o laboratório.
Mão na massa: Adicionar SQS/DLQ, SES, Secrets Manager, ECR, ECS, ALB, RDS e ElastiCache conforme
cobertura. Conectar aplicação aos endpoints/outputs locais; S3 somente se houver uso real.
Aceite: Plan/apply/destroy reprodutíveis, smoke tests por serviço e nenhuma credencial/conta AWS real. Se a
licença bloquear, o marco fica explicitamente pendente, sem fingir paridade.

 Orientação da mentoria
 Explicar cada recurso antes de extrair um módulo Terraform. Esta etapa não pode invalidar o aprendizado já feito.



M17 | Entrega, desempenho e restore
Depende de: M15 e M16. Fechar CI/CD leve, experiências de escala e recuperação com evidências
reproduzíveis.
Mão na massa: Testar release local e rollback de imagem compatível; restaurar banco separado. Medir
gargalo e só então adicionar Redis em uma leitura adequada.
Aceite: Um checkout limpo constrói e testa; relátório declara carga/máquina; cache não vaza acesso e invalida
corretamente; restore medido.

 Orientação da mentoria
 Manter frontend em typecheck/lint/build e checklist manual, sem adicionar suíte automatizada agora.



M18 | Evidências e consolidação
Depende de: Marcos anteriores concluídos ou pendências declaradas. Transformar o projeto em um portfólio
honesto que o autor consegue explicar e depurar.
Mão na massa: Revisar ADRs, README, diagramas, limites e demo. Registrar problemas encontrados e
decisões revistas. Atualizar o estado do projeto para outra IA continuar sem inventar progresso.
Aceite: Demonstração une nota, dúvida, fila, resposta, fonte e publicação; uma falha e sua recuperação são
reproduzidas ao vivo.

 Orientação da mentoria
 Evoluções como responsividade, testes frontend, busca semântica ou rede social exigem nova decisão de escopo.
```

## Página 43 — ADRs: decisões que deve defender

```text
APRENDIZADO E EVIDÊNCIAS


ADRs: decisões que você deve defender
ADR é um registro curto de decisão arquitetural: contexto, opções, escolha, consequências e sinal para
reconsiderar. Não precisa de uma tese nem de formalidade para toda linha de código. As decisões abaixo
merecem documentação porque têm alternativas e custos reais.

   ADR                       Decisão inicial / quando reconsiderar

                             Módulos por negócio e API/Worker separados. Reconsiderar serviços quando houver
   001 | Monólito
                             necessidade demonstrada de isolamento ou equipes independentes.

                             React/Vite com .NET separado. Reavaliar SSR/prerender se a descoberta por
   002 | SPA + API
                             buscadores das publicações virar requisito central.

                             Identity + sessão explicitamente desenhada. Comparar cookies e JWT; não chamar
   003 | Identidade
                             token proprietário de JWT.

                             Blocos com IDs e schema versionado. Avaliar migração do schema antes de adicionar
   004 | Conteúdo
                             tipos de bloco.

                             Snapshot separado e revogável. Reavaliar somente se autor aceitar atualização
   005 | Publicação
                             pública automática.

                             Providers com capacidades declaradas; resultado separado com fontes. Integração
   006 | Pesquisa
                             nativa web é adaptador, não contrato do domínio.

                             Outbox + SQS + checkpoints PostgreSQL. Registrar retries, leases e limite da garantia
   007 | Entrega durável
                             externa.

                             PostgreSQL como fonte de verdade. Redis entra depois de medição; embeddings são
   008 | Cache / FTS
                             opção futura.

                             AWS somente emulada. Plano/cobertura são pré-condições do perfil full-local, sem
   009 | LocalStack
                             declarar paridade de produção.

                             Backend automatizado; frontend manual e build tipado agora. Documentar lacuna,
   010 | Qualidade
                             sem afirmar cobertura E2E que não existe.


 Sinal de profundidade
 Conseguir contar qual problema apareceu, por que a alternativa simples deixou de atender, como a escolha foi
 testada e que custo ela trouxe. Um projeto pessoal pode demonstrar isso sem se apresentar como experiência de
 produção que não ocorreu.
```
