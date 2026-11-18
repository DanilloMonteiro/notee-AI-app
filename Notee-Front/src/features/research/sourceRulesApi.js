// Sites bloqueados e confiáveis da pessoa (ADR-047). Os bloqueios fixos do Notee (ex.: Wikipédia) vêm com fixed: true.
import { apiFetch } from '../../api';

export const sourceRulesApi = {
  list: () => apiFetch('/me/source-rules'),
  /** kind: 'blocked' | 'trusted'. Cria ou troca o tipo de um site; devolve a lista atualizada. */
  save: (domain, kind) => apiFetch('/me/source-rules', { method: 'PUT', body: JSON.stringify({ domain, kind }) }),
  remove: domain => apiFetch(`/me/source-rules/${encodeURIComponent(domain)}`, { method: 'DELETE' }),
};
