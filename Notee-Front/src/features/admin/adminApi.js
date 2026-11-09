// Cliente da área administrativa. O token admin fica só nesta aba (sessionStorage) e expira em 30 min.
// A rota é "escondida" (VITE_ADMIN_PATH) apenas para não aparecer na navegação; a proteção real é
// a conta com papel admin validada pela API.
import { API_URL, ApiError } from '../../api';

const KEY = 'notee_admin_token';

export const ADMIN_PATH = (import.meta.env.VITE_ADMIN_PATH || '/console-7k2q').replace(/\/$/, '');

export const adminSession = {
  get: () => sessionStorage.getItem(KEY),
  set: token => sessionStorage.setItem(KEY, token),
  clear: () => sessionStorage.removeItem(KEY),
};

async function request(path, options = {}) {
  const token = adminSession.get();
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  });
  if (!res.ok) {
    let problem = null;
    try {
      problem = await res.json();
    } catch {
      // sem corpo
    }
    if (res.status === 401 || res.status === 403) adminSession.clear();
    throw new ApiError(res.status, problem);
  }
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export const adminApi = {
  login: async (email, password) => {
    const data = await request('/auth/admin/login', { method: 'POST', body: JSON.stringify({ email, password }) });
    adminSession.set(data.token);
    return data;
  },
  state: () => request('/admin/ai'),
  setActive: provider => request('/admin/ai/active', { method: 'PUT', body: JSON.stringify({ provider }) }),
  setModel: (id, model) => request(`/admin/ai/providers/${id}/model`, { method: 'PUT', body: JSON.stringify({ model }) }),
  test: id => request(`/admin/ai/providers/${id}/test`, { method: 'POST' }),
};
