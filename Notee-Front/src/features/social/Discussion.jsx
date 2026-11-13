import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../../api';
import { locateQuote } from '../notes/anchors';
import { useToast } from '../../contexts/ToastContext';

export function Discussion({ publicationId, blocks = [], commentVersion = 0, commentsEnabled = true }) {
  const [open, setOpen] = useState(() => window.location.hash.startsWith('#comment-'));
  const [items, setItems] = useState([]);
  const [view, setView] = useState('top');
  const [page, setPage] = useState(1);
  const [text, setText] = useState('');
  const [reply, setReply] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const { addToast } = useToast();
  const load = useCallback(async (nextView = view, nextPage = page) => {
    try { setItems(await apiFetch(`/discussion/${publicationId}?view=${nextView}&page=${nextPage}`) || []); setError(''); }
    catch (err) { setError(err.message || 'Não foi possível carregar a conversa.'); }
  }, [publicationId, view, page]);
  useEffect(() => { if (open) load(); }, [open, load, commentVersion]);
  useEffect(() => { if (open && items.length && window.location.hash.startsWith('#comment-')) document.getElementById(window.location.hash.slice(1))?.scrollIntoView({ behavior: 'smooth' }); }, [open, items]);
  useEffect(() => { const onHash = () => { if (window.location.hash.startsWith('#comment-')) setOpen(true); }; window.addEventListener('hashchange', onHash); return () => window.removeEventListener('hashchange', onHash); }, []);
  const action = async (path, method = 'POST') => {
    setBusy(true);
    try { await apiFetch(path, { method }); await load(); }
    catch (err) { addToast(err.message || 'Não foi possível concluir.', 'warning'); }
    finally { setBusy(false); }
  };
  const send = async e => {
    e.preventDefault(); setBusy(true);
    try {
      await apiFetch(`/discussion/${publicationId}`, { method: 'POST', body: JSON.stringify({ text, parentId: reply?.id || null }) });
      setText(''); setReply(null); await load();
    } catch (err) { addToast(err.message || 'Não foi possível comentar.', 'warning'); }
    finally { setBusy(false); }
  };
  const roots = items.filter(c => !c.parentId);
  const anchored = roots.filter(c => c.anchorQuote && (!blocks.length || locateQuote(blocks, c)));
  const previous = roots.filter(c => c.anchorQuote && blocks.length && !locateQuote(blocks, c));
  const general = roots.filter(c => !c.anchorQuote);
  const renderComment = c => <div key={c.id} id={`comment-${c.id}`} className="social-comment">
    <div className="row between"><Link to={`/u/${c.handle}`} className="social-author">{c.displayName}</Link><span className="tiny muted">{new Date(c.createdAt).toLocaleDateString('pt-BR')}</span></div>
    {c.anchorQuote && !c.parentId && <blockquote className="quote-selection">{c.anchorQuote}</blockquote>}
    {c.hidden && <span className="pill">Oculto para outras pessoas</span>}
    <p>{c.isDeleted ? 'Comentário removido.' : c.text}</p>
    <div className="row gap8 wrap">
      {!c.isDeleted && <button type="button" className="btn compact ghost" disabled={busy || c.canDelete} aria-pressed={c.likedByMe} onClick={() => action(`/discussion/comments/${c.id}/like`, c.likedByMe ? 'DELETE' : 'PUT')}>♥ {c.likeCount}</button>}
      {!c.parentId && !c.isDeleted && !c.hidden && <button type="button" className="btn compact ghost" disabled={busy} onClick={() => setReply(c)}>Responder</button>}
      {c.canDelete && <button type="button" className="btn compact ghost" disabled={busy} onClick={() => action(`/discussion/${c.id}`, 'DELETE')}>Excluir</button>}
      {c.canHide && <button type="button" className="btn compact ghost" disabled={busy} onClick={() => action(`/discussion/comments/${c.id}/${c.hidden ? 'unhide' : 'hide'}`)}>{c.hidden ? 'Mostrar' : 'Ocultar'}</button>}
    </div>
    {c.moreCount > 0 && <button type="button" className="btn compact ghost" onClick={() => { setView('all'); setPage(1); }}>Ver mais {c.moreCount}</button>}
    {!c.parentId && <div className="social-replies">{items.filter(r => r.parentId === c.id).map(renderReply)}</div>}
  </div>;
  const renderReply = c => <div key={c.id} id={`comment-${c.id}`} className="social-comment"><Link to={`/u/${c.handle}`}>{c.displayName}</Link><p>{c.isDeleted ? 'Comentário removido.' : c.text}</p><div className="row gap8">{!c.isDeleted && <button type="button" className="btn compact ghost" disabled={busy || c.canDelete} onClick={() => action(`/discussion/comments/${c.id}/like`, c.likedByMe ? 'DELETE' : 'PUT')}>♥ {c.likeCount}</button>}{c.canDelete && <button type="button" className="btn compact ghost" onClick={() => action(`/discussion/${c.id}`, 'DELETE')}>Excluir</button>}</div></div>;
  return <div className="social-discussion">
    <button type="button" className="btn compact ghost social-discussion-toggle" aria-expanded={open} onClick={() => setOpen(!open)}>{open ? 'Fechar conversa' : 'Abrir conversa'}</button>
    {open && <div className="social-discussion-panel">
      {error && <p role="alert">{error} <button className="btn compact" onClick={() => load()}>Tentar novamente</button></p>}
      {!error && <>
        {!!anchored.length && <section><h3>Nos trechos</h3>{anchored.map(renderComment)}</section>}
        {!!previous.length && <section><h3>Comentários sobre versões anteriores</h3>{previous.map(renderComment)}</section>}
        <section><h3>Comentários gerais</h3>{general.length ? general.map(renderComment) : <p className="small muted">Nenhum comentário geral.</p>}</section>
        {view === 'top' && roots.length > 0 && <button type="button" className="btn compact" onClick={() => setView('all')}>Ver todos</button>}
        {view === 'all' && roots.length >= 50 && <button type="button" className="btn compact" onClick={() => setPage(p => p + 1)}>Próxima página</button>}
      </>}
      {commentsEnabled ? <form className="social-comment-form" onSubmit={send}>
        {reply && <div className="social-reply-banner">Respondendo a {reply.displayName} <button type="button" className="btn compact" onClick={() => setReply(null)}>Cancelar</button></div>}
        <label className="field">{reply ? 'Sua resposta' : 'Seu comentário'}<textarea value={text} onChange={e => setText(e.target.value)} maxLength={2000} required /></label>
        <button className="btn primary compact" disabled={busy || !text.trim()}>{reply ? 'Responder' : 'Comentar'}</button>
      </form> : <p className="small muted">Comentários desligados nesta publicação.</p>}
    </div>}
  </div>;
}
