import test from 'node:test';
import assert from 'node:assert/strict';
import { locateQuote } from './anchors.js';

test('prefere o mesmo bloco e ocorrência mais próxima', () => {
  assert.deepEqual(locateQuote([{ id: 'a', text: 'sol e sol' }, { id: 'b', text: 'sol' }], { blockId: 'a', exactQuote: 'sol', startIndex: 6 }), { blockId: 'a', startIndex: 6, endIndex: 9 });
});
test('reencontra trecho movido', () => {
  assert.equal(locateQuote([{ id: 'b', text: 'novo trecho' }], { blockId: 'a', exactQuote: 'trecho' }).blockId, 'b');
});
test('retorna ausente quando trecho some', () => {
  assert.equal(locateQuote([{ id: 'b', text: 'outro texto' }], { blockId: 'a', exactQuote: 'trecho' }), null);
});
