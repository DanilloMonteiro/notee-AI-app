import { call, newUser, blocks, ok, summary } from './lib.mjs';

const owner = await newUser('private-comment');
const other = await newUser('private-comment-other');
const note = (await call(owner, 'POST', '/notes', { title: 'Comentário privado', content: blocks('Trecho original') })).json;
let r = await call(owner, 'POST', `/notes/${note.id}/comments`, { blockId: 'b1', exactQuote: 'Trecho', startIndex: 0, endIndex: 6, text: 'Minha observação' });
ok(r.status === 201, 'cria comentário privado no trecho salvo');
const id = r.json?.id;
ok((await call(owner, 'GET', `/notes/${note.id}/comments`)).json?.length === 1, 'dono lista');
ok((await call(other, 'GET', `/notes/${note.id}/comments`)).status === 404, 'outra conta não lista');
ok((await call(other, 'PATCH', `/note-comments/${id}`, { text: 'Invadido' })).status === 404, 'outra conta não edita');
ok((await call(owner, 'POST', `/notes/${note.id}/comments`, { blockId: 'b1', exactQuote: 'Inexistente', startIndex: 0, endIndex: 11, text: 'x' })).status === 400, 'trecho inválido recusado');
ok((await call(owner, 'PATCH', `/note-comments/${id}`, { text: 'Atualizado' })).json?.text === 'Atualizado', 'dono edita');
ok((await call(owner, 'DELETE', `/note-comments/${id}`)).status === 204, 'dono exclui');
ok((await call(owner, 'GET', `/notes/${note.id}/comments`)).json?.length === 0, 'exclusão efetiva');
process.exit(summary('note-comments') ? 1 : 0);
