import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../../api';
import { useToast } from '../../contexts/ToastContext';
import { Discussion } from './Communities';
export function ReadingActions({ handle, slug }) {
  const [state, setState] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  useEffect(() => { let alive = true; setState(null); setError(''); apiFetch('/social/reading/' + encodeURIComponent(handle) + '/' + encodeURIComponent(slug), { method: 'POST' }).then(s => { if (alive) setState(s); }).catch(e => { if (alive) setError(e.status === 401 ? 'login' : e.message); }); return () => { alive = false; }; }, [handle, slug]);
  const react = async kind => { setBusy(true); const active = kind === 'like' ? state.liked : state.reposted; try { await apiFetch('/social/publications/' + state.id + '/' + kind, { method: active ? 'DELETE' : 'PUT' }); setState(s => ({ ...s, [kind === 'like' ? 'liked' : 'reposted']: !active, [kind === 'like' ? 'likes' : 'reposts']: s[kind === 'like' ? 'likes' : 'reposts'] + (active ? -1 : 1) })); } catch (e) { addToast(e.message, 'warning'); } finally { setBusy(false); } };
  if (error === 'login') return <p className="mt16"><Link to="/login">Entre para avaliar, repostar e conversar sobre esta nota.</Link></p>;
  if (error) return <p role="alert">{error}</p>;
  if (!state) return null;
  return <section className="card pad mt16" aria-label="Interacoes da publicacao"><div className="row gap8 wrap"><span className="small muted">{state.views} leituras</span><button className="btn compact" disabled={busy || state.isOwner} aria-pressed={state.liked} onClick={() => react('like')}>Util / {state.likes}</button><button className="btn compact" disabled={busy} aria-pressed={state.reposted} onClick={() => react('repost')}>Repostar / {state.reposts}</button></div><Discussion publicationId={state.id} /></section>;
}
