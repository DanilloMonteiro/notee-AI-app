import test from 'node:test';
import assert from 'node:assert/strict';
import {
  adjustStylesForEdit,
  blockTypeKey,
  convertBlockType,
  createTextBlock,
  hasStyle,
  listLabels,
  normalizeStyles,
  splitBlockAt,
  toggleStyle,
} from './blockFormatting.js';

test('toggleStyle aplica, remove e mescla trechos vizinhos', () => {
  let s = toggleStyle([], 0, 3, 'bold', 10);
  assert.deepEqual(s, [{ start: 0, end: 3, bold: true }]);
  s = toggleStyle(s, 3, 5, 'bold', 10);
  assert.deepEqual(s, [{ start: 0, end: 5, bold: true }]);
  s = toggleStyle(s, 1, 2, 'bold', 10);
  assert.deepEqual(s, [{ start: 0, end: 1, bold: true }, { start: 2, end: 5, bold: true }]);
});

test('toggleStyle em trecho parcialmente formatado aplica no trecho todo', () => {
  const s = toggleStyle([{ start: 0, end: 2, bold: true }], 0, 4, 'bold', 6);
  assert.deepEqual(s, [{ start: 0, end: 4, bold: true }]);
  assert.equal(hasStyle(s, 0, 4, 'bold', 6), true);
});

test('negrito e itálico convivem no mesmo trecho', () => {
  const s = toggleStyle([{ start: 0, end: 4, bold: true }], 2, 6, 'italic', 8);
  assert.deepEqual(s, [
    { start: 0, end: 2, bold: true },
    { start: 2, end: 4, bold: true, italic: true },
    { start: 4, end: 6, italic: true },
  ]);
});

test('adjustStylesForEdit: inserir antes desloca, dentro e no fim estende', () => {
  const bold = [{ start: 2, end: 5, bold: true }]; // "ab[cde]f"
  assert.deepEqual(adjustStylesForEdit('abcdef', 'XXabcdef', bold), [{ start: 4, end: 7, bold: true }]);
  assert.deepEqual(adjustStylesForEdit('abcdef', 'abcXdef', bold), [{ start: 2, end: 6, bold: true }]);
  assert.deepEqual(adjustStylesForEdit('abcdef', 'abcdeXf', bold), [{ start: 2, end: 6, bold: true }]);
  assert.deepEqual(adjustStylesForEdit('abcdef', 'abXcdef', bold), [{ start: 3, end: 6, bold: true }]);
  assert.deepEqual(adjustStylesForEdit('abcdef', 'abcdefX', bold), [{ start: 2, end: 5, bold: true }]);
});

test('adjustStylesForEdit: apagar trecho que cruza o negrito encolhe ou remove', () => {
  const bold = [{ start: 2, end: 5, bold: true }];
  assert.deepEqual(adjustStylesForEdit('abcdef', 'abf', bold), []);
  assert.deepEqual(adjustStylesForEdit('abcdef', 'aef', bold), [{ start: 1, end: 2, bold: true }]);
  assert.deepEqual(adjustStylesForEdit('abcdef', 'abcf', bold), [{ start: 2, end: 3, bold: true }]);
});

test('normalizeStyles corta no tamanho do texto e descarta vazios', () => {
  assert.deepEqual(normalizeStyles([{ start: 3, end: 3, bold: true }, { start: 1, end: 99, italic: true }], 4), [
    { start: 1, end: 4, italic: true },
  ]);
});

test('listLabels: números com subníveis e reinício após parágrafo', () => {
  const blocks = [
    { id: '1', type: 'list', listStyle: 'number', indent: 0 },
    { id: '2', type: 'list', listStyle: 'number', indent: 1 },
    { id: '3', type: 'list', listStyle: 'number', indent: 1 },
    { id: '4', type: 'list', listStyle: 'number', indent: 2 },
    { id: '5', type: 'list', listStyle: 'number', indent: 0 },
    { id: '6', type: 'list', listStyle: 'number', indent: 1 },
    { id: 'p', type: 'paragraph' },
    { id: '7', type: 'list', listStyle: 'number', indent: 0 },
  ];
  const labels = listLabels(blocks);
  assert.deepEqual(['1', '2', '3', '4', '5', '6', '7'].map(id => labels.get(id)), ['1.', '1.1', '1.2', '1.2.1', '2.', '2.1', '1.']);
});

test('listLabels: letras no primeiro nível e números ligados nos internos', () => {
  const blocks = [
    { id: 'a', type: 'list', listStyle: 'letter' },
    { id: 'a1', type: 'list', listStyle: 'letter', indent: 1 },
    { id: 'a2', type: 'list', listStyle: 'letter', indent: 1 },
    { id: 'b', type: 'list', listStyle: 'letter' },
    { id: 'c', type: 'list', listStyle: 'letter' },
  ];
  const labels = listLabels(blocks);
  assert.deepEqual(['a', 'a1', 'a2', 'b', 'c'].map(id => labels.get(id)), ['a.', 'a.1', 'a.2', 'b.', 'c.']);
});

test('listLabels: recuo que pula nível e marcadores', () => {
  const labels = listLabels([
    { id: 'x', type: 'list', listStyle: 'number', indent: 2 },
    { id: 'm', type: 'list' },
  ]);
  assert.equal(labels.get('x'), '1.1.1');
  assert.equal(labels.get('m'), '•');
});

