// Mapas de ideias: critérios MAP-AC01..AC13 (MAPAS_DE_IDEIAS.md §7).
import { blocks as blk, call, newUser, ok, summary } from "./lib.mjs";

const alice = await newUser("alice");
const bob = await newUser("bob");
ok(alice && bob, 'login de duas contas');

ok((await call(null, 'GET', '/boards')).status === 401, 'sem token -> 401');

const blocks = blk("Texto da nota A");
const nA = (await call(alice, 'POST', '/notes', { title: 'Nota A', content: blocks })).json;
const nB = (await call(alice, 'POST', '/notes', { title: 'Nota B', content: 'texto puro B' })).json;
const nC = (await call(alice, 'POST', '/notes', { title: 'Nota C', content: '' })).json;
const bobNote = (await call(bob, 'POST', '/notes', { title: 'Nota do Bob', content: 'privada' })).json;

// MAP-AC01
const b1r = await call(alice, 'POST', '/boards', { title: 'Política', description: 'Primeiro' });
const b2r = await call(alice, 'POST', '/boards', { title: 'Estudos', description: null });
ok(b1r.status === 201 && b2r.status === 201 && b1r.json.id !== b2r.json.id, 'AC01: dois mapas criados com IDs diferentes');
const b1 = b1r.json, b2 = b2r.json;
const list = await call(alice, 'GET', '/boards');
ok(list.json.length === 2 && list.json.every(b => b.noteCount === 3), 'AC01: catálogo lista 2 mapas com 3 notas cada');
ok((await call(bob, 'GET', '/boards')).json.length === 0, 'AC11: Bob não vê mapas da Alice');
ok((await call(alice, 'POST', '/boards', { title: '' })).status === 400, 'validação: título vazio -> 400');

// GET sem efeito colateral
let d1 = (await call(alice, 'GET', `/boards/${b1.id}`)).json;
ok(d1.nodes.length === 3 && d1.nodes.every(n => n.positionVersion === null), 'GET usa posições padrão sem gravar');
ok(d1.nodes.find(n => n.noteId === nA.id).excerpt === 'Texto da nota A', 'resumo extraído dos blocos JSON');

// MAP-AC02 / AC06
const p1 = await call(alice, 'PUT', `/boards/${b1.id}/placements/${nA.id}`, { x: 500, y: 600, expectedVersion: null });
ok(p1.status === 200 && p1.json.positionVersion === 1, 'AC06: mover cria posição v1');
const d2 = (await call(alice, 'GET', `/boards/${b2.id}`)).json;
const a1 = (await call(alice, 'GET', `/boards/${b1.id}`)).json.nodes.find(n => n.noteId === nA.id);
const a2 = d2.nodes.find(n => n.noteId === nA.id);
ok(a1.x === 500 && a1.y === 600 && a2.positionVersion === null && a2.title === a1.title, 'AC02: mesma nota, posições independentes');

// MAP-AC13
const stale = await call(alice, 'PUT', `/boards/${b1.id}/placements/${nA.id}`, { x: 1, y: 1, expectedVersion: null });
ok(stale.status === 409, 'AC13: posição com versão obsoleta -> 409');
const p2 = await call(alice, 'PUT', `/boards/${b1.id}/placements/${nA.id}`, { x: 510, y: 610, expectedVersion: 1 });
ok(p2.status === 200 && p2.json.positionVersion === 2, 'AC13: versão correta -> 200 v2');
ok((await call(alice, 'PUT', `/boards/${b1.id}/placements/${nA.id}`, { x: 'NaN', y: 0, expectedVersion: 2 })).status === 400, 'coordenada inválida -> 400');
ok((await call(alice, 'PUT', `/boards/${b1.id}/placements/${nA.id}`, { x: 99999, y: 0, expectedVersion: 2 })).status === 400, 'coordenada fora do limite -> 400');

