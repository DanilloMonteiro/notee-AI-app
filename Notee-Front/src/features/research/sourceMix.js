// Origem das fontes (ADR-047): artigos acadêmicos × internet, e quanto de cada uma.

export const SOURCE_MODES = [
  { value: 'mixed', label: 'Artigos primeiro, completa com a internet', short: 'Artigos + internet', hint: 'Usa só artigos quando há o bastante; se faltar, completa com páginas da internet. Recomendado.' },
  { value: 'academic', label: 'Só artigos acadêmicos', short: 'Só artigos', hint: 'Nunca usa páginas da internet. Se não houver artigo, a resposta pode sair sem fonte.' },
  { value: 'web', label: 'Só internet', short: 'Só internet', hint: 'Páginas da internet, como antes. Sem artigos acadêmicos.' },
  { value: 'none', label: 'Não pesquisar fontes', short: 'Não pesquisar', hint: 'A IA responde sem pesquisar; as fontes que ela citar são conferidas pelo Notee.' },
];

export const sourceModeLabel = value => SOURCE_MODES.find(m => m.value === value)?.short || value;

/**
 * 'academic' | 'web' | null. Usa a origem gravada (respostas novas); para fontes da biblioteca, DOI/ISBN ou tipo
 * artigo/livro contam como acadêmicas e link de site como internet. Origem só declarada pela IA (sem link) = null.
 */
export function sourceOriginOf(source) {
  if (!source) return null;
  if (source.origin === 'academic' || source.origin === 'web') return source.origin;
  if (source.doi || source.isbn || ['article', 'book', 'chapter', 'thesis'].includes(source.kind)) return 'academic';
  if (source.url && source.verified !== false) return 'web';
  return null;
}

/** Contagem e porcentagens por origem; só fontes de origem conhecida entram na conta. */
export function sourceComposition(sources = []) {
  const origins = sources.map(sourceOriginOf).filter(Boolean);
  const academic = origins.filter(o => o === 'academic').length;
  const web = origins.length - academic;
  const total = origins.length;
  const academicPercent = total ? Math.round((100 * academic) / total) : null;
  return { academic, web, total, academicPercent, webPercent: total ? 100 - academicPercent : null };
}

/** "75% artigos · 25% internet" (ou "100% artigos"); null sem fontes de origem conhecida. */
export function formatComposition(composition) {
  if (!composition?.total) return null;
  const parts = [];
  if (composition.academic) parts.push(`${composition.academicPercent}% artigos`);
  if (composition.web) parts.push(`${composition.webPercent}% internet`);
  return parts.join(' · ');
}

/** Autores curtos: "Ana Souza, João Lima e outros". */
export function shortAuthors(authors) {
  const list = (Array.isArray(authors) ? authors : String(authors || '').split(';'))
    .map(a => a.trim())
    .filter(Boolean);
  if (list.length === 0) return '';
  return list.length > 2 ? `${list.slice(0, 2).join(', ')} e outros` : list.join(', ');
}

/** Domínio de uma página da internet para bloquear ("www." fora); null para artigos (link do DOI) e links inválidos. */
export function blockableDomain(source) {
  if (sourceOriginOf(source) !== 'web' || !source?.url) return null;
  try {
    const host = new URL(source.url).hostname.toLowerCase();
    return host.startsWith('www.') ? host.slice(4) : host;
  } catch {
    return null;
  }
}

/** Linha "Fontes" da estimativa da criação de nota com IA. */
export function sourcesEstimateText(est) {
  if (!est) return '—';
  const cost = Number(est.estimatedCostUsd || 0);
  const costPart = cost > 0 ? ` (até ≈ US$ ${cost.toFixed(4)}, incluído no custo abaixo)` : '';
  if (est.mode === 'none') return 'Sem pesquisa';
  if (est.willSearchAcademic && est.mayUseWeb) return `Artigos acadêmicos; a internet completa se faltar${costPart}`;
  if (est.willSearchAcademic) return `Artigos acadêmicos${costPart}`;
  if (est.mayUseWeb) return `Internet${costPart}`;
  return est.mode === 'web'
    ? 'Indisponível: a busca na internet só funciona com o DeepSeek ativo e autorizado'
    : 'Indisponível: autorize a IA externa em Configurações para pesquisar';
}

/** "3 artigo(s) e 1 página(s) da web" a partir do resumo da busca da prévia. */
export function searchBasisText(sourceSearch, total) {
  const academic = sourceSearch?.academicResults || 0;
  const web = sourceSearch?.webResults ?? (sourceSearch ? 0 : total || 0);
  const parts = [];
  if (academic) parts.push(`${academic} artigo(s)`);
  if (web) parts.push(`${web} página(s) da web`);
  return parts.join(' e ') || `${total || 0} resultado(s)`;
}
