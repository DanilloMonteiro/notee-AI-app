import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiFetch } from '../../api';
import { Icon } from '../../components/Icon';
import { UserAvatar } from '../../components/UserAvatar';
import { useToast } from '../../contexts/ToastContext';
import { PublicationCard, SocialHeading, SocialPagination, SocialState } from './SocialUI';
import { Discussion as AnchoredDiscussion } from './Discussion';

export function CommunitiesPage() {
  const [items, setItems] = useState([]);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const formPanel = useRef(null);
  const nameInput = useRef(null);
  const { addToast } = useToast();
  const load = useCallback(async () => {
    setLoading(true);
    try { setItems(await apiFetch('/communities')); setError(''); }
    catch (e) { setError(e.message); } finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);
  const create = async e => {
    e.preventDefault(); setBusy(true);
    try {
      await apiFetch('/communities', { method: 'POST', body: JSON.stringify({ name, description }) });
      setName(''); setDescription(''); formPanel.current.open = false;
      addToast('Comunidade criada.'); await load();
    } catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); }
  };
  return <div className="social-page">
    <SocialHeading eyebrow="CONHECIMENTO EM COMUM" title="Comunidades" description="Um lugar para compartilhar descobertas e conversar sobre o que você está aprendendo."><button className="btn primary" onClick={() => { formPanel.current.open = true; nameInput.current?.focus(); }}><Icon name="plus" />Nova comunidade</button></SocialHeading>
    <details className="card social-create" ref={formPanel}><summary><Icon name="globe" />Criar uma comunidade</summary><form onSubmit={create}><label className="field">Nome da comunidade<input type="text" ref={nameInput} value={name} onChange={e => setName(e.target.value)} required minLength={2} maxLength={80} placeholder="Ex.: Ciência e descobertas" /></label><label className="field">Sobre a comunidade<textarea value={description} onChange={e => setDescription(e.target.value)} maxLength={500} placeholder="Que assuntos vocês vão explorar juntos?" /></label><p className="social-hint">A comunidade será pública. Você poderá gerenciar os participantes e as notas compartilhadas.</p><div className="social-form-actions"><button type="button" className="btn" disabled={busy} onClick={() => { formPanel.current.open = false; }}>Cancelar</button><button className="btn primary" disabled={busy || name.trim().length < 2}>{busy ? 'Criando…' : 'Criar comunidade'}</button></div></form></details>
    {loading || error || !items.length ? <SocialState loading={loading} error={error} retry={load} title="Conhecimento fica melhor em companhia" description="Crie a primeira comunidade e reúna notas sobre um assunto que você gosta." /> : <div className="social-community-grid">{items.map(c => <Link className="card social-community-card" key={c.id} to={'/communities/' + c.id}><span className="category-mark"><Icon name="globe" /></span><h2>{c.name}</h2><p>{c.description || 'Um espaço aberto para novas ideias e descobertas.'}</p><span className="small muted">{c.members} {c.members === 1 ? 'participante' : 'participantes'}</span><footer>Explorar comunidade<Icon name="arrow" className="sm" /></footer></Link>)}</div>}
  </div>;
}

