// Rótulos das verificações de fonte (SourceCheckStatus da API, em minúsculas, ou "unverified").
// Um só lugar para o painel de fontes, a biblioteca e o card do trecho.

export const CHECK_STATUS = {
  unverified: { label: 'Não verificada', tone: '' },
  pending: { label: 'Conferindo…', tone: 'blue' },
  running: { label: 'Conferindo…', tone: 'blue' },
  confirmed: { label: 'Confirmada', tone: 'green' },
  foundwithdifferences: { label: 'Com diferenças', tone: 'amber' },
  quotenotfound: { label: 'Trecho não encontrado', tone: 'amber' },
  mismatch: { label: 'Não confere', tone: 'red' },
  notfound: { label: 'Não encontrada', tone: 'red' },
  unverifiable: { label: 'Não deu para conferir', tone: '' },
  failed: { label: 'Falha na conferência', tone: 'red' },
};

// Explicação curta de cada etiqueta, mostrada no ícone de informação ao lado dela.
export const CHECK_STATUS_HELP = {
  unverified: 'O Notee ainda não fez a checagem automática desta fonte.',
  pending: 'A checagem automática está na fila e termina em alguns segundos.',
  running: 'A checagem automática está em andamento.',
  confirmed: 'O Notee encontrou a fonte (DOI no Crossref, ISBN na Open Library ou o link respondeu) e os dados cadastrados batem. Isso não confirma o conteúdo.',
  foundwithdifferences:
    'A fonte existe, mas algo não bate: o título, o ano ou o primeiro autor cadastrados são diferentes dos da página ou do registro, ou o site respondeu com erro (por exemplo, bloqueando acesso automático). Nas fontes sugeridas pela IA, o mais comum é o título não ser idêntico ao da página — não quer dizer que a fonte está errada.',
  quotenotfound: 'O trecho citado não foi encontrado no arquivo da fonte guardado no Notee.',
  mismatch: 'A análise indicou que a fonte contradiz o trecho citado.',
  notfound: 'O DOI, ISBN ou link não foi encontrado (por exemplo, a página não existe mais). Confira a fonte.',
  unverifiable:
    'O Notee não conseguiu checar: a fonte não tem DOI, ISBN nem link, não há arquivo da fonte para procurar o trecho, ou o limite diário de checagens foi atingido. Não quer dizer que a fonte está errada.',
  failed: 'A checagem falhou depois de várias tentativas (por exemplo, o site ficou fora do ar). Edite e salve a fonte para tentar de novo.',
};

/** Explicação da etiqueta para o ícone de informação; null se não houver. */
export const checkStatusHelp = status => CHECK_STATUS_HELP[key(status)] || null;

const AI_VERDICT = {
  supports: 'IA: apoia o trecho',
  partially: 'IA: apoia em parte',
  contradicts: 'IA: contradiz o trecho',
  insufficient: 'IA: texto insuficiente',
};

const key = status => String(status || '').toLowerCase();

/** Rótulo e cor (classe da pill) de um status; status desconhecido aparece como veio. */
export function checkStatusInfo(status) {
  return CHECK_STATUS[key(status)] || { label: String(status || '—'), tone: '' };
}

export const isCheckInProgress = check => ['pending', 'running'].includes(key(check?.status));

/** Rótulo de uma verificação: análise por IA mostra o veredito; as demais, o status. */
export function checkLabel(check) {
  if (check?.layer === 'ai' && check.verdict && AI_VERDICT[check.verdict]) return AI_VERDICT[check.verdict];
  return checkStatusInfo(check?.status).label;
}

/** O que a pessoa pode fazer quando a conferência não foi possível. */
export function checkHint(check) {
  if (key(check?.status) !== 'unverifiable') return null;
  const summary = String(check?.summary || '');
  if (/arquivad|extra[ií]vel/i.test(summary))
    return 'Para conferir, envie o PDF ou arquivo da fonte na Biblioteca de fontes; o Notee procura o trecho nele.';
  if (/trecho da fonte/i.test(summary)) return 'Informe o trecho da fonte na citação para o Notee procurar no arquivo.';
  return null;
}
