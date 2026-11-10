import React from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { UserAvatar } from '../../components/UserAvatar';
import './social.css';

export function SocialHeading({ eyebrow, title, description, children }) {
  return <div className="page-heading social-heading"><div><div className="overline">{eyebrow}</div><h1>{title}</h1><p>{description}</p></div>{children && <div className="row gap8 wrap">{children}</div>}</div>;
}

export function SocialState({ loading, error, title = 'Nada por aqui ainda', description, children, retry }) {
  if (loading) return <div className="social-skeleton" role="status" aria-label="Carregando"><span className="small muted">Carregando…</span>{[0, 1, 2].map(i => <div className="card social-skeleton-card" key={i}><span /><span /><span /></div>)}</div>;
  if (error) return <div className="callout warning social-error" role="alert"><Icon name="warning" /><div className="grow"><strong>Não foi possível carregar</strong><p>{error}</p></div>{retry && <button className="btn compact" onClick={retry}>Tentar novamente</button>}</div>;
  return <div className="empty"><Icon name="book" /><h3>{title}</h3><p>{description}</p>{children}</div>;
}

export function PublicationCard({ note, actions, children, period = false }) {
  return <article className="card social-publication"><div className="social-publication-body"><Link className="social-author" to={'/u/' + note.handle}><UserAvatar name={note.displayName} /><span><strong>{note.displayName}</strong><span className="tiny muted">@{note.handle}</span></span></Link><h2><Link to={'/p/' + note.handle + '/' + note.slug}>{note.title}</Link></h2><div className="social-metrics"><span><Icon name="check" className="sm" />{note.likes ?? 0} úteis</span><span><Icon name="refresh" className="sm" />{note.reposts ?? 0} reposts</span>{note.views != null && <span><Icon name="eye" className="sm" />{note.views} leituras</span>}{period && <span className="tiny muted">no período</span>}</div></div>{actions && <div className="social-card-actions">{actions}</div>}{children && <div className="social-card-detail">{children}</div>}</article>;
}

export function SocialPagination({ page, loading, hasNext, onChange }) {
  return <nav className="social-pagination" aria-label="Paginação"><button className="btn compact" disabled={loading || page === 1} onClick={() => onChange(page - 1)}><Icon name="back" className="sm" />Anterior</button><span className="small muted" aria-live="polite">Página {page}</span><button className="btn compact" disabled={loading || !hasNext} onClick={() => onChange(page + 1)}>Próxima<Icon name="arrow" className="sm" /></button></nav>;
}
