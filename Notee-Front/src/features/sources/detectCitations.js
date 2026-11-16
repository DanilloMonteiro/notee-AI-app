const patterns = [
  { type: 'author-year-paren', regex: /\(([\p{L}][\p{L}'’.-]*(?:\s+et\s+al\.)?),\s*((?:19|20)\d{2}[a-z]?)\)/giu },
  { type: 'author-year', regex: /\b([\p{L}][\p{L}'’.-]+)\s+\(((?:19|20)\d{2}[a-z]?)\)/giu },
  { type: 'source-label', regex: /^\s*fontes?\s*:\s*([^\n]+)$/gimu },
  { type: 'ibge-mention', regex: /\bsegundo\s+(o\s+)?IBGE\b/giu },
  { type: 'venue-year', regex: /\b([\p{L}][\p{L}'’.-]+(?:\s+[\p{L}][\p{L}'’.-]+)?)\s*[—–-]\s*([^,\n]+),\s*((?:19|20)\d{2})\b/gu },
  { type: 'doi', regex: /\b(?:https?:\/\/doi\.org\/)?10\.\d{4,9}\/[A-Z0-9._;()/:-]+/giu },
  { type: 'isbn', regex: /\b(?:ISBN(?:-1[03])?:?\s*)?(?:97[89][ -]?)?\d(?:[ -]?\d){8,11}[ -]?[\dX]\b/giu },
  { type: 'url', regex: /https?:\/\/[^\s<>()]+/giu },
];

function buildSuggestion(match, type, block) {
  const text = match[0].trim();
  const author = match[1] && type.startsWith('author-year') ? match[1].replace(/\s+et\s+al\./i, '') : null;
  const yearText = type === 'author-year-paren' ? match[2] : type === 'author-year' ? match[2] : type === 'venue-year' ? match[3] : null;
  const clean = text.replace(/^\(|\)$/g, '');
  const id = type === 'doi' ? clean.replace(/^https?:\/\/doi\.org\//i, '').replace(/[.,;:]+$/, '')
    : type === 'url' ? text.replace(/[.,;:]+$/, '')
      : type === 'isbn' ? text.replace(/^ISBN(?:-1[03])?:?\s*/i, '').replace(/[.,;:]+$/, '') : null;
  const extractedAuthor = author || (type === 'venue-year' ? match[1] : type === 'ibge-mention' ? 'IBGE' : null);
  const year = yearText ? Number(yearText.slice(0, 4)) : null;
  const title = type === 'source-label' ? (match[1] || text).trim() : `Referência mencionada: ${text}`;
  return {
    id: `${block.id}:${match.index}:${type}`,
    blockId: block.id,
    quote: text,
    startIndex: match.index,
    endIndex: match.index + text.length,
    type,
    identifier: id,
    title: title.slice(0, 300),
    authors: extractedAuthor ? [extractedAuthor] : [],
    year,
    venue: type === 'venue-year' ? match[2].trim() : null,
    kind: type === 'doi' ? 'article' : type === 'isbn' ? 'book' : type === 'url' ? 'website' : 'other',
  };
}

export function detectCitations(blocks = []) {
  const suggestions = [];
  for (const block of blocks) {
    if (!block?.id || typeof block.text !== 'string') continue;
    for (const { type, regex } of patterns) {
      regex.lastIndex = 0;
      for (const match of block.text.matchAll(regex)) {
        if (!match[0].trim()) continue;
        suggestions.push(buildSuggestion(match, type, block));
      }
    }
  }
  const unique = new Map();
  for (const suggestion of suggestions.sort((a, b) => a.startIndex - b.startIndex || a.quote.length - b.quote.length)) {
    const key = `${suggestion.blockId}:${suggestion.startIndex}:${suggestion.quote.toLowerCase()}`;
    if (!unique.has(key)) unique.set(key, suggestion);
  }
  return [...unique.values()];
}
