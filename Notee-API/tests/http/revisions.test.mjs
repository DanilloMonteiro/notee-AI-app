// Revisões (M06): histórico imutável, leitura, restauração como nova versão, isolamento.
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('rev');
const other = await newUser('rev-other');

const n = (await call(t, 'POST', '/notes', { title: 'Versão 1', content: blocks('primeiro texto') })).json;
const v2 = (await call(t, 'PUT', `/notes/${n.id}`, { title: 'Versão 2', content: blocks('segundo texto'), expectedVersion: 1, categoryId: null, tagIds: [] })).json;
const v3 = (await call(t, 'PUT', `/notes/${n.id}`, { title: 'Versão 3', content: blocks('terceiro texto'), expectedVersion: v2.version, categoryId: null, tagIds: [] })).json;

let revs = (await call(t, 'GET', `/notes/${n.id}/revisions`)).json;
ok(revs.length === 3 && revs[0].version === 3 && revs[2].reason === 'Created', 'três revisões, mais recente primeiro');
ok((await call(other, 'GET', `/notes/${n.id}/revisions`)).status === 404, 'outra conta não lista revisões -> 404');

const first = revs.find(r => r.version === 1);
const detail = await call(t, 'GET', `/notes/${n.id}/revisions/${first.id}`);
ok(detail.status === 200 && detail.json.title === 'Versão 1' && detail.json.content.includes('primeiro'), 'ler revisão antiga');
ok((await call(other, 'GET', `/notes/${n.id}/revisions/${first.id}`)).status === 404, 'outra conta não lê revisão -> 404');

ok((await call(t, 'POST', `/notes/${n.id}/restore-revision`, { revisionId: first.id, expectedVersion: 1 })).status === 409, 'restaurar com versão obsoleta -> 409');
const restored = await call(t, 'POST', `/notes/${n.id}/restore-revision`, { revisionId: first.id, expectedVersion: v3.version });
ok(restored.status === 200 && restored.json.title === 'Versão 1' && restored.json.version === v3.version + 1, 'restaurar cria nova versão com o conteúdo antigo');

revs = (await call(t, 'GET', `/notes/${n.id}/revisions`)).json;
ok(revs.length === 4 && revs[0].reason === 'Restored' && revs[0].restoredFromVersion === 1, 'histórico registra a restauração sem reescrever versões');

// Mudar estado não cria revisão
await call(t, 'POST', `/notes/${n.id}/archive`, {});
ok((await call(t, 'GET', `/notes/${n.id}/revisions`)).json.length === 4, 'arquivar não gera revisão');

// Na lixeira não restaura revisão
const trashed = (await call(t, 'POST', `/notes/${n.id}/trash`, {})).json;
ok((await call(t, 'POST', `/notes/${n.id}/restore-revision`, { revisionId: first.id, expectedVersion: trashed.version })).status === 400, 'restaurar revisão na lixeira -> 400');

process.exit(summary('revisions') ? 1 : 0);
