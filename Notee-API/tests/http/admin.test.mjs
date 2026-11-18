// Painel administrativo de IA (ADR-030) e adaptadores ChatGPT/Grok/DeepSeek/Claude de ponta a ponta,
// SEM rede externa: esta suíte sobe um servidor falso na porta 5198. A API precisa rodar com:
//   Admin__Email / Admin__Password (NOTEE_ADMIN_EMAIL / NOTEE_ADMIN_PASSWORD iguais aqui)
//   OpenAI__ApiKey, Grok__ApiKey, Claude__ApiKey = fake-key  (DeepSeek SEM chave: ela é cadastrada pelo painel)
//   OpenAI__BaseUrl, Grok__BaseUrl, DeepSeek__BaseUrl, Claude__BaseUrl, OpenAlex__BaseUrl = http://localhost:5198
// Sem essa configuração a suíte é pulada.
import http from 'node:http';
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const ADMIN_EMAIL = process.env.NOTEE_ADMIN_EMAIL || 'admin@notee.local';
const ADMIN_PASSWORD = process.env.NOTEE_ADMIN_PASSWORD || 'Admin!Teste123';

const requests = [];
const article = (doi, score) => ({
  id: `https://openalex.org/${doi}`, doi: `https://doi.org/10.1/${doi}`, display_name: `Artigo ${doi}`, publication_date: '2024-05-02',
  relevance_score: score, authorships: [{ author: { display_name: 'Ana Souza' } }],
  primary_location: { landing_page_url: `https://revista.org/${doi}`, source: { display_name: 'Revista Teste' } },
  open_access: { oa_url: null }, abstract_inverted_index: { Resumo: [0], do: [1], artigo: [2] },
});
// Artigos devolvidos pelo OpenAlex falso (ADR-047); cada cenário troca a lista.
let openAlexResults = [];
const server = http.createServer((req, res) => {
  let body = '';
  req.on('data', c => (body += c));
  req.on('end', () => {
    const parsed = JSON.parse(body || '{}');
    requests.push({ url: req.url, auth: req.headers.authorization, apiKey: req.headers['x-api-key'], body: parsed });
    res.writeHead(200, { 'Content-Type': 'application/json' });
    if (req.url.startsWith('/works')) {
      res.end(JSON.stringify({ meta: { count: openAlexResults.length }, results: openAlexResults }));
    } else if (req.url.endsWith('/v1/messages')) {
      res.end(
        JSON.stringify({
          id: 'msg_fake',
          type: 'message',
          role: 'assistant',
          model: parsed.model,
          content: [
            { type: 'server_tool_use', id: 'srvtoolu_1', name: 'web_search', input: { query: 'teste' } },
            {
              type: 'web_search_tool_result',
              tool_use_id: 'srvtoolu_1',
              content: [{ type: 'web_search_result', url: 'https://example.org/claude', title: 'Fonte Claude', encrypted_content: 'x', page_age: null }],
            },
            { type: 'text', text: 'Resposta do Claude falso.' },
          ],
          stop_reason: 'end_turn',
          stop_sequence: null,
          usage: { input_tokens: 20, output_tokens: 8 },
        }),
      );
    } else if (req.url.endsWith('/v1/tools/search')) {
      // Busca na web do DeepSeek (ADR-046): um resultado bom e um abaixo da nota mínima (1,5).
      res.end(
        JSON.stringify({
          results: [
            { rerank_score: 2.4, title: 'Fonte DeepSeek', url: 'https://example.org/deepseek', snippet: 's', summary: 'Resumo da fonte.',
              content: '', site_name: 'Exemplo', site_icon: null, date: '2026-06-01T00:00:00+00:00', authority: 3 },
            { rerank_score: 1.0, title: 'Fraca', url: 'https://example.org/fraca', snippet: 's', summary: 'Resumo fraco.',
              content: '', site_name: null, site_icon: null, date: null, authority: -1 },
            { rerank_score: 2.9, title: 'Wiki', url: 'https://pt.wikipedia.org/wiki/Teste', snippet: 's', summary: 'Enciclopédia.',
              content: '', site_name: 'Wikipédia', site_icon: null, date: null, authority: 2 },
            { rerank_score: 2.8, title: 'Bloqueado', url: 'https://bloqueado.org/x', snippet: 's', summary: 'Site bloqueado.',
              content: '', site_name: null, site_icon: null, date: null, authority: 1 },
          ],
        }),
      );
    } else {
      // Palavras-chave pt/en (ADR-047), resposta embasada quando o prompt traz resultados de busca; senão, texto simples.
      const system = String(parsed.messages?.[0]?.content || '');
      const content = system.includes('prepara consultas')
        ? '{"pt":"termos pt","en":"terms en"}'
        : system.includes('resultados de busca numerados')
          ? '{"answer":"Resposta embasada [1][2]. Inventado [9]."}'
          : `Resposta de ${parsed.model}.`;
      res.end(
        JSON.stringify({
          choices: [{ message: { role: 'assistant', content } }],
          usage: { prompt_tokens: 11, completion_tokens: 4 },
        }),
      );
    }
  });
});
await new Promise(r => server.listen(5198, r));
const sleep = ms => new Promise(r => setTimeout(r, ms));