export function CommunityPage() {
  const { id } = useParams();
  const [community, setCommunity] = useState(null);
  const [notes, setNotes] = useState([]);
  const [publications, setPublications] = useState([]);
  const [selected, setSelected] = useState('');
  const [sort, setSort] = useState('recent');
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [noteError, setNoteError] = useState('');
  const [reload, setReload] = useState(0);
  const { addToast } = useToast();
  useEffect(() => {
    let alive = true; setCommunity(null); setError('');
    Promise.all([apiFetch('/communities/' + id), apiFetch('/publications')]).then(([c, p]) => { if (alive) { setCommunity(c); setPublications(p.filter(x => x.visibility === 'public' && x.status === 'Published')); } }).catch(e => { if (alive) setError(e.message); });
    return () => { alive = false; };
  }, [id, reload]);
  useEffect(() => {
    let alive = true; setLoading(true);
    apiFetch('/communities/' + id + '/notes?sort=' + sort + '&page=' + page).then(n => { if (alive) { setNotes(n); setNoteError(''); } }).catch(e => { if (alive) setNoteError(e.message); }).finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
  }, [id, sort, page, reload]);
  const run = async fn => { setBusy(true); try { await fn(); } catch (e) { addToast(e.message, 'warning'); } finally { setBusy(false); } };
  const notesPath = '/communities/' + id + '/notes?sort=' + sort + '&page=' + page;
  if (error || !community) return <SocialState loading={!error} error={error} retry={() => setReload(r => r + 1)} />;
  return <div className="social-page">
    <Link className="social-back" to="/communities"><Icon name="back" className="sm" />Todas as comunidades</Link>
    <SocialHeading eyebrow="COMUNIDADE" title={community.name} description={community.description || 'Novas ideias começam com uma boa conversa.'}><button className={community.joined ? 'btn' : 'btn primary'} disabled={busy || community.isOwner} onClick={() => run(async () => { await apiFetch('/communities/' + id + '/membership', { method: community.joined ? 'DELETE' : 'PUT' }); setCommunity(c => ({ ...c, joined: !c.joined })); })}><Icon name={community.joined ? 'check' : 'plus'} />{busy ? 'Atualizando…' : community.isOwner ? 'Sua comunidade' : community.joined ? 'Sair da comunidade' : 'Participar'}</button></SocialHeading>
    {community.isOwner && <MemberManagement communityId={id} />}
    {community.joined && <form className="card social-share" onSubmit={e => { e.preventDefault(); run(async () => { await apiFetch('/communities/' + id + '/publications/' + selected, { method: 'PUT' }); setNotes(await apiFetch(notesPath)); setSelected(''); addToast('Nota compartilhada na comunidade.'); }); }}><div><strong>Compartilhe uma descoberta</strong><p className="social-hint">Escolha uma das suas notas publicadas para trazer à conversa.</p></div>{publications.length ? <div className="social-share-controls"><select aria-label="Nota para compartilhar" value={selected} onChange={e => setSelected(e.target.value)}><option value="">Selecione uma publicação pública</option>{publications.map(p => <option key={p.id} value={p.id}>{p.noteTitle}</option>)}</select><button className="btn primary" disabled={busy || !selected}><Icon name="plus" className="sm" />Compartilhar nota</button></div> : <Link className="btn compact" to="/publications">Ver minhas publicações<Icon name="arrow" className="sm" /></Link>}</form>}
    <div className="social-sort"><h2>Notas da comunidade</h2><label>Ordenar por<select value={sort} onChange={e => { setSort(e.target.value); setPage(1); }}><option value="recent">Mais recentes</option><option value="likes">Mais úteis</option><option value="reposts">Mais repostadas</option><option value="combined">Curtidas + reposts</option></select></label></div>
    {loading || noteError || !notes.length ? <SocialState loading={loading} error={noteError} retry={() => setReload(r => r + 1)} title="A próxima descoberta pode ser sua" description="Ainda não há notas nesta página. Compartilhe uma publicação para começar a conversa." /> : <div className="stack">{notes.map(n => <PublicationCard note={n} key={n.id}><AnchoredDiscussion publicationId={n.id} commentsEnabled={n.commentsEnabled} />{community.canModerate && <button className="btn compact ghost mt16" disabled={busy} onClick={() => run(async () => { await apiFetch('/communities/' + id + '/publications/' + n.id, { method: 'DELETE' }); setNotes(prev => prev.filter(x => x.id !== n.id)); addToast('Nota removida da comunidade.'); })}><Icon name="shield" className="sm" />Remover da comunidade</button>}</PublicationCard>)}</div>}
    {!noteError && <SocialPagination page={page} loading={loading} hasNext={notes.length === 20} onChange={setPage} />}
  </div>;
}

function MemberManagement({ communityId }) {
  const [members, setMembers] = useState([]);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { addToast } = useToast();
  const load = async () => { setLoading(true); try { setMembers(await apiFetch('/communities/' + communityId + '/members')); setError(''); } catch (e) { setError(e.message); } finally { setLoading(false); } };
  const update = async (m, changes) => {
    setBusy(true);
    try { await apiFetch('/communities/' + communityId + '/members/' + encodeURIComponent(m.handle), { method: 'PUT', body: JSON.stringify({ isModerator: m.isModerator, isSuspended: m.isSuspended, ...changes }) }); await load(); addToast('Permissões atualizadas.'); }
    catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); }
  };
  return <details className="card social-members" onToggle={e => { if (e.currentTarget.open) load(); }}><summary><Icon name="shield" />Gerenciar participantes</summary><div className="social-members-body"><p className="small muted">Moderadores podem remover notas desta comunidade. A suspensão impede novos compartilhamentos.</p>{loading || error ? <SocialState loading={loading} error={error} retry={load} /> : members.map(m => <div key={m.handle} className="social-member"><div className="social-member-identity"><UserAvatar name={m.displayName} /><div><strong className="small">{m.displayName}</strong><span className="tiny muted">@{m.handle}</span><span className="tiny muted">{m.isOwner ? 'Criador' : m.isSuspended ? 'Suspenso' : m.isModerator ? 'Moderador' : 'Participante'}</span></div></div><div className="row gap8 wrap">{!m.isOwner && <><button className="btn compact" disabled={busy || m.isSuspended} onClick={() => update(m, { isModerator: !m.isModerator })}>{m.isModerator ? 'Remover moderador' : 'Tornar moderador'}</button><button className={'btn compact ' + (m.isSuspended ? '' : 'danger')} disabled={busy} onClick={() => update(m, { isSuspended: !m.isSuspended })}>{m.isSuspended ? 'Restaurar acesso' : 'Suspender'}</button></>}</div></div>)}</div></details>;
}
