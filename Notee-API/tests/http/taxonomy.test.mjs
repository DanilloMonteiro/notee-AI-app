import { call, newUser, blocks, ok, summary } from './lib.mjs';

const owner = await newUser('taxonomy');
const other = await newUser('taxonomy-other');
const category = (await call(owner, 'POST', '/notes/categories', { name: 'Economia' })).json;
const tag = (await call(owner, 'POST', '/notes/tags', { name: 'História' })).json;
const note = (await call(owner, 'POST', '/notes', { title: 'Economia e História', content: blocks('Inflação inflação crescimento crescimento') })).json;
let r = await call(owner, 'POST', `/notes/${note.id}/taxonomy-ai/estimate`, {});
ok(r.status === 200 && r.json.provider === 'fake' && r.json.estimatedCostUsd === 0, 'estimativa local sem custo');
ok((await call(owner, 'POST', `/notes/${note.id}/taxonomy-ai`, {})).status === 400, 'confirmação obrigatória');
r = await call(owner, 'POST', `/notes/${note.id}/taxonomy-ai`, { confirm: true });
ok(r.status === 200 && r.json.category.existingId === category.id && r.json.tags.some(t => t.existingId === tag.id), 'existentes normalizadas');
ok(r.json.tags.some(t => t.isNew), 'tag nova sugerida');
ok((await call(other, 'POST', `/notes/${note.id}/taxonomy-ai`, { confirm: true })).status === 404, 'nota alheia isolada');
ok((await call(owner, 'GET', `/notes/${note.id}`)).json.version === note.version, 'nenhuma alteração aplicada no servidor');
process.exit(summary('taxonomy') ? 1 : 0);