test('convertBlockType: lista antiga de várias linhas vira um item por linha, levando a formatação', () => {
  let n = 0;
  const out = convertBlockType(
    { id: 'b1', type: 'list', text: 'um\ndois', styles: [{ start: 3, end: 7, bold: true }] },
    'list-number',
    () => `n${++n}`,
  );
  assert.deepEqual(out, [
    { id: 'b1', type: 'list', listStyle: 'number', indent: 0, text: 'um' },
    { id: 'n1', type: 'list', listStyle: 'number', indent: 0, text: 'dois', styles: [{ start: 0, end: 4, bold: true }] },
  ]);
});

test('convertBlockType e blockTypeKey: parágrafo ↔ título mantém id, texto e estilos', () => {
  const [h] = convertBlockType({ id: 'x', type: 'paragraph', text: 'Oi', styles: [{ start: 0, end: 2, italic: true }] }, 'heading-1');
  assert.deepEqual(h, { id: 'x', type: 'heading', level: 1, text: 'Oi', styles: [{ start: 0, end: 2, italic: true }] });
  assert.equal(blockTypeKey(h), 'heading-1');
  const [p] = convertBlockType(h, 'paragraph');
  assert.equal(blockTypeKey(p), 'paragraph');
  assert.equal(p.level, undefined);
});

test('splitBlockAt divide texto e formatação no cursor', () => {
  const [a, b] = splitBlockAt(
    { id: 'x', type: 'list', listStyle: 'number', indent: 1, text: 'abcdef', styles: [{ start: 2, end: 5, bold: true }] },
    3,
    'y',
  );
  assert.deepEqual(a, { id: 'x', type: 'list', listStyle: 'number', indent: 1, text: 'abc', styles: [{ start: 2, end: 3, bold: true }] });
  assert.deepEqual(b, { id: 'y', type: 'list', listStyle: 'number', indent: 1, text: 'def', styles: [{ start: 0, end: 2, bold: true }] });
});

test('createTextBlock: item novo de lista herda estilo e nível', () => {
  const ref = { id: 'r', type: 'list', listStyle: 'letter', indent: 2, text: 'x' };
  assert.deepEqual(createTextBlock('list-letter', 'n', ref), { id: 'n', type: 'list', listStyle: 'letter', indent: 2, text: '' });
  assert.deepEqual(createTextBlock('heading-2', 'h', ref), { id: 'h', type: 'heading', level: 2, text: '' });
});

import { deleteTextRange, formatTextRange, orderPositions, textInRange } from './blockFormatting.js';

const range = [
  { id: 'a', type: 'heading', level: 1, text: 'Título', styles: [{ start: 0, end: 6, bold: true }] },
  { id: 'b', type: 'paragraph', text: 'meio' },
  { id: 'c', type: 'chart', chartType: 'bar' },
  { id: 'd', type: 'paragraph', text: 'fim do texto', styles: [{ start: 7, end: 12, italic: true }] },
];

test('orderPositions aceita seleção feita de baixo para cima', () => {
  assert.deepEqual(orderPositions(range, { blockId: 'd', offset: 2 }, { blockId: 'a', offset: 1 }), [
    { blockId: 'a', offset: 1 },
    { blockId: 'd', offset: 2 },
  ]);
});

test('deleteTextRange junta o começo do primeiro bloco com o fim do último, levando a formatação', () => {
  const { blocks, caret } = deleteTextRange(range, { blockId: 'd', offset: 4 }, { blockId: 'a', offset: 3 });
  assert.deepEqual(blocks, [
    // 'Tít' (início do título) + 'do texto' (resto de 'fim do texto' a partir do offset 4)
    { id: 'a', type: 'heading', level: 1, text: 'Títdo texto', styles: [{ start: 0, end: 3, bold: true }, { start: 6, end: 11, italic: true }] },
  ]);
  assert.deepEqual(caret, { blockId: 'a', offset: 3 });
});

test('deleteTextRange com texto digitado substitui a seleção; tudo selecionado vira um bloco vazio', () => {
  const typed = deleteTextRange(range, { blockId: 'b', offset: 1 }, { blockId: 'b', offset: 3 }, 'X');
  assert.equal(typed.blocks[1].text, 'mXo');
  assert.deepEqual(typed.caret, { blockId: 'b', offset: 2 });
  const all = deleteTextRange(range, { blockId: 'a', offset: 0 }, { blockId: 'd', offset: 12 });
  assert.deepEqual(all.blocks, [{ id: 'a', type: 'heading', level: 1, text: '' }]);
});

test('formatTextRange aplica em todos os blocos do trecho e remove quando tudo já tem', () => {
  const bold = formatTextRange(range, { blockId: 'a', offset: 0 }, { blockId: 'd', offset: 3 }, 'bold');
  assert.deepEqual(bold[1].styles, [{ start: 0, end: 4, bold: true }]);
  assert.deepEqual(bold[3].styles, [{ start: 0, end: 3, bold: true }, { start: 7, end: 12, italic: true }]);
  assert.deepEqual(bold[0].styles, [{ start: 0, end: 6, bold: true }]);
  const removed = formatTextRange(bold, { blockId: 'a', offset: 0 }, { blockId: 'd', offset: 3 }, 'bold');
  assert.equal(removed[0].styles, undefined);
  assert.equal(removed[1].styles, undefined);
  assert.equal(bold[2], range[2]); // gráfico intacto
});

test('textInRange devolve o texto selecionado entre blocos', () => {
  assert.equal(textInRange(range, { blockId: 'a', offset: 3 }, { blockId: 'd', offset: 3 }), 'ulo\nmeio\nfim');
});
