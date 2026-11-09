// src/components/NoteCard.jsx
import React from 'react';
import { Link } from 'react-router-dom';
import { Icon } from './Icon';
import { STATUS_LABEL, formatShortDate, noteExcerpt } from '../notes';

/** Cartão de nota. `actions` (opcional) aparece abaixo do cartão, fora do link. */
export function NoteCard({ note, actions }) {
  const tags = note.tags || [];
  const excerpt = noteExcerpt(note.content);

  const card = (
    <Link className="card note-card" to={`/note/${note.id}`}>
      <div className="row between">
        <div className="note-icon">
          <Icon name="note" />
        </div>
        <span className="tiny muted">{note.categoryName || 'Sem categoria'}</span>
      </div>

      <div>
        <h3>{note.title}</h3>
        <p className="note-preview" style={{ marginTop: '8px' }}>
          {excerpt || 'Nota sem texto ainda.'}
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
        <span>
          {formatShortDate(note.updatedAt)}
          {note.status === 'Archived' || note.status === 'Trashed' ? ` · ${STATUS_LABEL[note.status]}` : ''}
        </span>
        <span className="row gap8">
          <Icon name="lock" className="sm" /> Privada
        </span>
      </div>
    </Link>
  );

  if (!actions) return card;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {card}
      <div className="row gap8 wrap">{actions}</div>
    </div>
  );
}
