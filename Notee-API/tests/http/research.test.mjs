// Dúvidas e pesquisa durável (M09/M12, AC09): jobs 202, idempotência, cancelamento,
// provider Fake sem consentimento, pesquisa desligada e dúvidas do mapa sem âncora.
import { blocks, call, newUser, ok, summary } from './lib.mjs';

const sleep = ms => new Promise(r => setTimeout(r, ms));
async function waitFor(fn, done, timeoutMs = 30000) {
  const end = Date.now() + timeoutMs;
  let v;
  while (Date.now() < end) {
    v = await fn();
    if (done(v)) return v;
    await sleep(500);
  }
  return v;
}
const getDoubt = (t, id) => call(t, 'GET', `/doubts/${id}`).then(r => r.json);
const finished = d => ['Completed', 'Failed', 'Cancelled'].includes(d?.status);

const t = await newUser('research');
const other = await newUser('research-other');
const note = (await call(t, 'POST', '/notes', { title: 'Nota', content: blocks('O padrão Outbox evita perda') })).json;

// Criar dúvida enfileira job na mesma transação
const created = await call(t, 'POST', '/doubts', { noteId: note.id, question: 'O que é Outbox?', blockId: 'b1', exactQuote: 'Outbox', startIndex: 10, endIndex: 16 });
ok(created.status === 200 && created.json.jobId, 'dúvida criada já com job');
const done = await waitFor(() => getDoubt(t, created.json.doubtId), finished);
ok(done?.status === 'Completed' && done.answer.startsWith('[Resposta simulada]'), 'sem consentimento: resposta do provider Fake');
ok(done?.answerProvider === 'fake' && done.evidence === 'NoSource', 'resposta marcada como fake e "Sem fonte"');
ok(done?.latestJob?.status === 'Succeeded' && done.latestJob.attempt === 1, 'job concluído na primeira tentativa');

// Nova pesquisa: 202 + Location; Idempotency-Key repete o mesmo job
const key = `k-${Date.now()}`;
const res1 = await fetch(`${process.env.NOTEE_API || 'http://localhost:5099'}/doubts/${created.json.doubtId}/research-jobs`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${t}`, 'Idempotency-Key': key },
});
const job1 = await res1.json();
ok(res1.status === 202 && res1.headers.get('location') === `/research-jobs/${job1.id}`, '202 Accepted com Location');
const res2 = await fetch(`${process.env.NOTEE_API || 'http://localhost:5099'}/doubts/${created.json.doubtId}/research-jobs`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${t}`, 'Idempotency-Key': key },
});
ok(res2.status === 202 && (await res2.json()).id === job1.id, 'mesma Idempotency-Key devolve o mesmo job');
ok((await call(other, 'GET', `/research-jobs/${job1.id}`)).status === 404, 'outra conta não vê o job -> 404');
ok((await call(other, 'POST', `/doubts/${created.json.doubtId}/research-jobs`)).status === 404, 'outra conta não enfileira -> 404');
const job1Done = await waitFor(() => call(t, 'GET', `/research-jobs/${job1.id}`).then(r => r.json), j => j?.status === 'Succeeded');
ok(job1Done?.status === 'Succeeded', 'novo job processado');
ok((await call(t, 'POST', `/research-jobs/${job1.id}/cancel`)).status === 409, 'cancelar job concluído -> 409');

// Pesquisa desligada: dúvida fica pendente, sem job; enfileirar -> 409; cancelar job na fila
const t2 = await newUser('research-off');
const p = (await call(t2, 'GET', '/me/preferences')).json;
await call(t2, 'PATCH', '/me/preferences', { researchEnabled: false, expectedVersion: p.version });
const n2 = (await call(t2, 'POST', '/notes', { title: 'Nota', content: blocks('texto') })).json;
const d2 = await call(t2, 'POST', '/doubts', { noteId: n2.id, question: 'Pergunta?', blockId: 'b1', exactQuote: 'texto', startIndex: 0, endIndex: 5 });
ok(d2.json.jobId === null && d2.json.status === 'Pending', 'pesquisa desligada: sem job, dúvida pendente');
ok((await call(t2, 'POST', `/doubts/${d2.json.doubtId}/research-jobs`)).status === 409, 'pesquisa desligada: enfileirar -> 409');

// AC09: dúvida livre do mapa, sem âncora
const board = await call(t, 'POST', `/notes/${note.id}/doubts`, { question: 'Como isso se relaciona com filas?' });
ok(board.status === 201 && board.json.origin === 'BoardQuestion' && board.json.blockId === null, 'AC09: dúvida do mapa sem âncora');
ok((await call(t, 'POST', `/notes/${note.id}/doubts`, { question: '   ' })).status === 400, 'pergunta vazia -> 400');
ok((await call(other, 'POST', `/notes/${note.id}/doubts`, { question: 'x?' })).status === 404, 'dúvida em nota de outra conta -> 404');
const boardDone = await waitFor(() => getDoubt(t, board.json.id), finished);
ok(boardDone?.status === 'Completed', 'dúvida do mapa também é pesquisada (Fake)');
const list = (await call(t, 'GET', `/doubts/note/${note.id}`)).json;
ok(list.length === 2 && list.some(d => d.origin === 'BoardQuestion'), 'dúvidas da nota incluem a do mapa');
ok((await call(t, 'GET', '/doubts')).status === 200 && (await call(other, 'GET', '/doubts')).json.length === 0, 'lista vazia é 200 []');

// Dúvida em nota na lixeira -> 404
await call(t2, "POST", `/notes/${n2.id}/trash`, {});
ok((await call(t2, 'POST', `/notes/${n2.id}/doubts`, { question: 'x?' })).status === 404, 'nota na lixeira não aceita dúvida -> 404');

process.exit(summary('research') ? 1 : 0);
