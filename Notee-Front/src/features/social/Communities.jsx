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
    {community.isOwner && <MemberManagement communityId={id} />}
    {community.joined && <form className="card pad row gap8 wrap mb24" onSubmit={e => { e.preventDefault(); run(async () => { await apiFetch('/communities/' + id + '/publications/' + selected, { method: 'PUT' }); setNotes(await apiFetch('/communities/' + id + '/notes?sort=' + sort)); setSelected(''); }); }}><label htmlFor="community-publication">Compartilhe uma nota sua</label><select id="community-publication" value={selected} onChange={e => setSelected(e.target.value)}><option value="">Selecione uma publicacao publica</option>{publications.map(p => <option key={p.id} value={p.id}>{p.noteTitle}</option>)}</select><button className="btn primary" disabled={busy || !selected}>Compartilhar</button></form>}
    <label>Ordenar notas<select value={sort} onChange={e => setSort(e.target.value)}><option value="recent">Mais recentes</option><option value="likes">Mais uteis</option><option value="reposts">Mais repostadas</option><option value="combined">Curtidas + reposts</option></select></label>
    <div className="stack mt16">{!notes.length && <p>Nenhuma nota publicada nesta comunidade.</p>}{notes.map(n => <article className="card pad" key={n.id}><Link to={'/u/' + n.handle}>{n.displayName}</Link><h2><Link to={'/p/' + n.handle + '/' + n.slug}>{n.title}</Link></h2><p className="tiny muted">{n.likes} curtidas / {n.reposts} reposts</p><Discussion publicationId={n.id} />{community.canModerate && <button className="btn compact mt16" disabled={busy} onClick={() => run(async () => { await apiFetch('/communities/' + id + '/publications/' + n.id, { method: 'DELETE' }); setNotes(prev => prev.filter(x => x.id !== n.id)); })}>Remover da comunidade</button>}</article>)}</div></>;
}

export function Discussion({ publicationId }) {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([]);
  const [text, setText] = useState('');
  const [reply, setReply] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const { addToast } = useToast();
  useEffect(() => { if (!open) return; let alive = true; apiFetch('/discussion/' + publicationId).then(rows => { if (alive) setItems(rows); }).catch(e => { if (alive) setError(e.message); }); return () => { alive = false; }; }, [open, publicationId]);
  const send = async e => { e.preventDefault(); setBusy(true); try { await apiFetch('/discussion/' + publicationId, { method: 'POST', body: JSON.stringify({ text, parentId: reply?.id || null }) }); setText(''); setReply(null); setItems(await apiFetch('/discussion/' + publicationId)); } catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); } };
  const remove = async c => { setBusy(true); try { await apiFetch('/discussion/' + c.id, { method: 'DELETE' }); setItems(prev => prev.map(x => x.id === c.id ? { ...x, text: '', isDeleted: true, canDelete: false } : x)); if (reply?.id === c.id) setReply(null); } catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); } };
  const comment = c => <div key={c.id} className="discussion-comment"><Link to={'/u/' + c.handle}>{c.displayName}</Link><p style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{c.isDeleted ? 'Comentario removido.' : c.text}</p><div className="row gap8">{!c.parentId && !c.isDeleted && <button className="btn compact" disabled={busy} onClick={() => setReply(c)}>Responder</button>}{c.canDelete && <button className="btn compact" disabled={busy} onClick={() => remove(c)}>Excluir comentario</button>}</div></div>;
  return <div className="mt16"><button className="btn compact" aria-expanded={open} onClick={() => setOpen(!open)}>Conversa</button>{open && <div className="stack mt16">{error && <p role="alert">{error}</p>}{items.filter(c => !c.parentId).map(c => <div key={c.id}>{comment(c)}<div className="discussion-replies">{items.filter(r => r.parentId === c.id).map(comment)}</div></div>)}<form className="stack" onSubmit={send}>{reply && <div className="row gap8"><span>Respondendo a {reply.displayName}</span><button type="button" className="btn compact" onClick={() => setReply(null)}>Cancelar resposta</button></div>}<label>Seu comentario<textarea value={text} onChange={e => setText(e.target.value)} maxLength={2000} required /></label><button className="btn compact" disabled={busy || !text.trim()}>Comentar</button></form></div>}</div>;
}

function MemberManagement({ communityId }) {
  const [members, setMembers] = useState([]);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  const load = () => apiFetch('/communities/' + communityId + '/members').then(setMembers).catch(e => addToast(e.message, 'warning'));
  const update = async (m, changes) => {
    setBusy(true);
    try { await apiFetch('/communities/' + communityId + '/members/' + encodeURIComponent(m.handle), { method: 'PUT', body: JSON.stringify({ isModerator: m.isModerator, isSuspended: m.isSuspended, ...changes }) }); await load(); }
    catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); }
  };
  return <details className="card pad mb24" onToggle={e => { if (e.currentTarget.open) load(); }}><summary>Gerenciar participantes</summary><div className="stack mt16">{members.map(m => <div key={m.handle} className="row between gap8 wrap"><span>{m.displayName} / @{m.handle}{m.isSuspended ? ' / suspenso' : m.isModerator ? ' / moderador' : ''}</span><div className="row gap8"><button className="btn compact" disabled={busy || m.isSuspended || m.isOwner} onClick={() => update(m, { isModerator: !m.isModerator })}>{m.isModerator ? 'Remover moderador' : 'Tornar moderador'}</button><button className="btn compact" disabled={busy || m.isOwner} onClick={() => update(m, { isSuspended: !m.isSuspended })}>{m.isSuspended ? 'Restaurar acesso' : 'Suspender'}</button></div></div>)}</div></details>;
}
