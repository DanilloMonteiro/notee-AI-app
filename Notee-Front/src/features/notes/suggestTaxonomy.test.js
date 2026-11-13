import test from 'node:test';
import assert from 'node:assert/strict';
import { suggestTaxonomy } from './suggestTaxonomy.js';

test('prefere categoria e tags existentes encontradas no título, sem sugerir a tag aplicada', () => {
  const result = suggestTaxonomy({ title: 'História da economia', blocks: [{ type: 'heading', text: 'Economia brasileira' }], categories: [{ id: 'c1', name: 'Economia' }], tags: [{ id: 't1', name: 'História' }, { id: 't2', name: 'Economia' }], currentTagIds: ['t1'] });
  assert.equal(result.category.existingId, 'c1');
  assert.equal(result.tags[0].existingId, 't2');
  assert.equal(result.tags.some(t => t.existingId === 't1'), false);
});

test('sugere nomes novos por repetição e categoria nova só sem correspondente', () => {
  const result = suggestTaxonomy({ title: 'Pesquisa', blocks: [{ type: 'heading', text: 'Astronomia solar' }, { type: 'paragraph', text: 'Astronomia solar explora astronomia solar.' }], categories: [{ id: 'c1', name: 'Literatura' }] });
  assert.equal(result.category.isNew, true);
  assert.equal(result.tags.some(t => t.name === 'astronomia' && t.isNew), true);
});
