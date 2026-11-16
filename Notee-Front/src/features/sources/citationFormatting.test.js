import test from 'node:test';
import assert from 'node:assert/strict';
import { formatBibliographyCitation, formatLocator, formatTimestamp, parseTimestamp } from './citationFormatting.js';

test('interpreta página e intervalo de páginas', () => {
  assert.equal(formatLocator({ pageStart: 45 }), 'p. 45');
  assert.equal(formatLocator({ pageStart: 45, pageEnd: 47 }), 'p. 45–47');
});

test('interpreta minuto:segundo e hora:minuto:segundo', () => {
  assert.equal(parseTimestamp('12:34'), 754);
  assert.equal(parseTimestamp('1:02:03'), 3723);
  assert.equal(formatTimestamp(754), '12:34');
  assert.equal(formatTimestamp(3723), '1:02:03');
  assert.equal(formatLocator({ timeStartSec: 754, timeEndSec: 790 }), '12:34–13:10');
});

test('recusa timestamps malformados e segundos/minutos fora do intervalo', () => {
  assert.ok(Number.isNaN(parseTimestamp('12')));
  assert.ok(Number.isNaN(parseTimestamp('12:90')));
  assert.ok(Number.isNaN(parseTimestamp('1:60:00')));
  assert.ok(Number.isNaN(parseTimestamp('abc:12')));
  assert.equal(parseTimestamp(''), null);
});

test('formata a referência e não publica trecho da fonte', () => {
  const citation = {
    authors: ['Ada Lovelace'], title: 'Anotações', venue: 'Editora Teste', year: 2024,
    pageStart: 45, pageEnd: 47, sourceQuote: 'trecho privado',
  };
  assert.equal(formatBibliographyCitation(citation), 'Ada Lovelace. Anotações. Editora Teste. 2024. p. 45–47');
  assert.ok(!formatBibliographyCitation(citation).includes(citation.sourceQuote));
});
