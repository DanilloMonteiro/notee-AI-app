import { fakeSourceProviders } from './source-test-setup.mjs';
import { call, newUser, ok, summary } from './lib.mjs';

const owner = await newUser('source-files');
const other = await newUser('source-files-other');
const sourceResult = await call(owner, 'POST', '/sources', { kind: 'document', title: `Arquivo ${Date.now()}` });
ok(sourceResult.status === 201, 'cria fonte sem identificador externo');
const source = sourceResult.json;

async function upload(token, sourceId, name, content, type = 'text/plain') {
  const form = new FormData();
  form.append('file', new Blob([content], { type }), name);
  const response = await fetch(`${process.env.NOTEE_API || 'http://localhost:5099'}/sources/${sourceId}/files`, {
    method: 'POST', headers: { Authorization: `Bearer ${token}` }, body: form,
  });
  const text = await response.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* resposta sem JSON */ }
  return { status: response.status, json };
}

const contents = 'Arquivo privado para teste.\n';
const uploaded = await upload(owner, source.id, 'nota.txt', contents);
ok(uploaded.status === 201 && uploaded.json.basis === 'user-upload', 'upload de texto registra a base user-upload');
const files = await call(owner, 'GET', `/sources/${source.id}/files`);
ok(files.status === 200 && files.json.length === 1, 'dono lista o arquivo da fonte');
ok((await call(other, 'GET', `/sources/${source.id}/files`)).status === 404, 'outra conta não acessa os arquivos');

const download = await fetch(files.json[0].downloadUrl);
ok(download.ok && await download.text() === contents, 'URL pré-assinada curta baixa o conteúdo privado');
const badPdf = await upload(owner, source.id, 'corrompido.pdf', '%PDF-not-a-real-pdf', 'application/pdf');
ok(badPdf.status === 400, 'PDF com magic bytes, mas corrompido, é recusado');
const oversized = await upload(owner, source.id, 'grande.txt', 'a'.repeat(10 * 1024 * 1024 + 1));
ok(oversized.status === 413, 'arquivo acima de 10 MB é recusado');

ok((await call(owner, 'DELETE', `/sources/${source.id}/files/${uploaded.json.id}`)).status === 204, 'dono remove o arquivo e o objeto privado');
ok((await call(owner, 'GET', `/sources/${source.id}/files`)).json.length === 0, 'arquivo removido deixa de aparecer');
ok((await call(owner, 'DELETE', `/sources/${source.id}`)).status === 204, 'remove fonte depois do arquivo');

const failed = summary('source-files');
await fakeSourceProviders.close();
process.exit(failed ? 1 : 0);
