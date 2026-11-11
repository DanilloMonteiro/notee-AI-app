// Identidade (M04): access token curto, refresh rotativo com detecção de reuso, logout e erros.
import { call, ok, summary } from './lib.mjs';

const email = `auth${Date.now()}@test.local`;
const account = { email, password: 'Senha!Forte123', fullName: '  Ana   Souza ', phoneNumber: '(11) 98765-4321' };

const bare = await call(null, 'POST', '/auth/register', { email: `sem${email}`, password: 'Senha!Forte123' });
ok(bare.status === 400 && bare.json?.some(e => e.code === 'InvalidFullName'), 'registro sem nome -> 400 InvalidFullName');
ok(bare.status === 400 && bare.json?.some(e => e.code === 'InvalidPhoneNumber'), 'registro sem telefone -> 400 InvalidPhoneNumber');
const badPhone = await call(null, 'POST', '/auth/register', { ...account, email: `tel${email}`, phoneNumber: '123' });
ok(badPhone.status === 400 && badPhone.json?.some(e => e.code === 'InvalidPhoneNumber'), 'telefone inválido -> 400 InvalidPhoneNumber');

ok((await call(null, 'POST', '/auth/register', account)).status === 200, 'registro');
ok((await call(null, 'POST', '/auth/register', account)).status === 400, 'e-mail duplicado -> 400');
ok((await call(null, 'POST', '/auth/login', { email, password: 'errada' })).status === 401, 'senha errada -> 401');

const login = (await call(null, 'POST', '/auth/login', { email, password: 'Senha!Forte123' })).json;
ok(login.token && login.refreshToken && new Date(login.expiresAt) > new Date(), 'login devolve access e refresh token');
const me = await call(login.token, 'GET', '/me');
ok(me.status === 200, 'access token autentica');
ok(me.json?.fullName === 'Ana Souza' && me.json?.phoneNumber === '11987654321', '/me devolve nome e telefone normalizados');

const r1 = await call(null, 'POST', '/auth/refresh', { refreshToken: login.refreshToken });
ok(r1.status === 200 && r1.json.refreshToken !== login.refreshToken, 'refresh rotaciona o token');
ok((await call(r1.json.token, 'GET', '/me')).status === 200, 'novo access token funciona');

// Reuso do refresh antigo = possível roubo: revoga a família inteira
ok((await call(null, 'POST', '/auth/refresh', { refreshToken: login.refreshToken })).status === 401, 'reusar refresh já usado -> 401');
ok((await call(null, 'POST', '/auth/refresh', { refreshToken: r1.json.refreshToken })).status === 401, 'reuso revoga também o token mais novo');

// Logout revoga a sessão
const s2 = (await call(null, 'POST', '/auth/login', { email, password: 'Senha!Forte123' })).json;
ok((await call(null, 'POST', '/auth/logout', { refreshToken: s2.refreshToken })).status === 204, 'logout');
ok((await call(null, 'POST', '/auth/refresh', { refreshToken: s2.refreshToken })).status === 401, 'refresh após logout -> 401');
ok((await call(null, 'POST', '/auth/logout', { refreshToken: 'inexistente' })).status === 204, 'logout não revela tokens inexistentes');
ok((await call(null, 'POST', '/auth/refresh', { refreshToken: 'inexistente' })).status === 401, 'refresh inválido -> 401');

// Sessões independentes: logout de uma não derruba a outra
const a = (await call(null, 'POST', '/auth/login', { email, password: 'Senha!Forte123' })).json;
const b = (await call(null, 'POST', '/auth/login', { email, password: 'Senha!Forte123' })).json;
await call(null, 'POST', '/auth/logout', { refreshToken: a.refreshToken });
ok((await call(null, 'POST', '/auth/refresh', { refreshToken: b.refreshToken })).status === 200, 'outra sessão continua válida');

ok((await call(null, 'POST', '/auth/forgot-password', { email: 'naoexiste@test.local' })).status === 200, 'esqueci a senha não revela se o e-mail existe');

process.exit(summary('auth') ? 1 : 0);
