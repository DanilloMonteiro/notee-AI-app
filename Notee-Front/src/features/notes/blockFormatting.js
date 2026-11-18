// Formatação dos blocos do editor, sem mexer no texto:
// - negrito/itálico ficam em `block.styles = [{ start, end, bold?, italic? }]` (intervalos sobre block.text);
// - listas são um bloco por item: { type: 'list', listStyle: 'bullet'|'number'|'letter', indent: 0..3 };
//   a numeração (1., 1.1, a., a.1) é calculada aqui e desenhada por CSS, nunca salva no texto.
// Assim dúvidas, comentários e fontes (ancorados por texto), busca e IA continuam iguais.

export const MAX_INDENT = 3;
const BOLD = 1;
const ITALIC = 2;
const FLAG = { bold: BOLD, italic: ITALIC };

// --- estilos inline ------------------------------------------------------------------

const toFlags = (styles = [], length) => {
  const flags = new Uint8Array(length);
  for (const s of styles || []) {
    const start = Math.max(0, s.start | 0);
    const end = Math.min(length, s.end | 0);
    const bits = (s.bold ? BOLD : 0) | (s.italic ? ITALIC : 0);
    for (let i = start; i < end; i++) flags[i] |= bits;
  }
  return flags;
};

const fromFlags = flags => {
  const out = [];
  let i = 0;
  while (i < flags.length) {
    const bits = flags[i];
    let j = i + 1;
    while (j < flags.length && flags[j] === bits) j++;
    if (bits) {
      const range = { start: i, end: j };
      if (bits & BOLD) range.bold = true;
      if (bits & ITALIC) range.italic = true;
      out.push(range);
    }
    i = j;
  }
  return out;
};

/** Mescla vizinhos iguais, descarta vazios e corta no tamanho do texto. */
export const normalizeStyles = (styles, textLength) => fromFlags(toFlags(styles, textLength));

/** O trecho [start, end) inteiro tem o estilo? */
export function hasStyle(styles, start, end, kind, textLength) {
  if (end <= start) return false;
  const flags = toFlags(styles, textLength);
  for (let i = start; i < end; i++) if (!(flags[i] & FLAG[kind])) return false;
  return true;
}

/** Aplica o estilo ao trecho; se o trecho inteiro já o tem, remove. */
export function toggleStyle(styles, start, end, kind, textLength) {
  const flags = toFlags(styles, textLength);
  const from = Math.max(0, start);
  const to = Math.min(textLength, end);
  if (to <= from) return fromFlags(flags);
  const remove = hasStyle(styles, from, to, kind, textLength);
  for (let i = from; i < to; i++) flags[i] = remove ? flags[i] & ~FLAG[kind] : flags[i] | FLAG[kind];
  return fromFlags(flags);
}

/**
 * Ajusta os intervalos depois de uma edição de texto (digitar, apagar, colar).
 * Compara prefixo e sufixo comuns: texto inserido no fim de um trecho formatado continua formatado
 * (como em editores de texto); inserido no início, não.
 */
export function adjustStylesForEdit(oldText, newText, styles) {
  if (!styles?.length || oldText === newText) return styles || [];
  const max = Math.min(oldText.length, newText.length);
  let p = 0;
  while (p < max && oldText[p] === newText[p]) p++;
  let s = 0;
  while (s < max - p && oldText[oldText.length - 1 - s] === newText[newText.length - 1 - s]) s++;
  const oldEnd = oldText.length - s;
  const inserted = newText.length - s - p;
  const delta = newText.length - oldText.length;

  const mapStart = x => (x < p ? x : x >= oldEnd ? x + delta : p + inserted);
  const mapEnd = x => {
    if (x < p) return x;
    if (x === p && oldEnd === p) return x + inserted; // inserção logo no fim do trecho: estende
    if (x >= oldEnd) return x + delta;
    return p + inserted; // fim dentro do trecho apagado/substituído
  };
  const moved = styles.map(r => ({ ...r, start: mapStart(r.start), end: mapEnd(r.end) })).filter(r => r.end > r.start);
  return normalizeStyles(moved, newText.length);
}

// --- tipos de bloco --------------------------------------------------------------------

/** Opções do menu e do seletor de tipo. `chart` só serve para inserir. */
export const BLOCK_TYPES = [
  { key: 'paragraph', label: 'Parágrafo' },
  { key: 'heading-1', label: 'Título 1' },
  { key: 'heading-2', label: 'Título 2' },
  { key: 'heading-3', label: 'Título 3' },
  { key: 'list-bullet', label: 'Lista com marcadores' },
  { key: 'list-number', label: 'Lista numerada (1. 1.1)' },
  { key: 'list-letter', label: 'Lista com letras (a. a.1)' },
  { key: 'quote', label: 'Citação' },
  { key: 'code', label: 'Código' },
];

