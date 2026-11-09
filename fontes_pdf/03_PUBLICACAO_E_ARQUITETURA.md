# Fonte integral do PDF — páginas 18–20

> **Transcrição de referência, não uma especificação nova.** Conteúdo extraído de `Notee_Planejamento_v1_0.pdf` (30/09/2026). Tabelas preservam o alinhamento aproximado do PDF em blocos de texto. Para especificações operacionais, consulte também `../docs/` e o suplemento de Mapas de Ideias.

## Página 18 — Publicação sem rede social

```text
PRODUTO E EXPERIÊNCIA


Publicação sem rede social
A nota é o rascunho privado; Publication é um snapshot autorizado. O endpoint público nunca serializa
diretamente a entidade Note. Construir um DTO público com allowlist de campos, conteúdo validado e
respostas explicitamente incluídas.

   Visibilidade         Acesso e descoberta

   Private              Sem publicação acessível anonimamente. Leitura autenticada pelo dono.

                        Link com token aleatório opaco. Não aparece na biblioteca/perfil; noindex é uma instrução
   Unlisted
                        de indexação, não segurança.

                        URL por handle/slug; aparece em biblioteca e perfil público de leitura. Sem ranking,
   Public
                        seguidores ou comentários.



Publicar é um comando, não um efeito de autosave
O autor escolhe revisão, título, visibilidade e respostas revisadas que deseja incluir. Copiar esses dados para o
snapshot na transação de publicação. A resposta da IA não entra por padrão; ao incluir conteúdo sem
evidência, preservar o rótulo “Sem fonte” e pedir confirmação informada.
Editar ou restaurar o rascunho não atualiza o publicado. Um novo Publish/UpdatePublication cria nova revisão
publicada. Unpublish revoga o acesso e invalida caches; arquivo/lixeira revogam a publicação nesta versão.
Um link já compartilhado pode ter sido copiado por terceiros; retirada do ar não desfaz cópias externas.

Segurança e concorrência entre módulos
Validar owner e revisão no comando. Na leitura pública, verificar se a publicação permanece ativa antes de
devolver o snapshot; não confiar em um cache antigo para autorizar. Impedir a corrida publicar-versus-excluir
com uma política transacional documentada. Metadados privados, e-mail, prompts e outras perguntas não
incluídas jamais integram o DTO.

 SEO como decisão, não requisito oculto
 A SPA React atende à primeira versão. Pré-visualização de links e indexação completa podem requerer HTML público
 renderizado no servidor ou pré-gerado. Registrar isso como evolução, sem adicionar Next.js ao escopo aprovado.
```

## Página 19 — Arquitetura: limites antes de camadas

```text
ENGENHARIA DO SISTEMA


Arquitetura: limites antes de camadas
Manter um monólito modular, com dois hosts: API e Worker. Módulos de negócio compartilham o processo
HTTP e um PostgreSQL inicialmente, mas possuem contratos e escrita de dados bem definidos. Ter pastas
Domain/Application/Infrastructure, por si só, não estabelece esses limites. A base do NoteNest é preservada e
ganha organização por capacidade de negócio. [B0, pp. 3–4]

   Módulo               Responsabilidade                                Não deve assumir

                        Contas, credenciais, sessões, recuperação e     Conteúdo de notas ou resultados da
   Identity
                        preferências globais.                           pesquisa.

                        Documentos, blocos, categorias, tags,
   Notes                                                                SDK de IA, fila ou integração de pesquisa.
                        revisões e ciclo de vida.

                        Dúvidas, sugestões, jobs, evidências,           Reescrever texto autoral ou publicar
   Research
                        respostas e orçamento.                          automaticamente.

                        Snapshots, slugs, visibilidade, perfil e        Expor a tabela privada de notas como
   Publishing
                        catálogo de leitura.                            resposta pública.

   Infraestrutura       Relógio, transações, Outbox, telemetria e       Um módulo genérico com todas as regras
   compartilhada        primitivas pequenas.                            do sistema.


  Browser / React
    -> Notee.Api -> contratos de aplicacao dos modulos
                      -> PostgreSQL (fonte de verdade)
                      -> Redis (cache seletivo)

  PostgreSQL.Outbox -> SQS / LocalStack -> Notee.Worker
    -> Research -> Search / LLM opt-in -> PostgreSQL

  API + Worker -> OpenTelemetry -> observabilidade local

Domain contém regras sem ASP.NET, EF Core, Redis ou AWS. Application coordena casos de uso e portas.
Infrastructure implementa integrações. Endpoints traduzem HTTP e não decidem regras de negócio. Uma
consulta entre módulos passa por contrato explícito; evitar escrita direta nas tabelas do vizinho.

 O primeiro fluxo não precisa de tudo
 Comece com Notes e testes. API, persistência e identidade entram incrementalmente. Research é a justificativa real
 para a fila e o Worker; o webhook genérico do material original vira exercício opcional posterior.
```

## Página 20 — Solution, dependências e frontend

```text
ENGENHARIA DO SISTEMA


Solution, dependências e frontend
  notee/
    src/
      Notee.Api/
      Notee.Worker/
      Notee.BuildingBlocks/
      Modules/
         Identity/{Domain,Application,Infrastructure,Contracts}/
         Notes/{Domain,Application,Infrastructure,Contracts}/
         Research/{Domain,Application,Infrastructure,Contracts}/
         Publishing/{Domain,Application,Infrastructure,Contracts}/
    web/src/{app,features,shared}/
    tests/{Domain,Application,Integration,Architecture}/
    infra/terraform/{core,full-localstack}/
    docs/{adr,runbooks,learning,evaluations}/
    compose.yaml
    .github/workflows/ci.yml

Começar com poucos projects .NET e namespaces internos por módulo; extrair assemblies quando a fronteira
estiver clara. A árvore acima descreve responsabilidades, não obriga dezenas de bibliotecas vazias no primeiro
commit. Restringir tipos internos e criar testes arquiteturais para dependências proibidas.

   Camada               Pode depender de                             Evitar

                        Primitivas mínimas e tipos do próprio        Frameworks web, SDKs e implementações
   Domain
                        módulo.                                      de persistência.

   Application          Domain e Contracts explícitos.               Controllers ou DbContext concreto.

   Infrastructure       Application/Domain e bibliotecas externas.   API e UI.

   API / Worker         Composition roots e casos de uso.            Regras duplicadas entre hosts.


No frontend: app contém rotas e providers; features separa notes, research, auth e publishing; shared/ui
concentra o design system. TanStack Query cuida do estado remoto, invalidação e polling. O conteúdo ainda
não salvo é estado local do editor, não uma segunda cópia indiscriminada do cache. [R05]
Fixar .NET 10/C# 14 como baseline e usar patches suportados; registrar SDK em global.json, dependências em
arquivos de lock e versões de ferramentas no README. Para React/Vite/Router, escolher uma combinação
estável compatível e verificar a exigência de Node na instalação; não usar latest no CI sem lock. [R02–R04]
```
