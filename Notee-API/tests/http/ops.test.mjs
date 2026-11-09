// Operação e segurança (M14/M15): health checks, cabeçalhos, Problem Details com traceId, cota de pesquisa.
// A suíte assume Research__MaxJobsPerHour=5 na API de teste (como no CI).
import { BASE, blocks, call, newUser, ok, summary } from './lib.mjs';

const live = await fetch(`${BASE}/health/live`);
ok(live.status === 200, '/health/live 200');
const ready = await fetch(`${BASE}/health/ready`);
ok(ready.status === 200 && (await ready.text()) === 'Healthy', '/health/ready verifica o banco');
ok(live.headers.get('x-content-type-options') === 'nosniff' && live.headers.get('x-frame-options') === 'DENY', 'cabeçalhos de segurança');
ok((live.headers.get('content-security-policy') || '').includes("default-src 'none'"), 'CSP restritiva na API');

const t = await newUser('ops');
const missing = await call(t, 'GET', '/boards/00000000-0000-0000-0000-000000000001');
ok(missing.status === 404 && typeof missing.json?.traceId === 'string' && missing.json.traceId.length > 0, 'Problem Details com traceId');
const bad = await call(t, 'PATCH', '/me/preferences', { externalAiConsent: true, expectedVersion: 1 });
ok(bad.status === 400 && !JSON.stringify(bad.json).includes('   at '), 'erro sem stack trace');

// Cota: 5 jobs/hora na API de teste
const note = (await call(t, 'POST', '/notes', { title: 'Cota', content: blocks('texto') })).json;
const created = [];
for (let i = 0; i < 6; i++) {
  created.push((await call(t, 'POST', `/notes/${note.id}/doubts`, { question: `Pergunta ${i}?` })).json);
}
ok(created.slice(0, 5).every(d => d.latestJob) && created[5].latestJob === null, 'acima da cota a dúvida fica pendente, sem job');
const over = await call(t, 'POST', `/doubts/${created[5].id}/research-jobs`);
ok(over.status === 429, 'enfileirar acima da cota -> 429');
const other = await newUser('ops-other');
const n2 = (await call(other, 'POST', '/notes', { title: 'x', content: blocks('y') })).json;
ok((await call(other, 'POST', `/notes/${n2.id}/doubts`, { question: 'z?' })).json.latestJob !== null, 'cota é por conta');

process.exit(summary('ops') ? 1 : 0);
