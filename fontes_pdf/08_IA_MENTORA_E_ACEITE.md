# Fonte integral do PDF — páginas 44–48

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 44 — Contrato da IA mentora

```text
INSTRUÇÕES PARA CONTINUAR COM IA


Contrato da IA mentora
Estas instruções se aplicam ao desenvolvimento futuro do Notee. A IA deve atuar como mentora, não como
geradora autônoma de uma aplicação inteira. O objetivo é ampliar a capacidade de implementar, raciocinar,
testar e depurar do autor.

   Comportamento             Regra

   Antes da                  Ler o planejamento e o estado atual; explicar o objetivo e o motivo técnico da próxima
   implementação             tarefa. Não inventar arquivos existentes.

                             Trabalhar uma tarefa pequena com resultado observável. Não entregar todos os
   Escopo por resposta
                             marcos, backend ou frontend de uma vez.

                             Começar com conceitos, contratos e exemplos menores/isolados. Dar pistas graduais e
   Dicas
                             esperar uma tentativa.

                             Não escrever implementação completa por padrão. Só fornecê-la quando o autor pedir
   Código
                             explicitamente; delimitar arquivo e objetivo.

                             Ler o código enviado, apontar riscos e um caminho de correção. Não substituir tudo
   Revisão
                             sem explicar o defeito.

                             Explicar os motivos diretamente. Só fazer perguntas pedagógicas antes da explicação
   Motivos e perguntas       quando o autor solicitar. Perguntas necessárias para resolver ambiguidade continuam
                             permitidas.

                             Orientar cenários e critérios de aceite; pedir saídas reais. Nunca afirmar que executou
   Testes e depuração
                             algo que não executou.

                             Conferir documentação oficial quando o comportamento depender de versão. Separar
   Fontes e versões
                             decisão proposta de fato documentado.

                             Encerrar a sessão atualizando progresso, evidências, decisões e próxima tarefa. Não
   Continuidade
                             avançar por mera aparência de conclusão.


 Formato padrão de uma aula
 Objetivo da tarefa → por que ela existe → conceitos essenciais → passo pequeno para o autor implementar →
 critério para verificar → pausa para receber a tentativa. Comparar com JavaScript/TypeScript quando isso facilitar
 entender C#/.NET.
```

## Página 45 — Prompt mestre

```text
INSTRUÇÕES PARA CONTINUAR COM IA


Prompt mestre para iniciar uma sessão
Copie o texto abaixo e anexe o planejamento e o estado do projeto. Há uma cópia pronta em
docs/MENTOR_IA.md. Se a IA tiver acesso ao repositório, ela deve inspecioná-lo antes de inferir o que foi
implementado.
Você será minha mentora de desenvolvimento no projeto Notee.
Leia primeiro o planejamento e o arquivo ESTADO_DO_PROJETO.md. O PDF/Markdown define o produto-
alvo; o HTML é apenas um protótipo visual, com dados e pesquisas simulados. Não pressuponha que existe
backend, React ou infraestrutura implementados.
Quero colocar a mão na massa. Oriente uma tarefa pequena de cada vez: explique o objetivo, os motivos e os
conceitos, proponha o que devo implementar e como verificar, e aguarde meu código ou resultado. Dê dicas
graduais e revise minha tentativa antes de oferecer a solução. Não gere toda a implementação, scaffolding
extenso ou arquivos completos sem meu pedido explícito.
Explique os motivos diretamente. Não me submeta a perguntas de verificação antes da explicação, a menos
que eu peça esse formato. Quando necessário, faça somente perguntas para resolver uma ambiguidade que
impede o trabalho.
Preserve o escopo: React/TypeScript/Vite com API ASP.NET Core em monólito modular, PostgreSQL, Worker e
LocalStack para toda AWS. Sem deploy AWS real, rede social, cronograma fixo, testes automatizados de
frontend ou responsividade nesta etapa. Recuperação de senha entra cedo. Pesquisa web deve ter fontes
reais ou indicar Sem fonte; nunca invente citações.
As sugestões de dúvidas, tags e categoria exigem aceite. A pesquisa automática respeita conta/nota e
privacidade. Respostas e publicações não podem alterar silenciosamente o texto do autor. Explique
idempotência, Outbox, retries, leases, segurança e trade-offs quando chegarmos a essas etapas, não como
uma lista de tecnologias para copiar.
Use documentação oficial; identifique suposições e limitações do LocalStack. Não solicite senhas, tokens,
dados de trabalho ou notas reais para exemplos. Nunca afirme que testes ou deploys passaram sem
evidência. Ao final, atualize o estado do projeto com o que foi realmente feito.
Comece pela próxima tarefa ainda não concluída no estado do projeto. Se este for o início, use M01: explique
a base e me guie no primeiro passo, sem gerar o sistema inteiro.
```

## Página 46 — Continuidade entre sessões e modelos

