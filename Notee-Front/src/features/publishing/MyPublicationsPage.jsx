// Lista das publicações do autor, com links e estado.
import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { VISIBILITY, publishingApi } from './publishingApi';

export function MyPublicationsPage() {
  const [items, setItems] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    publishingApi
      .mine()
      .then(list => setItems(list || []))
      .catch(err => {
        setItems([]);
        setError(err.message || 'Não foi possível carregar.');
      });
  }, []);

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">PUBLICAÇÕES</div>
          <h1>O que você escolheu compartilhar.</h1>
          <p>Cada publicação é uma cópia revisada da nota. Revogue quando quiser.</p>
        </div>
        <div className="row gap8">
          <Link className="btn" to="/library">
            <Icon name="globe" className="sm" /> Biblioteca pública
          </Link>
        </div>
      </div>

      {error && (
        <div className="callout warning mb24">
          <Icon name="warning" />
          <span>{error}</span>
        </div>
      )}

      {items === null ? (
        <div className="empty">
          <h3>A carregar...</h3>
        </div>
      ) : items.length === 0 ? (
        <div className="empty">
          <Icon name="globe" />
          <h3>Nada publicado ainda</h3>
          <p>Abra uma nota e use “Publicar” para criar uma cópia para leitura.</p>
        </div>
      ) : (
        <div className="stack">
          {items.map(p => (
            <div key={p.id} className="card pad row between wrap gap8">
              <div>
                <strong>{p.noteTitle}</strong>
                <div className="tiny muted">
                  /{p.slug} · {VISIBILITY[p.visibility]?.label} · {p.status === 'Published' ? `v${p.publishedNoteVersion}` : 'Revogada'}
                </div>
              </div>
              <div className="row gap8">
                {p.publicPath && (
                  <Link className="btn compact" to={p.publicPath}>
                    Ver página
                  </Link>
                )}
                {!p.publicPath && p.sharedPath && (
                  <Link className="btn compact" to={p.sharedPath}>
                    Abrir link secreto
                  </Link>
                )}
                <Link className="btn compact ghost" to={`/note/${p.noteId}`}>
                  Nota
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
