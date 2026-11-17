// Criação de nota assistida por IA (ADR-044) com o provedor simulado: prévia sem gravar, link de nota e criação com taxonomia.
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const owner = await newUser('compose');
const other = await newUser('compose-other');
const category = (await call(owner, 'POST', '/notes/categories', { name: 'Economia' })).json;
const generate = { mode: 'generate', title: 'Inflação', instructions: 'Explicar a inflação na economia brasileira. Comparar inflação e juros.' };

let r = await call(owner, 'POST', '/notes/compose-ai/estimate', generate);
ok(r.status === 200 && r.json.provider === 'fake' && r.json.estimatedCostUsd === 0, 'estimativa local sem custo');
ok((await call(owner, 'POST', '/notes/compose-ai', generate)).status === 400, 'confirmação obrigatória');
ok((await call(owner, 'POST', '/notes/compose-ai/estimate', { ...generate, title: '' })).status === 400, 'título obrigatório');
ok((await call(owner, 'POST', '/notes/compose-ai/estimate', { ...generate, instructions: '' })).status === 400, 'descrição obrigatória ao gerar');

const before = (await call(owner, 'GET', '/notes')).json;
r = await call(owner, 'POST', '/notes/compose-ai', { ...generate, confirm: true });
ok(r.status === 200 && r.json.blocks.length > 0 && r.json.category?.existingId === category.id, 'prévia com texto e categoria existente');
ok(r.json.sources.length === 0 && r.json.charts.length === 0, 'simulação não inventa fontes nem gráficos');
ok(JSON.stringify((await call(owner, 'GET', '/notes')).json) === JSON.stringify(before), 'gerar não grava nada');

// Criar com taxonomia aceita (o cliente faz isso depois do aceite).
const tag = (await call(owner, 'POST', '/notes/tags', { name: 'juros' })).json;
const created = await call(owner, 'POST', '/notes', { title: 'Inflação', content: blocks('Texto aceito'), categoryId: category.id, tagIds: [tag.id] });
ok(created.status === 201 && created.json.categoryId === category.id && created.json.tags?.some(t => t.id === tag.id), 'cria nota já com categoria e tag');
const otherTag = (await call(other, 'POST', '/notes/tags', { name: 'alheia' })).json;
ok((await call(owner, 'POST', '/notes', { title: 'X', content: '[]', tagIds: [otherTag.id] })).status === 400, 'tag de outra conta recusada');
ok((await call(owner, 'POST', '/notes', { title: 'X', content: '[]', categoryId: crypto.randomUUID() })).status === 400, 'categoria inexistente recusada');

// Partir de outra nota.
const mine = created.json;
const rewrite = { mode: 'rewrite', title: 'Versão curta', instructions: 'Deixar mais curto', baseLink: `http://localhost:5173/note/${mine.id}` };
r = await call(owner, 'POST', '/notes/compose-ai', { ...rewrite, confirm: true });
ok(r.status === 200 && r.json.attribution === null && r.json.blocks.some(b => b.text === 'Texto aceito'), 'reescreve a própria nota sem atribuição');
ok((await call(other, 'POST', '/notes/compose-ai/estimate', rewrite)).status === 404, 'nota privada de outra conta -> 404');
ok((await call(owner, 'POST', '/notes/compose-ai/estimate', { ...rewrite, baseLink: 'https://example.com/x' })).status === 400, 'link não reconhecido -> 400');

// Publicação de outra pessoa: entra como atribuição; link revogado some.
const handle = `cmp${Date.now().toString().slice(-8)}`;
await call(other, 'PUT', '/me/profile', { handle, displayName: 'Autora Base' });
const baseNote = (await call(other, 'POST', '/notes', { title: 'Nota publicada', content: blocks('Conteúdo público') })).json;
const pub = (await call(other, 'POST', `/notes/${baseNote.id}/publications`, { visibility: 'unlisted' })).json;
const sharedLink = `http://localhost:5173/s/${pub.shareToken}`;
r = await call(owner, 'POST', '/notes/compose-ai', { ...rewrite, baseLink: sharedLink, confirm: true });
ok(r.status === 200 && r.json.attribution?.url === sharedLink && r.json.attribution.authors[0] === 'Autora Base', 'publicação alheia vira atribuição');
ok(r.json.blocks.some(b => b.text === 'Conteúdo público'), 'usa o snapshot publicado');
await call(other, 'DELETE', `/publications/${pub.id}`);
ok((await call(owner, 'POST', '/notes/compose-ai/estimate', { ...rewrite, baseLink: sharedLink })).status === 404, 'link revogado -> 404');

process.exit(summary('compose') ? 1 : 0);
