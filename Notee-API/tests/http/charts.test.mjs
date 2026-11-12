// Gráficos estruturados em notas (ADR-036): validação, busca, revisões e snapshot publicado.
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('chart');
const other = await newUser('chart-other');

const marker = `indicador${Date.now()}`;
const chart = (values, extra = {}) => ({
  id: 'c1',
  type: 'chart',
  schemaVersion: 1,
  chartType: 'line',
  title: `Inflação ${marker}`,
  description: 'Variação anual do IPCA',
  xAxisLabel: 'Ano',
  yAxisLabel: 'Variação',
  unit: '%',
  labels: ['2019', '2020', '2021'],
  series: [{ name: 'IPCA', values }],
  sources: [{ label: 'IBGE', url: 'https://www.ibge.gov.br' }],
  ...extra,
});
const content = (...bs) => JSON.stringify([{ id: 'b1', type: 'paragraph', text: 'Contexto' }, ...bs]);
const put = (id, version, c, tk = t) => call(tk, 'PUT', `/notes/${id}`, { title: 'Indicadores', content: c, expectedVersion: version, categoryId: null, tagIds: [] });

// Criação com valor ausente (null) preservado
let r = await call(t, 'POST', '/notes', { title: 'Indicadores', content: content(chart([4.31, null, 10.06])) });
ok(r.status === 201 || r.status === 200, 'cria nota com gráfico');
const note = r.json;
let saved = JSON.parse(note.content)[1];
ok(saved.type === 'chart' && saved.series[0].values[1] === null && saved.series[0].values[0] === 4.31, 'valor ausente continua null (não vira zero)');

// Validação no servidor
ok((await call(t, 'POST', '/notes', { title: 'x', content: content(chart([1, 2, 3], { chartType: 'pie' })) })).status === 400, 'tipo não suportado -> 400');
ok((await put(note.id, note.version, content(chart([1, 2])))).status === 400, 'série desalinhada -> 400');
ok((await put(note.id, note.version, content(chart([1, '2', 3])))).status === 400, 'valor não numérico -> 400');
ok((await put(note.id, note.version, content(chart([1, 2, 3], { sources: [{ label: 'x', url: 'javascript:alert(1)' }] })))).status === 400, 'URL insegura -> 400');
ok(
  (await put(note.id, note.version, JSON.stringify(Array.from({ length: 21 }, (_, i) => chart([1, 2, 3], { id: `c${i}` }))))).status === 400,
  'mais de 20 gráficos -> 400',
);
ok((await call(t, 'GET', `/notes/${note.id}`)).json.version === note.version, 'conteúdo inválido não altera a nota');

// Busca pelo texto do gráfico, isolada por dono
ok((await call(t, 'GET', `/notes?query=${marker}`)).json.length === 1, 'busca encontra o título do gráfico');
ok((await call(other, 'GET', `/notes?query=${marker}`)).json.length === 0, 'busca do gráfico isolada por dono');

// Edição e revisões
r = await put(note.id, note.version, content(chart([4.31, 4.52, 10.06], { chartType: 'bar' })));
ok(r.status === 200 && JSON.parse(r.json.content)[1].chartType === 'bar', 'atualiza valores e tipo do gráfico');
const v2 = r.json;

// Publicação (snapshot) não recebe dados posteriores
const pub = await call(t, 'POST', `/notes/${note.id}/publications`, { visibility: 'unlisted' });
ok(pub.status === 201, 'publica nota com gráfico');
r = await put(note.id, v2.version, content(chart([0, 0, 0])));
const v3 = r.json;
const shared = await call(null, 'GET', `/shared/${pub.json.shareToken}`);
const publishedChart = JSON.parse(shared.json.content)[1];
ok(shared.status === 200 && publishedChart.series[0].values[1] === 4.52 && publishedChart.chartType === 'bar', 'publicação mantém os dados do snapshot');

const revs = (await call(t, 'GET', `/notes/${note.id}/revisions`)).json;
const first = revs.find(x => x.version === 1);
r = await call(t, 'POST', `/notes/${note.id}/restore-revision`, { revisionId: first.id, expectedVersion: v3.version });
saved = JSON.parse(r.json.content)[1];
ok(r.status === 200 && saved.series[0].values[1] === null && saved.chartType === 'line', 'restaurar revisão recupera o gráfico original');

// Notas antigas continuam funcionando
const legacy = (await call(t, 'POST', '/notes', { title: 'Antiga', content: 'texto puro [sem json' })).json;
ok((await put(legacy.id, legacy.version, 'texto puro editado')).status === 200, 'nota em texto puro continua salvando');
ok((await put(legacy.id, legacy.version + 1, blocks('agora em blocos'))).status === 200, 'nota em blocos sem gráfico continua salvando');

process.exit(summary('charts') ? 1 : 0);
