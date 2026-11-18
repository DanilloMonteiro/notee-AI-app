import test from 'node:test';
import assert from 'node:assert/strict';
import {
  UNVERIFIED_ORIGIN_NOTE,
  acceptedSourceEntries,
  acceptedSources,
  buildBlocks,
  citationBodies,
  createComposedNote,
  initialSelection,
  parseNoteLink,
  toggleChart,
  toggleOrigin,
  toggleSource,
} from './composeNote.js';

const chart = { type: 'chart', schemaVersion: 1, chartType: 'bar', title: 'PIB', labels: ['2014'], series: [{ name: 'PIB', values: [0.5] }] };
const ibge = { kind: 'website', title: 'IBGE', authors: [], year: 2020, url: 'https://ibge.gov.br' };
const result = {
  blocks: [
    { type: 'paragraph', text: 'Intro' },
    { type: 'chart', ref: 0 },
    { type: 'table', text: '| a | b |' },
    { type: 'heading', text: 'Seção', level: 2 },
  ],
  charts: [{ chart: { ...chart, sources: [{ label: 'IBGE', url: 'https://ibge.gov.br' }] }, origin: 'generated', sourceIndex: 0 }],
  category: { name: 'Economia', existingId: 'c1', isNew: false },
  tags: [{ name: 'pib', existingId: null, isNew: true }, { name: 'juros', existingId: 't2', isNew: false }],
  sources: [{ identifier: 'https://ibge.gov.br', reason: 'dados', resolved: ibge }],
  attribution: null,
};

test('reconhece links de nota própria, pública e compartilhada e recusa o resto', () => {
  assert.equal(parseNoteLink('https://x.app/note/8f9b1a8e-2c3d-4e5f-8a9b-1c2d3e4f5a6b').kind, 'own');
  assert.deepEqual(parseNoteLink('/p/maria/economia', 'http://localhost:5173'), { kind: 'public', url: 'http://localhost:5173/p/maria/economia' });
  assert.equal(parseNoteLink('http://localhost:5173/s/AbC_1?x=1').url, 'http://localhost:5173/s/AbC_1');
  for (const bad of ['', 'nota', '/note/123', 'ftp://x/s/abc', 'https://example.com/qualquer']) assert.equal(parseNoteLink(bad), null);
});

test('desmarcar a fonte desmarca o gráfico gerado; marcar o gráfico remarca a fonte', () => {
  let sel = initialSelection(result);
  sel = toggleSource(sel, result, 0);
  assert.deepEqual([sel.sources[0], sel.charts[0]], [false, false]);
  sel = toggleChart(sel, result, 0);
  assert.deepEqual([sel.sources[0], sel.charts[0]], [true, true]);
});

test('monta blocos do editor: tabela vira texto, gráfico desmarcado sai, ids novos', () => {
  const all = buildBlocks(result, initialSelection(result));
  assert.deepEqual(all.map(b => b.type), ['paragraph', 'chart', 'paragraph', 'heading']);
  assert.equal(all[3].level, 2);
  assert.ok(all.every(b => b.id));
  assert.equal(all[1].sources[0].label, 'IBGE');

  const withoutChart = buildBlocks(result, { ...initialSelection(result), charts: [false] });
  assert.equal(withoutChart.some(b => b.type === 'chart'), false);
});

test('atribuição de nota de outra pessoa entra sempre como fonte', () => {
  const withBase = { ...result, attribution: { kind: 'website', title: 'Nota da Maria', authors: ['Maria'], year: 2026, url: 'https://x/p/maria/a' } };
  const list = acceptedSources(withBase, { ...initialSelection(withBase), sources: [false] });
  assert.deepEqual(list.map(s => s.title), ['Nota da Maria']);
});

test('cria a nota com taxonomia aceita e cita as fontes; reaproveita tag e fonte já existentes', async () => {
  const calls = [];
  const conflict = Object.assign(new Error('dup'), { status: 409 });
  const api = {
    createTaxonomy: async (kind, name) => {
      calls.push(['tax', kind, name]);
      throw conflict;
    },
    listTaxonomy: async () => [{ id: 't1', name: 'PIB' }],
    createNote: async body => {
      calls.push(['note', body.categoryId, body.tagIds]);
      return { id: 'n1' };
    },
    createSource: async () => {
      throw Object.assign(new Error('dup'), { status: 409 });
    },
    listSources: async () => [],
    addCitation: async () => calls.push(['cite']),
  };

  const { note, warnings } = await createComposedNote({ api, title: 'T', result, selection: initialSelection(result) });

  assert.equal(note.id, 'n1');
  assert.deepEqual(calls.find(c => c[0] === 'note'), ['note', 'c1', ['t1', 't2']]);
  // A fonte não pôde ser reaproveitada nem criada: a nota continua criada e o problema volta como aviso.
  assert.deepEqual(warnings, ['IBGE']);
});

test('citações ficam ancoradas nos trechos que a fonte apoia; sem trecho localizado, nota inteira', () => {
  const blocks = [{ id: 'b1', type: 'paragraph', text: 'O PIB caiu 3,5% em 2015. Outra frase.' }];
  assert.deepEqual(citationBodies('s1', ['O PIB caiu 3,5% em 2015.', 'não existe'], blocks), [
    { sourceId: 's1', blockId: 'b1', exactQuote: 'O PIB caiu 3,5% em 2015.', startIndex: 0, endIndex: 24 },
  ]);
  assert.deepEqual(citationBodies('s1', ['não existe'], blocks), [{ sourceId: 's1' }]);
  assert.deepEqual(citationBodies('s1', [], blocks), [{ sourceId: 's1' }]);
});

test('origens aceitas viram fonte "Outro" sem link e com aviso de não conferida', () => {
  const withOrigins = {
    ...result,
    origins: [
      { label: 'IBGE, Censo 2022', supports: ['Intro'], unresolvedIdentifier: false },
      { label: '10.9999/x', supports: [], unresolvedIdentifier: true },
    ],
  };
  let sel = initialSelection(withOrigins);
  assert.deepEqual(sel.origins, [true, true]);
  sel = toggleOrigin(sel, 1);
  const entries = acceptedSourceEntries(withOrigins, sel);
  const origin = entries.at(-1);
  assert.equal(entries.length, 2); // fonte conferida + 1 origem marcada
  assert.equal(origin.input.kind, 'other');
  assert.equal(origin.input.url, null);
  assert.equal(origin.input.notes, UNVERIFIED_ORIGIN_NOTE);
  assert.deepEqual(origin.supports, ['Intro']);
});

test('criar nota cita a fonte no trecho que ela apoia', async () => {
  const cited = [];
  const api = {
    createTaxonomy: async (kind, name) => ({ id: `${kind}-${name}` }),
    listTaxonomy: async () => [],
    createNote: async () => ({ id: 'n1' }),
    createSource: async () => ({ id: 's1' }),
    listSources: async () => [],
    addCitation: async (noteId, body) => cited.push(body),
  };
  const withSupport = { ...result, sources: [{ ...result.sources[0], supports: ['Intro'] }] };
  const { warnings } = await createComposedNote({ api, title: 'T', result: withSupport, selection: initialSelection(withSupport) });
  assert.deepEqual(warnings, []);
  assert.equal(cited.length, 1);
  assert.equal(cited[0].exactQuote, 'Intro');
  assert.equal(cited[0].startIndex, 0);
  assert.ok(cited[0].blockId);
});
