import test from 'node:test';
import assert from 'node:assert/strict';
import { buildMarkedHtml, findQuote, parseMarks, segmentText } from './markSegments.js';

test('findQuote prefere a ocorrência mais próxima da posição gravada', () => {
  assert.equal(findQuote('sol e sol', 'sol', 6), 6);
  assert.equal(findQuote('sol e sol', 'sol', 0), 0);
  assert.equal(findQuote('sol', 'lua', 0), -1);
});

test('texto sem âncoras não ganha marcação', () => {
  assert.equal(buildMarkedHtml('texto simples', []), 'texto simples');
});

test('tipos diferentes sobrepostos viram segmentos com classes combinadas, sem mark aninhado', () => {
  const html = buildMarkedHtml('abcdefgh', [
    { kind: 'doubt', id: 'd1', exactQuote: 'bcd', startIndex: 1 },
    { kind: 'source', id: 's1', exactQuote: 'cdef', startIndex: 2, referenceNumber: 1 },
  ]);
  assert.equal(
    html,
    'a<mark class="nm nm-doubt" data-marks="doubt:d1">b</mark>' +
      '<mark class="nm nm-doubt nm-source" data-marks="doubt:d1,source:s1">cd</mark>' +
      '<mark class="nm nm-source" data-marks="source:s1" data-ref="1">ef</mark>gh',
  );
  assert.equal(html.includes('<mark class="nm nm-doubt"><mark'), false);
});

test('número da fonte só no segmento onde a citação termina', () => {
  const segs = segmentText('um dois tres', [
    { kind: 'source', id: 's1', exactQuote: 'um dois', startIndex: 0, referenceNumber: 2 },
    { kind: 'comment', id: 'c1', exactQuote: 'um', startIndex: 0 },
  ]);
  assert.deepEqual(segs.map(s => s.refs), [[], [2], []]);
});

test('escapa o texto e ignora âncoras que não existem mais', () => {
  const html = buildMarkedHtml('a < b & "c"', [
    { kind: 'comment', id: 'c1', exactQuote: '< b', startIndex: 2 },
    { kind: 'doubt', id: 'd1', exactQuote: 'sumiu', startIndex: 0 },
  ]);
  assert.equal(html, 'a <mark class="nm nm-comment" data-marks="comment:c1">&lt; b</mark> &amp; &quot;c&quot;');
});

test('mesma âncora repetida não duplica a marca no segmento', () => {
  const segs = segmentText('abc', [
    { kind: 'doubt', id: 'd1', exactQuote: 'abc' },
    { kind: 'doubt', id: 'd1', exactQuote: 'abc' },
  ]);
  assert.deepEqual(segs[0].marks, [{ kind: 'doubt', id: 'd1' }]);
});

test('negrito e itálico entram por dentro das marcações, sem mudar o texto', () => {
  const html = buildMarkedHtml(
    'abcdef',
    [{ kind: 'doubt', id: 'd1', exactQuote: 'cdef', startIndex: 2 }],
    [{ start: 0, end: 4, bold: true }, { start: 3, end: 5, italic: true }],
  );
  assert.equal(
    html,
    '<strong>ab</strong>' +
      '<mark class="nm nm-doubt" data-marks="doubt:d1"><strong>c</strong></mark>' +
      '<mark class="nm nm-doubt" data-marks="doubt:d1"><strong><em>d</em></strong></mark>' +
      '<mark class="nm nm-doubt" data-marks="doubt:d1"><em>e</em></mark>' +
      '<mark class="nm nm-doubt" data-marks="doubt:d1">f</mark>',
  );
  assert.equal(html.replace(/<[^>]+>/g, ''), 'abcdef');
});

test('parseMarks lê o atributo data-marks e descarta tipos desconhecidos', () => {
  assert.deepEqual(parseMarks('doubt:1,source:a-b,xpto:9'), [
    { kind: 'doubt', id: '1' },
    { kind: 'source', id: 'a-b' },
  ]);
});
