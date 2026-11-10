// src/pages/Register.jsx
import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch, saveSession } from '../api';

// Códigos do ASP.NET Identity (POST /auth/register devolve IdentityError[] em 400).
const IDENTITY_ERRORS = {
  DuplicateUserName: 'Já existe uma conta com este e-mail.',
  DuplicateEmail: 'Já existe uma conta com este e-mail.',
  InvalidEmail: 'E-mail inválido.',
  InvalidUserName: 'E-mail inválido.',
  PasswordTooShort: 'A senha precisa ter pelo menos 8 caracteres.',
  PasswordRequiresDigit: 'A senha precisa ter pelo menos um número.',
  PasswordRequiresLower: 'A senha precisa ter pelo menos uma letra minúscula.',
  PasswordRequiresUpper: 'A senha precisa ter pelo menos uma letra maiúscula.',
  PasswordRequiresNonAlphanumeric: 'A senha precisa ter pelo menos um símbolo (ex.: ! @ #).',
  PasswordRequiresUniqueChars: 'A senha precisa ter mais caracteres diferentes.',
  InvalidFullName: 'Informe seu nome (pelo menos 2 caracteres).',
  InvalidPhoneNumber: 'Informe um telefone válido com DDD, ex.: (11) 98765-4321.',
};

/** Máscara visual (11) 98765-4321 / (11) 3456-7890. A API recebe e normaliza os dígitos. */
function formatPhone(value) {
  if (value.trim().startsWith('+')) return value; // número internacional: sem máscara
  const d = value.replace(/\D/g, '').slice(0, 11);
  if (d.length <= 2) return d.length ? `(${d}` : '';
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
}

function describeErrors(err) {
  if (err?.status === 429) return ['Muitas tentativas. Aguarde um pouco e tente de novo.'];
  if (Array.isArray(err?.problem) && err.problem.length) {
    return [...new Set(err.problem.map(e => IDENTITY_ERRORS[e.code] || e.description))];
  }
  if (err?.status) return [err.message];
  return ['Não foi possível falar com a API. Verifique se ela está em execução.'];
}

export function Register() {
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState([]);
  const [submitting, setSubmitting] = useState(false);
  const navigate = useNavigate();

  const handleRegister = async e => {
    e.preventDefault();
    if (password !== confirm) {
      setErrors(['As senhas não coincidem.']);
      return;
    }

    setErrors([]);
    setSubmitting(true);
    try {
      await apiFetch('/auth/register', {
        method: 'POST',
        body: JSON.stringify({ email, password, fullName: fullName.trim(), phoneNumber: phoneNumber.trim() }),
      });
    } catch (err) {
      setErrors(describeErrors(err));
      setSubmitting(false);
      return;
    }

    // Conta criada: entra direto, sem pedir a senha de novo.
    try {
      const session = await apiFetch('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      });
      saveSession({ token: session.token, refreshToken: session.refreshToken });
      navigate('/home', { replace: true });
    } catch {
      navigate('/login', { replace: true, state: { registeredEmail: email } });
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
        <p>Uma boa pergunta não precisa se perder entre as coisas que você ainda vai fazer.</p>
        <p className="tiny" style={{ marginTop: 30 }}>
          Privado por padrão. Compartilhado por escolha.
        </p>
      </aside>

      <main className="auth-main">
        <div className="auth-form">
          <div className="overline mb16">NOTEE / CADASTRO</div>
          <h1>Comece pelo que te interessa.</h1>
          <p className="subtitle">Crie seu espaço para notas e perguntas.</p>

          <form onSubmit={handleRegister}>
            <div className="field">
              <label htmlFor="auth-name">Seu nome</label>
              <input
                type="text"
                id="auth-name"
                placeholder="Como você quer ser chamado?"
                autoComplete="name"
                minLength={2}
                maxLength={120}
                required
                value={fullName}
                onChange={e => setFullName(e.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="auth-phone">Telefone</label>
              <input
                type="tel"
                id="auth-phone"
                placeholder="(11) 98765-4321"
                autoComplete="tel"
                inputMode="tel"
                maxLength={20}
                required
                value={phoneNumber}
                onChange={e => setPhoneNumber(formatPhone(e.target.value))}
              />
            </div>
            <div className="field">
              <label htmlFor="auth-email">E-mail</label>
              <input
                type="email"
                id="auth-email"
                placeholder="voce@example.test"
                autoComplete="email"
                required
                value={email}
                onChange={e => setEmail(e.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="auth-password">Senha</label>
              <input
                type="password"
                id="auth-password"
                minLength={8}
                autoComplete="new-password"
                aria-describedby="password-hint"
                required
                value={password}
                onChange={e => setPassword(e.target.value)}
              />
              <span id="password-hint" className="tiny muted">
                Mínimo de 8 caracteres, com maiúscula, minúscula, número e símbolo.
              </span>
            </div>
            <div className="field">
              <label htmlFor="auth-confirm">Repita a senha</label>
              <input
                type="password"
                id="auth-confirm"
                minLength={8}
                autoComplete="new-password"
                required
                value={confirm}
                onChange={e => setConfirm(e.target.value)}
              />
            </div>

            {errors.length > 0 && (
              <div className="callout warning mb16" role="alert">
                <Icon name="info" />
                <span>
                  {errors.map(msg => (
                    <span key={msg} style={{ display: 'block' }}>
                      {msg}
                    </span>
                  ))}
                </span>
              </div>
            )}

            <button className="btn primary full" type="submit" disabled={submitting}>
              {submitting ? 'Criando conta…' : 'Criar minha conta'} <Icon name="arrow" className="sm" />
            </button>
          </form>

          <p className="small muted" style={{ textAlign: 'center', marginTop: 22 }}>
            Já conhece o Notee?{' '}
            <Link to="/login" style={{ color: 'var(--brand)' }}>
              Entrar
            </Link>
          </p>
        </div>
      </main>
    </div>
  );
}
