// Painel "Publicar" do editor: publicar snapshot, mudar visibilidade, nova revisão, links e revogação.
import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { useToast } from '../../contexts/ToastContext';
import { VISIBILITY, absoluteUrl, publishingApi } from './publishingApi';

export function PublishPanel({ noteId, noteTitle, isDirty, disabled, onClose }) {
  const [items, setItems] = useState(null);
  const [visibility, setVisibility] = useState('unlisted');
  const [slug, setSlug] = useState('');
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();

  const load = useCallback(async () => {
    try {
      setItems((await publishingApi.forNote(noteId)) || []);
    } catch {
      setItems([]);
    }
  }, [noteId]);

  useEffect(() => {
    load();
    const onKey = e => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [load, onClose]);

  const run = async (fn, okMsg) => {
    setBusy(true);
    try {
      await fn();
      if (okMsg) addToast(okMsg);
      await load();
    } catch (err) {
      addToast(err.message || 'Não foi possível concluir.', 'warning');
      if (err.status === 409) await load();
    } finally {
      setBusy(false);
    }
  };

  const copy = async url => {
    try {
      await navigator.clipboard.writeText(url);
      addToast('Link copiado.');
    } catch {
      addToast(url, 'info');
    }
  };

  const active = (items || []).filter(p => p.status === 'Published');
  const revoked = (items || []).filter(p => p.status !== 'Published');

  return (
    <div className="modal-overlay" onMouseDown={e => e.target === e.currentTarget && onClose()}>
      <section className="modal wide" role="dialog" aria-modal="true" aria-label="Publicar nota">
        <div className="modal-title">
          <h2>Publicar “{noteTitle}”</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Fechar">
            <Icon name="close" />
          </button>
        </div>

        <div className="callout neutral mb16">
          <Icon name="info" className="sm" />
          <span>
            Publicar cria uma <strong>cópia congelada</strong> da nota salva. Edições posteriores só aparecem quando você publicar uma nova
            versão. Dúvidas, respostas de IA e referências não são publicadas.
          </span>
        </div>

        {items === null ? (
          <p className="small muted">A carregar...</p>
        ) : (
          active.map(p => (
            <div key={p.id} className="card pad mb16">
              <div className="row between wrap gap8">
                <div>
                  <strong>/{p.slug}</strong>
                  <div className="tiny muted">
                    Publicada a v{p.publishedNoteVersion} em {new Date(p.publishedAt).toLocaleString('pt-BR')}
                    {p.publishedNoteVersion < p.currentNoteVersion && ' · há alterações não publicadas'}
                  </div>
                </div>
                <select
                  aria-label="Visibilidade"
                  value={p.visibility}
                  disabled={busy}
                  style={{ width: 'auto' }}
                  onChange={e => run(() => publishingApi.setVisibility(p.id, e.target.value, p.version), 'Visibilidade atualizada.')}
                >
                  {Object.entries(VISIBILITY).map(([v, info]) => (
                    <option key={v} value={v}>
                      {info.label}
                    </option>
                  ))}
                </select>
              </div>
              <p className="tiny muted mt16">{VISIBILITY[p.visibility]?.help}</p>
              <div className="row gap8 wrap mt16">
                {p.publicPath && (
                  <button className="btn compact" onClick={() => copy(absoluteUrl(p.publicPath))}>
                    <Icon name="globe" className="sm" /> Copiar link público
                  </button>
                )}
                {p.sharedPath && (
                  <button className="btn compact" onClick={() => copy(absoluteUrl(p.sharedPath))}>
                    <Icon name="link" className="sm" /> Copiar link secreto
                  </button>
                )}
                {p.sharedPath && (
                  <button className="btn compact ghost" disabled={busy} onClick={() => run(() => publishingApi.rotateToken(p.id), 'Novo link secreto gerado; o anterior parou de funcionar.')}>
                    <Icon name="refresh" className="sm" /> Gerar novo link
                  </button>
                )}
                <button
                  className="btn compact"
                  disabled={busy || isDirty || disabled || p.publishedNoteVersion === p.currentNoteVersion}
                  title={isDirty ? 'Salve a nota antes' : undefined}
                  onClick={() => run(() => publishingApi.republish(p.id), 'Nova versão publicada.')}
                >
                  <Icon name="spark" className="sm" /> Publicar versão atual
                </button>
                <button
                  className="btn compact danger"
                  disabled={busy}
                  onClick={() =>
                    window.confirm('Revogar tira a publicação do ar imediatamente. Continuar?') &&
                    run(() => publishingApi.revoke(p.id), 'Publicação revogada.')
                  }
                >
                  Revogar
                </button>
              </div>
            </div>
          ))
        )}

        <div className="card pad">
          <h3 className="mb16">{active.length ? 'Nova publicação' : 'Publicar esta nota'}</h3>
          <div className="row gap8 wrap" style={{ alignItems: 'flex-end' }}>
            <div className="field" style={{ marginBottom: 0 }}>
              <label htmlFor="pub-visibility">Visibilidade</label>
              <select id="pub-visibility" value={visibility} onChange={e => setVisibility(e.target.value)} style={{ width: 'auto' }}>
                {Object.entries(VISIBILITY).map(([v, info]) => (
                  <option key={v} value={v}>
                    {info.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field grow" style={{ marginBottom: 0 }}>
              <label htmlFor="pub-slug">Endereço (opcional)</label>
              <input id="pub-slug" maxLength={80} placeholder="gerado a partir do título" value={slug} onChange={e => setSlug(e.target.value)} />
            </div>
            <button
              className="btn primary"
              disabled={busy || isDirty || disabled}
              onClick={() => run(() => publishingApi.publish(noteId, visibility, slug.trim()), 'Nota publicada.')}
            >
              Publicar
            </button>
          </div>
          {isDirty && <p className="tiny muted mt16">Salve a nota antes de publicar: só a versão salva é publicada.</p>}
          {visibility === 'public' && (
            <p className="tiny muted mt16">
              Publicações públicas usam o seu identificador. Configure em <Link to="/settings">Configurações → Conta</Link>.
            </p>
          )}
        </div>

        {revoked.length > 0 && <p className="tiny muted mt16">{revoked.length} publicação(ões) revogada(s) no histórico.</p>}

        <div className="modal-actions">
          <Link className="btn ghost" to="/publications">
            Minhas publicações
          </Link>
          <span className="grow" />
          <button className="btn" onClick={onClose}>
            Fechar
          </button>
        </div>
      </section>
    </div>
  );
}