const adminLogin = await call(null, 'POST', '/auth/admin/login', { email: ADMIN_EMAIL, password: ADMIN_PASSWORD });
if (adminLogin.status !== 200) {
  console.log('  (pulada: API sem conta admin configurada para esta suíte)');
  server.close();
  process.exit(summary('admin') ? 1 : 0);
}
const admin = adminLogin.json.token;

// Acesso
const user = await newUser('admin-user');
ok((await call(null, 'GET', '/admin/ai')).status === 401, 'sem token -> 401');
ok((await call(user, 'GET', '/admin/ai')).status === 403, 'usuário comum -> 403');
ok((await call(null, 'POST', '/auth/admin/login', { email: ADMIN_EMAIL, password: 'errada' })).status === 401, 'senha admin errada -> 401');
const plain = (await call(null, 'POST', '/auth/login', { email: ADMIN_EMAIL, password: ADMIN_PASSWORD })).json.token;
ok((await call(plain, 'GET', '/admin/ai')).status === 403, 'login comum da conta admin NÃO dá acesso ao painel');

let state = (await call(admin, 'GET', '/admin/ai')).json;
ok(state.providers.length === 5 && ['chatgpt', 'claude', 'grok'].every(id => state.providers.find(p => p.id === id)?.configured), 'lista 5 provedores com status de configuração');

// Chaves pelo painel: só onde não há chave na configuração; valor nunca é devolvido
const ds = () => state.providers.find(p => p.id === 'deepseek');
const gpt = state.providers.find(p => p.id === 'chatgpt');
ok(gpt.keySource === 'config' && gpt.keyEditable === false, 'chave do appsettings aparece como "servidor" e não é editável');
ok(ds().keySource === 'none' && ds().keyEditable && !ds().configured, 'provedor sem chave aparece como editável');
ok((await call(admin, 'PUT', '/admin/ai/providers/chatgpt/key', { apiKey: 'sk-outra-chave' })).status === 409, 'não sobrescreve chave do servidor -> 409');
ok((await call(admin, 'DELETE', '/admin/ai/providers/chatgpt/key')).status === 409, 'não remove chave do servidor -> 409');
ok((await call(admin, 'PUT', '/admin/ai/providers/deepseek/key', { apiKey: 'a b' })).status === 400, 'chave inválida -> 400');
ok((await call(user, 'PUT', '/admin/ai/providers/deepseek/key', { apiKey: 'fake-key' })).status === 403, 'usuário comum não cadastra chave');
const saved = await call(admin, 'PUT', '/admin/ai/providers/deepseek/key', { apiKey: 'fake-key' });
state = saved.json;
ok(saved.status === 200 && ds().keySource === 'panel' && ds().configured, 'chave cadastrada pelo painel');
ok(!JSON.stringify(state).includes('fake-key') && !JSON.stringify((await call(admin, 'GET', '/admin/ai')).json).includes('fake-key'), 'valor da chave nunca é devolvido');
const original = state.activeProvider;
ok((await call(admin, 'PUT', '/admin/ai/active', { provider: 'inexistente' })).status === 400, 'provedor desconhecido -> 400');
ok((await call(user, 'PUT', '/admin/ai/active', { provider: 'grok' })).status === 403, 'usuário comum não troca a IA');

// Conta que consentiu
let prefs = (await call(user, 'GET', '/me/preferences')).json;
await call(user, 'PATCH', '/me/preferences', { externalAiConsent: true, consentVersion: prefs.currentConsentVersion, expectedVersion: prefs.version });
const note = (await call(user, 'POST', '/notes', { title: 'Nota', content: blocks('texto da nota') })).json;

async function askWith(provider) {
  state = (await call(admin, 'PUT', '/admin/ai/active', { provider })).json;
  const d = (await call(user, 'POST', `/notes/${note.id}/doubts`, { question: `Pergunta para ${provider}?` })).json;
  let doubt;
  for (let i = 0; i < 40; i++) {
    doubt = (await call(user, 'GET', `/doubts/${d.id}`)).json;
    if (doubt.status === 'Completed' || doubt.status === 'Failed') break;
    await sleep(300);
  }
  return doubt;
}

