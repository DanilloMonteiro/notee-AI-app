// Notas: ciclo de vida (M02), filtros/busca (M07), categorias e tags, isolamento por dono.
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('notes');
const other = await newUser('notes-other');

const a = (await call(t, 'POST', '/notes', { title: 'Democracia direta', content: blocks('Assembleias e plebiscitos') })).json;
const b = (await call(t, 'POST', '/notes', { title: 'Separação de poderes', content: blocks('Freios e contrapesos') })).json;
ok(a.createdAt && a.updatedAt && a.content.includes('Assembleias'), 'criar devolve datas e conteúdo');

// Busca textual no servidor (título e texto simples, sem casar com a estrutura JSON)
let r = await call(t, 'GET', '/notes?query=contrapesos');
ok(r.json.length === 1 && r.json[0].id === b.id, 'busca encontra pelo texto');
ok((await call(t, 'GET', '/notes?query=DEMOCRACIA')).json.length === 1, 'busca ignora maiúsculas no título');
ok((await call(t, 'GET', '/notes?query=paragraph')).json.length === 0, 'busca não casa com chaves do JSON');
ok((await call(t, 'GET', '/notes?query=100%25')).status === 200, 'caracteres curinga escapados');
ok((await call(other, 'GET', '/notes?query=contrapesos')).json.length === 0, 'busca isolada por dono');
ok((await call(t, 'GET', '/notes?pageSize=1')).json.length === 1, 'paginação respeita pageSize');

// Categorias e tags
const cat = await call(t, 'POST', '/notes/categories', { name: 'Política' });
ok(cat.status === 201, 'criar categoria');
ok((await call(t, 'POST', '/notes/categories', { name: 'política' })).status === 409, 'categoria duplicada -> 409');
ok((await call(t, 'POST', '/notes/categories', { name: '  ' })).status === 400, 'categoria vazia -> 400');
const tag = (await call(t, 'POST', '/notes/tags', { name: 'estado' })).json;

const upd = await call(t, 'PUT', `/notes/${a.id}`, { title: a.title, content: a.content, expectedVersion: a.version, categoryId: cat.json.id, tagIds: [tag.id] });
ok(upd.status === 200 && upd.json.categoryName === 'Política' && upd.json.tags.length === 1, 'PUT define categoria e tag');
ok((await call(t, 'GET', `/notes?categoryId=${cat.json.id}`)).json.length === 1, 'filtro por categoria');
ok((await call(t, 'GET', `/notes?tagId=${tag.id}`)).json.length === 1, 'filtro por tag');
ok((await call(t, 'GET', '/notes/categories')).json[0].noteCount === 1, 'lista de categorias com contagem');
ok((await call(other, 'GET', '/notes/categories')).json.length === 0, 'categorias isoladas por dono');
ok((await call(other, 'PATCH', `/notes/categories/${cat.json.id}`, { name: 'x' })).status === 404, 'renomear categoria de outra conta -> 404');
ok((await call(t, 'PATCH', `/notes/categories/${cat.json.id}`, { name: 'Ciência política' })).json.name === 'Ciência política', 'renomear categoria');

const otherCat = (await call(other, 'POST', '/notes/categories', { name: 'Alheia' })).json;
ok((await call(t, 'PUT', `/notes/${a.id}`, { title: a.title, content: a.content, expectedVersion: upd.json.version, categoryId: otherCat.id, tagIds: [] })).status === 400,
  'categoria de outra conta -> 400');

const cleared = await call(t, 'PUT', `/notes/${a.id}`, { title: a.title, content: a.content, expectedVersion: upd.json.version, categoryId: null, tagIds: [] });
ok(cleared.status === 200 && cleared.json.categoryId === null && cleared.json.tags.length === 0, 'PUT remove categoria e todas as tags');
ok((await call(t, 'PUT', `/notes/${a.id}`, { title: 'x', content: '', expectedVersion: 1, categoryId: null, tagIds: [] })).status === 409, 'PUT com versão obsoleta -> 409');

await call(t, 'PUT', `/notes/${b.id}`, { title: b.title, content: b.content, expectedVersion: b.version, categoryId: cat.json.id, tagIds: null });
ok((await call(t, 'DELETE', `/notes/categories/${cat.json.id}`)).status === 204, 'excluir categoria');
ok((await call(t, 'GET', `/notes/${b.id}`)).json.categoryId === null, 'nota fica sem categoria, não é apagada');
ok((await call(t, 'DELETE', `/notes/tags/${tag.id}`)).status === 204, 'excluir tag');

// Ciclo de vida
const arch = await call(t, 'POST', `/notes/${a.id}/archive`, {});
ok(arch.status === 200 && arch.json.status === 'Archived', 'arquivar');
ok(!(await call(t, 'GET', '/notes')).json.some(n => n.id === a.id), 'arquivada sai da lista padrão');
ok((await call(t, 'GET', '/notes?status=archived')).json.some(n => n.id === a.id), 'aparece no filtro de arquivadas');
ok((await call(t, 'POST', `/notes/${a.id}/archive`, {})).status === 400, 'arquivar de novo -> 400');
ok((await call(t, 'POST', `/notes/${a.id}/trash`, { expectedVersion: 1 })).status === 409, 'lixeira com versão obsoleta -> 409');
const trashed = await call(t, 'POST', `/notes/${a.id}/trash`, {});
ok(trashed.json.status === 'Trashed' && trashed.json.trashedAt, 'mover para a lixeira');
ok((await call(t, 'PUT', `/notes/${a.id}`, { title: 'x', content: '', expectedVersion: trashed.json.version, categoryId: null, tagIds: null })).status === 400,
  'editar nota na lixeira -> 400');
ok((await call(t, 'GET', '/notes?status=trashed')).json.length === 1, 'filtro da lixeira');
ok((await call(t, 'POST', `/notes/${a.id}/restore`, {})).json.status === 'Active', 'restaurar da lixeira');
ok((await call(t, 'DELETE', `/notes/${a.id}`)).status === 409, 'excluir fora da lixeira -> 409');
await call(t, 'POST', `/notes/${a.id}/trash`, {});
ok((await call(other, 'DELETE', `/notes/${a.id}`)).status === 404, 'outra conta não exclui -> 404');
ok((await call(t, 'DELETE', `/notes/${a.id}`)).status === 204, 'excluir definitivamente da lixeira');
ok((await call(t, 'GET', `/notes/${a.id}`)).status === 404, 'nota excluída não existe mais');

process.exit(summary('notes') ? 1 : 0);
