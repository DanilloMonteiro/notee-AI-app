// src/api.js
// A URL pode ser sobrescrita com VITE_API_URL (ex.: apontar para uma API de teste).
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

/** Erro HTTP com status e Problem Details (title/detail) quando a API os envia. */
export class ApiError extends Error {
  constructor(status, problem) {
    super(problem?.detail || problem?.title || `Erro ${status}: Falha na requisição ao backend.`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

// ---- Sessão (M04): access token curto + refresh token rotativo ----
const TOKEN_KEY = 'notee_token';
const REFRESH_KEY = 'notee_refresh';
let refreshing = null;

export function saveSession({ token, refreshToken }) {
  localStorage.setItem(TOKEN_KEY, token);
  if (refreshToken) localStorage.setItem(REFRESH_KEY, refreshToken);
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
}

/** Encerra a sessão no servidor (revoga o refresh token) e localmente. */
export async function logout() {
  const refreshToken = localStorage.getItem(REFRESH_KEY);
  clearSession();
  if (refreshToken) {
    try {
      await fetch(`${API_URL}/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
    } catch {
      // sem rede: a sessão local já foi apagada
    }
  }
}

/** Uma única renovação por vez, compartilhada pelas requisições que receberam 401. */
function refreshSession() {
  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return Promise.resolve(false);
  refreshing ??= fetch(`${API_URL}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
    .then(async res => {
      if (!res.ok) return false;
      saveSession(await res.json());
      return true;
    })
    .catch(() => false)
    .finally(() => {
      refreshing = null;
    });
  return refreshing;
}

export async function apiFetch(endpoint, options = {}) {
  // Vamos buscar o token que guardaremos no navegador após o login
  const token = localStorage.getItem('notee_token');

  const headers = {
    'Content-Type': 'application/json',
    ...options.headers,
  };

  // Se tivermos token, injetamos na requisição!
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_URL}${endpoint}`, {
    ...options,
    headers,
  });

  // Access token expirado: renova uma vez e repete a requisição.
  if (response.status === 401 && token && !options._retried && !endpoint.startsWith('/auth/')) {
    if (await refreshSession()) {
      return apiFetch(endpoint, { ...options, _retried: true });
    }
    clearSession();
    if (!window.location.pathname.startsWith('/login')) window.location.assign('/login');
  }

  if (!response.ok) {
    let problem = null;
    try {
      problem = await response.json();
    } catch {
      // corpo vazio ou não-JSON
    }
    throw new ApiError(response.status, problem);
  }

  // O .NET retorna 204 (No Content) para DELETE ou PUTs vazios.
  if (response.status === 204) return null;

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}
