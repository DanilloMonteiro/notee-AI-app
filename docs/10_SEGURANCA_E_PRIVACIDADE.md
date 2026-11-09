# Identidade, privacidade, publicação e segurança

**Base:** PDF pp. 13, 18, 28–29; extensão aos mapas proposta neste pacote.

## Conta e sessão

- Usar ASP.NET Core Identity para usuários, hashing e recuperação. Plano prevê emissão JWT explícita para estudo: tokens nativos de Identity **não** são automaticamente JWTs.
- Access token curto em memória da SPA; refresh rotativo em cookie `HttpOnly; Secure`, com hash no banco, família revogável, CSRF em rotas com cookie, CORS restrito. **Nunca** `localStorage` para token.
- Validar assinatura, algoritmo permitido, issuer, audience, `exp`, chave e clock skew.
- Login/reset com rate limiting, respostas genéricas contra enumeração e token de reset específico/expirável/de uso único; nunca devolver token de reset em resposta pública.
- Escolha cookie session vs JWT deve ser defendida no ADR 003; não implementar criptografia ou provedor OAuth caseiro.

## Ownership e autorização

- Cada leitura/escrita privada filtra por owner derivado da sessão: Note, Category, Tag, Doubt, Answer, Board, Placement, Edge, Suggestion, Reference e ContextSnapshot.
- IDs externos ou noteIds selecionados para contexto não são permissão. Validar **todos** os IDs e condições da nota antes de gerar JSON ou chamar IA.
- Para 404, ocultar existência de recursos privados de outra conta. Use FKs compostas/constraints sempre que viável para impedir relações entre donos.
- Publicação e perfil **não incluem mapas privados por padrão**. Nunca gerar endpoint público do grafo sem feature/consentimento específico novo.

## Publicação segura

- Rascunho `Note` continua privado. `Publication` contém **snapshot revisado** com allowlist de campos, `visibility=Private|Unlisted|Public` e seleção explícita de respostas a incluir.
- `Unlisted` usa token opaco e não aparece em catálogo; `noindex` não equivale a controle de acesso. Público usa handle/slug e catálogo de leitura.
- Editar nota/revisão não altera publicação existente. Atualizar publicação cria snapshot novo deliberado; retirar acesso valida estado ativo e invalida caches.
- Arquivo/lixeira revoga publicação nesta versão, restaurar não republica. Não incluir e-mails, outras dúvidas, prompts ou metadados privados no DTO público.

## Pesquisa externa e IA

- Nota privada **não significa** que enviá-la a terceiro é seguro. Enviar somente escopo necessário após consentimento específico para provider/destinação; nada externo por padrão.
- `GlobalBlock`, bloqueio por nota, orçamento, status, autorização e consentimento são revalidados antes de cada chamada externa.
- Conteúdo de páginas web é dado não confiável. Prompt injection não pode ganhar ferramentas de escrita, acesso a outras notas, credenciais, publicação, requests internos ou secrets.
- SSRF: bloquear loopback, redes privadas, link-local e endpoints metadata; validar DNS/IP **e redirecionamentos**, restringir protocolo, tamanho, timeout e egress. Regex de URL não basta contra rebinding.
- Sanitizar/renderizar HTML com allowlist de atributos/tags, evitar XSS e esquemas de link perigosos. Não confiar em `contenteditable` da demo como sanitização do produto real.
- Logs/traces sem texto, prompts, JWT, tokens, e-mails, URLs sensíveis ou fontes completas. JobId/EventId em logs com acesso controlado, não labels de métricas.
- Evidências brutas, revisões, respostas e backups precisam de política de retenção e exclusão auditável. Os prazos sugeridos no PDF são hipóteses (p.ex. lixeira 30 dias, evidência bruta 7 dias).

## Casos críticos para Mapas de Ideias

1. Contexto `all` só contém notas **do owner** e **ativas**, dentro do board autorizado; `selected` também valida membership antes de incluir.
2. Livro/fonte cadastrados pelo usuário são metadados, não uma URL verificada pela IA. Citações reais precisam ser ligadas a evidências recuperadas.
3. Exibir JSON local na UI não autoriza enviá-lo à web/LLM. Ao executar, mostrar escopo, destinatário, consentimento, limites e tamanho estimado.
4. Sugestões de grafo não aplicam CRUD sem aceite e validação transacional. O modelo nunca decide autorização de escrita.
5. Mudanças de dono, exclusão, arquivamento e revogação não devem deixar contextos antigos acessíveis por cache, job ou link.

## Conteúdo de demonstração

O HTML guarda texto em `localStorage`, sem criptografia de aplicação, e simula login/pesquisa. Usar somente dados fictícios nesse protótipo. A presença da palavra “Privado” na tela não equivale a autenticação/autorização real.
