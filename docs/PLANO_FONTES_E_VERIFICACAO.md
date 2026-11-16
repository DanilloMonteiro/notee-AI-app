# Plano — Fontes: biblioteca, citações, arquivamento e verificação

**Status:** implementação e validações unitárias/build concluídas em 16/11/2026; validação HTTP de integração aguarda PostgreSQL e LocalStack disponíveis.
**Data:** 13/11/2026. Relacionado: [PLANO_COMENTARIOS_E_SUGESTOES.md](PLANO_COMENTARIOS_E_SUGESTOES.md) (abas do editor: Dúvidas | Comentários | Fontes).

## Contexto

O usuário quer uma aba **Fontes** ao lado de Dúvidas e Comentários. Ela lista as fontes da nota (livro, artigo, vídeo, pesquisa, pesquisador, documento, site…), ligadas às **citações no texto**, com a localização exata: **página** no livro, **minuto** no vídeo, seção no documento.

Quando possível e permitido, o Notee **arquiva o documento**. Também faz um **“check”** da fonte e, se houver problema, **argumenta o porquê**.

**O que existe hoje (verificado)**

- **`NoteReference`** (`Modules/Notes/Domain/NoteReference.cs`, tabela `NoteReferences`, ADR-016):
  - tipos `Book | Source`;
  - campos título, autor, ano, editora e URL http(s);
  - sem página e sem ISBN;
  - aparece só no inspetor do mapa (`features/boards/BoardInspector.jsx`);
  - não é publicada (ADR-026);
  - é lida por `GraphContextService`.
- **`ResearchSource`:** fontes devolvidas pela IA nas dúvidas, um conceito separado.
- **Fontes declaradas dos gráficos:** ficam no próprio bloco.
- **Armazenamento de arquivos:** não existe. Só Postgres e SQS no LocalStack.
- **SSRF:** hoje o servidor **nunca** busca URLs do usuário (ADR-025: “SSRF não se aplica”). Esta feature passa a buscar, então a regra de `docs/10_SEGURANCA_E_PRIVACIDADE.md` §SSRF entra em vigor.
- **Princípios de `docs/08_PESQUISA_IA_E_FONTES.md`:**
  - não inventar fontes;
  - “fonte encontrada” ≠ “fonte que sustenta a afirmação”;
  - nunca mostrar certeza em percentual;
  - referências declaradas ≠ evidência verificada.

**Decisões do usuário**

| Tema | Decisão |
| --- | --- |
| Organização | **Biblioteca de fontes** reutilizável e **citações** por nota (trecho + localização). |
| Arquivo | **Cópia só se a licença for aberta** ou o arquivo for **enviado pelo usuário**. Nos outros casos, **snapshot no Internet Archive**. Limite de **10 MB por arquivo e 200 MB por conta**. |
| Armazenamento | **S3**: LocalStack no local e no CI; S3 ou compatível em produção. |
| Verificação | **3 camadas**: existência e metadados; trecho na cópia arquivada; IA opcional com custo. Nunca afirma “verdadeira” com certeza; sempre mostra os argumentos e as evidências. |
| Quando verificar | **Automaticamente ao cadastrar**, em job de segundo plano. A camada de IA só sob demanda. |
| Cadastro | Colar link, DOI ou ISBN; selecionar trecho e citar; detectar citações no texto; enviar o próprio PDF. |
| Publicação | As fontes aparecem como **bibliografia** na publicação. Arquivos privados **nunca** são publicados. |

**Entrega em 4 etapas:**

1. **F1:** biblioteca, citações, aba Fontes, migração do `NoteReference` e bibliografia publicada.
2. **F2:** preencher metadados a partir de link, DOI ou ISBN; detectar citações; verificação de existência e metadados (job); snapshot no Internet Archive.
3. **F3:** S3, upload, cópia de acesso aberto, extração de texto e checagem do trecho na página ou seção.
4. **F4:** análise por IA com argumentos (custo e consentimento).

As quatro etapas foram implementadas em uma alteração consolidada, conforme orientação do usuário para criar um único commit e fazer o push ao final. As migrations continuam separadas por capacidade.

## Modelo de dados (migrations por etapa)

**`Sources`** — biblioteca, por dono
- `Id`, `OwnerId`.
- `Kind`: `book | article | video | study | dataset | document | website | person | interview | other`.
- `Title` (até 300), `Authors` (JSON, até 20 nomes), `Year`, `Venue` (editora, periódico ou canal), `Language`.
- Identificadores: `Doi`, `Isbn` (normalizado para 13 dígitos), `Url` (http/https).
- `License`: `unknown | public-domain | cc-* | open-access | proprietary`.
- `Notes`, `CreatedAt`, `UpdatedAt`.
- Índices únicos parciais `(OwnerId, Doi)` e `(OwnerId, Isbn)`, para evitar duplicatas.

