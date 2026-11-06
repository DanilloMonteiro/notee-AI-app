// src/components/NoteCard.jsx
import React from 'react';
import { Link } from 'react-router-dom';
import { Icon } from './Icon';

export function NoteCard({ note }) {
  // A API C# devolve o status, titulo e tags. O React lida com as datas e a interface.
  const formattedDate = new Date().toLocaleDateString('pt-PT', { day: '2-digit', month: 'short' });
  const tags = note.tags || [];

  return (
    // No React Router usamos <Link> em vez de <a> para navegar sem recarregar a página inteira
    <Link className="card note-card" to={`/note/${note.id}`}>
      <div className="row between">
        <div className="note-icon">
          <Icon name="note" />
        </div>
        <span className="tiny muted">{note.categoryName || 'Sem categoria'}</span>
      </div>

      <div>
        <h3>{note.title}</h3>
        <p className="note-preview mt16" style={{ marginTop: '8px' }}>
          {note.status === 'Active' ? 'Ideia em desenvolvimento...' : 'Nota guardada.'}
        </p>
      </div>

      <div className="row gap8 wrap">
        {tags.slice(0, 3).map(tag => (
          <span key={tag.id} className="tag">
            {tag.name}
          </span>
        ))}
      </div>

      <div className="note-card-footer">
        <span>{formattedDate}</span>
        <span className="row gap8">
          <Icon name="lock" className="sm" /> Privada
        </span>
      </div>
    </Link>
  );
}
