// "Organizar em tópicos" (regras locais, ADR-037): transforma texto com marcação leve (markdown, negritos,
// listas, tabelas, cartões de destaque) em blocos do editor. Não reescreve palavras — só muda a estrutura.
// Determinístico e local: nada é enviado para fora.
import { normalizePasted, parseCell, splitPastedText, stripInline } from '../charts/detectTables.js';

const DEFAULT_TITLES = new Set(['', 'sem título', 'uma nova ideia']);
const SHORT = 80;

const HR = /^\s*([-*_])(\s*\1){2,}\s*$/;
const HEADING = /^\s*(#{1,6})\s+(.*?)\s*#*\s*$/;
const LIST_ITEM = /^\s*(?:[-*+•]|\d{1,3}[.)])\s+(.*)$/;
const QUOTE = /^\s*>\s?(.*)$/;
const FENCE = /^\s*```/;
const BOLD_LINE = /^\s*(\*\*|__)(.+?)\1\s*:?\s*$/;

/** Texto de um cartão "# +7,5%": devolve o valor se a linha for só um número. */
function statValue(content) {
  const parsed = parseCell(normalizePasted(content));
  return parsed && !parsed.missing ? content.trim() : null;
}

/** Indica se o texto tem marcação que o organizador sabe estruturar. */
export function hasStructure(text) {
  const s = String(text ?? '');
  return /^\s*#{1,6}\s+\S/m.test(s) || /^\s*(?:[-*+•]|\d{1,3}[.)])\s+\S/m.test(s) || /^\s*\|.+\|\s*$/m.test(s) || /\*\*[^*]+\*\*/.test(s) || /^\s*-{3,}\s*$/m.test(s);
}

/**
 * Organiza um texto em blocos.
 * @returns {{ blocks: Array<{type:string,text?:string,level?:number,chart?:object,source?:string}>, title?: string }}
 */
export function organizeText(text, { currentTitle = '' } = {}) {
  const state = {
    blocks: [],
    title: undefined,
    canSetTitle: DEFAULT_TITLES.has(String(currentTitle).trim().toLowerCase()),
    lastHeading: null,
  };

  for (const seg of splitPastedText(text)) {
    if (seg.type === 'chart') {
      const chart = { ...seg.chart };
      if (!chart.title && state.lastHeading) chart.title = state.lastHeading.slice(0, 200);
      state.blocks.push({ type: 'chart', chart, source: seg.source });
    } else {
      parseText(seg.text, state);
    }
  }

  return { blocks: state.blocks, title: state.title };
}

function push(state, block) {
  state.blocks.push(block);
}

function parseText(text, state) {
  const lines = String(text).replace(/\r\n?/g, '\n').split('\n');
  let paragraph = [];

  const flush = () => {
    if (!paragraph.length) return;
    const raw = paragraph;
    paragraph = [];
    const last = state.blocks[state.blocks.length - 1];

    // Linha única toda em negrito vira subtítulo; negritos seguidos são unidos ("2011–14 · Dilma I").
    const bold = raw.length === 1 ? BOLD_LINE.exec(raw[0]) : null;
    if (bold && bold[2].trim().length <= 100) {
      const content = stripInline(bold[2]).trim();
      if (last?.type === 'heading' && last._fromBold) {
        last.text = `${last.text} · ${content}`;
        return;
      }
      if (last?.type === 'heading' && !last._fromBold && last.level === 3) {
        // Logo após um subtítulo (###), o negrito é autoria/linha fina: fica como parágrafo.
        push(state, { type: 'paragraph', text: content });
        return;
      }
      push(state, { type: 'heading', level: 3, text: content, _fromBold: true });
      return;
    }
    push(state, { type: 'paragraph', text: raw.map(l => stripInline(l).trim()).join('\n') });
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    if (FENCE.test(line)) {
      flush();
      const code = [];
      i++;
      while (i < lines.length && !FENCE.test(lines[i])) code.push(lines[i++]);
      push(state, { type: 'code', text: code.join('\n') });
      continue;
    }

    if (!line.trim()) {
      flush();
      continue;
    }

    if (HR.test(line)) {
      flush();
      continue;
    }

    const heading = HEADING.exec(line);
    if (heading) {
      const content = stripInline(heading[2]).trim();
      const value = statValue(content);
      if (value) {
        addStat(state, lines, i, value, paragraph, flush, newIndex => (i = newIndex));
        paragraph = [];
        continue;
      }
      flush();
      if (!content) continue;
      const level = heading[1].length === 1 ? 1 : heading[1].length === 2 ? 2 : 3;
      if (level === 1 && state.canSetTitle && state.title === undefined) {
        state.title = content.slice(0, 100);
        state.lastHeading = content;
        continue;
      }
      push(state, { type: 'heading', level, text: content });
      state.lastHeading = content;
      continue;
    }

    const item = LIST_ITEM.exec(line);
    if (item) {
      flush();
      const items = [stripInline(item[1]).trim()];
      while (i + 1 < lines.length && LIST_ITEM.test(lines[i + 1])) items.push(stripInline(LIST_ITEM.exec(lines[++i])[1]).trim());
      push(state, { type: 'list', text: items.join('\n') });
      continue;
    }

    const quote = QUOTE.exec(line);
    if (quote) {
      flush();
      const parts = [stripInline(quote[1]).trim()];
      while (i + 1 < lines.length && QUOTE.test(lines[i + 1])) parts.push(stripInline(QUOTE.exec(lines[++i])[1]).trim());
      push(state, { type: 'quote', text: parts.join('\n') });
      continue;
    }

    paragraph.push(line);
  }
  flush();
}

/**
 * Cartão de destaque: "rótulo" (linha curta antes) + "# valor" + "legenda" (linha curta depois).
 * Cartões seguidos viram um único bloco de lista: "2010 — antes: +7,5% — Recuperação forte".
 */
function addStat(state, lines, i, value, paragraph, flush, setIndex) {
  let label = '';
  if (paragraph.length === 1 && paragraph[0].trim().length <= SHORT) {
    label = stripInline(paragraph[0]).trim();
  } else {
    flush();
    const last = state.blocks[state.blocks.length - 1];
    if (last?.type === 'paragraph' && !last.text.includes('\n') && last.text.length <= SHORT) {
      label = last.text;
      state.blocks.pop();
    }
  }

  // Legenda: próxima linha não vazia, curta e sem marcação estrutural.
  let caption = '';
  let j = i + 1;
  let blanks = 0;
  while (j < lines.length && !lines[j].trim() && blanks < 2) {
    j++;
    blanks++;
  }
  const next = lines[j];
  if (
    next &&
    next.trim().length <= SHORT &&
    !HEADING.test(next) &&
    !LIST_ITEM.test(next) &&
    !QUOTE.test(next) &&
    !HR.test(next) &&
    !FENCE.test(next)
  ) {
    caption = stripInline(next).trim();
    setIndex(j);
  }

  const text = [label ? `${label}: ${value}` : value, caption].filter(Boolean).join(' — ');
  // Junta ao bloco de destaques imediatamente anterior (o rótulo já foi retirado do fim da lista de blocos).
  const last = state.blocks[state.blocks.length - 1];
  if (last?._stats) last.text = `${last.text}\n${text}`;
  else push(state, { type: 'list', text, _stats: true });
}

/** Remove marcadores internos antes de virar bloco do editor. */
export const cleanBlock = ({ _fromBold, _stats, ...block }) => block;
