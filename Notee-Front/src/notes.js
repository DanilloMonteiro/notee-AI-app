// Utilitários e chamadas compartilhadas de notas (lista, organização e ciclo de vida).
import { apiFetch } from './api';

export const STATUS_LABEL = { Draft: 'Rascunho', Active: 'Ativa', Archived: 'Arquivada', Trashed: 'Na lixeira' };

/** Texto simples a partir do conteúdo em blocos JSON (ou texto puro). */
export function noteExcerpt(content, max = 140) {
  if (!content) return '';
  let text = content;
  try {
    const parsed = JSON.parse(content);
    if (Array.isArray(parsed)) text = parsed.map(b => b?.text || '').filter(Boolean).join(' ');
  } catch {
    // texto puro
  }
  text = text.replace(/\s+/g, ' ').trim();
  return text.length > max ? `${text.slice(0, max).trimEnd()}…` : text;
}

export const formatShortDate = iso =>
  iso ? new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' }) : '';

export const notesApi = {
  list: ({ status = 'active', query = '', categoryId = '', tagId = '', page = 1, pageSize = 30 } = {}) => {
    const params = new URLSearchParams({ status, page: String(page), pageSize: String(pageSize) });
    if (query) params.set('query', query);
    if (categoryId) params.set('categoryId', categoryId);
    if (tagId) params.set('tagId', tagId);
    return apiFetch(`/notes?${params}`);
  },
  lifecycle: (id, action, expectedVersion) =>
    apiFetch(`/notes/${id}/${action}`, { method: 'POST', body: JSON.stringify({ expectedVersion }) }),
  remove: id => apiFetch(`/notes/${id}`, { method: 'DELETE' }),
};

export const taxonomyApi = {
  list: kind => apiFetch(`/notes/${kind}`),
  create: (kind, name) => apiFetch(`/notes/${kind}`, { method: 'POST', body: JSON.stringify({ name }) }),
  rename: (kind, id, name) => apiFetch(`/notes/${kind}/${id}`, { method: 'PATCH', body: JSON.stringify({ name }) }),
  remove: (kind, id) => apiFetch(`/notes/${kind}/${id}`, { method: 'DELETE' }),
};
