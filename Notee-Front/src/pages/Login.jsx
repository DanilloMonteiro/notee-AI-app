// src/pages/Login.jsx
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';

export function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const handleLogin = async e => {
    e.preventDefault();
    setError('');

    try {
      // Chama o endpoint C# que criámos no IdentityEndpoints.cs
      const response = await apiFetch('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      });

      // O Identity do .NET retorna o token geralmente num campo chamado "accessToken" ou "token"
      const token = response.accessToken || response.token;

      if (token) {
        localStorage.setItem('notee_token', token);
        navigate('/'); // Redireciona para a Visão Geral
      } else {
        setError('Token não encontrado na resposta da API.');
      }
    } catch (err) {
      setError('Falha ao entrar. Verifique o seu e-mail, senha e se a API C# está a correr.');
    }
  };

  return (
    <div className="auth-shell">
      <aside className="auth-art">
        <div className="logo">
          <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
        </div>
        <h1>
          Anote agora.
          <br />
          Entenda depois.
        </h1>
        <p>Uma boa pergunta não precisa de se perder.</p>
      </aside>

      <main className="auth-main">
        <div className="auth-form">
          <div className="overline mb16">NOTEE / ENTRAR</div>
          <h1>Que bom ter você por aqui.</h1>
          <p className="subtitle">Volte para as suas notas e perguntas.</p>

          <form onSubmit={handleLogin}>
            <div className="field">
              <label htmlFor="auth-email">E-mail</label>
              <input type="email" id="auth-email" required value={email} onChange={e => setEmail(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="auth-password">Senha</label>
              <input
                type="password"
                id="auth-password"
                required
                value={password}
                onChange={e => setPassword(e.target.value)}
              />
            </div>

            {error && (
              <div className="callout warning mb16">
                <Icon name="info" />
                <span>{error}</span>
              </div>
            )}

            <button className="btn primary full" type="submit">
              Entrar na conta <Icon name="arrow" className="sm" />
            </button>
          </form>
        </div>
      </main>
    </div>
  );
}