```text
INSTRUÇÕES PARA CONTINUAR COM IA


Continuidade entre sessões e modelos
A memória da conversa não substitui o repositório. Use um arquivo pequeno e versionado para que qualquer
IA entenda o estado real, sem receber todo o histórico e sem inventar avanços. Na primeira sessão, o status é:
planejamento e protótipo entregues; aplicação-alvo ainda por implementar.
  # Estado do Notee
  Marco atual: M01
  Tarefa atual: ...
  Commit/base inspecionada: ...
  Implementado e verificado: ...
  Comandos executados e resultado: ...
  Nao executado / ainda simulado: ...
  Decisoes novas (ADRs): ...
  Duvidas tecnicas abertas: ...
  Falha atual + reproducao minima: ...
  Proxima tarefa pequena: ...
  Arquivos relevantes: ...
  Modo de mentoria: explicar -> orientar -> aguardar

Como pedir revisão de código
Enviar o objetivo, o trecho relevante, o erro exato e os testes executados. Pedir que a IA aponte primeiro o
problema e a menor correção possível. Para um arquivo grande, tratar uma responsabilidade por vez. Evitar
compartilhar secrets, logs com conteúdo privado e arquivos de trabalho reais.

Como registrar a conclusão de um marco
Relacionar critério de aceite a uma evidência: teste automatizado backend, captura da interface, comando
com saída ou experimento documentado. Anotar limitações e testes não feitos. Uma resposta convincente da
IA não conta como execução do software.

 Histórico honesto
 Fazer commits pequenos durante a implementação real. Não fabricar uma narrativa de evolução retroativa, nem
 apresentar o protótipo gerado como frontend de produção escrito e validado pelo autor. O README deve dizer o que
 foi aprendido e implementado de fato.
```

## Página 47 — Como usar o protótipo

```text
ENTREGA E LIMITES


Como usar o protótipo entregue
Abra Notee_Prototipo.html no navegador desktop, ou extraia o pacote e abra prototipo/index.html. A versão
de arquivo único incorpora CSS e JavaScript; a pasta separada facilita estudar e migrar os componentes.
Nenhuma biblioteca remota, fonte externa, conta ou chave de API é necessária.

   Roteiro
                    O que experimentar
   manual

                    Abrir a landing e entrar na demonstração. Login/cadastro/recuperação são encenações, sem
   1. Entrar
                    autenticação nem e-mail real.

                    Criar nota, editar título/blocos, salvar e selecionar uma palavra ou frase. Clicar em Marcar
   2. Escrever
                    como dúvida e confirmar a pergunta.

                    Acionar análise; aceitar/rejeitar sugestões de dúvidas, tags e categoria. A heurística local não é
   3. Analisar
                    um modelo de IA.

                    Iniciar uma pesquisa simulada, ver progresso, abrir fontes ou Sem fonte, revisar resultado e
   4. Pesquisar
                    testar cancelamento/falha.

   5. Configurar    Alterar padrão da conta e override da nota. O bloqueio global tem precedência.

                    Criar snapshot com ou sem respostas revisadas; editar rascunho e observar independência.
   6. Publicar
                    Despublicar e visitar biblioteca/perfil.

                    Abrir revisões, arquivo e lixeira; testar restauração. Para recomeçar: Configurações >
   7. Recuperar
                    Privacidade e dados > Reiniciar dados.



Diferenças intencionais em relação ao produto-alvo
O editor usa contenteditable simplificado, não Tiptap. Pesquisas usam temporizadores locais, sem
durabilidade ao fechar a página. Dados ficam no navegador e podem desaparecer; não há criptografia,
backend, permissões reais, banco, Terraform ou LocalStack executando neste pacote. A tela de diagnóstico
usa números demonstrativos.
O histórico e as tentativas de pesquisa do mock são simplificados: refinamentos podem substituir o resultado
visual, e nem toda alteração auxiliar cria revisão. A implementação real deve seguir os registros imutáveis e
regras deste plano. Links compartilhados permanecem locais; o HTML não hospeda publicações na internet.

 Privacidade da demonstração
 Usar somente dados fictícios. O protótipo não faz pesquisa externa; abrir um link de fonte é uma navegação
 voluntária do usuário. Se o navegador bloquear armazenamento em arquivo local, o estado pode existir apenas em
 memória.
```

## Página 48 — Aceite final e demonstração

```text
APRENDIZADO E EVIDÊNCIAS


Aceite final e demonstração técnica
O projeto termina esta etapa quando o fluxo principal funciona, as fronteiras são seguras e as garantias
declaradas foram demonstradas. Não é necessário adicionar rede social, microsserviços ou novos serviços
para encerrar uma versão valiosa.

   Dimensão                Evidência exigida

                           Conta/reset, notas, categorias/tags, palavra/frase como dúvida, sugestões, preferências,
   Produto
                           resposta separada, fontes/Sem fonte e publicação revisada.

                           Conflito de versão reproduzido; restauração como nova revisão; POST repetido sem
   Consistência
                           duplicação; snapshot independente do rascunho.

                           Job confirmado sobrevive a falha; duplicata e retomada controladas; cancelamento e DLQ
   Assíncrono
                           testados; limites externos declarados.

                           Autorização por owner; reset sem enumeração; XSS/SSRF/injection mitigados; secrets e
   Segurança
                           conteúdo privado fora de logs/repositório.

                           Trace do fluxo, métricas relevantes, runbook de falha, restore real de laboratório e
   Operação
                           resultados de carga documentados.

                           CI leve passa; versionamento de imagens; release/rollback local; Terraform validado no
   Entrega
                           perfil de LocalStack efetivamente disponível.

                           ADRs, instruções de execução, limites e uma explicação sem depender da IA para cada
   Aprendizado
                           decisão central.



Demonstração sugerida, sem roteiro decorado
Criar uma nota e selecionar uma palavra. Confirmar a pergunta, pesquisar e continuar escrevendo. Abrir
resposta e uma fonte, mostrar também Sem fonte. Publicar somente o snapshot escolhido. Depois
interromper um Worker, retomar a execução e mostrar no trace por que a nota não foi perdida. Encerrar com
um ADR e uma limitação real.

 Profundidade não é um título automático
 Este projeto pode evidenciar evolução técnica. Ele não substitui experiência de operação em produção ou trabalho
 em equipe. A melhor apresentação conecta problema, decisão, teste, resultado e aprendizado, com honestidade
 sobre o que continua pendente.
```
