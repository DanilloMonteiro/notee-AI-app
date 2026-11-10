import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../../api';
import { useToast } from '../../contexts/ToastContext';
import { Icon } from '../../components/Icon';
import { SocialState } from './SocialUI';
import { Discussion } from './Communities';
export function ReadingActions({ handle, slug }) {
  const [state, setState] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  useEffect(() => { let alive = true; setState(null); setError(''); apiFetch('/social/reading/' + encodeURIComponent(handle) + '/' + encodeURIComponent(slug), { method: 'POST' }).then(s => { if (alive) setState(s); }).catch(e => { if (alive) setError(e.status === 401 ? 'login' : e.message); }); return () => { alive = false; }; }, [handle, slug]);
  const react = async kind => { setBusy(true); const active = kind === 'like' ? state.liked : state.reposted; try { await apiFetch('/social/publications/' + state.id + '/' + kind, { method: active ? 'DELETE' : 'PUT' }); setState(s => ({ ...s, [kind === 'like' ? 'liked' : 'reposted']: !active, [kind === 'like' ? 'likes' : 'reposts']: s[kind === 'like' ? 'likes' : 'reposts'] + (active ? -1 : 1) })); } catch (e) { addToast(e.message, 'warning'); } finally { setBusy(false); } };
  if (error === 'login') return <section className="card social-reading"><div className="social-reading-header"><div><strong>A conversa continua aqui</strong><p className="social-hint">Entre para avaliar, repostar e compartilhar suas ideias sobre esta nota.</p></div><Link className="btn primary compact" to="/login">Entrar<Icon name="arrow" className="sm" /></Link></div></section>;
  if (error) return <SocialState error={error} />;
  if (!state) return null;
  return <section className="card social-reading" aria-label="Interações da publicação"><div className="social-reading-header"><span className="social-metrics"><span><Icon name="eye" className="sm" />{state.views} leituras</span></span><div className="row gap8 wrap"><button className="btn compact" disabled={busy || state.isOwner} aria-pressed={state.liked} title={state.isOwner ? 'Você não pode avaliar a própria nota' : undefined} onClick={() => react('like')}><Icon name="check" className="sm" />{state.liked ? 'Marcada como útil' : 'Útil'} · {state.likes}</button><button className="btn compact" disabled={busy} aria-pressed={state.reposted} onClick={() => react('repost')}><Icon name="refresh" className="sm" />{state.reposted ? 'Repostada' : 'Repostar'} · {state.reposts}</button></div></div><Discussion publicationId={state.id} /></section>;
}
