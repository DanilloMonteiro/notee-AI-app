// Carga leve e reproduzível (M17): N usuários simultâneos criam notas, listam, buscam e abrem um mapa.
// Uso: NOTEE_API=http://localhost:5099 USERS=20 ROUNDS=10 node tests/load/smoke-load.mjs
// Rode SOMENTE contra ambiente de teste (cria contas e dados).
const BASE = process.env.NOTEE_API || 'http://localhost:5099';
const USERS = Number(process.env.USERS || 20);
const ROUNDS = Number(process.env.ROUNDS || 10);
const timings = {};
let errors = 0;

async function timed(name, fn) {
  const t0 = performance.now();
  try {
    const res = await fn();
    if (!res.ok) errors++;
    return res;
  } catch {
    errors++;
    return null;
  } finally {
    (timings[name] ??= []).push(performance.now() - t0);
  }
}

const json = (token, method, path, body) =>
  fetch(BASE + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });

async function user(i) {
  const email = `load${Date.now()}_${i}@test.local`;
  await json(null, 'POST', '/auth/register', { email, password: 'Senha!Forte123' });
  const { token } = await (await json(null, 'POST', '/auth/login', { email, password: 'Senha!Forte123' })).json();
  const board = await (await json(token, 'POST', '/boards', { title: 'Carga' })).json();
  for (let r = 0; r < ROUNDS; r++) {
    const content = JSON.stringify([{ id: 'b1', type: 'paragraph', text: `Rodada ${r} sobre democracia e participação ${i}` }]);
    await timed('POST /notes', () => json(token, 'POST', '/notes', { title: `Nota ${r}`, content }));
    await timed('GET /notes', () => json(token, 'GET', '/notes?pageSize=30'));
    await timed('GET /notes?query', () => json(token, 'GET', '/notes?query=democracia'));
    await timed('GET /boards/{id}', () => json(token, 'GET', `/boards/${board.id}`));
  }
}

const pct = (arr, p) => {
  const s = [...arr].sort((a, b) => a - b);
  return s[Math.min(s.length - 1, Math.floor((p / 100) * s.length))];
};

const t0 = performance.now();
await Promise.all(Array.from({ length: USERS }, (_, i) => user(i)));
const total = (performance.now() - t0) / 1000;
const requests = Object.values(timings).reduce((n, a) => n + a.length, 0);

console.log(`usuários=${USERS} rodadas=${ROUNDS} requisições=${requests} erros=${errors} duração=${total.toFixed(1)}s (${(requests / total).toFixed(0)} req/s)`);
for (const [name, arr] of Object.entries(timings)) {
  console.log(`${name.padEnd(18)} p50=${pct(arr, 50).toFixed(0)}ms p95=${pct(arr, 95).toFixed(0)}ms max=${Math.max(...arr).toFixed(0)}ms`);
}
process.exit(errors ? 1 : 0);
