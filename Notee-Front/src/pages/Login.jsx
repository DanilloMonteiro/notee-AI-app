// src/pages/Login.jsx
import React, { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch, saveSession } from '../api';

export function Login() {
  const location = useLocation();
  const [email, setEmail] = useState(location.state?.registeredEmail || '');
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
        saveSession({ token, refreshToken: response.refreshToken });
        navigate('/home'); // Redireciona para a Visão Geral
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
        <Link className="logo" to="/" aria-label="Notee, início">
          <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
        </Link>
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

          <p className="small muted" style={{ textAlign: 'center', marginTop: 22 }}>
            Ainda não tem uma conta?{' '}
            <Link to="/register" style={{ color: 'var(--brand)' }}>
              Criar conta
            </Link>
          </p>
        </div>
      </main>
    </div>
  );
}
