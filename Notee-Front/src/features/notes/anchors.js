/** Localiza uma citação em blocos atuais, preferindo o bloco e a posição originais. */
export function locateQuote(blocks, anchor) {
  const quote = anchor?.exactQuote || anchor?.anchorQuote;
  if (!quote) return null;
  const candidates = [...blocks.filter(b => b.id === (anchor.blockId || anchor.anchorBlockId)), ...blocks.filter(b => b.id !== (anchor.blockId || anchor.anchorBlockId))];
  for (const block of candidates) {
    const text = block.text || '';
    let position = text.indexOf(quote);
    if (position < 0) continue;
    const hint = anchor.startIndex ?? anchor.anchorStart ?? 0;
    let best = position;
    while ((position = text.indexOf(quote, position + 1)) >= 0) if (Math.abs(position - hint) < Math.abs(best - hint)) best = position;
    return { blockId: block.id, startIndex: best, endIndex: best + quote.length };
  }
  return null;
}
