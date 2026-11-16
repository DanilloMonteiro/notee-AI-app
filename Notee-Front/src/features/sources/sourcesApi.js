import { apiFetch } from '../../api';

const json = body => JSON.stringify(body);

export const sourcesApi = {
  list: ({ query = '', kind = '', status = '' } = {}) => {
    const params = new URLSearchParams();
    if (query) params.set('query', query);
    if (kind) params.set('kind', kind);
    if (status) params.set('status', status);
    return apiFetch(`/sources${params.size ? `?${params}` : ''}`);
  },
  get: id => apiFetch(`/sources/${id}`),
  resolve: input => apiFetch('/sources/resolve', { method: 'POST', body: json({ input }) }),
  files: id => apiFetch(`/sources/${id}/files`),
  uploadFile: (id, file) => { const form = new FormData(); form.append('file', file); return apiFetch(`/sources/${id}/files`, { method: 'POST', body: form }); },
  deleteFile: (id, fileId) => apiFetch(`/sources/${id}/files/${fileId}`, { method: 'DELETE' }),
  sourceChecks: (id, status = '') => apiFetch(`/sources/${id}/checks${status ? `?status=${encodeURIComponent(status)}` : ''}`),
  retrySourceCheck: id => apiFetch(`/sources/${id}/checks`, { method: 'POST', body: '{}' }),
  citationChecks: id => apiFetch(`/citations/${id}/checks`),
  retryCitationCheck: id => apiFetch(`/citations/${id}/checks`, { method: 'POST', body: '{}' }),
  estimateAiCheck: id => apiFetch(`/citations/${id}/ai-check/estimate`, { method: 'POST', body: '{}' }),
  runAiCheck: id => apiFetch(`/citations/${id}/ai-check`, { method: 'POST', body: json({ confirm: true }) }),
  create: source => apiFetch('/sources', { method: 'POST', body: json(source) }),
  update: (id, source) => apiFetch(`/sources/${id}`, { method: 'PATCH', body: json(source) }),
  remove: id => apiFetch(`/sources/${id}`, { method: 'DELETE' }),
  citations: noteId => apiFetch(`/notes/${noteId}/citations`),
  addCitation: (noteId, citation) => apiFetch(`/notes/${noteId}/citations`, { method: 'POST', body: json(citation) }),
  updateCitation: (id, citation) => apiFetch(`/citations/${id}`, { method: 'PATCH', body: json(citation) }),
  removeCitation: id => apiFetch(`/citations/${id}`, { method: 'DELETE' }),
};