**`SourceCitations`** — liga a nota à fonte
- `Id`, `OwnerId`, `NoteId` (cascade), `SourceId` (restrict: uma fonte citada não pode ser apagada sem antes remover as citações).
- Âncora opcional: `BlockId`, `ExactQuote` (até 500), `StartIndex`, `EndIndex`. Sem âncora, a citação vale para a nota inteira.
- Localização:
  - `LocatorKind`: `page | pages | timestamp | section | paragraph | none`;
  - `LocatorText` (ex.: “p. 45–47”, “12:34–13:10”);
  - valores interpretados: `PageStart`, `PageEnd`, `TimeStartSec`, `TimeEndSec`.
- `SourceQuote` (até 2000): o que a fonte diz literalmente, opcional e usado na checagem.
- `CreatedAt`.

**`SourceArchives`** (F3)
- `Id`, `SourceId`, `Kind` (`file | snapshot`), `StorageKey` (S3), `ContentType`, `SizeBytes`, `Sha256`.
- `Basis`: `public-domain | cc | open-access | user-upload`.
- `OriginUrl`, `SnapshotUrl`, `TextKey` (texto extraído por página, em JSON no S3), `CreatedAt`.

**`SourceChecks`**
- `Id`, `SourceId`, `CitationId?`.
- `Layer`: `existence | citation | ai`.
- `Status`: `Pending | Confirmed | FoundWithDifferences | QuoteNotFound | Mismatch | NotFound | Unverifiable | Failed`.
- `Summary`, `Arguments` (JSON `[{point, evidence, link?}]`), `Provider`, `CheckedAt`.
- Tokens e custo, só na camada de IA.

**Publicação:** `PublicationRevisions.CitationsJson`, uma cópia imutável das citações e dos metadados públicos (com os status das checagens) no momento de publicar. A bibliografia não muda sem uma nova publicação.

**Migração do legado:** cada `NoteReference` vira um `Source` (Book → `book`, Source → `website` ou `document`) mais uma `SourceCitation` sem âncora. Os endpoints antigos e o `BoardInspector` passam a usar a nova API. A tabela antiga é removida numa etapa posterior, depois de conferida. `GraphContextService` passa a ler as citações.

## Etapa F1 — Biblioteca, citações e aba Fontes

**API (dono)**
- `GET/POST /sources`, com busca e filtro por tipo e status.
- `GET/PATCH/DELETE /sources/{id}` (409 se a fonte estiver citada).
- `GET/POST /notes/{id}/citations`.
- `PATCH/DELETE /citations/{id}`.
- **Validações:** URL http(s); DOI `10.\d{4,9}/…`; ISBN-10/13 com dígito verificador; página ≥ 1 e fim ≥ início; tempo em `mm:ss` ou `hh:mm:ss`.

**Editor (`pages/Note.jsx`)**
- Terceira aba **Fontes**, com destaque **verde** (`source-mark`) e número da referência. A lógica de destaque é a mesma generalizada no plano de comentários; `locateQuote` acompanha o trecho quando o texto muda.
- **“Citar trecho”:** seleciona o texto, busca ou cria a fonte e preenche a localização. O campo muda conforme o tipo: livro → página(s); vídeo → minuto (mm:ss); artigo ou documento → seção ou página. O “trecho da fonte” é opcional.
- O painel lista as citações agrupadas por fonte, com localização, selo de verificação e link (vídeo abre no minuto, por exemplo `&t=754s`).

**Biblioteca**
- Página `/sources` (item **Fontes** no menu): tabela com tipo, título, autores, ano, notas que citam e status.

**Publicação**
- No editor e na página pública, o texto citado ganha número sobrescrito [1], e a seção **Referências** fica no fim.
- A formatação é próxima da ABNT: autor, título, ano, localização, link e snapshot.
- Mostra o selo da verificação.
- Arquivos `user-upload` nunca aparecem.

## Etapa F2 — Preencher metadados, detectar citações e verificar existência

**`POST /sources/resolve` `{input}`**
- Identifica DOI, ISBN, YouTube ou URL e consulta:
  - **DOI** → Crossref `api.crossref.org/works/{doi}`;
  - **ISBN** → Open Library `openlibrary.org/isbn/{isbn}.json`;
  - **YouTube** → oEmbed (título e canal);
  - **URL** → `<title>`, `citation_*`, OpenGraph e `<link rel=license>`.
- Devolve um formulário preenchido para a pessoa conferir. Não salva nada.

