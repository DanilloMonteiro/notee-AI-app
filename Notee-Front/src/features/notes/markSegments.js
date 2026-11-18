// Marcações do texto da nota: dúvidas, comentários e fontes aparecem juntas no mesmo bloco.
// O HTML é sempre gerado a partir do texto CRU + intervalos (nunca por replace sobre HTML),
// e trechos sobrepostos viram segmentos com classes combinadas, sem <mark> aninhado.

export const MARK_KINDS = ['doubt', 'comment', 'source'];

export const escapeHtml = s =>
  s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Ocorrência de `quote` mais próxima de `hint` (startIndex gravado na âncora). -1 se não existir. */
export function findQuote(text, quote, hint = 0) {
  if (!quote) return -1;
  let best = -1;
  let idx = text.indexOf(quote);
  while (idx !== -1) {
    if (best === -1 || Math.abs(idx - hint) < Math.abs(best - hint)) best = idx;
    idx = text.indexOf(quote, idx + 1);
  }
  return best;
}

/** Intervalos { start, end, kind, id, referenceNumber } das âncoras encontradas no texto. */
export function locateMarks(text, anchors) {
  return anchors
    .filter(a => MARK_KINDS.includes(a.kind) && a.exactQuote)
    .map(a => {
      const start = findQuote(text, a.exactQuote, a.startIndex || 0);
      return start === -1 ? null : { start, end: start + a.exactQuote.length, kind: a.kind, id: String(a.id ?? ''), referenceNumber: a.referenceNumber };
    })
    .filter(Boolean);
}

/**
 * Quebra o texto nas fronteiras de todos os intervalos (marcações e formatação).
 * Cada segmento: { text, marks: [{ kind, id }], refs: [n], bold, italic }.
 * `refs` só aparece no segmento onde uma citação de fonte termina (para desenhar o [n] ao lado do trecho).
 * `styles`: negrito/itálico do bloco ([{ start, end, bold?, italic? }], ver blockFormatting.js).
 */
export function segmentText(text, anchors, styles = []) {
  const ranges = locateMarks(text, anchors);
  const styleRanges = (styles || []).filter(s => s.end > s.start);
  const cuts = new Set([0, text.length]);
  ranges.forEach(r => { cuts.add(r.start); cuts.add(r.end); });
  styleRanges.forEach(s => { cuts.add(Math.min(text.length, s.start)); cuts.add(Math.min(text.length, s.end)); });
  const points = [...cuts].sort((a, b) => a - b);

  const segments = [];
  for (let i = 0; i < points.length - 1; i++) {
    const start = points[i];
    const end = points[i + 1];
    if (end <= start) continue;
    const covering = ranges.filter(r => r.start <= start && r.end >= end);
    const marks = [];
    covering.forEach(r => { if (!marks.some(m => m.kind === r.kind && m.id === r.id)) marks.push({ kind: r.kind, id: r.id }); });
    const refs = [...new Set(covering.filter(r => r.kind === 'source' && r.end === end && r.referenceNumber).map(r => r.referenceNumber))];
    const styled = styleRanges.filter(s => s.start <= start && s.end >= end);
    segments.push({
      text: text.slice(start, end),
      marks,
      refs,
      bold: styled.some(s => s.bold),
      italic: styled.some(s => s.italic),
    });
  }
  return segments;
}

/** HTML do bloco com <mark class="nm nm-doubt …" data-marks="doubt:id,…" data-ref="1,2"> e <strong>/<em>. */
export function buildMarkedHtml(text, anchors, styles = []) {
  return segmentText(text || '', anchors, styles)
    .map(seg => {
      let inner = escapeHtml(seg.text);
      if (seg.italic) inner = `<em>${inner}</em>`;
      if (seg.bold) inner = `<strong>${inner}</strong>`;
      if (seg.marks.length === 0) return inner;
      const kinds = [...new Set(seg.marks.map(m => m.kind))];
      const classes = ['nm', ...kinds.map(k => `nm-${k}`)].join(' ');
      const data = seg.marks.map(m => `${m.kind}:${m.id}`).join(',');
      const ref = seg.refs.length ? ` data-ref="${escapeHtml(seg.refs.join(','))}"` : '';
      return `<mark class="${classes}" data-marks="${escapeHtml(data)}"${ref}>${inner}</mark>`;
    })
    .join('');
}

/** Lê o atributo data-marks de volta para [{ kind, id }]. */
export function parseMarks(value) {
  return (value || '')
    .split(',')
    .map(part => {
      const i = part.indexOf(':');
      return i > 0 ? { kind: part.slice(0, i), id: part.slice(i + 1) } : null;
    })
    .filter(m => m && MARK_KINDS.includes(m.kind));
}
