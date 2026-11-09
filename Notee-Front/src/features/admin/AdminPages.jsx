// Área administrativa: login próprio e painel para escolher a IA usada nas pesquisas.
import React, { useCallback, useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { ADMIN_PATH, adminApi, adminSession } from './adminApi';

function AdminShell({ children, onLogout }) {
  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg, #f7f8f4)' }}>
      <header className="row between" style={{ maxWidth: 980, margin: '0 auto', padding: '22px 20px' }}>
        <span className="logo">
          <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
          <span className="pill" style={{ marginLeft: 10 }}>
            Administração
          </span>
        </span>
        {onLogout && (
          <button className="btn compact ghost" onClick={onLogout}>
            <Icon name="lock" className="sm" /> Sair
          </button>
        )}
      </header>
      <main style={{ maxWidth: 980, margin: '0 auto', padding: '0 20px 60px' }}>{children}</main>
    </div>
  );
}

export function AdminLoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const navigate = useNavigate();

  const submit = async e => {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await adminApi.login(email.trim(), password);
      navigate(`${ADMIN_PATH}/ia`, { replace: true });
    } catch (err) {
      setError(err.status === 429 ? 'Muitas tentativas. Aguarde um minuto.' : 'Credenciais inválidas.');
      setBusy(false);
    }
  };

  return (
    <AdminShell>
      <div className="card pad" style={{ maxWidth: 420, margin: '40px auto' }}>
        <h1 className="mb16" style={{ fontSize: 24 }}>
          Acesso administrativo
        </h1>
        <form onSubmit={submit}>
          <div className="field">
            <label htmlFor="admin-email">E-mail</label>
            <input id="admin-email" type="email" autoComplete="username" required value={email} onChange={e => setEmail(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="admin-password">Senha</label>
            <input
              id="admin-password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={e => setPassword(e.target.value)}
            />
          </div>
          {error && <p className="small" style={{ color: '#a6452f' }}>{error}</p>}
          <button className="btn primary" type="submit" disabled={busy} style={{ width: '100%' }}>
            {busy ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </AdminShell>
  );
}

export function AdminAiPage() {
  const [state, setState] = useState(null);
  const [models, setModels] = useState({});
  const [tests, setTests] = useState({});
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const navigate = useNavigate();
  const loggedIn = Boolean(adminSession.get());

  const handleError = useCallback(
    err => {
      if (err.status === 401 || err.status === 403) navigate(ADMIN_PATH, { replace: true });
      else setMessage(err.message || 'Não foi possível concluir.');
    },
    [navigate],
  );

  const apply = s => {
    setState(s);
    setModels(Object.fromEntries(s.providers.map(p => [p.id, p.model])));
  };

  useEffect(() => {
    if (!loggedIn) return;
    adminApi.state().then(apply).catch(handleError);
  }, [loggedIn, handleError]);

  if (!loggedIn) return <Navigate to={ADMIN_PATH} replace />;

  const run = async (key, fn, okMsg) => {
    setBusy(key);
    setMessage('');
    try {
      const result = await fn();
      if (result?.providers) apply(result);
      if (okMsg) setMessage(okMsg);
      return result;
    } catch (err) {
      handleError(err);
      return null;
    } finally {
      setBusy('');
    }
  };

  const logout = () => {
    adminSession.clear();
    navigate(ADMIN_PATH, { replace: true });
  };

  return (
    <AdminShell onLogout={logout}>
      <div className="overline">INTELIGÊNCIA ARTIFICIAL</div>
      <h1 className="mb16">Qual IA responde às pesquisas</h1>
      <div className="callout neutral mb24">
        <Icon name="shield" />
        <span>
          A IA escolhida aqui só recebe dados de contas que <strong>autorizaram IA externa</strong> em Configurações; as demais continuam com
          respostas simuladas locais. Chaves de API ficam apenas na configuração do servidor (<code>Seção:ApiKey</code>), nunca neste painel.
        </span>
      </div>

      {message && <p className="small mb16">{message}</p>}

      {!state ? (
        <p className="muted">A carregar...</p>
      ) : (
        <>
          <p className="small muted mb16">
            Ativa: <strong>{state.providers.find(p => p.active)?.name}</strong>
            {state.updatedAt && (
              <>
                {' '}
                · alterada em {new Date(state.updatedAt).toLocaleString('pt-BR')} por {state.updatedBy}
              </>
            )}
          </p>
          <div className="stack">
            {state.providers.map(p => (
              <div key={p.id} className="card pad" style={{ borderColor: p.active ? '#91b18b' : undefined }}>
                <div className="row between wrap gap8">
                  <div>
                    <strong>{p.name}</strong>{' '}
                    {p.active && <span className="pill green">Ativa</span>}{' '}
                    <span className={`pill ${p.configured ? '' : 'amber'}`}>{p.configured ? 'Chave configurada' : 'Sem chave no servidor'}</span>
                  </div>
                  <div className="row gap8">
                    <button
                      className="btn compact"
                      disabled={!p.configured || busy !== ''}
                      onClick={async () => {
                        const r = await run(`test-${p.id}`, () => adminApi.test(p.id));
                        if (r) setTests(t => ({ ...t, [p.id]: r }));
                      }}
                    >
                      <Icon name="refresh" className="sm" /> {busy === `test-${p.id}` ? 'Testando...' : 'Testar conexão'}
                    </button>
                    <button
                      className="btn primary compact"
                      disabled={p.active || !p.configured || busy !== ''}
                      onClick={() => run(`active-${p.id}`, () => adminApi.setActive(p.id), `${p.name} agora é a IA ativa.`)}
                    >
                      <Icon name="check" className="sm" /> Usar esta IA
                    </button>
                  </div>
                </div>
                <div className="row gap8 wrap mt16" style={{ alignItems: 'center' }}>
                  <label className="small muted" htmlFor={`model-${p.id}`}>
                    Modelo
                  </label>
                  <input
                    id={`model-${p.id}`}
                    value={models[p.id] ?? ''}
                    maxLength={100}
                    onChange={e => setModels(m => ({ ...m, [p.id]: e.target.value }))}
                    style={{ width: 260, padding: 6 }}
                  />
                  <button
                    className="btn compact ghost"
                    disabled={busy !== '' || !models[p.id] || models[p.id] === p.model}
                    onClick={() => run(`model-${p.id}`, () => adminApi.setModel(p.id, models[p.id].trim()), `Modelo de ${p.name} atualizado.`)}
                  >
                    Salvar modelo
                  </button>
                  <span className="tiny muted">padrão: {p.defaultModel}</span>
                </div>
                {tests[p.id] && (
                  <p className="tiny mt16" style={{ color: tests[p.id].ok ? '#376346' : '#a6452f' }}>
                    {tests[p.id].ok
                      ? `Conexão ok em ${tests[p.id].latencyMs} ms${tests[p.id].sources ? ` · ${tests[p.id].sources} fonte(s)` : ''}.`
                      : `Falhou em ${tests[p.id].latencyMs} ms: ${tests[p.id].error}`}
                  </p>
                )}
              </div>
            ))}
          </div>
        </>
      )}
    </AdminShell>
  );
}
