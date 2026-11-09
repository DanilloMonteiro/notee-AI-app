// Preferências, consentimento de IA externa (ADR-019) e segurança básica de autenticação.
import { call, newUser, ok, summary } from './lib.mjs';

const t = await newUser('prefs');
const other = await newUser('prefs-other');

ok((await call(null, 'GET', '/me/preferences')).status === 401, 'sem token -> 401');
const me = await call(t, 'GET', '/me');
ok(me.status === 200 && me.json.email?.endsWith('@test.local'), '/me devolve o e-mail da conta');

let p = (await call(t, 'GET', '/me/preferences')).json;
ok(p.researchEnabled === true && p.externalAiConsent === false && p.effectiveProvider === 'fake', 'padrão: pesquisa ligada, sem consentimento, provider fake');

const noVersion = await call(t, 'PATCH', '/me/preferences', { externalAiConsent: true, expectedVersion: p.version });
ok(noVersion.status === 400, 'consentir sem versão do termo -> 400');

const granted = await call(t, 'PATCH', '/me/preferences', { externalAiConsent: true, consentVersion: p.currentConsentVersion, expectedVersion: p.version });
ok(granted.status === 200 && granted.json.externalAiConsent && granted.json.consentedAt, 'consentimento concedido com data');
ok((await call(t, 'PATCH', '/me/preferences', { researchEnabled: false, expectedVersion: p.version })).status === 409, 'versão obsoleta -> 409');

ok((await call(other, 'GET', '/me/preferences')).json.externalAiConsent === false, 'consentimento é isolado por conta');

const revoked = await call(t, 'PATCH', '/me/preferences', { externalAiConsent: false, expectedVersion: granted.json.version });
ok(revoked.status === 200 && !revoked.json.externalAiConsent && revoked.json.effectiveProvider === 'fake', 'revogar volta para o provider fake');

// Token assinado com a chave antiga embutida no código não é mais aceito
const forged = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IngiLCJleHAiOjQxMDI0NDQ4MDAsImlzcyI6Ik5vdGVlQXBpIiwiYXVkIjoiTm90ZWVBcHAifQ.invalid';
ok((await call(forged, 'GET', '/me')).status === 401, 'token com assinatura inválida -> 401');

process.exit(summary('preferences') ? 1 : 0);
