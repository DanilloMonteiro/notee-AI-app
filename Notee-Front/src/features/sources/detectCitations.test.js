import test from 'node:test';
import assert from 'node:assert/strict';
import { detectCitations } from './detectCitations.js';

test('identifica menção de fonte, autor e ano dos exemplos do plano', () => {
  const results = detectCitations([{ id: 'b1', text: 'Fonte: IBGE, série histórica do IPCA. Barbosa Filho — FGV, 2017' }]);
  assert.ok(results.some(item => item.type === 'source-label' && item.quote.includes('IBGE')));
  assert.ok(results.some(item => item.type === 'venue-year' && item.quote.includes('Barbosa Filho') && item.year === 2017));
});

test('reconhece citação autor-data, DOI, ISBN, link e menção institucional', () => {
  const results = detectCitations([{ id: 'b2', text: '(Silva, 2017), Silva (2018), https://doi.org/10.1000/test, ISBN 978-0-306-40615-7; segundo o IBGE' }]);
  assert.ok(results.some(item => item.type === 'author-year-paren'));
  assert.ok(results.some(item => item.type === 'author-year'));
  assert.ok(results.some(item => item.type === 'doi'));
  assert.ok(results.some(item => item.type === 'isbn'));
  assert.ok(results.some(item => item.type === 'url'));
  assert.ok(results.some(item => item.type === 'ibge-mention'));
});
