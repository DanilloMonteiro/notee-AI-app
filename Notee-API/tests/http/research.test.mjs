// Dúvidas e worker: sem consentimento, a resposta vem do provider Fake (nada é enviado para fora).
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const sleep = ms => new Promise(r => setTimeout(r, ms));
async function waitDoubt(token, id, done, timeoutMs = 30000) {
  const end = Date.now() + timeoutMs;
  let d;
  while (Date.now() < end) {
    d = (await call(token, 'GET', `/doubts/${id}`)).json;
    if (done(d)) return d;
    await sleep(1000);
  }
  return d;
}

const t = await newUser('research');
const note = (await call(t, 'POST', '/notes', { title: 'Nota', content: blocks('O padrão Outbox evita perda') })).json;
const created = await call(t, 'POST', '/doubts', { noteId: note.id, question: 'O que é Outbox?', blockId: 'b1', exactQuote: 'Outbox', startIndex: 10, endIndex: 16 });
ok(created.status === 200, 'dúvida criada');
const doubt = await waitDoubt(t, created.json.doubtId, d => d?.status === 'Completed' || d?.status === 'Failed');
ok(doubt?.status === 'Completed' && doubt.answer.startsWith('[Resposta simulada]'), 'sem consentimento: resposta do provider Fake');
ok(doubt?.answer.includes('Sem fonte'), 'Fake declara "Sem fonte"');

// Pesquisa desligada: a dúvida fica pendente
const t2 = await newUser('research-off');
const p = (await call(t2, 'GET', '/me/preferences')).json;
await call(t2, 'PATCH', '/me/preferences', { researchEnabled: false, expectedVersion: p.version });
const n2 = (await call(t2, 'POST', '/notes', { title: 'Nota', content: blocks('texto') })).json;
const d2 = await call(t2, 'POST', '/doubts', { noteId: n2.id, question: 'Pergunta?', blockId: 'b1', exactQuote: 'texto', startIndex: 0, endIndex: 5 });
const pending = await waitDoubt(t2, d2.json.doubtId, () => false, 12000);
ok(pending?.status === 'Pending', 'pesquisa desligada: dúvida permanece pendente');

process.exit(summary('research') ? 1 : 0);