**Detectar citações (front, `features/sources/detectCitations.js`, regras locais)**
- Padrões: `(Sobrenome, 2017)`, `Sobrenome (2017)`, `Fonte(s): …`, links, DOI, ISBN, “segundo o IBGE”.
- Gera sugestões “criar fonte e citação” para aceitar uma a uma, ligadas ao trecho.
- Teste com o texto de referência: “Fonte: IBGE, série histórica do IPCA.” e “Barbosa Filho — FGV, 2017”.

**Verificação de existência e metadados (camada 1, automática)**
- Ao criar ou alterar uma fonte, grava `SourceCheck(Pending)` e uma mensagem no **Outbox**. O novo worker `SourceCheckWorker` segue o padrão de lease, retry e idempotência do ADR-023/028.
- Resultados possíveis:
  - **Confirmada (existência):** o identificador existe e os metadados batem.
  - **Divergência:** argumentos do tipo “Crossref lista ano 2016; você informou 2017”, “o autor registrado é X”.
  - **Não encontrada:** DOI ou ISBN inexistente; link 404 ou 410 sem snapshot.
  - **Não verificável:** sem identificador nem link (ex.: entrevista, livro sem ISBN), com a explicação.
- **Snapshot:** consulta a disponibilidade no Wayback (`archive.org/wayback/available`). Se não houver e `Sources:Wayback:SaveEnabled=true`, pede “Save Page Now” e guarda o `SnapshotUrl`.

**Saída de rede segura (`Shared/Net/SafeHttpClient`)**
- Resolve o DNS e **bloqueia** loopback, redes privadas, link-local, metadados de nuvem e IPv6 locais. Revalida a cada redirecionamento (no máximo 5).
- Só `http/https` nas portas 80 e 443. Timeout de 10 s. Lê no máximo **10 MB**.
- Content-type permitido: html, pdf e texto.
- User-Agent genérico. O e-mail de contato do Crossref e do Unpaywall vem **da configuração** (`Sources:ContactEmail`), **nunca** do usuário.
- Só os **metadados da fonte** saem do Notee, nunca o texto da nota.
- Cota: `Sources:ChecksPerDay`, padrão 200 por conta.

## Etapa F3 — Arquivamento e checagem do trecho

**S3**
- Bucket `notee-source-archives` via Terraform no LocalStack (`infra/terraform/localstack`, junto do SQS), `AWSSDK.S3` e `docker-compose` com `SERVICES: sqs,s3`. Em produção, configurável (S3 ou compatível).
- Arquivos privados por dono.
- Download só por **URL pré-assinada curta** (5 min) para o dono. Nada é publicado.

**Upload: `POST /sources/{id}/files` (multipart)**
- PDF e texto. Verifica o tipo pelos bytes iniciais (*magic bytes*).
- **Limites:** 10 MB por arquivo e 200 MB por conta. A cota é checada antes de gravar. Acima dela → 413 ou 409 com mensagem clara.
- `Basis=user-upload`.

**Cópia de acesso aberto (automática, só com licença permitida)**
- Para DOI, consulta o Unpaywall (`api.unpaywall.org/v2/{doi}`). Com `is_oa` e licença `cc-*` ou domínio público, baixa o PDF de acesso aberto (≤ 10 MB) pelo `SafeHttpClient`.
- Páginas com `rel=license` Creative Commons guardam o texto extraído.
- Nos outros casos, **só snapshot e link**, com o motivo exibido: “licença não permite cópia”.

**Extração de texto**
- PDF por página com **PdfPig** (Apache-2.0; licença a confirmar no ADR). HTML vira texto. Resultado gravado em `TextKey`.

**Checagem do trecho (camada 2, automática quando há texto arquivado e trecho)**
- Procura `SourceQuote` (ou o trecho citado na nota) normalizado: primeiro na página ou intervalo indicado, depois no documento inteiro.
- Resultados possíveis:
  - **Confirmada (trecho):** encontrado literalmente na página citada.
  - **Encontrado com diferenças:** similaridade alta, com as diferenças mostradas, ou encontrado em **outra página** (argumento: “aparece na p. 52, não na p. 45”).
  - **Trecho não localizado:** mostra o que foi procurado e onde.
  - **Não verificável:** sem cópia; vídeo sem transcrição.
- Para vídeo, a pessoa pode colar a transcrição do intervalo como “trecho da fonte”. Não há transcrição automática nesta versão.

## Etapa F4 — Análise por IA (opcional, com custo)

- `POST /citations/{id}/ai-check/estimate` e `POST /citations/{id}/ai-check` (`confirm:true`). Seguem o padrão do “Organizar com IA”: `IAiService.CompleteAsync`, consentimento (ADR-019), `AiPricing`, cota `Ai:SourceCheckMaxPerHour`, Fake sem custo.
- **Entrada:** a afirmação da nota (trecho citado e contexto curto) e o **excerto da fonte** em volta da localização (do texto arquivado, até 6 mil caracteres). Sem texto arquivado → “Não verificável” sem chamar a IA.
- **Saída:** JSON `{verdict: supports|partially|contradicts|insufficient, arguments:[{point, sourceExcerpt}]}`.
- **Proteção contra invenção:** todo `sourceExcerpt` precisa ser **substring literal** do excerto enviado. Senão, a resposta é recusada (422) e nada é gravado.
- A interface mostra “Segundo a análise por IA (pode errar)…”, com os argumentos e os trechos da fonte lado a lado. Nunca mostra “verdadeira”, “falsa” ou percentual.

