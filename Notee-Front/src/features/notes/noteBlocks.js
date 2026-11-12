// Leitura compartilhada do conteúdo da nota: array JSON de blocos ou texto puro (notas antigas).
import { chartPlainText, isChartBlock } from '../charts/chartModel';

const isTextBlock = b => !!b && typeof b === 'object' && typeof b.text === 'string';

/**
 * Converte o conteúdo salvo em blocos renderizáveis. Mantém blocos de texto e de gráfico;
 * ignora entradas desconhecidas. Texto puro vira um parágrafo.
 */
export function parseBlocks(content) {
  if (!content) return [];
  try {
    const parsed = JSON.parse(content);
    if (Array.isArray(parsed)) return parsed.filter(b => isChartBlock(b) || isTextBlock(b));
  } catch {
    // texto puro
  }
  return [{ id: 'p', type: 'paragraph', text: content }];
}

export const blockPlainText = block => (isChartBlock(block) ? chartPlainText(block) : isTextBlock(block) ? block.text : '');

/** Texto simples a partir do conteúdo (blocos ou texto puro), truncado. */
export function contentExcerpt(content, max = 140) {
  const text = parseBlocks(content).map(blockPlainText).filter(Boolean).join(' ').replace(/\s+/g, ' ').trim();
  return text.length > max ? `${text.slice(0, max).trimEnd()}…` : text;
}
