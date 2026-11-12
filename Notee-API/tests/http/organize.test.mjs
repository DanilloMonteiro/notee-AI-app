// "Organizar com IA" (ADR-037): estimativa, confirmação, simulação sem consentimento, isolamento e limites.
// Nenhum provedor real é chamado: sem consentimento a API usa a simulação local (Fake).
import { call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('org');
const other = await newUser('org-other');

const note = (await call(t, 'POST', '/notes', { title: 'Organizar', content: '[]' })).json;
const text = '# Economia\n\nLevantamento de **indicadores**.\n\n- PIB\n- Inflação\n\n[[grafico:0]]\n\n| Ano | PIB |\n| --- | --- |\n| 2014 | 0,5 |\n| 2015 | -3,5 |';

let r = await call(t, 'POST', `/notes/${note.id}/organize-ai/estimate`, { text });
ok(
  r.status === 200 && r.json.provider === 'fake' && r.json.external === false && r.json.consentGiven === false && r.json.estimatedCostUsd === 0,
  'estimativa sem consentimento usa simulação sem custo',
);
ok(r.json.estimatedInputTokens > 0 && r.json.maxPerHour >= 1, 'estimativa informa tokens e cota');

ok((await call(t, 'POST', `/notes/${note.id}/organize-ai`, { text })).status === 400, 'sem confirmação -> 400');

r = await call(t, 'POST', `/notes/${note.id}/organize-ai`, { text, confirm: true });
const types = r.json?.blocks?.map(b => b.type) || [];
ok(r.status === 200 && r.json.external === false && r.json.costUsd === 0, 'execução confirmada responde com a simulação');
ok(JSON.stringify(types) === JSON.stringify(['heading', 'paragraph', 'list', 'chart', 'table']), 'blocos organizados na ordem original');
ok(r.json.blocks[3].ref === 0, 'gráfico existente volta como referência');

const after = (await call(t, 'GET', `/notes/${note.id}`)).json;
ok(after.version === note.version && after.content === '[]', 'a nota não é alterada no servidor');

ok((await call(other, 'POST', `/notes/${note.id}/organize-ai/estimate`, { text })).status === 404, 'nota alheia -> 404 (estimativa)');
ok((await call(other, 'POST', `/notes/${note.id}/organize-ai`, { text, confirm: true })).status === 404, 'nota alheia -> 404 (execução)');
ok((await call(t, 'POST', `/notes/${note.id}/organize-ai`, { text: 'x'.repeat(50_001), confirm: true })).status === 400, 'texto acima do limite -> 400');
ok((await call(t, 'POST', `/notes/${note.id}/organize-ai/estimate`, { text: '  ' })).status === 400, 'texto vazio -> 400');

await call(t, 'POST', `/notes/${note.id}/trash`, {});
ok((await call(t, 'POST', `/notes/${note.id}/organize-ai`, { text, confirm: true })).status === 400, 'nota na lixeira -> 400');

process.exit(summary('organize') ? 1 : 0);