const setSourceMode = async mode => {
  const current = (await call(user, 'GET', '/me/preferences')).json;
  return call(user, 'PATCH', '/me/preferences', { sourceMode: mode, expectedVersion: current.version });
};
prefs = (await call(user, 'GET', '/me/preferences')).json;
ok(prefs.sourceMode === 'mixed', 'origem padrão das fontes: artigos primeiro (mixed)');
ok((await setSourceMode('livros')).status === 400, 'origem das fontes desconhecida -> 400');

// ChatGPT, modo misturado (ADR-047): artigos suficientes no OpenAlex -> resposta só com artigos, sem internet.
{
  openAlexResults = [article('A1', 120), article('A2', 100), article('A3', 90)];
  const before = requests.length;
  const doubt = await askWith('chatgpt');
  const made = requests.slice(before);
  const req = made[0];
  const works = made.filter(r => r.url.startsWith('/works'));
  const params = new URL(works[0]?.url || '/', 'http://x').searchParams;
  ok(req?.url === '/chat/completions' && req.auth === 'Bearer fake-key' && req.body.model === 'gpt-4o-mini', 'chatgpt: endpoint, chave e modelo padrão corretos');
  ok(String(req?.body.messages?.[0]?.content).includes('prepara consultas'), 'chatgpt: palavras-chave pt/en geradas antes da busca');
  ok(params.get('search') === 'terms en' && !works[0]?.auth, 'chatgpt: OpenAlex consultado com os termos em inglês, sem chave (opcional)');
  ok(params.get('filter') === 'type:article|review,has_abstract:true,language:en|pt', 'chatgpt: filtro de artigos com resumo em en/pt');
  ok(!made.some(r => r.url.endsWith('/v1/tools/search')), 'chatgpt: sem busca na web (só o DeepSeek tem)');
  const [s1, s2] = doubt.sources || [];
  ok(doubt.status === 'Completed' && doubt.evidence === 'Sourced' && doubt.answer === 'Resposta embasada [1][2]. Inventado.', 'chatgpt: resposta embasada em artigos');
  ok(
    doubt.sources?.length === 2 && s1.origin === 'academic' && s1.doi === '10.1/A1' && s1.url === 'https://doi.org/10.1/A1' &&
      s1.siteName === 'Revista Teste' && s1.authors === 'Ana Souza' && s2.doi === '10.1/A2',
    'chatgpt: fontes são artigos com DOI, periódico e autores',
  );
  ok(doubt.latestJob?.academicSearches >= 1 && doubt.latestJob?.webSearches === 0, 'chatgpt: busca de artigos registrada na execução');
}

// Grok, modo "só internet": a busca web é do DeepSeek -> sem pesquisa, sem fontes inventadas.
{
  ok((await setSourceMode('web')).json?.sourceMode === 'web', 'origem das fontes trocada para só internet');
  const before = requests.length;
  const doubt = await askWith('grok');
  const made = requests.slice(before);
  ok(
    doubt.status === 'Completed' && doubt.answerProvider === 'grok' && doubt.evidence === 'NoSource',
    'grok: resposta pelo adaptador, sem fontes inventadas',
  );
  ok(made[0]?.url === '/chat/completions' && made[0].body.model === 'grok-4', 'grok: endpoint e modelo padrão corretos');
  ok(!made.some(r => r.url.endsWith('/v1/tools/search') || r.url.startsWith('/works')), 'grok: só internet sem DeepSeek não pesquisa');
  await setSourceMode('mixed');
}

// Sites bloqueados e confiáveis (ADR-047).
{
  const rules = (await call(user, 'GET', '/me/source-rules')).json;
  ok(rules.some(r => r.domain === 'wikipedia.org' && r.kind === 'blocked' && r.fixed), 'Wikipédia bloqueada fixa');
  const blocked = await call(user, 'PUT', '/me/source-rules', { domain: 'https://www.bloqueado.org/qualquer', kind: 'blocked' });
  ok(blocked.status === 200 && blocked.json.some(r => r.domain === 'bloqueado.org' && r.kind === 'blocked' && !r.fixed), 'site bloqueado pela pessoa (domínio normalizado)');
  ok((await call(user, 'PUT', '/me/source-rules', { domain: 'pt.wikipedia.org', kind: 'trusted' })).status === 400, 'Wikipédia não pode ser confiável');
  ok((await call(user, 'PUT', '/me/source-rules', { domain: 'nao e site', kind: 'blocked' })).status === 400, 'site inválido -> 400');
  await call(user, 'PUT', '/me/source-rules', { domain: 'confiavel.org', kind: 'trusted' });
  ok((await call(user, 'DELETE', '/me/source-rules/confiavel.org')).json.every(r => r.domain !== 'confiavel.org'), 'site removido da lista');
}

