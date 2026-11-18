// Resposta de dúvida com marcadores [n] (ADR-045): divide o texto em partes de texto e referências.
// sources[n - 1] é a fonte do marcador [n]; verified = false é origem informada pela IA, não conferida (sem link).

export const UNVERIFIED_LABEL = 'Origem informada pela IA — não conferida';

/** "A [1]. B [2]" → [{ type: 'text', value: 'A ' }, { type: 'ref', n: 1, source }, …]. Marcador sem fonte fica como texto. */
export function splitAnswerRefs(text, sources = []) {
  const parts = [];
  const re = /\[(\d{1,3})\]/g;
  let cursor = 0;
  let match;
  const value = String(text || '');
  while ((match = re.exec(value))) {
    const n = Number(match[1]);
    const source = sources[n - 1];
    if (!source) continue;
    if (match.index > cursor) parts.push({ type: 'text', value: value.slice(cursor, match.index) });
    parts.push({ type: 'ref', n, source });
    cursor = match.index + match[0].length;
  }
  if (cursor < value.length) parts.push({ type: 'text', value: value.slice(cursor) });
  return parts;
}

/** Fonte da resposta tem link conferido? (respostas antigas não têm o campo e eram todas links da busca). */
export const isVerifiedSource = source => Boolean(source?.url) && source.verified !== false;

/** Domínio para mostrar ao lado do link. */
export function sourceHost(url) {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return '';
  }
}
