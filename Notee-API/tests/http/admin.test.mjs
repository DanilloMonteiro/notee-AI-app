// Painel administrativo de IA (ADR-030) e adaptadores ChatGPT/Grok/DeepSeek/Claude de ponta a ponta,
// SEM rede externa: esta suíte sobe um servidor falso na porta 5198. A API precisa rodar com:
//   Admin__Email / Admin__Password (NOTEE_ADMIN_EMAIL / NOTEE_ADMIN_PASSWORD iguais aqui)
//   OpenAI__ApiKey, Grok__ApiKey, Claude__ApiKey = fake-key  (DeepSeek SEM chave: ela é cadastrada pelo painel)
//   OpenAI__BaseUrl, Grok__BaseUrl, DeepSeek__BaseUrl, Claude__BaseUrl = http://localhost:5198
// Sem essa configuração a suíte é pulada.
import http from 'node:http';
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const ADMIN_EMAIL = process.env.NOTEE_ADMIN_EMAIL || 'admin@notee.local';
const ADMIN_PASSWORD = process.env.NOTEE_ADMIN_PASSWORD || 'Admin!Teste123';

const requests = [];
const server = http.createServer((req, res) => {
  let body = '';
  req.on('data', c => (body += c));
  req.on('end', () => {
    const parsed = JSON.parse(body || '{}');
    requests.push({ url: req.url, auth: req.headers.authorization, apiKey: req.headers['x-api-key'], body: parsed });
    res.writeHead(200, { 'Content-Type': 'application/json' });
    if (req.url.endsWith('/v1/messages')) {
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
    } else {
      res.end(
        JSON.stringify({
          choices: [{ message: { role: 'assistant', content: `Resposta de ${parsed.model}.` } }],
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

for (const [provider, model] of [['chatgpt', 'gpt-4o-mini'], ['grok', 'grok-4'], ['deepseek', 'deepseek-chat']]) {
  const before = requests.length;
  const doubt = await askWith(provider);
  const req = requests[before];
  ok(
    state.activeProvider === provider && doubt.status === 'Completed' && doubt.answerProvider === provider && doubt.evidence === 'NoSource',
    `${provider}: resposta pelo adaptador, sem fontes inventadas`,
  );
  ok(req?.url === '/chat/completions' && req.auth === 'Bearer fake-key' && req.body.model === model, `${provider}: endpoint, chave e modelo padrão corretos`);
}

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
