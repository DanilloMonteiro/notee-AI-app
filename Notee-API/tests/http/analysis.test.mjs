// Análise assistida (M10): sugestões de tag/categoria/dúvida com aceite individual,
// revalidação de versão e precedência conta/nota da pesquisa automática (sem backfill).
import { call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('analysis');
const other = await newUser('analysis-other');

await call(t, 'POST', '/notes/categories', { name: 'Política' });
const content = JSON.stringify([
  { id: 'b1', type: 'paragraph', text: 'A participação popular fortalece a democracia. Participação exige informação.' },
  { id: 'b2', type: 'paragraph', text: 'Como garantir participação em política local? Plebiscitos ajudam.' },
]);
let note = (await call(t, 'POST', '/notes', { title: 'Participação', content })).json;

const run = await call(t, 'POST', `/notes/${note.id}/analysis-runs`);
ok(run.status === 200 && run.json.length > 0, 'análise gera sugestões pendentes');
const tag = run.json.find(s => s.kind === 'tag' && s.value === 'participação');
const cat = run.json.find(s => s.kind === 'category' && s.value === 'Política');
const doubt = run.json.find(s => s.kind === 'doubt' && s.value.startsWith('Como garantir'));
ok(tag && cat && doubt, 'sugere tag frequente, categoria existente e pergunta do texto');
ok((await call(t, 'GET', `/notes/${note.id}`)).json.version === note.version, 'analisar não altera a nota');
ok((await call(t, 'POST', `/notes/${note.id}/analysis-runs`)).json.length === run.json.length, 'reanalisar não duplica pendentes');
ok((await call(other, 'GET', `/notes/${note.id}/suggestions`)).status === 404, 'outra conta não vê sugestões');
ok((await call(other, 'POST', `/suggestions/${tag.id}/accept`)).status === 404, 'outra conta não aceita');

ok((await call(t, 'POST', `/suggestions/${tag.id}/accept`)).status === 200, 'aceitar tag');
note = (await call(t, 'GET', `/notes/${note.id}`)).json;
ok(note.tags.some(x => x.name === 'participação') && note.version === 2, 'tag aplicada com nova versão');
ok((await call(t, 'POST', `/suggestions/${cat.id}/accept`)).status === 409, 'sugestão de versão anterior fica obsoleta (409)');

const run2 = (await call(t, 'POST', `/notes/${note.id}/analysis-runs`)).json;
const cat2 = run2.find(s => s.kind === 'category');
const doubt2 = run2.find(s => s.kind === 'doubt');
ok((await call(t, 'POST', `/suggestions/${cat2.id}/accept`)).status === 200, 'aceitar categoria após nova análise');
note = (await call(t, 'GET', `/notes/${note.id}`)).json;
ok(note.categoryName === 'Política', 'categoria aplicada');

const run3 = (await call(t, 'POST', `/notes/${note.id}/analysis-runs`)).json;
const d3 = run3.find(s => s.kind === 'doubt');
ok(d3 && doubt2, 'pergunta continua sugerida');
ok((await call(t, 'POST', `/suggestions/${d3.id}/accept`)).status === 200, 'aceitar dúvida');
const doubts = (await call(t, 'GET', `/doubts/note/${note.id}`)).json;
ok(doubts.length === 1 && doubts[0].blockId === 'b2' && doubts[0].exactQuote.startsWith('Como garantir'), 'dúvida criada com âncora real no bloco');
const left = (await call(t, 'GET', `/notes/${note.id}/suggestions`)).json;
if (left.length) ok((await call(t, 'POST', `/suggestions/${left[0].id}/dismiss`)).status === 204, 'descartar sugestão');
ok(!(await call(t, 'POST', `/notes/${note.id}/analysis-runs`)).json.some(s => s.kind === 'doubt'), 'dúvida já registrada não é sugerida de novo');

// Precedência conta/nota
let prefs = (await call(t, 'GET', '/me/preferences')).json;
ok(prefs.autoResearch === true, 'conta: pesquisa automática ligada por padrão');
prefs = (await call(t, 'PATCH', '/me/preferences', { autoResearch: false, expectedVersion: prefs.version })).json;
const n2 = (await call(t, 'POST', '/notes', { title: 'Outra', content: '[]' })).json;
let d = (await call(t, 'POST', `/notes/${n2.id}/doubts`, { question: 'Sem auto?' })).json;
ok(d.latestJob === null && d.status === 'Pending', 'conta com auto desligado: dúvida sem job');
const s = (await call(t, 'PATCH', `/notes/${n2.id}/settings`, { autoResearch: 'on' })).json;
ok(s.autoResearch === 'on' && s.effectiveAutoResearch === true, 'nota sobrepõe a conta');
ok((await call(t, 'GET', `/doubts/${d.id}`)).json.latestJob === null, 'sem backfill: dúvida antiga continua sem job');
d = (await call(t, 'POST', `/notes/${n2.id}/doubts`, { question: 'Com auto pela nota?' })).json;
ok(d.latestJob !== null, 'nova dúvida usa a configuração da nota');
ok((await call(t, 'PATCH', `/notes/${n2.id}/settings`, { autoResearch: 'talvez' })).status === 400, 'valor inválido -> 400');
ok((await call(other, 'GET', `/notes/${n2.id}/settings`)).status === 404, 'configuração de nota alheia -> 404');

process.exit(summary('analysis') ? 1 : 0);
