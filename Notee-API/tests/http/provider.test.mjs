// Provider externo com fontes (M13) de ponta a ponta, SEM rede externa: esta suíte sobe um servidor
// falso que imita o generateContent do Gemini. A API precisa rodar com:
//   Gemini__ApiKey=fake-key  Gemini__BaseUrl=http://localhost:5199
// Se a API não estiver configurada assim, a suíte é pulada.
import http from 'node:http';
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const requests = [];
const server = http.createServer((req, res) => {
  let body = '';
  req.on('data', c => (body += c));
  req.on('end', () => {
    requests.push({ url: req.url, key: req.headers['x-goog-api-key'], body: JSON.parse(body || '{}') });
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(
      JSON.stringify({
        candidates: [
          {
            content: { parts: [{ text: 'Resposta do provider falso.' }] },
            groundingMetadata: { groundingChunks: [{ web: { uri: 'https://example.org/fonte', title: 'Fonte de teste' } }] },
          },
        ],
        usageMetadata: { promptTokenCount: 10, candidatesTokenCount: 5 },
      }),
    );
  });
});
await new Promise(r => server.listen(5199, r));

const sleep = ms => new Promise(r => setTimeout(r, ms));
const t = await newUser('provider');
let prefs = (await call(t, 'GET', '/me/preferences')).json;

if (!prefs.externalProviderConfigured) {
  console.log('  (pulada: API sem Gemini__ApiKey/Gemini__BaseUrl apontando para o servidor falso)');
  server.close();
  process.exit(summary('provider') ? 1 : 0);
}

const note = (await call(t, 'POST', '/notes', { title: 'Nota privada', content: blocks('conteúdo privado que não deve ir para fora') })).json;

// Sem consentimento: nada chega ao provider
const d1 = (await call(t, 'POST', `/notes/${note.id}/doubts`, { question: 'Sem consentimento?' })).json;
for (let i = 0; i < 20 && (await call(t, 'GET', `/doubts/${d1.id}`)).json.status !== 'Completed'; i++) await sleep(300);
ok(requests.length === 0, 'sem consentimento, o provider externo não recebe nada');

prefs = (await call(t, 'PATCH', '/me/preferences', { externalAiConsent: true, consentVersion: prefs.currentConsentVersion, expectedVersion: prefs.version })).json;
ok(prefs.effectiveProvider === 'gemini', 'com consentimento, provider efetivo é o externo');

const d2 = (await call(t, 'POST', `/notes/${note.id}/doubts`, { question: 'Com consentimento?' })).json;
let doubt;
for (let i = 0; i < 40; i++) {
  doubt = (await call(t, 'GET', `/doubts/${d2.id}`)).json;
  if (doubt.status === 'Completed' || doubt.status === 'Failed') break;
  await sleep(300);
}
ok(doubt.status === 'Completed' && doubt.answerProvider === 'gemini', 'resposta do provider externo');
ok(doubt.evidence === 'Sourced' && doubt.sources.length === 1 && doubt.sources[0].url === 'https://example.org/fonte', 'fontes devolvidas são registradas');
// ADR-047: com consentimento há antes a consulta curta de palavras-chave (pt/en) para a busca de artigos;
// sem OpenAlex alcançável aqui, a resposta segue pelo Gemini com a busca dele, e os tokens das duas chamadas entram no job.
ok(doubt.latestJob.inputTokens === 20 && doubt.latestJob.outputTokens === 10, 'uso de tokens registrado no job (palavras-chave + resposta)');
ok(requests.length === 2 && requests.every(r => r.key === 'fake-key' && !r.url.includes('key=')), 'chave enviada no cabeçalho, não na URL');
const sent = JSON.stringify(requests[0].body);
ok(sent.includes('Com consentimento?') && !sent.includes('conteúdo privado'), 'só a pergunta/trecho são enviados, não a nota inteira');

server.close();
process.exit(summary('provider') ? 1 : 0);