// batch
const batch = await call(alice, 'PUT', `/boards/${b1.id}/placements`, { placements: [
  { noteId: nA.id, x: 100, y: 100, expectedVersion: 2 },
  { noteId: nB.id, x: 400, y: 100, expectedVersion: null },
] });
ok(batch.status === 200 && batch.json.length === 2, 'batch de posições atômico');
const badBatch = await call(alice, 'PUT', `/boards/${b1.id}/placements`, { placements: [
  { noteId: nC.id, x: 700, y: 100, expectedVersion: null },
  { noteId: nA.id, x: 0, y: 0, expectedVersion: 1 },
] });
const cAfter = (await call(alice, 'GET', `/boards/${b1.id}`)).json.nodes.find(n => n.noteId === nC.id);
ok(badBatch.status === 409 && cAfter.positionVersion === null, 'batch com conflito não grava nada');

// MAP-AC04 / AC05
const e1 = await call(alice, 'POST', `/boards/${b1.id}/edges`, { fromNoteId: nA.id, toNoteId: nB.id, type: 'none', label: '' });
ok(e1.status === 201 && e1.json.type === 'none' && e1.json.label === null && e1.json.fromNoteId === nA.id, 'AC05: aresta none sem rótulo preserva direção');
ok((await call(alice, 'GET', `/boards/${b2.id}`)).json.edges.length === 0, 'AC04: Board 2 não recebe a aresta');
ok((await call(alice, 'POST', `/boards/${b1.id}/edges`, { fromNoteId: nA.id, toNoteId: nB.id, type: 'none', label: null })).status === 409, 'aresta duplicada -> 409');
ok((await call(alice, 'POST', `/boards/${b1.id}/edges`, { fromNoteId: nB.id, toNoteId: nA.id, type: 'none' })).status === 201, 'direção inversa é permitida');
ok((await call(alice, 'POST', `/boards/${b1.id}/edges`, { fromNoteId: nA.id, toNoteId: nA.id, type: 'related' })).status === 400, 'auto-conexão -> 400');
ok((await call(alice, 'POST', `/boards/${b1.id}/edges`, { fromNoteId: nA.id, toNoteId: nC.id, type: 'friendship' })).status === 400, 'tipo inválido -> 400');
ok((await call(alice, 'POST', `/boards/${b1.id}/edges`, { fromNoteId: nA.id, toNoteId: bobNote.id, type: 'related' })).status === 404, 'AC11: conectar nota de outra conta -> 404');

const up = await call(alice, 'PATCH', `/boards/${b1.id}/edges/${e1.json.id}`, { fromNoteId: nA.id, toNoteId: nC.id, type: 'causes', label: 'gera', expectedVersion: 1 });
ok(up.status === 200 && up.json.version === 2 && up.json.label === 'gera', 'editar aresta -> v2');
ok((await call(alice, 'PATCH', `/boards/${b1.id}/edges/${e1.json.id}`, { fromNoteId: nA.id, toNoteId: nC.id, type: 'causes', label: 'x', expectedVersion: 1 })).status === 409, 'AC13: editar aresta obsoleta -> 409');

// MAP-AC11 board de outra conta
ok((await call(bob, 'GET', `/boards/${b1.id}`)).status === 404, 'AC11: Bob GET mapa da Alice -> 404');
ok((await call(bob, 'PATCH', `/boards/${b1.id}`, { title: 'hack', expectedVersion: 1 })).status === 404, 'AC11: Bob PATCH -> 404');
ok((await call(bob, 'DELETE', `/boards/${b1.id}`)).status === 404, 'AC11: Bob DELETE -> 404');
ok((await call(bob, 'PUT', `/boards/${b1.id}/placements/${nA.id}`, { x: 0, y: 0, expectedVersion: null })).status === 404, 'AC11: Bob mover -> 404');
ok((await call(alice, 'PUT', `/boards/${b1.id}/placements/${bobNote.id}`, { x: 0, y: 0, expectedVersion: null })).status === 404, 'AC11: posicionar nota de outra conta -> 404');

// Metadados
ok((await call(alice, 'PATCH', `/boards/${b1.id}`, { title: 'Política e sociedade', description: 'x', expectedVersion: 1 })).json.version === 2, 'editar mapa -> v2');
ok((await call(alice, 'PATCH', `/boards/${b1.id}`, { title: 'Outro', description: 'x', expectedVersion: 1 })).status === 409, 'AC13: editar mapa obsoleto -> 409');

