import { fakeSourceProviders } from './source-test-setup.mjs';
import { call, newUser, ok, summary } from './lib.mjs';

const owner = await newUser('source-library');
const other = await newUser('source-library-other');
const stamp = Date.now().toString(36);
const input = {
  kind: 'article', title: `Artigo ${stamp}`, authors: ['Ada Lovelace', 'Grace Hopper'], year: 2023,
  venue: 'Revista de Teste', doi: 'https://doi.org/10.1234/ABC', url: 'http://127.0.0.1:58081/page',
  license: 'cc-by', notes: 'observação privada',
};

let response = await call(owner, 'POST', '/sources', input);
ok(response.status === 201 && response.json.doi === '10.1234/abc', 'cria fonte e normaliza DOI');
const source = response.json;
ok((await call(owner, 'POST', '/sources', { ...input, title: 'Duplicada', doi: '10.1234/abc' })).status === 409, 'DOI duplicado na mesma biblioteca -> 409');
ok((await call(other, 'POST', '/sources', input)).status === 201, 'DOI igual pode existir em outra biblioteca');
ok((await call(owner, 'GET', `/sources/${source.id}`)).json.title === input.title, 'dono consulta fonte');
ok((await call(other, 'GET', `/sources/${source.id}`)).status === 404, 'outra conta recebe 404');
ok((await call(owner, 'PATCH', `/sources/${source.id}`, { ...input, title: `Artigo atualizado ${stamp}` })).json.title === `Artigo atualizado ${stamp}`, 'dono atualiza metadados');
ok((await call(owner, 'PATCH', `/sources/${source.id}`, { ...input, url: 'https://user:secret@example.org/' })).status === 400, 'URL com credenciais é rejeitada');
ok((await call(owner, 'GET', `/sources?kind=invalid`)).status === 400, 'tipo de fonte inválido -> 400');
ok((await call(owner, 'DELETE', `/sources/${source.id}`)).status === 204, 'dono exclui fonte sem citações');
ok((await call(owner, 'GET', `/sources/${source.id}`)).status === 404, 'fonte excluída não aparece');

const failed = summary('sources');
await fakeSourceProviders.close();
process.exit(failed ? 1 : 0);
