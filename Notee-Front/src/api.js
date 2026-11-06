// src/api.js
const API_URL = 'http://localhost:5000'; // A porta onde o seu C# está a correr

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
    throw new Error(`Erro ${response.status}: Falha na requisição ao backend.`);
  }

  // O .NET retorna 204 (No Content) para DELETE ou PUTs vazios.
  if (response.status === 204) return null;

  return response.json();
}
