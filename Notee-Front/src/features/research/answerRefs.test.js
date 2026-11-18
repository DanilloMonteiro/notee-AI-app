import test from 'node:test';
import assert from 'node:assert/strict';
import { isVerifiedSource, sourceHost, splitAnswerRefs } from './answerRefs.js';

const sources = [
  { title: 'Artigo', url: 'https://doi.org/10.1/x', verified: true },
  { title: 'IBGE, Censo 2022', url: null, verified: false },
];

test('divide a resposta em texto e marcadores ligados às fontes', () => {
  assert.deepEqual(splitAnswerRefs('A [1]. B [2]. C [7].', sources), [
    { type: 'text', value: 'A ' },
    { type: 'ref', n: 1, source: sources[0] },
    { type: 'text', value: '. B ' },
    { type: 'ref', n: 2, source: sources[1] },
    { type: 'text', value: '. C [7].' },
  ]);
  assert.deepEqual(splitAnswerRefs('Sem marcador', sources), [{ type: 'text', value: 'Sem marcador' }]);
  assert.deepEqual(splitAnswerRefs('', sources), []);
});

test('só fonte com link e não marcada como declarada é conferida; respostas antigas contam como conferidas', () => {
  assert.equal(isVerifiedSource(sources[0]), true);
  assert.equal(isVerifiedSource(sources[1]), false);
  assert.equal(isVerifiedSource({ title: 'antiga', url: 'https://ex.org' }), true);
  assert.equal(sourceHost('https://www.ibge.gov.br/x'), 'ibge.gov.br');
  assert.equal(sourceHost('não é url'), '');
});

import { isHighAuthority, sourceMeta } from './answerRefs.js';

test('metadados da busca: site, mês/ano e autoridade', () => {
  assert.match(sourceMeta({ siteName: 'IBGE', publishedAt: '2026-06-01T00:00:00Z' }), /^IBGE · jun\.? de 2026$/);
  assert.equal(sourceMeta({ siteName: 'IBGE' }), 'IBGE');
  assert.equal(sourceMeta({}), '');
  assert.equal(isHighAuthority({ authority: 3 }), true);
  assert.equal(isHighAuthority({ authority: 2 }), false);
  assert.equal(isHighAuthority({}), false);
});
