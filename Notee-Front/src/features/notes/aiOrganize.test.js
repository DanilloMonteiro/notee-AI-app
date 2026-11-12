// Conversões do "Organizar com IA" (`npm test`).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { blocksToText, responseToItems } from './aiOrganize.js';

const chart = { id: 'c1', type: 'chart', chartType: 'bar', labels: ['a', 'b'], series: [{ name: 's', values: [1, 2] }] };

test('blocos viram texto com marcadores e gráficos voltam pela referência', () => {
  const blocks = [
    { id: '1', type: 'heading', level: 1, text: 'Título' },
    { id: '2', type: 'paragraph', text: 'Texto' },
    { id: '3', type: 'list', text: 'um\ndois' },
    chart,
    { id: '4', type: 'paragraph', text: '   ' },
  ];
  const { text, charts } = blocksToText(blocks);
  assert.equal(text, '# Título\n\nTexto\n\n- um\n- dois\n\n[[grafico:0]]');
  assert.deepEqual(charts, [chart]);

  const items = responseToItems(
    [
      { type: 'heading', level: 1, text: 'Título' },
      { type: 'chart', ref: 0 },
      { type: 'table', text: '| Ano | PIB |\n| --- | --- |\n| 2014 | 0,5 |\n| 2015 | -3,5 |' },
      { type: 'chart', ref: 7 },
    ],
    charts,
  );
  assert.equal(items[0].type, 'heading');
  assert.equal(items[1].chart, chart, 'gráfico original reaproveitado');
  assert.equal(items[2].type, 'chart');
  assert.deepEqual(items[2].chart.series[0].values, [0.5, -3.5]);
  assert.equal(items.length, 3, 'referência inexistente é ignorada');
});
