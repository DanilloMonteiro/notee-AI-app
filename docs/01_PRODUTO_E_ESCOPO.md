# Notee — produto, princípios e escopo

**Base:** PDF pp. 4–8, 11, 18; README de 05/11/2026; HTML demonstrativo.

## Problema e propósito

Ao escrever, surgem perguntas que interrompem o fluxo. O Notee permite capturar uma palavra/frase como dúvida com contexto, continuar escrevendo e voltar à pesquisa e a suas fontes depois. O autor controla texto, organização e publicação. Não é um mecanismo de certificação automática de verdade.

**Ciclo principal:** criar conta → criar e editar uma nota em blocos → organizar por categoria/tags → marcar uma dúvida manualmente ou aceitar sugestão → escolher pesquisar (ou automatizar segundo autorização) → acompanhar job → revisar resposta separada com fontes/limites → opcionalmente publicar um snapshot autorizado.

**Extensão v1.1/v1.2 — Mapas de Ideias:** navegar por listas de mapas privados; organizar visualmente as **mesmas notas reais** como nós em quadros independentes; criar conexões direcionadas com tipo/rótulo; mostrar dúvidas satélites e referências; selecionar mapa inteiro/subgrafo como contexto para uma IA; aceitar ou recusar cada proposta antes de alterar o grafo.

## Vocabulário imutável

- **Note:** documento canônico do usuário. Não é cópia do nó no mapa.
- **Block:** unidade editável com ID persistente dentro da nota.
- **Category:** zero ou uma categoria principal por nota e dono.
- **Tag:** múltiplas tags do dono, N:N com notas.
- **NoteRevision:** snapshot imutável; restaurar cria revisão nova.
- **Doubt:** pergunta do usuário vinculada a uma nota e, se originada de seleção, a uma âncora; não é uma resposta.
- **ResearchJob / ResearchAnswer:** execução assíncrona e síntese separadas; status do job não é status da evidência.
- **Publication:** snapshot público/não listado deliberado, nunca leitura direta de rascunho privado.
- **Board:** mapa de ideias privado; possui título/descrição, posição dos nós e ligações, mas não possui o conteúdo textual da nota.
- **BoardPlacement:** posição de uma nota no mapa; noteId reutilizado em mais de um Board.
- **BoardEdge:** relação entre duas notas *neste mapa*, com direção, tipo e rótulo opcional.
- **NoteReference:** metadado de livro/fonte associado à nota; não é automaticamente `ResearchSource` verificada.

## Princípios obrigatórios

1. **Privado por padrão**: escrita, dúvidas, mapas e suas relações são privados. Publicação, quando permitida, usa modelo e snapshot próprios.
2. **Autoridade humana**: sugestão de IA não edita notas, adiciona nós, conecta nós, inicia pesquisa nem publica por si só, salvo ação expressamente autorizada nos termos da funcionalidade.
3. **Notas únicas, visões múltiplas**: editar nota em um mapa reflete na biblioteca e em todos os mapas; mover/ligar nós de um mapa não afeta os demais.
4. **Resultados rastreáveis**: dados e interpretações distinguíveis; evidência por afirmação, rótulos `Com fontes`, `Evidência parcial` e `Sem fonte`.
5. **Integração não mágica**: provider agnóstico por contratos, não por presumir capacidades idênticas. Fake determinístico sem custo é obrigatório.
6. **Operação local realista**: AWS somente por LocalStack no escopo original, com limitações registradas; pesquisa live é opt-in externa e pode custar.
7. **Aprendizado verificável**: 18 marcos sem prazo, com teste/aceite/evidência; não declarar execução onde só há protótipo.

## Fora do escopo original

Rede social (seguidores, comentários, curtidas, ranking, feed), colaboração simultânea, app mobile, modo offline completo, pagamentos, Kubernetes, microsserviços, SSR obrigatório, embeddings como pré-requisito, responsividade e testes automatizados frontend nesta etapa. Novas funcionalidades precisam de decisão de escopo (ADR).

## Experiência central bem-sucedida

Um usuário autenticado consegue criar/editar notas, marcar dúvidas sem abandonar o texto, obter uma pesquisa que pode continuar apesar de ter fechado a tela, identificar limites das fontes e compartilhar apenas o snapshot escolhido. Em mapas, consegue abrir um quadro, rearranjar nós, criar relações, usar partes da rede como contexto sem vazamento inadvertido e aprovar mudanças de IA individualmente.
