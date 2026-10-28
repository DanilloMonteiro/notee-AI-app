# CSharpModernLabs

API de anotações feita com ASP.NET Core, .NET 10 e PostgreSQL.

## Pré-requisitos

- .NET SDK 10
- PostgreSQL local ou Docker
- `dotnet-ef` versão 10.0.12 para aplicar as migrations

## Iniciar o projeto

Execute os comandos abaixo a partir da pasta raiz do repositório.

1. Inicie o PostgreSQL. Se usar Docker, este comando cria o banco e publica a porta esperada pela aplicação:

   ```bash
   docker run --name notelab-postgres -e POSTGRES_DB=notelab_db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=admin -p 5434:5432 -d postgres:16
   ```

   Se o contêiner já existir, inicie-o com `docker start notelab-postgres`. Se usar uma instalação local do PostgreSQL, configure-a para aceitar a conexão `Host=localhost;Port=5434;Database=notelab_db;Username=postgres;Password=admin`, definida atualmente no projeto.

2. Restaure as dependências do .NET:

   ```bash
   dotnet restore
   ```

3. Instale a ferramenta do Entity Framework Core, caso ainda não esteja instalada:

   ```bash
   dotnet tool install --global dotnet-ef --version 10.0.12
   ```

4. Crie ou atualize as tabelas do banco usando as migrations do projeto:

   ```bash
   dotnet ef database update --project src/NoteLab/NoteLab.csproj
   ```

5. Inicie a API:

   ```bash
   dotnet run --project src/NoteLab/NoteLab.csproj
   ```

Quando o terminal informar o endereço em que a aplicação está ouvindo, abra esse endereço no navegador e acrescente `/swagger` para acessar a documentação interativa da API.
