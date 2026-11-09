// Cliente HTTP do módulo KnowledgeMaps (Mapas de ideias).
import { apiFetch } from '../../api';

const json = body => JSON.stringify(body);

export const boardsApi = {
  list: () => apiFetch('/boards'),
  get: boardId => apiFetch(`/boards/${boardId}`),
  create: ({ title, description }) => apiFetch('/boards', { method: 'POST', body: json({ title, description }) }),
  update: (boardId, { title, description, expectedVersion }) =>
    apiFetch(`/boards/${boardId}`, { method: 'PATCH', body: json({ title, description, expectedVersion }) }),
  remove: boardId => apiFetch(`/boards/${boardId}`, { method: 'DELETE' }),

  movePlacement: (boardId, noteId, { x, y, expectedVersion }) =>
    apiFetch(`/boards/${boardId}/placements/${noteId}`, { method: 'PUT', body: json({ x, y, expectedVersion }) }),
  movePlacements: (boardId, placements) =>
    apiFetch(`/boards/${boardId}/placements`, { method: 'PUT', body: json({ placements }) }),

  createEdge: (boardId, edge) => apiFetch(`/boards/${boardId}/edges`, { method: 'POST', body: json(edge) }),
  updateEdge: (boardId, edgeId, edge) =>
    apiFetch(`/boards/${boardId}/edges/${edgeId}`, { method: 'PATCH', body: json(edge) }),
  removeEdge: (boardId, edgeId) => apiFetch(`/boards/${boardId}/edges/${edgeId}`, { method: 'DELETE' }),
};

// Livros e fontes pertencem à nota (não ao mapa).
export const referencesApi = {
  list: noteId => apiFetch(`/notes/${noteId}/references`),
  create: (noteId, reference) => apiFetch(`/notes/${noteId}/references`, { method: 'POST', body: json(reference) }),
  remove: (noteId, referenceId) => apiFetch(`/notes/${noteId}/references/${referenceId}`, { method: 'DELETE' }),
};

// "Nova nota" no mapa usa exatamente o mesmo endpoint de Minhas notas.
export const createNote = ({ title, text }) =>
  apiFetch('/notes', {
    method: 'POST',
    body: json({
      title,
      content: JSON.stringify(text ? [{ id: crypto.randomUUID(), type: 'paragraph', text }] : []),
    }),
  });
