# Arquitetura, fronteiras e stack

**Base:** PDF pp. 17, 19–20, 25–26, 31; complemento proposto para Mapas de Ideias.

## Tecnologia consolidada

| Camada | Escolha | Observação |
|---|---|---|
| Web | React + TypeScript + Vite + React Router | desktop-first, sem exigir Next.js |
| Estado de servidor | TanStack Query | cache/invalidação/polling; rascunho da edição é estado local |
| Estilos | tokens Notee + Tailwind como utilitário | a identidade não deve ser substituída por biblioteca genérica |
| Editor futuro | Tiptap/ProseMirror | schema JSON validado + IDs estáveis; HTML usa `contenteditable` simples |
| HTTP | ASP.NET Core / .NET 10 e C# 14 baseline do plano | fixar versões realmente instaladas |
| Dados | PostgreSQL + EF Core | schema por módulo, migrações versionadas |
| Trabalho async | Worker host separado + Outbox PostgreSQL + SQS LocalStack | idempotência + lease + checkpoints |
| Cache | Redis **se medição justificar** | opcional no início |
| Infra | Terraform e AWS **exclusivamente emulada** com LocalStack | confirmar cobertura/licença antes |
| Observabilidade | OpenTelemetry em destinos locais | conteúdo privado fora da telemetria |

## Monólito modular, dois hosts

```text
Browser / React
  -> Notee.Api -> Application/Contracts -> Domain -> Infrastructure -> PostgreSQL
                                           |--> Outbox (transação)
OutboxPublisher --> SQS (LocalStack) --> Notee.Worker
Notee.Worker --> Research provider Fake/Live opt-in --> PostgreSQL
Notee.Api / Notee.Worker --> OpenTelemetry --> observabilidade local
```

Não criar um microsserviço por funcionalidade. Domínio não referencia ASP.NET, EF Core, AWS ou SDK de IA; Application coordena casos de uso, portas e política de autorização; Infrastructure faz I/O; API/Worker são composition roots, sem regras repetidas.

## Propriedade dos módulos

- `Identity`: credenciais, sessões, recuperação, preferências e consentimentos.
- `Notes`: notas, blocos, categorias, tags, revisão, ciclo de vida e referências editoriais.
- `Research`: dúvidas, análise, sugestões, jobs, respostas, evidências, orçamento.
- `Publishing`: snapshot, perfil, visibilidade, slug/token e biblioteca pública.
- `KnowledgeMaps` (proposta): Boards, placements, edges e propostas de grafo. Usa contratos de consulta com Notes/Research, **sem modificar diretamente tabelas de outro módulo**.
- `BuildingBlocks/Infra`: relógio, Outbox, Inbox, idempotência, instrumentos e limites técnicos mínimos.

**Regra-chave:** grafo não armazena cópia canônica do corpo da nota nem copia uma dúvida. Persistência de posicionamento/aresta é do `KnowledgeMaps`; a edição de texto segue `Notes`, novas dúvidas seguem `Research`.

## Estrutura sugerida

```text
notee/
  src/Notee.Api/
  src/Notee.Worker/
  src/Notee.BuildingBlocks/
  src/Modules/{Identity,Notes,Research,Publishing,KnowledgeMaps}/
       {Domain,Application,Infrastructure,Contracts}/
  web/src/app/                # rotas, providers, session
  web/src/features/{auth,notes,research,publishing,boards}/
  web/src/shared/ui/          # tokens, componentes
  tests/{Domain,Application,Integration,Architecture}/
  infra/terraform/{core,full-localstack}/
  docs/{adr,runbooks,learning,evaluations}/
  compose.yaml
  .github/workflows/ci.yml
```

Essa árvore é **direção**, não obrigação de criar dezenas de projetos vazios. Começar pequeno, separar quando o limite estiver claro. Propostas de `KnowledgeMaps` exigem ADR.

## Regras de dependência

- `Domain ->` apenas primitivas pequenas e contratos internos ao módulo.
- `Application -> Domain + Contracts` explícitos; sem controller ou DbContext concreto.
- `Infrastructure -> Application/Domain + SDKs` externos; sem depender do frontend/API.
- Consultas de mapa a notas/dúvidas por serviço/porta com owner; nunca `JOIN` irrestrito entre schemas.
- Use eventos de domínio/integração só quando houver uma necessidade demonstrada (ex.: remoção definitiva precisa limpar placements/edges).
