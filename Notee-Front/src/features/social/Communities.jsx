import React, { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '../../api';
import { useToast } from '../../contexts/ToastContext';

export function CommunitiesPage() {
  const [items, setItems] = useState([]);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const { addToast } = useToast();
  const load = () => apiFetch('/communities').then(setItems).catch(e => setError(e.message));
  useEffect(() => { load(); }, []);
  const create = async e => {
    e.preventDefault(); setBusy(true);
    try { await apiFetch('/communities', { method: 'POST', body: JSON.stringify({ name, description }) }); setName(''); setDescription(''); await load(); }
    catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); }
  };
  return <><div className="overline">CONHECIMENTO EM COMUM</div><h1>Comunidades</h1><p className="muted mb24">Encontre notas publicadas por assunto e participe das conversas.</p>
    <details className="card pad mb24"><summary>Criar comunidade</summary><form className="stack mt16" onSubmit={create}><label>Nome<input value={name} onChange={e => setName(e.target.value)} required minLength={2} maxLength={80} /></label><label>Sobre a comunidade<textarea value={description} onChange={e => setDescription(e.target.value)} maxLength={500} /></label><button className="btn primary" disabled={busy}>Criar comunidade</button></form></details>
    {error && <p role="alert">{error}</p>}<div className="stack">{items.map(c => <Link className="card pad" key={c.id} to={'/communities/' + c.id}><h2>{c.name}</h2><p>{c.description}</p><span className="tiny muted">{c.members} participantes</span></Link>)}{!items.length && !error && <p>Nenhuma comunidade ainda.</p>}</div></>;
}

export function CommunityPage() {
  const { id } = useParams();
  const [community, setCommunity] = useState(null);
  const [notes, setNotes] = useState([]);
  const [publications, setPublications] = useState([]);
  const [selected, setSelected] = useState('');
  const [sort, setSort] = useState('recent');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const { addToast } = useToast();
  useEffect(() => { let alive = true; Promise.all([apiFetch('/communities/' + id), apiFetch('/publications')]).then(([c, p]) => { if (alive) { setCommunity(c); setPublications(p.filter(x => x.visibility === 'public' && x.status === 'Published')); } }).catch(e => { if (alive) setError(e.message); }); return () => { alive = false; }; }, [id]);
  useEffect(() => { let alive = true; apiFetch('/communities/' + id + '/notes?sort=' + sort).then(n => { if (alive) setNotes(n); }).catch(e => { if (alive) setError(e.message); }); return () => { alive = false; }; }, [id, sort]);
  const run = async fn => { setBusy(true); try { await fn(); } catch (e) { addToast(e.message, 'warning'); } finally { setBusy(false); } };
  if (error) return <p role="alert">{error}</p>;
  if (!community) return <p role="status">Carregando comunidade...</p>;
  return <><Link to="/communities">Comunidades</Link><h1>{community.name}</h1><p className="muted mb24">{community.description}</p>
    <button className="btn mb24" disabled={busy || community.isOwner} onClick={() => run(async () => { await apiFetch('/communities/' + id + '/membership', { method: community.joined ? 'DELETE' : 'PUT' }); setCommunity(c => ({ ...c, joined: !c.joined })); })}>{community.joined ? 'Participando' : 'Participar'}</button>
    {community.joined && <form className="card pad row gap8 wrap mb24" onSubmit={e => { e.preventDefault(); run(async () => { await apiFetch('/communities/' + id + '/publications/' + selected, { method: 'PUT' }); setNotes(await apiFetch('/communities/' + id + '/notes?sort=' + sort)); setSelected(''); }); }}><label htmlFor="community-publication">Compartilhe uma nota sua</label><select id="community-publication" value={selected} onChange={e => setSelected(e.target.value)}><option value="">Selecione uma publicacao publica</option>{publications.map(p => <option key={p.id} value={p.id}>{p.noteTitle}</option>)}</select><button className="btn primary" disabled={busy || !selected}>Compartilhar</button></form>}
    <label>Ordenar notas<select value={sort} onChange={e => setSort(e.target.value)}><option value="recent">Mais recentes</option><option value="likes">Mais uteis</option><option value="reposts">Mais repostadas</option><option value="combined">Curtidas + reposts</option></select></label>
    <div className="stack mt16">{!notes.length && <p>Nenhuma nota publicada nesta comunidade.</p>}{notes.map(n => <article className="card pad" key={n.id}><Link to={'/u/' + n.handle}>{n.displayName}</Link><h2><Link to={'/p/' + n.handle + '/' + n.slug}>{n.title}</Link></h2><p className="tiny muted">{n.likes} curtidas / {n.reposts} reposts</p><Discussion publicationId={n.id} />{community.isOwner && <button className="btn compact mt16" disabled={busy} onClick={() => run(async () => { await apiFetch('/communities/' + id + '/publications/' + n.id, { method: 'DELETE' }); setNotes(prev => prev.filter(x => x.id !== n.id)); })}>Remover da comunidade</button>}</article>)}</div></>;
}

function Discussion({ publicationId }) {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([]);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const { addToast } = useToast();
  useEffect(() => { if (!open) return; let alive = true; apiFetch('/discussion/' + publicationId).then(rows => { if (alive) setItems(rows); }).catch(e => { if (alive) setError(e.message); }); return () => { alive = false; }; }, [open, publicationId]);
  const send = async e => { e.preventDefault(); setBusy(true); try { await apiFetch('/discussion/' + publicationId, { method: 'POST', body: JSON.stringify({ text }) }); setText(''); setItems(await apiFetch('/discussion/' + publicationId)); } catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); } };
  return <div className="mt16"><button className="btn compact" aria-expanded={open} onClick={() => setOpen(!open)}>Conversa</button>{open && <div className="stack mt16">{error && <p role="alert">{error}</p>}{items.map(c => <div key={c.id}><Link to={'/u/' + c.handle}>{c.displayName}</Link><p style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{c.text}</p></div>)}<form className="stack" onSubmit={send}><label>Seu comentario<textarea value={text} onChange={e => setText(e.target.value)} maxLength={2000} required /></label><button className="btn compact" disabled={busy || !text.trim()}>Comentar</button></form></div>}</div>;
}
