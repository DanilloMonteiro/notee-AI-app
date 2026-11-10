import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../../api';
import { useToast } from '../../contexts/ToastContext';

export function FollowButton({ handle }) {
  const [profile, setProfile] = useState(null);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  useEffect(() => {
    let alive = true;
    apiFetch('/social/profiles/' + encodeURIComponent(handle)).then(p => { if (alive) setProfile(p); }).catch(() => {});
    return () => { alive = false; };
  }, [handle]);
  if (!profile) return <Link to="/login" className="small">Entre para seguir este perfil</Link>;
  const toggle = async () => {
    setBusy(true);
    try {
      await apiFetch('/social/following/' + encodeURIComponent(handle), { method: profile.following ? 'DELETE' : 'PUT' });
      setProfile(p => ({ ...p, following: !p.following, followers: p.followers + (p.following ? -1 : 1) }));
    } catch (err) { addToast(err.message, 'warning'); }
    finally { setBusy(false); }
  };
  return <div className="row gap8 mb24"><span className="small muted">{profile.followers} seguidores / seguindo {profile.follows}</span>{!profile.isMe && <button className="btn compact" disabled={busy} onClick={toggle}>{profile.following ? 'Deixar de seguir' : 'Seguir'}</button>}</div>;
}

export function FeedPage() {
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  useEffect(() => {
    let alive = true;
    setLoading(true);
    apiFetch('/social/feed?page=' + page).then(rows => { if (alive) { setItems(rows); setError(''); } }).catch(err => { if (alive) setError(err.message); }).finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
  }, [page]);
  const react = async (item, kind) => {
    setBusy(true);
    const active = kind === 'like' ? item.liked : item.reposted;
    try {
      await apiFetch('/social/publications/' + item.id + '/' + kind, { method: active ? 'DELETE' : 'PUT' });
      setItems(prev => prev.map(n => n.id !== item.id ? n : { ...n, [kind === 'like' ? 'liked' : 'reposted']: !active, [kind === 'like' ? 'likes' : 'reposts']: n[kind === 'like' ? 'likes' : 'reposts'] + (active ? -1 : 1) }));
    } catch (err) { addToast(err.message, 'warning'); }
    finally { setBusy(false); }
  };
  return <><div className="overline">PESSOAS QUE VOCÊ SEGUE</div><h1>Seu feed</h1><p className="muted mb24">Publicações e reposts públicos das pessoas que você acompanha.</p><Link to="/library" className="btn mb24">Explorar autores na biblioteca</Link>
    {loading ? <p role="status">Carregando publicações…</p> : error ? <p role="alert">{error}</p> : <div className="stack">{!items.length && <div className="card pad">Nenhuma publicação nesta página. Siga autores pela biblioteca para acompanhar suas notas.</div>}{items.map(n => <article className="card pad" key={n.id}><Link className="small muted" to={'/u/' + n.handle}>{n.displayName} / @{n.handle}</Link><h2><Link to={'/p/' + n.handle + '/' + n.slug}>{n.title}</Link></h2><div className="row gap8 mt16"><button className="btn compact" aria-pressed={n.liked} disabled={busy} onClick={() => react(n, 'like')}>Útil / {n.likes}</button><button className="btn compact" aria-pressed={n.reposted} disabled={busy} onClick={() => react(n, 'repost')}>Repostar / {n.reposts}</button></div></article>)}</div>}
    <div className="row gap8 mt16"><button className="btn" disabled={loading || page === 1} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Página {page}</span><button className="btn" disabled={loading || items.length < 20} onClick={() => setPage(p => p + 1)}>Próxima</button></div></>;
}