/** Chave do tipo atual de um bloco (ex.: 'heading-2', 'list-number'). */
export function blockTypeKey(block) {
  if (!block) return 'paragraph';
  if (block.type === 'heading') return `heading-${block.level || 2}`;
  if (block.type === 'list') return `list-${block.listStyle || 'bullet'}`;
  return block.type || 'paragraph';
}

/** Campos de um bloco de texto para a chave de tipo (sem id/texto). */
function typeFields(key, from) {
  if (key.startsWith('heading-')) return { type: 'heading', level: Number(key.split('-')[1]) || 2 };
  if (key.startsWith('list-')) {
    const listStyle = key.split('-')[1];
    const indent = from?.type === 'list' ? Math.min(MAX_INDENT, Math.max(0, from.indent | 0)) : 0;
    return { type: 'list', listStyle, indent };
  }
  return { type: ['paragraph', 'quote', 'code'].includes(key) ? key : 'paragraph' };
}

/** Novo bloco vazio do tipo pedido; listas herdam estilo e nível do bloco de referência. */
export function createTextBlock(key, id, reference) {
  const inherit = reference?.type === 'list' && key === blockTypeKey(reference) ? reference : null;
  return { id, ...typeFields(key, inherit), text: '' };
}

/**
 * Troca o tipo mantendo id, texto e formatação. Devolve uma LISTA de blocos:
 * uma lista antiga de várias linhas convertida para numerada/letras vira um bloco por linha.
 * `makeId` gera ids para os blocos extras.
 */
export function convertBlockType(block, key, makeId) {
  const text = block.text || '';
  const fields = typeFields(key, block);
  const splitLines = fields.type === 'list' && fields.listStyle !== 'bullet' && text.includes('\n');
  if (!splitLines) {
    const out = { id: block.id, ...fields, text };
    if (block.styles?.length) out.styles = block.styles;
    return [out];
  }
  const result = [];
  let offset = 0;
  text.split('\n').forEach((line, i) => {
    const lineStyles = (block.styles || [])
      .map(r => ({ ...r, start: Math.max(r.start, offset) - offset, end: Math.min(r.end, offset + line.length) - offset }))
      .filter(r => r.end > r.start);
    const out = { id: i === 0 ? block.id : makeId(), ...fields, text: line };
    if (lineStyles.length) out.styles = normalizeStyles(lineStyles, line.length);
    result.push(out);
    offset += line.length + 1;
  });
  return result;
}

/** Divide o bloco no `offset`: o original fica com o começo, o novo (mesmo tipo) com o resto. */
export function splitBlockAt(block, offset, newId) {
  const text = block.text || '';
  const at = Math.max(0, Math.min(offset, text.length));
  const { id: _id, text: _text, styles = [], ...rest } = block;
  const head = { ...block, text: text.slice(0, at) };
  const tail = { id: newId, ...rest, text: text.slice(at) };
  const headStyles = normalizeStyles(styles, at);
  const tailStyles = normalizeStyles(
    styles.map(r => ({ ...r, start: r.start - at, end: r.end - at })).filter(r => r.end > 0),
    text.length - at,
  );
  if (headStyles.length) head.styles = headStyles;
  else delete head.styles;
  if (tailStyles.length) tail.styles = tailStyles;
  return [head, tail];
}

// --- numeração das listas --------------------------------------------------------------

const toLetters = n => {
  let s = '';
  for (let x = n; x > 0; x = Math.floor((x - 1) / 26)) s = String.fromCharCode(97 + ((x - 1) % 26)) + s;
  return s;
};

/**
 * Rótulo de cada item de lista: '1.', '1.1', '1.1.2', 'a.', 'a.1', '•'.
 * Itens de lista consecutivos formam um grupo; qualquer outro bloco reinicia a contagem.
 * O nível 0 usa número ou letra; níveis internos usam números ligados ao pai.
 */
export function listLabels(blocks) {
  const labels = new Map();
  let counters = [];
  let rootStyle = null;
  for (const block of blocks) {
    if (block?.type !== 'list') {
      counters = [];
      rootStyle = null;
      continue;
    }
    const style = block.listStyle || 'bullet';
    if (style === 'bullet') {
      labels.set(block.id, '•');
      continue;
    }
    const level = Math.min(MAX_INDENT, Math.max(0, block.indent | 0));
    if (level === 0 && rootStyle && rootStyle !== style) counters = [];
    if (level === 0 || !rootStyle) rootStyle = style;
    for (let k = 0; k < level; k++) if (!counters[k]) counters[k] = 1;
    counters[level] = (counters[level] || 0) + 1;
    counters.length = level + 1;
    const first = rootStyle === 'letter' ? toLetters(counters[0]) : String(counters[0]);
    labels.set(block.id, level === 0 ? `${first}.` : [first, ...counters.slice(1)].join('.'));
  }
  return labels;
}

