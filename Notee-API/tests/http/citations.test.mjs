import { fakeSourceProviders } from './source-test-setup.mjs';
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const owner = await newUser('source-owner');
const other = await newUser('source-other');
const stamp = Date.now().toString(36);
const sourceInput = {
  kind: 'book', title: `Livro citável ${stamp}`, authors: ['Autora de Teste'], year: 2020,
  venue: 'Editora Teste', language: 'pt-BR', isbn: '0-306-40615-2',
  license: 'unknown', notes: 'Nota privada de teste',
};

let response = await call(owner, 'POST', '/sources', sourceInput);
ok(response.status === 201 && response.json.isbn === '9780306406157', 'fonte valida e normaliza o ISBN-10');
const source = response.json;
ok((await call(owner, 'POST', '/sources', { ...sourceInput, isbn: '9780306406157' })).status === 409, 'ISBN duplicado -> 409');
ok((await call(owner, 'POST', '/sources', { ...sourceInput, title: 'ISBN incorreto', isbn: '9780306406158' })).status === 400, 'dígito verificador incorreto -> 400');
ok((await call(other, 'GET', `/sources/${source.id}`)).status === 404, 'outra conta não lê a fonte');
ok((await call(other, 'GET', '/sources')).json.length === 0, 'biblioteca isolada por dono');
ok((await call(owner, 'GET', `/sources?query=${encodeURIComponent('citável')}&kind=book`)).json.some(s => s.id === source.id), 'busca e filtro por tipo');

const note = (await call(owner, 'POST', '/notes', {
  title: 'Nota com citação', content: blocks('Trecho citado na nota'),
})).json;
const citationInput = {
  sourceId: source.id, blockId: 'b1', exactQuote: 'Trecho citado', startIndex: 0, endIndex: 13,
  locatorKind: 'pages', pageStart: 45, pageEnd: 47, sourceQuote: 'Excerto fornecido pela pessoa',
};
response = await call(owner, 'POST', `/notes/${note.id}/citations`, citationInput);
ok(response.status === 201 && response.json.pageStart === 45 && response.json.pageEnd === 47, 'cria citação ancorada com páginas');
const citation = response.json;
ok((await call(owner, 'DELETE', `/sources/${source.id}`)).status === 409, 'fonte citada não pode ser apagada');
ok((await call(owner, 'GET', `/notes/${note.id}/citations`)).json.some(c => c.id === citation.id), 'nota lista sua citação');
response = await call(owner, 'PATCH', `/citations/${citation.id}`, { ...citationInput, pageStart: 46 });
ok(response.status === 200 && response.json.pageStart === 46, 'edita a localização da citação');
ok((await call(owner, 'POST', `/notes/${note.id}/citations`, { ...citationInput, exactQuote: 'trecho inexistente' })).status === 400, 'âncora fora do texto salvo -> 400');
ok((await call(other, 'PATCH', `/citations/${citation.id}`, { ...citationInput, sourceId: source.id })).status === 404, 'outra conta não altera a citação');

// O adapter mantém o contrato antigo usado pelo inspetor dos mapas.
response = await call(owner, 'POST', `/notes/${note.id}/references`, { kind: 'book', title: 'Livro legado', author: 'Pessoa', year: '2019' });
ok(response.status === 201 && response.json.kind === 'book', 'rota antiga cria uma referência compatível');
ok((await call(owner, 'GET', `/notes/${note.id}/references`)).json.some(r => r.id === response.json.id), 'rota antiga lista a referência');
ok((await call(owner, 'DELETE', `/notes/${note.id}/references/${response.json.id}`)).status === 204, 'rota antiga remove somente a citação');
ok((await call(owner, 'GET', '/sources')).json.some(s => s.title === 'Livro legado' && s.citationCount === 0), 'fonte legada fica na biblioteca após remover o vínculo');

// A publicação recebe um snapshot bibliográfico e não expõe o trecho privado da fonte.
const publicNote = (await call(owner, 'POST', '/notes', {
  title: `Nota publicada ${stamp}`, content: blocks('Afirmação publicada'),
})).json;
const publicSource = (await call(owner, 'POST', '/sources', {
  kind: 'article', title: 'Artigo público de teste', authors: ['Autora'], year: 2024,
  venue: 'Revista', url: 'http://127.0.0.1:58081/page', license: 'unknown',
})).json;
const publicCitationResponse = await call(owner, 'POST', `/notes/${publicNote.id}/citations`, {
  sourceId: publicSource.id, blockId: 'b1', exactQuote: 'Afirmação publicada', startIndex: 0,
  endIndex: 'Afirmação publicada'.length, locatorKind: 'section', locatorText: 'Resultados', sourceQuote: 'excerto privado',
});
ok(publicCitationResponse.status === 201, 'cria citação para a publicação');
const publicCitation = publicCitationResponse.json;
const handle = `fontes${stamp}`.slice(0, 30);
ok((await call(owner, 'PUT', '/me/profile', { handle, displayName: 'Autora Teste' })).status === 200, 'define perfil para publicação');
const publication = await call(owner, 'POST', `/notes/${publicNote.id}/publications`, { visibility: 'public' });
ok(publication.status === 201, 'publica nota com citações');
let published = await call(null, 'GET', `/public/notes/${handle}/${publication.json.slug}`);
ok(published.status === 200 && published.json.citations?.[0]?.title === publicSource.title, 'bibliografia aparece no snapshot público');
ok(!JSON.stringify(published.json).includes('excerto privado'), 'trecho da fonte não é publicado');
await call(owner, 'DELETE', `/citations/${publicCitation.id}`);
published = await call(null, 'GET', `/public/notes/${handle}/${publication.json.slug}`);
ok(published.json.citations?.length === 1, 'snapshot publicado não muda após editar as citações privadas');

ok((await call(owner, 'DELETE', `/citations/${citation.id}`)).status === 204, 'remove a citação da nota');
ok((await call(owner, 'DELETE', `/sources/${source.id}`)).status === 204, 'apaga fonte sem vínculos');

const failed = summary('citations');
await fakeSourceProviders.close();
process.exit(failed ? 1 : 0);
