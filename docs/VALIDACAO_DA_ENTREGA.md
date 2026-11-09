# Validação e limitações desta entrega de contexto

**Objetivo:** registrar exatamente o que pode e o que não pode ser inferido ao usar os documentos Markdown.

## Conteúdo conferido

- [x] PDF disponibilizado no pacote tem **51 páginas** e foi identificado como `v1.0, 30/09/2026`.
- [x] O PDF foi transcrito em arquivos `fontes_pdf/` por grupos de páginas e extraídos os 30 hyperlinks R01–R30 para um índice adicional para leitura/checagem, preservando texto e alinhamento aproximado de tabelas.
- [x] Requisitos RF01–RF14 e 18 marcos M01–M18 resumidos nos documentos de contexto, com indicação de páginas de origem.
- [x] README fornecido descreve a extensão Mapas v1.1 e mudanças visuais v1.2.
- [x] HTML fornecido contém recursos de catálogo, canvas, placements, arestas direcionadas, referências, dúvidas satélites, contexto `all/manual` e sugestões simuladas.
- [x] Dois PNGs fornecidos foram incluídos como referência visual; correspondem a catálogo de mapas e editor com faixa horizontal de dúvidas.
- [x] Contexto de IA separado em schema demonstrado vs propostas de backend.
- [x] Documentos identificam pontos não decididos, riscos de segurança e requisitos de aceite sugeridos.

## Não foi realizado/confirmado

- [ ] Implementação real de backend/API, banco/migrations ou React no repositório do usuário.
- [ ] Autenticação, isolamento de contas, web search, IA real, publicações externas ou SQS/LocalStack em execução.
- [ ] Testes do produto, CI/CD ou deploy reais; o pacote documental não os substitui.
- [ ] Prova de equivalência de todos os serviços LocalStack à AWS ou acesso/licenciamento atual do usuário.
- [ ] Conversão perfeita de tabelas do PDF em Markdown nativo: a transcrição guarda alinhamento aproximado em blocos de texto, e os documentos temáticos reconstroem as tabelas principais manualmente.
- [ ] Inclusão de screenshots `PROTOTIPO_V12_CANVAS.png` e `PROTOTIPO_V12_RESPOSTA_EXPANDIDA.png`: são citados pelo README original, mas **não foram anexados** nesta conversa. O HTML permite estudar as telas correspondentes.
- [ ] Disponibilidade dos arquivos `docs/*.md` antigos referenciados pelo README original; não estavam anexados e os novos `.md` deste pacote são **recriações informadas** pelo PDF/README/HTML.

## Divergência documental relevante

O README original diz que a versão atualizada do planejamento PDF tem **60 páginas**, com suplemento v1.1. O PDF efetivamente fornecido tem **51 páginas**, versão 1.0. Assim, o pacote usa o PDF recebido como base histórica e o README + HTML v1.2 para o requisito de mapas. Não apresenta o suplemento ausente como lido.

## Testes que o projeto real deve apresentar

- ownership e concorrência com PostgreSQL de teste;
- navegação/UX do editor e canvas com checklist manual;
- Worker/Outbox/lease e injeção de falhas;
- contexto do mapa filtrado e consentimento externo;
- sugestões aceitas/ignoradas idempotentes;
- validação de citações e estado `Sem fonte`.

**Todo resultado de validação deve indicar o comando e a evidência; não trocar “especificado” por “implementado”.**
