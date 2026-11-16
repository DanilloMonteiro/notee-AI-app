import { fakeSourceProviders } from './source-test-setup.mjs';
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const owner = await newUser('source-checks');
const stamp = Date.now().toString(36);

let response = await call(owner, 'POST', '/sources/resolve', { input: '10.1234/fake' });
ok(response.status === 200 && response.json.provider === 'crossref' && response.json.title === 'Fake Article', 'resolve DOI via Crossref falso');
ok(response.json.authors?.[0] === 'Alex Source' && response.json.year === 2024, 'retorna autor e ano para conferência');
response = await call(owner, 'POST', '/sources/resolve', { input: '0-306-40615-2' });
ok(response.status === 200 && response.json.kind === 'book' && response.json.isbn === '9780306406157', 'resolve ISBN via Open Library falso');
response = await call(owner, 'POST', '/sources/resolve', { input: 'https://www.youtube.com/watch?v=fake-video' });
ok(response.status === 200 && response.json.provider === 'youtube-oembed' && response.json.title === 'Fake Video', 'resolve vídeo via oEmbed falso');
response = await call(owner, 'POST', '/sources/resolve', { input: 'http://127.0.0.1:58081/page' });
ok(response.status === 200 && response.json.title === 'Fake Article' && response.json.license === 'cc-by', 'resolve metadados e licença Creative Commons da página falsa');

response = await call(owner, 'POST', '/sources', {
  kind: 'website', title: 'Fake Article', authors: ['Alex Source'], year: 2024,
  url: 'http://127.0.0.1:58081/page', license: 'unknown',
});
ok(response.status === 201, 'cria uma fonte de página com verificação automática');
const source = response.json;

async function waitForCheck(path, layer) {
  for (let attempt = 0; attempt < 40; attempt++) {
    const result = await call(owner, 'GET', path);
    const check = result.json?.find(item => item.layer === layer);
    if (check && !['pending', 'running'].includes(check.status)) return check;
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  return null;
}

const existence = await waitForCheck(`/sources/${source.id}/checks`, 'existence');
ok(existence?.status === 'confirmed', 'worker confirma resposta e metadados sem chamar URL externa');
ok(existence?.arguments?.some(argument => argument.point === 'Texto de acesso aberto arquivado'), 'arquiva somente texto de página com licença Creative Commons reconhecida');
ok(existence?.arguments?.some(argument => argument.point === 'Snapshot disponível'), 'consulta e registra snapshot do provedor falso');
const files = await call(owner, 'GET', `/sources/${source.id}/files`);
ok(files.status === 200 && files.json.length === 1 && files.json[0].basis === 'cc-by', 'arquivo privado de texto extraído fica disponível ao dono');

const noteText = 'Claim from the source.';
const note = (await call(owner, 'POST', '/notes', { title: `Source check ${stamp}`, content: blocks(noteText) })).json;
const citationResponse = await call(owner, 'POST', `/notes/${note.id}/citations`, {
  sourceId: source.id, blockId: 'b1', exactQuote: noteText, startIndex: 0, endIndex: noteText.length,
  locatorKind: 'none', sourceQuote: 'Evidence from the licensed page.',
});
ok(citationResponse.status === 201, 'cria citação ligada ao trecho da nota');
const citation = citationResponse.json;
const citationCheck = await waitForCheck(`/citations/${citation.id}/checks`, 'citation');
ok(citationCheck?.status === 'confirmed', 'worker encontra o trecho no texto extraído');

const estimate = await call(owner, 'POST', `/citations/${citation.id}/ai-check/estimate`);
ok(estimate.status === 200 && estimate.json.hasArchive && estimate.json.provider === 'fake' && estimate.json.estimatedCostUsd === 0, 'estimativa informa arquivo, provedor fake e custo zero');
const ai = await call(owner, 'POST', `/citations/${citation.id}/ai-check`, { confirm: true });
ok(ai.status === 200 && ai.json.verdict === 'insufficient' && ai.json.costUsd === 0, 'análise confirmada usa somente o Fake e registra custo zero');

const handle = `src${stamp}`.slice(0, 30);
ok((await call(owner, 'PUT', '/me/profile', { handle, displayName: 'Pessoa de Teste' })).status === 200, 'define perfil para publicar a referência');
const publication = await call(owner, 'POST', `/notes/${note.id}/publications`, { visibility: 'public' });
ok(publication.status === 201, 'publica a nota com snapshot de referências');
const publicNote = await call(null, 'GET', `/public/notes/${handle}/${publication.json.slug}`);
ok(publicNote.status === 200 && publicNote.json.citations?.[0]?.snapshotUrl?.startsWith('https://web.archive.org/web/'), 'bibliografia pública preserva link do snapshot no momento da publicação');
ok(!JSON.stringify(publicNote.json).includes('Evidence from the licensed page.'), 'excerto privado da fonte não aparece na publicação');

const failed = summary('source-checks');
await fakeSourceProviders.close();
process.exit(failed ? 1 : 0);
