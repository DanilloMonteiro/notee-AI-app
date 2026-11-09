// Publicação revisada (M08): snapshot, visibilidades, revogação, perfil e biblioteca.
import { BASE, blocks, call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('pub');
const other = await newUser('pub-other');
const handle = `autor${Date.now().toString().slice(-8)}`;

const note = (await call(t, 'POST', '/notes', { title: 'Democracia Direta', content: blocks('versão publicada') })).json;

ok((await call(t, 'POST', `/notes/${note.id}/publications`, { visibility: 'public' })).status === 400, 'pública exige perfil');
ok((await call(t, 'PUT', '/me/profile', { handle: 'X Y', displayName: 'Autor' })).status === 400, 'handle inválido -> 400');
ok((await call(t, 'PUT', '/me/profile', { handle, displayName: 'Autora Teste' })).status === 200, 'define perfil público');
ok((await call(other, 'PUT', '/me/profile', { handle, displayName: 'Outra' })).status === 409, 'handle em uso -> 409');

const created = await call(t, 'POST', `/notes/${note.id}/publications`, { visibility: 'public' });
ok(
  created.status === 201 && created.json.slug === 'democracia-direta' && created.json.publicPath === `/p/${handle}/democracia-direta`,
  'publica com slug normalizado',
);
const pub = created.json;
ok((await call(t, 'POST', `/notes/${note.id}/publications`, { visibility: 'unlisted' })).status === 409, 'mesmo endereço ativo -> 409');
ok((await call(other, 'POST', `/notes/${note.id}/publications`, { visibility: 'unlisted' })).status === 404, 'publicar nota alheia -> 404');

// Leitura anônima
let r = await call(null, 'GET', `/public/notes/${handle}/democracia-direta`);
ok(r.status === 200 && r.json.content.includes('versão publicada') && r.json.author.handle === handle, 'leitura pública anônima');
ok(!JSON.stringify(r.json).includes(note.id), 'resposta pública não expõe IDs internos');
ok((await call(null, 'GET', `/shared/${pub.shareToken}`)).status === 200, 'link secreto também funciona');
ok((await call(null, 'GET', `/public/profiles/${handle}`)).json.notes.length === 1, 'perfil lista a publicação');
ok((await call(null, 'GET', '/public/notes')).json.some(n => n.slug === 'democracia-direta'), 'biblioteca lista a publicação');

// Snapshot: editar a nota não muda o publicado até nova revisão
await call(t, 'PUT', `/notes/${note.id}`, {
  title: 'Democracia Direta',
  content: blocks('rascunho novo'),
  expectedVersion: note.version,
  categoryId: null,
  tagIds: [],
});
r = await call(null, 'GET', `/public/notes/${handle}/democracia-direta`);
ok(r.json.content.includes('versão publicada') && !r.json.content.includes('rascunho novo'), 'editar nota não altera o publicado');
const rev = await call(t, 'POST', `/publications/${pub.id}/revisions`);
ok(rev.status === 200 && rev.json.publishedNoteVersion === rev.json.currentNoteVersion, 'nova revisão publica o estado atual');
ok((await call(null, 'GET', `/public/notes/${handle}/democracia-direta`)).json.content.includes('rascunho novo'), 'leitores veem a nova revisão');

// Visibilidade
let v = (await call(t, 'PATCH', `/publications/${pub.id}`, { visibility: 'unlisted', expectedVersion: rev.json.version })).json;
ok(v.visibility === 'unlisted' && v.publicPath === null, 'não listada sai do caminho público');
ok((await call(null, 'GET', `/public/notes/${handle}/democracia-direta`)).status === 404, 'não listada não abre pelo perfil');
ok((await call(null, 'GET', `/shared/${pub.shareToken}`)).status === 200, 'não listada abre pelo link secreto');
ok((await call(null, 'GET', `/public/profiles/${handle}`)).json.notes.length === 0, 'não listada não aparece no perfil');
ok((await call(t, 'PATCH', `/publications/${pub.id}`, { visibility: 'public', expectedVersion: 1 })).status === 409, 'versão obsoleta -> 409');
const rotated = (await call(t, 'POST', `/publications/${pub.id}/rotate-token`)).json;
ok(
  (await call(null, 'GET', `/shared/${pub.shareToken}`)).status === 404 && (await call(null, 'GET', `/shared/${rotated.shareToken}`)).status === 200,
  'novo link invalida o antigo',
);
v = (await call(t, 'PATCH', `/publications/${pub.id}`, { visibility: 'private', expectedVersion: rotated.version })).json;
ok((await call(null, 'GET', `/shared/${rotated.shareToken}`)).status === 404, 'privada não abre nem com link');
ok((await call(t, 'GET', `/publications/${pub.id}/preview`)).status === 200, 'autor vê prévia da privada');
ok((await call(other, 'GET', `/publications/${pub.id}/preview`)).status === 404, 'outra conta não vê prévia');

// Revogação
await call(t, 'PATCH', `/publications/${pub.id}`, { visibility: 'public', expectedVersion: v.version });
ok((await call(other, 'DELETE', `/publications/${pub.id}`)).status === 404, 'outra conta não revoga');
ok((await call(t, 'DELETE', `/publications/${pub.id}`)).status === 204, 'revogar');
ok((await call(null, 'GET', `/public/notes/${handle}/democracia-direta`)).status === 404, 'revogada sai do ar imediatamente');
ok((await call(t, 'GET', `/notes/${note.id}/publications`)).json[0].status === 'Revoked', 'histórico mostra revogada');
ok((await call(t, 'POST', `/notes/${note.id}/publications`, { visibility: 'unlisted' })).status === 201, 'pode publicar de novo com o mesmo endereço');

const anon = await fetch(`${BASE}/publications`);
ok(anon.status === 401, 'rotas do autor exigem login');

process.exit(summary('publishing') ? 1 : 0);
