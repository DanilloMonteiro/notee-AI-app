// Conversões do "Organizar com IA" (ADR-037): blocos do editor <-> texto enviado e resposta da API.
import { isChartBlock } from '../charts/chartModel.js';
import { splitPastedText } from '../charts/detectTables.js';

/** Serializa os blocos em texto com marcação leve; gráficos viram marcadores [[grafico:N]]. */
export function blocksToText(blocks) {
  const charts = [];
  const parts = blocks
    .map(b => {
      if (isChartBlock(b)) {
        charts.push(b);
        return `[[grafico:${charts.length - 1}]]`;
      }
      const text = b.text || '';
      if (!text.trim()) return null;
      switch (b.type) {
        case 'heading':
          return `${'#'.repeat(b.level || 2)} ${text.replace(/\n/g, ' ')}`;
        case 'list':
          return text
            .split('\n')
            .map(l => `- ${l}`)
            .join('\n');
        case 'quote':
          return text
            .split('\n')
            .map(l => `> ${l}`)
            .join('\n');
        case 'code':
          return `\`\`\`\n${text}\n\`\`\``;
        default:
          return text;
      }
    })
    .filter(Boolean);
  return { text: parts.join('\n\n'), charts };
}

/** Converte a resposta da API em itens do organizador (tabelas viram gráficos; refs voltam ao gráfico original). */
export function responseToItems(blocks, charts) {
  const items = [];
  for (const b of blocks) {
    if (b.type === 'chart') {
      const original = charts[b.ref];
      if (original) items.push({ type: 'chart', chart: original, source: '' });
      continue;
    }
    if (b.type === 'table') {
      for (const seg of splitPastedText(b.text)) {
        items.push(seg.type === 'chart' ? seg : { type: 'paragraph', text: seg.text });
      }
      continue;
    }
    items.push(b.type === 'heading' ? { type: 'heading', level: b.level || 2, text: b.text } : { type: b.type, text: b.text });
  }
  return items;
}