// MAP-AC08 referências
const ref = await call(alice, 'POST', `/notes/${nA.id}/references`, { kind: 'book', title: 'O Príncipe', author: 'Maquiavel', year: '1532', url: 'https://example.org/livro' });
ok(ref.status === 201 && ref.json.kind === 'book', 'AC08: livro adicionado à nota');
ok((await call(alice, 'POST', `/notes/${nA.id}/references`, { kind: 'source', title: 'X', url: 'javascript:alert(1)' })).status === 400, 'URL javascript: rejeitada');
ok((await call(bob, 'GET', `/notes/${nA.id}/references`)).status === 404, 'AC11: Bob não lê referências da Alice');
const r1 = (await call(alice, 'GET', `/boards/${b1.id}`)).json.nodes.find(n => n.noteId === nA.id).referenceCount;
const r2 = (await call(alice, 'GET', `/boards/${b2.id}`)).json.nodes.find(n => n.noteId === nA.id).referenceCount;
ok(r1 === 1 && r2 === 1, 'AC08: referência aparece em todos os mapas');

// MAP-AC03 editar nota reflete em todos os mapas
await call(alice, 'PUT', `/notes/${nB.id}`, { title: 'Nota B editada', content: 'novo', expectedVersion: 1, categoryId: null, tagIds: [] });
const t1 = (await call(alice, 'GET', `/boards/${b1.id}`)).json.nodes.find(n => n.noteId === nB.id).title;
const t2 = (await call(alice, 'GET', `/boards/${b2.id}`)).json.nodes.find(n => n.noteId === nB.id).title;
ok(t1 === 'Nota B editada' && t2 === 'Nota B editada', 'AC03: edição da nota refletida nos dois mapas');

// MAP-AC10 excluir board não apaga notas nem afeta outro board
await call(alice, 'POST', `/boards/${b2.id}/edges`, { fromNoteId: nB.id, toNoteId: nC.id, type: 'related' });
ok((await call(alice, 'DELETE', `/boards/${b1.id}`)).status === 204, 'AC10: excluir mapa -> 204');
const notesAfter = (await call(alice, 'GET', '/notes?page=1&pageSize=50')).json;
const b2After = (await call(alice, 'GET', `/boards/${b2.id}`)).json;
ok(notesAfter.length === 3 && b2After.edges.length === 1, 'AC10: notas e conexões do outro mapa preservadas');

// MAP-AC12: arquivar tira a nota do mapa (conexões preservadas); restaurar devolve
await call(alice, 'POST', `/notes/${nB.id}/archive`, {});
const archived = (await call(alice, 'GET', `/boards/${b2.id}`)).json;
ok(!archived.nodes.some(n => n.noteId === nB.id) && archived.edges.length === 0, 'AC12: nota arquivada some do mapa');
await call(alice, 'POST', `/notes/${nB.id}/restore`, {});
const restored = (await call(alice, 'GET', `/boards/${b2.id}`)).json;
ok(restored.nodes.some(n => n.noteId === nB.id) && restored.edges.length === 1, 'AC12: restaurar devolve nó e conexão');

// Exclusão definitiva (só da lixeira) limpa posições/arestas (cascade)
ok((await call(alice, 'DELETE', `/notes/${nC.id}`)).status === 409, 'excluir nota fora da lixeira -> 409');
await call(alice, 'POST', `/notes/${nC.id}/trash`, {});
ok((await call(alice, 'GET', `/boards/${b2.id}`)).json.nodes.length === 2, 'nota na lixeira some do mapa');
ok((await call(alice, 'DELETE', `/notes/${nC.id}`)).status === 204, 'excluir nota da lixeira -> 204');
const b2Final = (await call(alice, 'GET', `/boards/${b2.id}`)).json;
ok(b2Final.nodes.length === 2 && b2Final.edges.length === 0, 'excluir nota remove nó e conexões no mapa');

process.exit(summary("maps") ? 1 : 0);
