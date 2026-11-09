// Histórico de revisões da nota (M06): listar, visualizar e restaurar como nova versão.
import React, { useEffect, useState } from 'react';
import { Icon } from './Icon';
import { apiFetch } from '../api';
import { noteExcerpt } from '../notes';

const REASON = { Created: 'Criação', Edited: 'Edição', Restored: 'Restauração', Baseline: 'Versão anterior' };

export function RevisionHistory({ noteId, currentVersion, isDirty, disabled, onRestored, onClose }) {
  const [revisions, setRevisions] = useState(null);
  const [selected, setSelected] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let alive = true;
    apiFetch(`/notes/${noteId}/revisions`)
      .then(list => alive && setRevisions(list || []))
      .catch(err => alive && setError(err.message || 'Não foi possível carregar o histórico.'));
    const onKey = e => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => {
      alive = false;
      document.removeEventListener('keydown', onKey);
    };
  }, [noteId, onClose]);

  const open = async revision => {
    try {
      setSelected(await apiFetch(`/notes/${noteId}/revisions/${revision.id}`));
    } catch (err) {
      setError(err.message || 'Não foi possível abrir a revisão.');
    }
  };

  const restore = async () => {
    setBusy(true);
    try {
      const note = await apiFetch(`/notes/${noteId}/restore-revision`, {
        method: 'POST',
        body: JSON.stringify({ revisionId: selected.id, expectedVersion: currentVersion }),
      });
      onRestored(note, selected.version);
    } catch (err) {
      setError(err.status === 409 ? 'A nota mudou desde que você abriu o histórico. Recarregue a página.' : err.message);
      setBusy(false);
    }
  };

  return (
    <div className="modal-overlay" onMouseDown={e => e.target === e.currentTarget && onClose()}>
      <section className="modal wide" role="dialog" aria-modal="true" aria-label="Histórico da nota">
        <div className="modal-title">
          <h2>Histórico da nota</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Fechar">
            <Icon name="close" />
          </button>
        </div>

        {error && (
          <div className="callout warning mb16">
            <Icon name="warning" className="sm" />
            <span>{error}</span>
          </div>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: '240px 1fr', gap: 18, minHeight: 320 }}>
          <div style={{ borderRight: '1px solid var(--line)', paddingRight: 12, overflow: 'auto', maxHeight: '60vh' }}>
            {revisions === null ? (
              <p className="small muted">A carregar...</p>
            ) : revisions.length === 0 ? (
              <p className="small muted">Ainda não há revisões. Elas são criadas a cada salvamento.</p>
            ) : (
              revisions.map(r => (
                <button
                  key={r.id}
                  type="button"
                  className={`graph-list-question ${selected?.id === r.id ? 'selected' : ''}`}
                  style={{ width: '100%', textAlign: 'left', fontWeight: selected?.id === r.id ? 600 : 400 }}
                  onClick={() => open(r)}
                >
                  <Icon name="clock" className="sm" />
                  <span>
                    v{r.version} · {REASON[r.reason] || r.reason}
                    {r.restoredFromVersion ? ` da v${r.restoredFromVersion}` : ''}
                    <br />
                    <span className="tiny muted">{new Date(r.createdAt).toLocaleString('pt-BR')}</span>
                  </span>
                </button>
              ))
            )}
          </div>

          <div>
            {selected ? (
              <>
                <div className="overline">VERSÃO {selected.version}</div>
                <h3 className="mb16">{selected.title}</h3>
                <p className="small muted mb16">
                  {selected.categoryName || 'Sem categoria'}
                  {selected.tags.length ? ` · ${selected.tags.map(t => t.name).join(', ')}` : ''}
                </p>
                <p style={{ whiteSpace: 'pre-wrap', lineHeight: 1.6 }}>{noteExcerpt(selected.content, 4000) || 'Sem texto.'}</p>
              </>
            ) : (
              <p className="small muted">Selecione uma versão para visualizar. Restaurar cria uma nova versão; nada do histórico é apagado.</p>
            )}
          </div>
        </div>

        <div className="modal-actions">
          {isDirty && <span className="tiny muted">Salve ou descarte suas alterações antes de restaurar.</span>}
          <span className="grow" />
          <button type="button" className="btn" onClick={onClose}>
            Fechar
          </button>
          <button
            type="button"
            className="btn primary"
            disabled={!selected || busy || isDirty || disabled || selected.version === currentVersion}
            onClick={restore}
          >
            <Icon name="refresh" className="sm" /> {busy ? 'Restaurando...' : 'Restaurar esta versão'}
          </button>
        </div>
      </section>
    </div>
  );
}
