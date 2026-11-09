// src/api.js
// A URL pode ser sobrescrita com VITE_API_URL (ex.: apontar para uma API de teste).
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

/** Erro HTTP com status e Problem Details (title/detail) quando a API os envia. */
export class ApiError extends Error {
  constructor(status, problem) {
    super(problem?.detail || problem?.title || `Erro ${status}: Falha na requisição ao backend.`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
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
