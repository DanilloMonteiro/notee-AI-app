// Detecta tabelas numéricas em texto colado e as converte em blocos de gráfico (ADR-036).
// Determinístico e local: nada é enviado para fora. Fontes nunca são inferidas do texto em volta.
import { CHART_LIMITS, createEmptyChart, validateChart } from './chartModel.js';

const MISSING = new Set(['', '-', '—', '–', 'n/d', 'nd', 'n.d.', 'n.d', '...', '…', 's/d']);
const NUMBER = /^([+-])?\s*(\d[\d.,]*)\s*(%)?$/;
const YEAR = /^\d{4}$/;
const MAX_BLANKS_INSIDE = 2;
const MD_SEPARATOR = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/;
const SOURCE_LINE = /^\s*(?:\*\*|__)?\s*fontes?\s*:\s*(?:\*\*|__)?\s*(.+)$/i;

/** Remove marcação markdown inline simples (negrito, código, links). */
export const stripInline = s =>
  String(s ?? '')
    .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, '$1 ($2)')
    .replace(/(\*\*|__)(.+?)\1/g, '$2')
    .replace(/`([^`]+)`/g, '$1')
    .replace(/(^|[^*\w])\*(?!\s)([^*\n]+?)\*(?!\*)/g, '$1$2')
    .replace(/^\*\*|\*\*$/g, '');

const clip = (s, max) => (s.length <= max ? s : `${s.slice(0, max - 1).trimEnd()}…`);

/** Normaliza sinais tipográficos e espaços antes de analisar. */
export function normalizePasted(text) {
  return String(text ?? '')
    .replace(/\r\n?/g, '\n')
    .replace(/ /g, ' ')
    .replace(/[−‒–—](?=\s*\d)/g, '-');
}

/** Converte "−3,0%", "+2,5", "1.234,5", "1234.5" em número. Devolve null quando não é número. */
export function parseCell(raw) {
  const s = String(raw ?? '').trim();
  if (MISSING.has(s.toLowerCase())) return { value: null, missing: true, signed: false, percent: false };
  const m = NUMBER.exec(s);
  if (!m) return null;
  let digits = m[2];
  if (digits.includes(',')) digits = digits.replace(/\./g, '').replace(',', '.');
  else if (/^\d{1,3}(\.\d{3})+$/.test(digits)) digits = digits.replace(/\./g, ''); // milhar pt-BR
  if (!/^\d+(\.\d+)?$/.test(digits)) return null;
  const value = Number(digits) * (m[1] === '-' ? -1 : 1);
  if (!Number.isFinite(value)) return null;
  return { value, missing: false, signed: !!m[1], percent: !!m[3] };
}

/** Divide uma linha em células por tab, "|", ";" ou 2+ espaços. */
function splitCells(line) {
  const t = line.trim();
  if (!t) return [];
  let cells;
  if (t.includes('\t')) cells = t.split('\t');
  else if (t.includes('|')) cells = t.replace(/^\|/, '').replace(/\|$/, '').split('|');
  else if (t.includes(';')) cells = t.split(';');
  else cells = t.split(/\s{2,}/);
  return cells.map(c => c.trim());
}

/** Linha de dados: rótulo + 1..8 valores numéricos (ou ausentes), com ao menos um número. */
function parseDataRow(line) {
  let cells = splitCells(line);
  if (cells.length < 2) {
    // Alternativa "rótulo valor" separados por um espaço, com o número no fim da linha.
    const m = /^(.+?)\s+([+-]?\s*\d[\d.,]*\s*%?)$/.exec(line.trim());
    if (!m) return null;
    // Evita frases que só terminam num ano ("Comparação internacional em 2015"): rótulo curto e valor que não seja ano solto.
    if (m[1].length > 40 || m[1].trim().split(/\s+/).length > 5 || /^(1[89]|20)\d\d$/.test(m[2].trim())) return null;
    cells = [m[1], m[2]];
  }
  const [label, ...rest] = cells;
  if (!label || /^[#>]/.test(label) || label.length > CHART_LIMITS.short || rest.length < 1 || rest.length > CHART_LIMITS.maxSeries)
    return null;
  const parsed = rest.map(parseCell);
  if (parsed.some(p => p === null) || !parsed.some(p => !p.missing)) return null;
  return { label, cells: parsed };
}

function parseHeader(line, columns) {
  const cells = splitCells(line);
  if (cells.length !== columns || cells.some(c => !c || c.length > CHART_LIMITS.short)) return null;
  if (parseDataRow(line)) return null;
  return cells;
}

function buildChart(header, rows) {
  const columns = rows[0].cells.length;
  const labels = rows.map(r => r.label);
  const all = rows.flatMap(r => r.cells).filter(c => !c.missing);
  const series = Array.from({ length: columns }, (_, i) => ({
    name: header ? stripInline(header[i + 1]) : columns === 1 ? 'Valores' : `Série ${i + 1}`,
    values: rows.map(r => r.cells[i].value),
  }));
  const signedOrNegative = all.some(c => c.signed || c.value < 0);
  const years = labels.every(l => YEAR.test(l));
  const chart = {
    ...createEmptyChart(),
    // Séries temporais com muitas séries ficam legíveis em linha; variações com sinal, em barras.
    chartType: years && (columns >= 3 || !signedOrNegative) ? 'line' : 'bar',
    title: header && columns === 1 ? stripInline(header[1]) : '',
    xAxisLabel: header ? stripInline(header[0]) : '',
    unit: all.length > 0 && all.every(c => c.percent) ? '%' : '',
    labels,
    series,
    sources: [],
  };
  return validateChart(chart).length === 0 ? chart : null;
}

const trimBlankLines = lines => {
  let a = 0;
  let b = lines.length;
  while (a < b && !lines[a].trim()) a++;
  while (b > a && !lines[b - 1].trim()) b--;
  return lines.slice(a, b).join('\n');
};

/**
 * Separa o texto colado em segmentos de texto e de gráfico.
 * Cada segmento de gráfico traz `source` (o texto original da tabela) para desfazer/recuperar.
 */
export function splitPastedText(text) {
  // `lines` (normalizadas) servem só para análise; o texto que volta para a nota usa `original`.
  const original = String(text ?? '').replace(/\r\n?/g, '\n').split('\n');
  // Linhas separadoras de tabela markdown (| --- | --- |) contam como linha em branco na análise.
  const lines = original.map(l => (MD_SEPARATOR.test(l) ? '' : normalizePasted(l)));
  const segments = [];
  let textStart = 0;
  let i = 0;

  while (i < lines.length) {
    const first = parseDataRow(lines[i]);
    if (!first) {
      i++;
      continue;
    }

    // Estende o bloco: mesmas colunas, tolerando poucas linhas em branco entre linhas de dados.
    const rows = [first];
    let last = i;
    let j = i + 1;
    let blanks = 0;
    while (j < lines.length && rows.length < CHART_LIMITS.maxLabels) {
      if (!lines[j].trim()) {
        if (++blanks > MAX_BLANKS_INSIDE) break;
        j++;
        continue;
      }
      const row = parseDataRow(lines[j]);
      if (!row || row.cells.length !== first.cells.length) break;
      rows.push(row);
      last = j;
      blanks = 0;
      j++;
    }

    if (rows.length < 2) {
      i++;
      continue;
    }

    // Cabeçalho opcional logo acima (pulando linhas em branco), sem invadir o texto já consumido.
    let start = i;
    let header = null;
    let k = i - 1;
    let skipped = 0;
    while (k >= textStart && !lines[k].trim() && skipped < MAX_BLANKS_INSIDE) {
      k--;
      skipped++;
    }
    if (k >= textStart && lines[k].trim()) {
      header = parseHeader(lines[k], first.cells.length + 1);
      if (header) start = k;
    }

    const chart = buildChart(header, rows);
    if (!chart) {
      i = last + 1;
      continue;
    }

    // "Fonte:" logo abaixo da tabela (no máximo 1 linha em branco entre elas) vira fonte declarada.
    let end = last;
    let n = last + 1;
    if (n < lines.length && !lines[n].trim()) n++;
    const sourceMatch = n < lines.length ? SOURCE_LINE.exec(original[n]) : null;
    if (sourceMatch) {
      const label = stripInline(sourceMatch[1]).trim();
      if (label) {
        chart.sources = [{ label: clip(label, CHART_LIMITS.sourceLabel) }];
        end = n;
      }
    }

    const before = trimBlankLines(original.slice(textStart, start));
    if (before) segments.push({ type: 'text', text: before });
    segments.push({ type: 'chart', chart, source: original.slice(start, end + 1).join('\n') });
    textStart = end + 1;
    i = end + 1;
  }

  const rest = trimBlankLines(original.slice(textStart));
  if (rest) segments.push({ type: 'text', text: rest });
  return segments;
}
