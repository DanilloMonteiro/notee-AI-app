# Notee

[![CI](https://github.com/DanilloMonteiro/notee-AI-app/actions/workflows/ci.yml/badge.svg)](https://github.com/DanilloMonteiro/notee-AI-app/actions/workflows/ci.yml)

Aplicação de anotações com módulo de pesquisa assistida por IA: você marca um trecho de uma nota, registra uma dúvida e um worker em segundo plano consulta a IA (Gemini ou OpenAI) para respondê-la.

| Pasta | Stack | Descrição |
|-------|-------|-----------|
| [`Notee-API/`](Notee-API) | ASP.NET Core (.NET 10), EF Core, PostgreSQL, Serilog | API REST com autenticação JWT, notas, categorias, tags e dúvidas |
| [`Notee-Front/`](Notee-Front) | React 19, Vite, React Router | Interface web |

## Arquitetura

```
 Navegador ──► Notee-Front (:5173) ──fetch──► Notee-API (:5000) ──► PostgreSQL (:5434)
                                                   │
                                                   └─► DoubtProcessingWorker ──► Gemini / OpenAI
```

## Configuração das chaves de IA

As chaves ficam em `Notee-API/src/NoteLab/appsettings.json`. Esse arquivo **não é versionado** (está no `.gitignore`). Crie-o a partir do exemplo:

```bash
cp Notee-API/src/NoteLab/appsettings.Example.json Notee-API/src/NoteLab/appsettings.json
```

Depois preencha `Gemini:ApiKey` e, fora do ambiente local, `Jwt:Key` e `ConnectionStrings:Default` (sem eles a API usa valores de desenvolvimento e avisa no log).

> **Privacidade:** a IA externa só é usada para contas que autorizarem em **Configurações → Usar IA externa**. Sem autorização, as dúvidas recebem uma resposta simulada local (provider Fake), sem enviar dados para fora.

> Faça isso **antes** de rodar o Docker Compose: se o arquivo não existir, o Docker cria uma pasta vazia no lugar dele.

## Rodando com Docker Compose (recomendado)

Pré-requisito: [Docker Desktop](https://www.docker.com/products/docker-desktop/).

```bash
docker compose up -d --build
```

O comando sobe quatro serviços:

| Serviço | O que faz | Endereço |
|---------|-----------|----------|
| `db` | PostgreSQL 16 | `localhost:5434` (usuário `postgres`, senha `admin`, banco `notelab_db`) |
| `migrate` | Aplica as migrations do EF Core e encerra | — |
| `api` | Notee-API | http://localhost:5000/swagger |
| `front` | Notee-Front (build de produção servido pelo nginx) | http://localhost:5173 |

Comandos úteis:

```bash
docker compose logs -f api      # acompanhar os logs da API
docker compose ps               # status dos serviços
docker compose down             # parar tudo (mantém os dados do banco)
docker compose down -v          # parar tudo e APAGAR os dados do banco
docker compose up -d --build api front   # rebuild após alterar código
```

> **Portas em uso:** o Compose usa as mesmas portas do ambiente local (5434, 5000 e 5173). Pare o `dotnet run`, o `npm run dev` e o container `notelab-postgres` antes de subir o Compose, e vice-versa.

### Detalhes do Compose

- A connection string padrão de desenvolvimento é `Host=localhost;Port=5434` (sobrescreva com `ConnectionStrings__Default`). Por isso a API e o `migrate` compartilham a rede do container do banco (`network_mode: service:db`), e o Postgres escuta na porta 5434. Pelo mesmo motivo, a porta 5000 da API é publicada pelo serviço `db`.
- O `appsettings.json` é montado como volume somente leitura e **não** entra na imagem (veja `Notee-API/.dockerignore`).
- O front chama a API em `http://localhost:5000` (`Notee-Front/src/api.js`), e o CORS da API libera `http://localhost:5173`. Mantenha essas portas.

## Rodando localmente (sem Docker para a API e o front)

Pré-requisitos: .NET SDK 10, Node.js 20.19+ (ou 22+) e um PostgreSQL acessível em `localhost:5434`.

1. **Banco de dados** (via Docker, só o Postgres):

   ```bash
   docker compose up -d db
   ```

   Ou use um container avulso:

   ```bash
   docker run --name notelab-postgres -e POSTGRES_DB=notelab_db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=admin -p 5434:5432 -d postgres:16
   ```

2. **Migrations:**

   ```bash
   dotnet tool install --global dotnet-ef --version 10.0.12
   cd Notee-API
   dotnet ef database update --project src/NoteLab/NoteLab.csproj
   ```

3. **API** (na pasta `Notee-API`), na porta esperada pelo front:

   ```bash
   dotnet run --project src/NoteLab/NoteLab.csproj --urls http://localhost:5000
   ```

4. **Front** (em outro terminal):

   ```bash
   cd Notee-Front
   npm install
   npm run dev
   ```

   Acesse http://localhost:5173.

## Testes

```bash
cd Notee-API
dotnet test                                   # testes de domínio (xUnit)

# Suítes HTTP de integração: rode contra uma API apontando para um banco de TESTE
NOTEE_API=http://localhost:5099 node tests/http/run-all.mjs
```

O workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) executa testes de domínio, migrations, as suítes HTTP contra um PostgreSQL efêmero e lint + build do front a cada push e pull request.

## Fila de pesquisa (Outbox + SQS no LocalStack)

Por padrão a tabela `ResearchJobs` é a fila e o Outbox só acorda o worker local (`Messaging:Provider=inprocess`). Para usar SQS **emulado** (nunca AWS real):

```bash
docker compose --profile aws up -d localstack
cd infra/terraform/localstack
terraform init && terraform apply -auto-approve      # cria a fila e a DLQ no LocalStack
# (sem Terraform instalado: docker run --rm -v "$PWD:/work" -w /work hashicorp/terraform:1.12 init/apply -var localstack_endpoint=http://host.docker.internal:4566)
```

Depois rode a API com:

```bash
Messaging__Provider=sqs
Messaging__Sqs__ServiceUrl=http://localhost:4566
Messaging__Sqs__QueueUrl=http://localhost:4566/000000000000/notee-research-jobs
```

O evento é gravado no Outbox na mesma transação do job, publicado no SQS e consumido com deduplicação (Inbox). O worker continua reivindicando jobs com lease/fencing; o polling segue como rede de segurança.

## Operação

- `GET /health/live` e `GET /health/ready` (verifica o PostgreSQL).
- Traces e métricas via OpenTelemetry: defina `OTEL_EXPORTER_OTLP_ENDPOINT` (ex.: `http://localhost:4317`) para exportar; sem isso nada é enviado.
- Erros seguem Problem Details com `traceId` para localizar o log correspondente.
- Limites configuráveis: `RateLimit:AuthPerMinute`, `RateLimit:ApiPerMinute`, `Research:MaxJobsPerHour`, `Research:PollSeconds`.

## Estrutura do repositório

```
.
├── docker-compose.yml
├── Notee-API/
│   ├── Dockerfile
│   ├── CSharpModernLabs.slnx
│   ├── src/NoteLab/
│   │   ├── Modules/
│   │   │   ├── Identity/   # registro, login, recuperação de senha (JWT)
│   │   │   ├── KnowledgeMaps/ # mapas de ideias (boards, posições, conexões)
│   │   │   ├── Notes/      # notas, revisões, categorias, tags e referências
│   │   │   └── Research/   # dúvidas + serviços de IA + worker
│   │   ├── Migrations/
│   │   ├── Shared/
│   │   ├── Program.cs
│   │   └── appsettings.Example.json
│   └── tests/NoteLab.Tests/
└── Notee-Front/
    ├── Dockerfile
    ├── docker/nginx.conf
    └── src/
        ├── pages/
        ├── components/
        └── api.js
```
