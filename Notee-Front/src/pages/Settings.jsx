// src/pages/Settings.jsx
// Preferências da conta salvas na API (não no navegador): pesquisa e consentimento para IA externa.
import React, { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { UserAvatar } from '../components/UserAvatar';
import { apiFetch } from '../api';
import { useToast } from '../contexts/ToastContext';
import { useCurrentUser } from '../contexts/CurrentUserContext';
import { publishingApi } from '../features/publishing/publishingApi';

const TABS = ['research', 'account'];

/** Sugestão de identificador a partir do nome: "Ana Souza" -> "ana_souza". */
function handleFromName(name) {
  const slug = (name || '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 30);
  return slug.length >= 3 ? slug : 'seu_nome';
}

const consentText = providerName =>
  'Autorizo o Notee a enviar a pergunta da dúvida e o trecho selecionado da nota ao provedor externo de IA escolhido pela ' +
  `administração do Notee (atualmente ${providerName || 'não definido'}) para gerar uma resposta; o provedor pode buscar na web para indicar fontes. ` +
  'Outras notas, mapas e dados da conta não são enviados. ' +
  'Posso revogar a qualquer momento; a revogação vale para as próximas pesquisas.';

export function Settings() {
  // A aba fica na URL (?tab=account) para o card da conta na sidebar abrir direto em "Conta".
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = TABS.includes(searchParams.get('tab')) ? searchParams.get('tab') : 'research';
  const setActiveTab = tab => setSearchParams(tab === 'research' ? {} : { tab }, { replace: true });

  const { user: me } = useCurrentUser();
  const [prefs, setPrefs] = useState(null);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  // displayName null = ainda não editado: mostra o nome da conta como sugestão.
  const [profile, setProfile] = useState({ handle: '', displayName: null });
  const [profileSaved, setProfileSaved] = useState(null);
  const { addToast } = useToast();
  const publicName = profile.displayName ?? (me?.fullName || '');

  useEffect(() => {
    let alive = true;
    Promise.all([apiFetch('/me/preferences'), publishingApi.profile()])
      .then(([p, prof]) => {
        if (!alive) return;
        setPrefs(p);
        if (prof) {
          setProfile(prof);
          setProfileSaved(prof);
        }
      })
      .catch(err => alive && setError(err.message || 'Não foi possível carregar as preferências.'));
    return () => {
      alive = false;
    };
  }, []);

  const update = async changes => {
    setSaving(true);
    try {
      const next = await apiFetch('/me/preferences', {
        method: 'PATCH',
        body: JSON.stringify({ ...changes, expectedVersion: prefs.version }),
      });
      setPrefs(next);
      addToast('Preferências salvas.');
    } catch (err) {
      addToast(err.message || 'Não foi possível salvar.', 'warning');
      if (err.status === 409) setPrefs(await apiFetch('/me/preferences'));
    } finally {
      setSaving(false);
    }
  };

  const saveProfile = async e => {
    e.preventDefault();
    setSaving(true);
    try {
      const saved = await publishingApi.saveProfile(profile.handle.trim(), publicName.trim());
      setProfile(saved);
      setProfileSaved(saved);
      addToast('Perfil público salvo.');
    } catch (err) {
      addToast(err.message || 'Não foi possível salvar o perfil.', 'warning');
    } finally {
      setSaving(false);
    }
  };

  const toggleConsent = () => {
    if (prefs.externalAiConsent) {
      update({ externalAiConsent: false });
    } else if (window.confirm(`${consentText(prefs?.externalProviderName)}\n\nConfirmar autorização?`)) {
      update({ externalAiConsent: true, consentVersion: prefs.currentConsentVersion });
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">CONFIGURAÇÕES</div>
          <h1>Um assistente do seu jeito.</h1>
          <p>Configure a organização e a pesquisa sem perder o controle sobre suas notas.</p>
        </div>
      </div>

      <div className="tabbar">
        <button className={`tab ${activeTab === 'research' ? 'active' : ''}`} onClick={() => setActiveTab('research')}>
          Inteligência e pesquisas
        </button>
        <button className={`tab ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>
          Conta
        </button>
      </div>

      {error && (
        <div className="callout warning mb24">
          <Icon name="warning" />
          <span>{error}</span>
        </div>
      )}

      <div style={{ maxWidth: '900px' }}>
        {activeTab === 'research' && prefs && (
          <div className="card settings-section">
            <h2 className="mb24">O seu ritmo. As suas escolhas.</h2>

            <div className="settings-row">
              <div>
                <h4>Permitir pesquisas</h4>
                <p>Chave geral. Desligada, as dúvidas ficam pendentes e nenhuma resposta é gerada.</p>
              </div>
              <button
                className={`switch ${prefs.researchEnabled ? 'on' : ''}`}
                role="switch"
                aria-checked={prefs.researchEnabled}
                aria-label="Permitir pesquisas"
                disabled={saving}
                onClick={() => update({ researchEnabled: !prefs.researchEnabled })}
              />
            </div>

            <div className="settings-row">
              <div>
                <h4>Pesquisa automática</h4>
                <p>Pesquisar assim que uma dúvida é criada. Cada nota pode sobrepor esta escolha. Mudar aqui não pesquisa dúvidas antigas.</p>
              </div>
              <button
                className={`switch ${prefs.autoResearch ? 'on' : ''}`}
                role="switch"
                aria-checked={prefs.autoResearch}
                aria-label="Pesquisa automática"
                disabled={saving || !prefs.researchEnabled}
                onClick={() => update({ autoResearch: !prefs.autoResearch })}
              />
            </div>

            <div className="settings-row">
              <div>
                <h4>Usar IA externa</h4>
                <p>{consentText(prefs?.externalProviderName)}</p>
                {prefs.consentedAt && (
                  <p className="tiny muted">
                    Autorizado em {new Date(prefs.consentedAt).toLocaleString('pt-BR')} (termo v{prefs.consentVersion}).
                  </p>
                )}
                {!prefs.externalProviderConfigured && (
                  <p className="tiny muted">Nenhuma chave de provedor externo está configurada no servidor.</p>
                )}
              </div>
              <button
                className={`switch ${prefs.externalAiConsent ? 'on' : ''}`}
                role="switch"
                aria-checked={prefs.externalAiConsent}
                aria-label="Autorizar IA externa"
                disabled={saving}
                onClick={toggleConsent}
              />
            </div>

            <div className="settings-row">
              <div>
                <h4>Provedor em uso</h4>
                <p>
                  {prefs.effectiveProvider !== 'fake'
                    ? `As dúvidas são respondidas por ${prefs.externalProviderName}.`
                    : 'Respostas simuladas e determinísticas: nenhum dado sai do Notee e nenhuma fonte é inventada.'}
                </p>
              </div>
              <span className={`pill ${prefs.effectiveProvider !== 'fake' ? 'blue' : 'green'}`}>
                {prefs.effectiveProvider !== 'fake' ? `${prefs.externalProviderName} (externo)` : 'Simulado (local)'}
              </span>
            </div>
          </div>
        )}

        {activeTab === 'account' && (
          <div className="card settings-section">
            <h2 className="mb24">Sua conta</h2>
            <div className="row gap8 mb24">
              {/* A foto virá de me.avatarUrl quando o upload existir; até lá, iniciais. */}
              <UserAvatar name={me?.fullName || me?.email} imageUrl={me?.avatarUrl} className="big" />
              <div className="grow" style={{ marginLeft: 8 }}>
                <strong>{me?.fullName || 'Nome não informado'}</strong>
                <div className="small muted">{me?.email}</div>
              </div>
            </div>
            <div className="field">
              <label htmlFor="me-name">Nome</label>
              <input id="me-name" type="text" value={me?.fullName || ''} placeholder="Não informado" readOnly />
            </div>
            <div className="field">
              <label htmlFor="me-phone">Telefone</label>
              <input id="me-phone" type="tel" value={me?.phoneNumber || ''} placeholder="Não informado" readOnly />
            </div>
            <div className="field">
              <label htmlFor="me-email">E-mail</label>
              <input id="me-email" type="email" value={me?.email || ''} readOnly />
            </div>
            <p className="tiny muted mt16">Seus dados são privados e isolados por conta.</p>

            <h3 className="mt16 mb16" style={{ marginTop: 28 }}>
              Perfil público
            </h3>
            <p className="small muted mb16">
              Usado apenas nas publicações que você marcar como públicas. Notas, mapas e dúvidas continuam privados.
            </p>
            <form onSubmit={saveProfile}>
              <div className="field">
                <label htmlFor="profile-handle">Identificador (endereço)</label>
                <input
                  id="profile-handle"
                  type="text"
                  required
                  maxLength={30}
                  pattern="[a-z0-9_-]{3,30}"
                  autoComplete="off"
                  aria-describedby="profile-handle-hint"
                  placeholder={`ex.: ${handleFromName(me?.fullName)}`}
                  value={profile.handle}
                  onChange={e => setProfile({ ...profile, handle: e.target.value.toLowerCase() })}
                />
                <span id="profile-handle-hint" className="tiny muted">
                  3 a 30 caracteres: letras minúsculas, números, _ ou -.
                </span>
              </div>
              <div className="field">
                <label htmlFor="profile-name">Nome público</label>
                <input
                  id="profile-name"
                  type="text"
                  required
                  maxLength={60}
                  value={publicName}
                  onChange={e => setProfile({ ...profile, displayName: e.target.value })}
                />
              </div>
              <div className="row gap8">
                <button className="btn primary" type="submit" disabled={saving}>
                  Salvar perfil
                </button>
                {profileSaved && (
                  <a className="btn ghost" href={`/u/${profileSaved.handle}`}>
                    Ver perfil público
                  </a>
                )}
              </div>
            </form>
          </div>
        )}
      </div>
    </div>
  );
}
