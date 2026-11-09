// MAP-04 (prévia de contexto) e MAP-05 (sugestões com aceite humano individual).
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('graph');
const other = await newUser('graph-other');

const tag = (await call(t, 'POST', '/notes/tags', { name: 'democracia' })).json;
const mk = async (title, text, tagIds = []) => {
  const n = (await call(t, 'POST', '/notes', { title, content: blocks(text) })).json;
  return tagIds.length
    ? (await call(t, 'PUT', `/notes/${n.id}`, { title, content: n.content, expectedVersion: n.version, categoryId: null, tagIds })).json
    : n;
};
const a = await mk('Participação popular', 'Plebiscitos ampliam a participação cidadã.', [tag.id]);
const b = await mk('Assembleias', 'Assembleias deliberativas e participação direta.', [tag.id]);
const c = await mk('Receita de bolo', 'Farinha, ovos e açúcar.');
const archived = await mk('Arquivada', 'participação');
await call(t, 'POST', `/notes/${archived.id}/archive`, {});
const secret = (await call(other, 'POST', '/notes', { title: 'Segredo', content: blocks('participação') })).json;

const board = (await call(t, 'POST', '/boards', { title: 'Política' })).json;
const edge = (await call(t, 'POST', `/boards/${board.id}/edges`, { fromNoteId: a.id, toNoteId: c.id, type: 'none' })).json;

// MAP-04: prévia
let ctx = await call(t, 'POST', `/boards/${board.id}/context-preview`, { scope: 'all' });
ok(ctx.status === 200 && ctx.json.schema === 'notee-graph-context/1' && ctx.json.noteCount === 3, 'contexto all: só notas elegíveis do dono');
ok(!ctx.json.nodes.some(n => n.id === archived.id), 'arquivada fica fora do contexto');
ok(ctx.json.nodes[0].content && !ctx.json.nodes[0].content.includes('"type"'), 'conteúdo em texto simples (sem JSON dos blocos)');
ok(ctx.json.edgeCount === 1 && ctx.json.contextHash.length === 32, 'arestas internas e hash do contexto');

ctx = await call(t, 'POST', `/boards/${board.id}/context-preview`, { scope: 'selected', noteIds: [a.id, b.id, secret.id, archived.id] });
ok(ctx.json.noteCount === 2 && !ctx.json.nodes.some(n => n.id === secret.id), 'subgrafo ignora IDs de outra conta e arquivados');
ok(ctx.json.edgeCount === 0, 'subgrafo só inclui arestas entre as notas escolhidas');
ok((await call(t, 'POST', `/boards/${board.id}/context-preview`, { scope: 'selected', noteIds: [] })).status === 400, 'subgrafo vazio -> 400');
ok((await call(t, 'POST', `/boards/${board.id}/context-preview`, { scope: 'tudo' })).status === 400, 'escopo inválido -> 400');
ok((await call(other, 'POST', `/boards/${board.id}/context-preview`, { scope: 'all' })).status === 404, 'contexto de mapa alheio -> 404');

// MAP-05: sugestões
const gen = await call(t, 'POST', `/boards/${board.id}/suggestions`, { scope: 'all' });
ok(gen.status === 200 && gen.json.length >= 1, 'gera sugestões pendentes');
const s = gen.json.find(x => [x.fromNoteId, x.toNoteId].includes(a.id) && [x.fromNoteId, x.toNoteId].includes(b.id));
ok(s && s.status === 'Pending' && s.reason.includes('democracia'), 'sugere ligar notas com tag em comum, com justificativa');
ok(!gen.json.some(x => [x.fromNoteId, x.toNoteId].includes(c.id) && [x.fromNoteId, x.toNoteId].includes(a.id)), 'não sugere par já conectado');
ok((await call(t, 'GET', `/boards/${board.id}`)).json.edges.length === 1, 'gerar sugestões NÃO altera o grafo');
const again = await call(t, 'POST', `/boards/${board.id}/suggestions`, { scope: 'all' });
ok(again.json.filter(x => x.id !== s.id && [x.fromNoteId, x.toNoteId].includes(a.id) && [x.fromNoteId, x.toNoteId].includes(b.id)).length === 0, 'não duplica sugestão pendente');

ok((await call(other, 'POST', `/boards/${board.id}/suggestions/${s.id}/accept`)).status === 404, 'outra conta não aceita -> 404');
const acc = await call(t, 'POST', `/boards/${board.id}/suggestions/${s.id}/accept`);
ok(acc.status === 200 && acc.json.type === 'related', 'aceite cria a aresta');
ok((await call(t, 'GET', `/boards/${board.id}`)).json.edges.length === 2, 'grafo agora tem a nova aresta');
ok((await call(t, 'POST', `/boards/${board.id}/suggestions/${s.id}/accept`)).status === 409, 'aceitar de novo -> 409');

// Obsoleta: nota mudou depois da sugestão
const d = await mk('Plebiscito', 'participação cidadã em plebiscitos');
const gen2 = (await call(t, 'POST', `/boards/${board.id}/suggestions`, { scope: 'all' })).json;
const s2 = gen2.find(x => [x.fromNoteId, x.toNoteId].includes(d.id));
await call(t, 'PUT', `/notes/${d.id}`, { title: 'Plebiscito (editado)', content: d.content, expectedVersion: d.version, categoryId: null, tagIds: [] });
ok(s2 && (await call(t, 'POST', `/boards/${board.id}/suggestions/${s2.id}/accept`)).status === 409, 'sugestão obsoleta (nota mudou) -> 409');
ok((await call(t, 'GET', `/boards/${board.id}/suggestions?status=all`)).json.find(x => x.id === s2.id).status === 'Stale', 'marcada como Stale');

const pend = (await call(t, 'GET', `/boards/${board.id}/suggestions`)).json;
if (pend.length) {
  ok((await call(t, 'POST', `/boards/${board.id}/suggestions/${pend[0].id}/dismiss`)).status === 204, 'descartar sugestão');
}

process.exit(summary('graph-ai') ? 1 : 0);