// --- seleção que atravessa vários blocos -------------------------------------------------
// Posições: { blockId, offset } (offset em caracteres do texto do bloco).

/** Ordena duas posições pela ordem dos blocos na nota (e pelo offset no mesmo bloco). */
export function orderPositions(blocks, a, b) {
  const ia = blocks.findIndex(x => x.id === a.blockId);
  const ib = blocks.findIndex(x => x.id === b.blockId);
  return ia < ib || (ia === ib && a.offset <= b.offset) ? [a, b] : [b, a];
}

/**
 * Apaga o texto entre `start` e `end` (podem estar em blocos diferentes) e insere `insert` no lugar.
 * Como num editor de texto comum: o primeiro bloco fica com o começo + o fim do último; blocos do meio somem.
 * A formatação (negrito/itálico) de cada lado é preservada. Devolve { blocks, caret }.
 */
export function deleteTextRange(blocks, start, end, insert = '') {
  const [a, b] = orderPositions(blocks, start, end);
  const ia = blocks.findIndex(x => x.id === a.blockId);
  const ib = blocks.findIndex(x => x.id === b.blockId);
  if (ia < 0 || ib < 0) return { blocks, caret: null };
  const first = blocks[ia];
  const last = blocks[ib];
  const firstText = first.text || '';
  const lastText = last.text || '';
  const head = firstText.slice(0, a.offset) + insert;
  const text = head + lastText.slice(b.offset);
  const styles = normalizeStyles(
    [
      ...(first.styles || []).map(r => ({ ...r, end: Math.min(r.end, a.offset) })),
      ...(last.styles || []).map(r => ({ ...r, start: Math.max(r.start, b.offset) - b.offset + head.length, end: r.end - b.offset + head.length })),
    ].filter(r => r.end > r.start),
    text.length,
  );
  const merged = { ...first, text };
  if (styles.length) merged.styles = styles;
  else delete merged.styles;
  return { blocks: [...blocks.slice(0, ia), merged, ...blocks.slice(ib + 1)], caret: { blockId: first.id, offset: head.length } };
}

/** Negrito/itálico no trecho entre `start` e `end` (vários blocos): aplica em tudo, ou remove se tudo já tem. */
export function formatTextRange(blocks, start, end, kind) {
  const [a, b] = orderPositions(blocks, start, end);
  const ia = blocks.findIndex(x => x.id === a.blockId);
  const ib = blocks.findIndex(x => x.id === b.blockId);
  if (ia < 0 || ib < 0) return blocks;
  const span = (i, block) => [i === ia ? a.offset : 0, i === ib ? b.offset : (block.text || '').length];
  const textBlocks = blocks.map((block, i) => ({ block, i })).filter(({ i, block }) => i >= ia && i <= ib && typeof block.text === 'string');
  const remove = textBlocks.every(({ block, i }) => {
    const [s, e] = span(i, block);
    return e <= s || hasStyle(block.styles || [], s, e, kind, block.text.length);
  });
  return blocks.map((block, i) => {
    if (i < ia || i > ib || typeof block.text !== 'string') return block;
    const [s, e] = span(i, block);
    if (e <= s) return block;
    let styles = toggleStyle(block.styles || [], s, e, kind, block.text.length);
    // toggleStyle decide sozinho por bloco; aqui a decisão é da seleção inteira.
    if (remove === !hasStyle(styles, s, e, kind, block.text.length)) {
      // já ficou como deveria
    } else {
      styles = toggleStyle(styles, s, e, kind, block.text.length);
    }
    const next = { ...block };
    if (styles.length) next.styles = styles;
    else delete next.styles;
    return next;
  });
}

/** Texto puro do trecho entre `start` e `end` (para copiar/recortar), blocos separados por linha. */
export function textInRange(blocks, start, end) {
  const [a, b] = orderPositions(blocks, start, end);
  const ia = blocks.findIndex(x => x.id === a.blockId);
  const ib = blocks.findIndex(x => x.id === b.blockId);
  const parts = [];
  for (let i = ia; i <= ib && i >= 0; i++) {
    const text = blocks[i].text;
    if (typeof text !== 'string') continue;
    parts.push(text.slice(i === ia ? a.offset : 0, i === ib ? b.offset : text.length));
  }
  return parts.join('\n');
}
