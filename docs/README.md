# Notee — pacote de contexto Markdown para IA desenvolvedora

**Notee · Anote agora. Entenda depois.**

Pacote organizado em Markdown para uma IA entender e desenvolver o produto, com uma especificação da **feature Mapas de Ideias** e as mudanças de UX v1.2. Contém transcrição textual por páginas do planejamento PDF original e documentação temática acionável. **Não é uma aplicação implementada.**

## Comece por aqui

1. Leia [`docs/00_COMECE_AQUI_E_PRECEDENCIA.md`](docs/00_COMECE_AQUI_E_PRECEDENCIA.md) para não confundir versões/protótipo com sistema real.
2. Leia [`docs/PLANEJAMENTO_NOTEE.md`](docs/PLANEJAMENTO_NOTEE.md) para a visão consolidada.
3. Se quiser que **sua IA desenvolva código diretamente**, use [`docs/PROMPT_IA_DESENVOLVEDORA.md`](docs/PROMPT_IA_DESENVOLVEDORA.md). Se quiser **aprender e implementar você mesmo**, use [`docs/MENTOR_IA.md`](docs/MENTOR_IA.md).
4. Para a nova feature, carregue principalmente [`docs/MAPAS_DE_IDEIAS.md`](docs/MAPAS_DE_IDEIAS.md), [`docs/MAPAS_CONTEXTO_IA.md`](docs/MAPAS_CONTEXTO_IA.md), [`docs/UX_V12.md`](docs/UX_V12.md), [`docs/05_MODELO_DE_DADOS.md`](docs/05_MODELO_DE_DADOS.md) e [`docs/06_API_E_CONTRATOS.md`](docs/06_API_E_CONTRATOS.md).
5. Junte [`docs/ESTADO_DO_PROJETO.md`](docs/ESTADO_DO_PROJETO.md) e **o repositório real** para a IA identificar o que existe antes de implementar.

## O que foi organizado

```text
Notee_Contexto_IA/
  README.md
  AGENTS.md                      # contexto para agentes de código
  docs/
    00_COMECE_AQUI_E_PRECEDENCIA.md
    01_PRODUTO_E_ESCOPO.md
    02_REQUISITOS_E_CASOS_DE_USO.md
    03_UX_DESIGN_SYSTEM.md
    04_ARQUITETURA_E_STACK.md
    05_MODELO_DE_DADOS.md
    06_API_E_CONTRATOS.md
    07_EDITOR_REVISOES_E_ANCORAS.md
    08_PESQUISA_IA_E_FONTES.md
    09_JOBS_OUTBOX_IDEMPOTENCIA.md
    10_SEGURANCA_E_PRIVACIDADE.md
    11_INFRA_TESTES_OBSERVABILIDADE.md
    12_PLANO_DE_IMPLEMENTACAO.md
    13_ADRS_E_DECISOES_ABERTAS.md
    PLANEJAMENTO_NOTEE.md
    MAPAS_DE_IDEIAS.md
    MAPAS_CONTEXTO_IA.md
    UX_V12.md
    PROMPT_IA_DESENVOLVEDORA.md
    MENTOR_IA.md
    ESTADO_DO_PROJETO.md
    VALIDACAO_DA_ENTREGA.md
    MATRIZ_RASTREABILIDADE.md
  fontes_pdf/
    00_ABERTURA_E_INDICE.md
    01_GUIA_PRODUTO_E_UX.md
    02_NOTAS_DUVIDAS_E_PESQUISA.md
    03_PUBLICACAO_E_ARQUITETURA.md
    04_MODELOS_E_API.md
    05_CONFIABILIDADE_E_SEGURANCA.md
    06_INFRA_QUALIDADE_E_OPERACAO.md
    07_MARCOS_E_ADRS.md
    08_IA_MENTORA_E_ACEITE.md
    09_REFERENCIAS_DO_PDF.md
    10_LINKS_ORIGINAIS_DO_PDF.md
  referencias/
    Notee_Planejamento_v1_0.pdf
    Notee_Prototipo_v1_2.html
    README_original_v1_2.md
    PROTOTIPO_V12_EDITOR.png
    PROTOTIPO_V12_LISTA_MAPAS.png
```

## Regras importantes

- **PDF recebido:** versão 1.0, 30/09/2026, **51 páginas**. O README da versão v1.2 descreve outro pacote com PDF de 60 páginas e suplemento v1.1 que **não consta no arquivo enviado**. Por isso, as evoluções Mapas v1.1 e UI v1.2 foram fundamentadas no README, protótipo HTML e capturas, sem simular ter lido o suplemento ausente.
- **Requisitos e propostas:** RF01–RF14 e M01–M18 vieram do PDF. RF15–RF21, MAP-01–MAP-05, endpoints de grafo e DDL são desdobramentos propostos para viabilizar a implementação. Não se presumem aprovados nem migrados.
- **Privacidade:** mapas e notas privados; consentimento externo e escopo de envio separados. Sugestões não alteram dados sem aceite.
- **Prototipagem:** o HTML armazena dados em `localStorage` e usa pesquisas/IA simuladas. Ele não prova APIs, banco, testes, autenticação, serviços em nuvem ou execução de modelos.
- **Fonte literal:** os 11 arquivos em `fontes_pdf/` preservam texto por página do PDF, inclusive tabelas em alinhamento aproximado, permitindo conferir diretamente o material original.

## Como enviar à IA de código sem sobrecarregar o contexto

Na primeira instrução, forneça o `README.md`, `docs/00_COMECE_AQUI_E_PRECEDENCIA.md`, `docs/PLANEJAMENTO_NOTEE.md`, `docs/ESTADO_DO_PROJETO.md` e o prompt do modo escolhido. Para uma tarefa de mapas, inclua os quatro a seis arquivos temáticos citados acima, o HTML e as capturas; consulte `fontes_pdf/` sob demanda, não despeje todas as páginas de uma só vez.

**Estado real e limitações:** consulte [`docs/VALIDACAO_DA_ENTREGA.md`](docs/VALIDACAO_DA_ENTREGA.md). Nenhuma alteração foi realizada no repositório de produção nesta entrega.
