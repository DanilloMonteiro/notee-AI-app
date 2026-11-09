// Publicação revisada (M08): chamadas do autor e leitura pública.
import { apiFetch } from '../../api';

const json = body => JSON.stringify(body);

export const publishingApi = {
  profile: () => apiFetch('/me/profile'),
  saveProfile: (handle, displayName) => apiFetch('/me/profile', { method: 'PUT', body: json({ handle, displayName }) }),
  mine: () => apiFetch('/publications'),
  forNote: noteId => apiFetch(`/notes/${noteId}/publications`),
  publish: (noteId, visibility, slug) =>
    apiFetch(`/notes/${noteId}/publications`, { method: 'POST', body: json({ visibility, slug: slug || null }) }),
  republish: id => apiFetch(`/publications/${id}/revisions`, { method: 'POST' }),
  setVisibility: (id, visibility, expectedVersion) =>
    apiFetch(`/publications/${id}`, { method: 'PATCH', body: json({ visibility, expectedVersion }) }),
  rotateToken: id => apiFetch(`/publications/${id}/rotate-token`, { method: 'POST' }),
  revoke: id => apiFetch(`/publications/${id}`, { method: 'DELETE' }),
};

export const publicApi = {
  library: () => apiFetch('/public/notes'),
  profile: handle => apiFetch(`/public/profiles/${encodeURIComponent(handle)}`),
  note: (handle, slug) => apiFetch(`/public/notes/${encodeURIComponent(handle)}/${encodeURIComponent(slug)}`),
  shared: token => apiFetch(`/shared/${encodeURIComponent(token)}`),
};

export const VISIBILITY = {
  private: { label: 'Privada', help: 'Só você vê a prévia.' },
  unlisted: { label: 'Não listada', help: 'Quem tiver o link secreto pode ler.' },
  public: { label: 'Pública', help: 'Aparece no seu perfil e na biblioteca.' },
};

export const absoluteUrl = path => (path ? `${window.location.origin}${path}` : '');