// DeepSeek, modo misturado: poucos artigos -> a web do DeepSeek completa, sem Wikipédia nem site bloqueado.
{
  openAlexResults = [article('B1', 50)];
  const before = requests.length;
  const doubt = await askWith('deepseek');
  const made = requests.slice(before);
  const search = made.find(r => r.url === '/v1/tools/search');
  const chat = made.filter(r => r.url === '/chat/completions').at(-1);
  ok(
    search?.auth === 'Bearer fake-key' && search.body.query === 'termos pt lang:pt,en' && search.body.ori_question === 'Pergunta para deepseek?' &&
      search.body.count === 10 && search.body.need_content === false && !('localization' in search.body),
    'deepseek: busca na web com as palavras-chave e a pergunta como intenção, sem IP do usuário',
  );
  const sent = JSON.stringify(chat?.body);
  ok(
    chat?.body.model === 'deepseek-chat' && !sent.includes('Resumo fraco') && !sent.includes('Enciclop') && !sent.includes('Site bloqueado'),
    'deepseek: resultado fraco, Wikipédia e site bloqueado não vão para a IA',
  );
  ok(doubt.status === 'Completed' && doubt.answerProvider === 'deepseek' && doubt.evidence === 'Sourced', 'deepseek: resposta embasada com fonte');
  ok(doubt.answer === 'Resposta embasada [1][2]. Inventado.', 'deepseek: [n] sem resultado correspondente é removido');
  const [art, web] = doubt.sources || [];
  ok(doubt.sources?.length === 2 && art.origin === 'academic' && art.doi === '10.1/B1', 'deepseek: artigo vem primeiro');
  ok(
    web?.origin === 'web' && web.url === 'https://example.org/deepseek' && web.verified === true && web.siteName === 'Exemplo' && web.authority === 3,
    'deepseek: internet completa com link, site e autoridade',
  );
  ok(doubt.latestJob?.webSearches === 1 && doubt.latestJob?.academicSearches >= 1, 'deepseek: buscas de artigos e web registradas');
}

// Claude no modo "só internet": usa a própria busca na web (sem OpenAlex nem DeepSeek).
await setSourceMode('web');
const beforeClaude = requests.length;
const claude = await askWith('claude');
const creq = requests[beforeClaude];
ok(claude.status === 'Completed' && claude.answerProvider === 'claude' && claude.answer === 'Resposta do Claude falso.', 'claude: resposta via SDK oficial');
ok(claude.evidence === 'Sourced' && claude.sources[0]?.url === 'https://example.org/claude', 'claude: fontes da busca na web registradas');
ok(creq?.url === '/v1/messages' && creq.auth === 'Bearer fake-key' && creq.body.model === 'claude-opus-5-5', 'claude: Messages API, chave (Authorization: Bearer, SDK atual) e modelo padrão');
ok(creq?.body.tools?.[0]?.type === 'web_search_20260209', 'claude: ferramenta de busca na web solicitada');

// Modelo configurável e teste de conexão
state = (await call(admin, 'PUT', '/admin/ai/providers/deepseek/model', { model: 'deepseek-reasoner' })).json;
ok(state.providers.find(p => p.id === 'deepseek').model === 'deepseek-reasoner', 'modelo alterado pelo painel');
ok((await call(admin, 'PUT', '/admin/ai/providers/deepseek/model', { model: 'x y;z' })).status === 400, 'modelo inválido -> 400');
const beforeTest = requests.length;
const test = (await call(admin, 'POST', '/admin/ai/providers/deepseek/test')).json;
ok(test.ok && requests[beforeTest]?.body.model === 'deepseek-reasoner', 'teste de conexão usa o modelo escolhido');
ok(!JSON.stringify(requests[beforeTest]?.body).includes('texto da nota'), 'teste de conexão não envia dados de usuários');
await call(admin, 'PUT', '/admin/ai/providers/deepseek/model', { model: 'deepseek-chat' });

// Sem consentimento continua indo para o Fake, qualquer que seja a IA ativa
const other = await newUser('admin-other');
const n2 = (await call(other, 'POST', '/notes', { title: 'x', content: blocks('y') })).json;
const before = requests.length;
const d2 = (await call(other, 'POST', `/notes/${n2.id}/doubts`, { question: 'Sem consentimento?' })).json;
let done;
for (let i = 0; i < 30; i++) {
  done = (await call(other, 'GET', `/doubts/${d2.id}`)).json;
  if (done.status === 'Completed') break;
  await sleep(300);
}
ok(done.answerProvider === 'fake' && requests.length === before, 'sem consentimento nada vai para a IA ativa');

// Remover a chave do painel deixa o provedor sem chave
state = (await call(admin, 'DELETE', '/admin/ai/providers/deepseek/key')).json;
ok(ds().keySource === 'none' && !ds().configured, 'remover chave do painel');

await call(admin, 'PUT', '/admin/ai/active', { provider: original });
server.close();
process.exit(summary('admin') ? 1 : 0);
