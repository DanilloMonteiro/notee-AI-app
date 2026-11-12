// Modelo do bloco de gráfico (ADR-036). Espelha os limites validados pela API em NoteContent.cs;
// a API continua sendo a autoridade — esta validação só antecipa o feedback no editor.

export const CHART_SCHEMA_VERSION = 1;

export const CHART_LIMITS = {
  maxCharts: 20,
  maxSeries: 8,
  maxLabels: 120,
  maxSources: 10,
  title: 200,
  description: 2000,
  short: 80,
  sourceLabel: 200,
  url: 2048,
};

export const CHART_TYPES = [
  { value: 'line', label: 'Linha' },
  { value: 'bar', label: 'Barras' },
];

const newId = () =>
  typeof crypto !== 'undefined' && crypto.randomUUID ? `c-${crypto.randomUUID()}` : `c-${Math.random().toString(36).slice(2, 11)}`;

export function createEmptyChart() {
  return {
    id: newId(),
    type: 'chart',
    schemaVersion: CHART_SCHEMA_VERSION,
    chartType: 'line',
    title: '',
    description: '',
    xAxisLabel: '',
    yAxisLabel: '',
    unit: '',
    labels: ['', ''],
    series: [{ name: 'Série 1', values: [null, null] }],
    sources: [],
  };
}

/** Converte o texto digitado num número finito ou null (ausente). Aceita "1,5" e "1.5". */
export function parseNumberInput(raw) {
  const text = String(raw ?? '').trim();
  if (text === '') return { value: null, valid: true };
  // "1.234,5" (pt-BR) -> remove separador de milhar quando há vírgula decimal
  const normalized = text.includes(',') ? text.replace(/\./g, '').replace(',', '.') : text;
  const number = Number(normalized);
  return Number.isFinite(number) && /^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(normalized)
    ? { value: number, valid: true }
    : { value: null, valid: false };
}

export const isHttpUrl = url => {
  try {
    const u = new URL(url);
    return u.protocol === 'http:' || u.protocol === 'https:';
  } catch {
    return false;
  }
};

export const isChartBlock = block => !!block && typeof block === 'object' && block.type === 'chart';

/** Lista de mensagens de erro (vazia = válido). */
export function validateChart(chart) {
  const L = CHART_LIMITS;
  const errors = [];
  const tooLong = (value, max, label) => {
    if ((value || '').length > max) errors.push(`${label} acima de ${max} caracteres.`);
  };

  if (!CHART_TYPES.some(t => t.value === chart.chartType)) errors.push('Tipo de gráfico não suportado.');
  tooLong(chart.title, L.title, 'Título');
  tooLong(chart.description, L.description, 'Descrição');
  tooLong(chart.xAxisLabel, L.short, 'Eixo X');
  tooLong(chart.yAxisLabel, L.short, 'Eixo Y');
  tooLong(chart.unit, L.short, 'Unidade');

  const labels = chart.labels || [];
  if (labels.length < 1 || labels.length > L.maxLabels) errors.push(`Informe de 1 a ${L.maxLabels} rótulos.`);
  if (labels.some(l => !String(l || '').trim())) errors.push('Todo rótulo (período/categoria) precisa ser preenchido.');
  if (labels.some(l => String(l || '').length > L.short)) errors.push(`Rótulo acima de ${L.short} caracteres.`);

  const series = chart.series || [];
  if (series.length < 1 || series.length > L.maxSeries) errors.push(`Informe de 1 a ${L.maxSeries} séries.`);
  series.forEach((s, i) => {
    if (!String(s.name || '').trim()) errors.push(`A série ${i + 1} precisa de nome.`);
    tooLong(s.name, L.short, `Nome da série ${i + 1}`);
    if ((s.values || []).length !== labels.length) errors.push(`A série "${s.name || i + 1}" está desalinhada dos rótulos.`);
    if ((s.values || []).some(v => v !== null && !Number.isFinite(v)))
      errors.push(`A série "${s.name || i + 1}" tem valor inválido.`);
  });

  const sources = chart.sources || [];
  if (sources.length > L.maxSources) errors.push(`No máximo ${L.maxSources} fontes.`);
  sources.forEach((src, i) => {
    if (!String(src.label || '').trim()) errors.push(`A fonte ${i + 1} precisa de um rótulo.`);
    tooLong(src.label, L.sourceLabel, `Rótulo da fonte ${i + 1}`);
    if (src.url && (!isHttpUrl(src.url) || src.url.length > L.url))
      errors.push(`A URL da fonte ${i + 1} deve começar com http:// ou https://.`);
  });

  return errors;
}

/** Remove fontes totalmente vazias antes de salvar (campos opcionais vazios viram ausentes). */
export function cleanChart(chart) {
  return {
    ...chart,
    sources: (chart.sources || [])
      .filter(s => (s.label || '').trim() || (s.url || '').trim())
      .map(s => (s.url && s.url.trim() ? { label: s.label.trim(), url: s.url.trim() } : { label: s.label.trim() })),
  };
}

/** Escala "agradável" para o eixo Y; sempre inclui zero para não exagerar diferenças em barras. */
export function niceScale(min, max, ticks = 5) {
  let lo = Math.min(0, min);
  let hi = Math.max(0, max);
  if (lo === hi) hi = lo + 1;
  const rough = (hi - lo) / ticks;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map(m => m * magnitude).find(s => s >= rough) || 10 * magnitude;
  lo = Math.floor(lo / step) * step;
  hi = Math.ceil(hi / step) * step;
  const values = [];
  for (let v = lo; v <= hi + step / 2; v += step) values.push(Number(v.toPrecision(12)));
  return { min: lo, max: hi, ticks: values };
}

const numberFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 4 });
export const formatValue = (value, unit) =>
  value === null || value === undefined ? 'sem dado' : `${numberFormat.format(value)}${unit ? ` ${unit}` : ''}`;
export const formatNumber = value => (value === null || value === undefined ? '' : numberFormat.format(value));

/** Texto simples de um gráfico, para trechos e buscas no cliente (sem valores). */
export function chartPlainText(chart) {
  return [
    chart.title ? `Gráfico: ${chart.title}` : 'Gráfico',
    chart.description,
    (chart.series || []).map(s => s.name).join(', '),
  ]
    .filter(Boolean)
    .join(' — ');
}
