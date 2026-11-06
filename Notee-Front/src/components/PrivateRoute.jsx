// src/components/PrivateRoute.jsx
import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';

export function PrivateRoute() {
  // Verifica se temos a "chave" guardada no navegador
  const token = localStorage.getItem('notee_token');

  // Se houver token, renderiza a página que está dentro (Outlet).
  // Se não, atira o utilizador para a página de login.
  return token ? <Outlet /> : <Navigate to="/login" replace />;
}
