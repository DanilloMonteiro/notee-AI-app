import test from 'node:test';
import assert from 'node:assert/strict';
import { blockableDomain, formatComposition, shortAuthors, sourceComposition, sourceModeLabel, sourceOriginOf } from './sourceMix.js';

test('origem: gravada, inferida da biblioteca ou desconhecida', () => {
  assert.equal(sourceOriginOf({ origin: 'academic', url: 'https://doi.org/10.1/x' }), 'academic');
  assert.equal(sourceOriginOf({ kind: 'article', title: 'x' }), 'academic');
  assert.equal(sourceOriginOf({ doi: '10.1/x', kind: 'website' }), 'academic');
  assert.equal(sourceOriginOf({ kind: 'website', url: 'https://ex.org' }), 'web');
  assert.equal(sourceOriginOf({ title: 'IBGE', url: null, verified: false }), null);
});

test('composição em porcentagem só com origens conhecidas', () => {
  const c = sourceComposition([{ origin: 'academic' }, { origin: 'academic' }, { origin: 'academic' }, { origin: 'web' }, { title: 'declarada', verified: false }]);
  assert.deepEqual(c, { academic: 3, web: 1, total: 4, academicPercent: 75, webPercent: 25 });
  assert.equal(formatComposition(c), '75% artigos · 25% internet');
  assert.equal(formatComposition(sourceComposition([{ origin: 'academic' }])), '100% artigos');
  assert.equal(formatComposition(sourceComposition([])), null);
});

test('autores curtos, domínio bloqueável e rótulo do modo', () => {
  assert.equal(shortAuthors('Ana Souza; João Lima; Bia Reis'), 'Ana Souza, João Lima e outros');
  assert.equal(shortAuthors(['Ana Souza']), 'Ana Souza');
  assert.equal(blockableDomain({ origin: 'web', url: 'https://www.exemplo.org/a' }), 'exemplo.org');
  assert.equal(blockableDomain({ origin: 'academic', url: 'https://doi.org/10.1/x' }), null);
  assert.equal(sourceModeLabel('mixed'), 'Artigos + internet');
});

test('textos da estimativa e da prévia da criação de nota', async () => {
  const { searchBasisText, sourcesEstimateText } = await import('./sourceMix.js');
  assert.match(sourcesEstimateText({ mode: 'mixed', willSearchAcademic: true, mayUseWeb: true, estimatedCostUsd: 0.0031 }), /internet completa se faltar \(até ≈ US\$ 0\.0031/);
  assert.equal(sourcesEstimateText({ mode: 'none' }), 'Sem pesquisa');
  assert.match(sourcesEstimateText({ mode: 'web', willSearchAcademic: false, mayUseWeb: false }), /DeepSeek/);
  assert.equal(searchBasisText({ academicResults: 3, webResults: 1 }, 4), '3 artigo(s) e 1 página(s) da web');
  assert.equal(searchBasisText(undefined, 2), '2 página(s) da web');
});
