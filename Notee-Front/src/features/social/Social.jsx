import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { SocialHeading, SocialState, PublicationCard, SocialPagination } from './SocialUI';
import { apiFetch } from '../../api';
import { useToast } from '../../contexts/ToastContext';

export function FollowButton({ handle }) {
  const [profile, setProfile] = useState(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState('');
  const [loadingProfile, setLoadingProfile] = useState(true);
  const { addToast } = useToast();
  useEffect(() => {
    let alive = true; setLoadingProfile(true); setFailure(''); setProfile(null);
    apiFetch('/social/profiles/' + encodeURIComponent(handle)).then(p => { if (alive) setProfile(p); }).catch(e => { if (alive) setFailure(e.status === 401 ? 'login' : e.message); }).finally(() => { if (alive) setLoadingProfile(false); });
    return () => { alive = false; };
  }, [handle]);
  if (loadingProfile) return <p className="small muted" role="status">Carregando perfil…</p>;
  if (failure === 'login') return <Link to="/login" className="btn compact">Entre para seguir este perfil</Link>;
  if (!profile) return <p className="callout warning" role="alert">{failure}</p>;
  const toggle = async () => {
    setBusy(true);
    try {
      await apiFetch('/social/following/' + encodeURIComponent(handle), { method: profile.following ? 'DELETE' : 'PUT' });
      setProfile(p => ({ ...p, following: !p.following, followers: p.followers + (p.following ? -1 : 1) }));
    } catch (err) { addToast(err.message, 'warning'); }
    finally { setBusy(false); }
  };
  return <div className="social-profile-actions"><span className="small muted">{profile.followers} seguidores / seguindo {profile.follows}</span>{!profile.isMe && <button className={profile.following ? "btn compact" : "btn primary compact"} disabled={busy} title={profile.following ? "Deixar de seguir" : "Seguir perfil"} onClick={toggle}><Icon name={profile.following ? 'check' : 'plus'} className="sm" />{busy ? 'Atualizando…' : profile.following ? 'Seguindo' : 'Seguir'}</button>}</div>;
}

export function FeedPage() {
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  useEffect(() => {
    let alive = true;
    setLoading(true);
    apiFetch('/social/feed?page=' + page).then(rows => { if (alive) { setItems(rows); setError(''); } }).catch(err => { if (alive) setError(err.message); }).finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
  }, [page, reload]);
  const react = async (item, kind) => {
    setBusy(true);
    const active = kind === 'like' ? item.liked : item.reposted;
    try {
      await apiFetch('/social/publications/' + item.id + '/' + kind, { method: active ? 'DELETE' : 'PUT' });
      setItems(prev => prev.map(n => n.id !== item.id ? n : { ...n, [kind === 'like' ? 'liked' : 'reposted']: !active, [kind === 'like' ? 'likes' : 'reposts']: n[kind === 'like' ? 'likes' : 'reposts'] + (active ? -1 : 1) }));
    } catch (err) { addToast(err.message, 'warning'); }
    finally { setBusy(false); }
  };
  return <div className="social-page">
    <SocialHeading eyebrow="PESSOAS QUE VOCÊ SEGUE" title="Seu feed" description="Publicações e reposts de quem inspira suas ideias."><Link to="/discover" className="btn"><Icon name="search" />Descobrir notas</Link></SocialHeading>
    {loading || error || !items.length ? <SocialState loading={loading} error={error} retry={() => setReload(r => r + 1)} title="Seu feed começa com uma conexão" description="Explore a biblioteca, encontre autores e siga os perfis que você quer acompanhar."><Link className="btn primary" to="/library">Explorar autores<Icon name="arrow" className="sm" /></Link></SocialState> : <div className="stack">{items.map(n => <PublicationCard note={n} key={n.id} actions={<><button className="btn compact" aria-pressed={n.liked} disabled={busy} onClick={() => react(n, 'like')}><Icon name="check" className="sm" />{n.liked ? 'Marcada como útil' : 'Marcar como útil'}</button><button className="btn compact" aria-pressed={n.reposted} disabled={busy} onClick={() => react(n, 'repost')}><Icon name="refresh" className="sm" />{n.reposted ? 'Repostada' : 'Repostar'}</button><Link className="btn compact ghost" to={'/p/' + n.handle + '/' + n.slug}>Ler nota<Icon name="arrow" className="sm" /></Link></>} />)}</div>}
    {!error && <SocialPagination page={page} loading={loading} hasNext={items.length === 20} onChange={setPage} />}
  </div>;
}