## Interface de status (todas as camadas)

- **Selo da fonte:** pior status relevante entre as camadas. Cores: verde = confirmada, âmbar = diferenças ou divergência, vermelho = não encontrada, cinza = não verificável ou pendente.
- **Painel da fonte:**
  - metadados;
  - **arquivo:** cópia (motivo da licença) / snapshot / só link;
  - histórico de checagens com **argumentos** e links de evidência (registro Crossref, página do snapshot, página do PDF);
  - botões “Verificar de novo” e “Analisar com IA (custo)”.
- Texto fixo: “Verificação automática não garante veracidade; confira as evidências”.

## Segurança, privacidade e direitos

- SSRF tratado pelo `SafeHttpClient`, com testes. ADR-025 atualizado.
- Nada de texto da nota em chamadas externas nas camadas 1 e 2. Na camada 4 só com consentimento e confirmação.
- Cópias só com base legal registrada (`Basis`). Uploads privados. Remover fonte ou conta apaga os objetos no S3.
- Limites: tamanho e cota, checagens por dia, timeouts. Métricas OTel sem conteúdo.

## Testes

- **`node --test`:** `detectCitations` (com o texto de referência), interpretação de localização (página e minuto), formatação de referências e `locateQuote` para citações.
- **xUnit:**
  - DOI e ISBN (dígito verificador), localização;
  - `SafeHttpClient`: IPs bloqueados, redirecionamento para rede privada, limite de tamanho;
  - comparação de metadados (divergência);
  - checagem de trecho (página certa, outra página, diferenças, ausente);
  - validação da resposta de IA (substring obrigatória);
  - cota de armazenamento.
- **Suítes HTTP** com **servidores falsos locais** para Crossref, Open Library, oEmbed, Wayback e Unpaywall (URLs base configuráveis, nenhuma chamada real):
  - `sources.test.mjs`
  - `citations.test.mjs`
  - `source-checks.test.mjs`
  - `source-files.test.mjs` (LocalStack S3)
- **Chrome:**
  - colar DOI/ISBN e conferir o preenchimento;
  - citar trecho com página e minuto;
  - aba Fontes verde;
  - detectar citações no texto de referência;
  - upload de PDF e checagem de trecho;
  - selos e argumentos;
  - bibliografia na publicação.

## Documentação

- ADR-040 (biblioteca e citações; substitui o ADR-016).
- ADR-041 (arquivamento: base legal, S3, limites).
- ADR-042 (verificação em camadas e argumentos; IA com substring obrigatória).
- Atualizar ADR-025 (SSRF) e ADR-026 (bibliografia publicada).
- Atualizar `05_MODELO_DE_DADOS.md`, `06_API_E_CONTRATOS.md`, `10_SEGURANCA_E_PRIVACIDADE.md` e `ESTADO_DO_PROJETO.md`.

## Recomendações aplicadas e limites

- Mantidos os tipos de fonte do modelo proposto. Instituição e ORCID para pessoas pesquisadoras ficam para uma evolução do modelo.
- Bibliografia em estilo próximo da ABNT, sem alegar conformidade formal.
- “Save Page Now” permanece desligado por padrão (`Sources:Wayback:SaveEnabled=false`); a consulta por snapshots existentes continua ativa.
- Transcrição automática de vídeo fica fora desta versão. A pessoa pode registrar trecho e minutagem manualmente.
- PdfPig `0.1.16` declara licença Apache-2.0 em seu `.nuspec` local.

## Estado de validação (16/11/2026)

- Implementação F1–F4, migrations, telas, armazenamento privado, resolver, detecção local, checagem por worker e análise Fake/IA foram adicionados.
- Suítes `source-checks.test.mjs` e `source-files.test.mjs` usam provedores falsos em `127.0.0.1`; não consultam serviços reais. O perfil `SourceTests` e o CI apontam os provedores para esse servidor.
- Builds e testes unitários/frontend executados nesta rodada estão detalhados em `docs/ESTADO_DO_PROJETO.md`. As suítes HTTP foram verificadas sintaticamente, mas não executadas: Docker Desktop não estava disponível e as portas locais de PostgreSQL/LocalStack estavam fechadas. Elas só serão consideradas aprovadas quando executadas contra serviços descartáveis.
