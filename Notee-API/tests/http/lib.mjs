// Utilitários dos testes HTTP de integração. Rode contra uma API de TESTE (nunca produção):
//   NOTEE_API=http://localhost:5099 node tests/http/run-all.mjs
export const BASE = process.env.NOTEE_API || 'http://localhost:5099';

const results = { passed: 0, failed: 0 };

export function ok(cond, label) {
  if (cond) {
    results.passed++;
    console.log('  ok  ', label);
  } else {
    results.failed++;
    console.log('  FAIL', label);
  }
}

export async function call(token, method, path, body) {
  const res = await fetch(BASE + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    // corpo não-JSON
  }
  return { status: res.status, json };
}

let seq = 0;
/** Registra e autentica um usuário novo e devolve o token. */
export async function newUser(prefix = 'user') {
  const email = `${prefix}${Date.now()}${seq++}@test.local`;
  await call(null, 'POST', '/auth/register', { email, password: 'Senha!Forte123' });
  const r = await call(null, 'POST', '/auth/login', { email, password: 'Senha!Forte123' });
  return r.json?.token;
}

export const blocks = text => JSON.stringify([{ id: 'b1', type: 'paragraph', text }]);

export function summary(name) {
  console.log(`\n[${name}] ${results.passed} ok, ${results.failed} falhas`);
  return results.failed;
}
