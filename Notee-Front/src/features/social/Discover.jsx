import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../../api';

const CRITERIA = { likes: 'Mais uteis', recent: 'Mais recentes', reposts: 'Mais repostadas' };
export function DiscoverPage() {
  const [communities, setCommunities] = useState([]);
  const [selected, setSelected] = useState([]);
  const [criteria, setCriteria] = useState(['likes', 'recent', 'reposts']);
  const [days, setDays] = useState(30);
  const [q, setQ] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  useEffect(() => { let alive = true; apiFetch('/communities').then(c => { if (alive) setCommunities(c); }).catch(e => { if (alive) setError(e.message); }); return () => { alive = false; }; }, []);
  useEffect(() => {
    let alive = true; setLoading(true);
    const params = new URLSearchParams({ communities: selected.join(','), criteria: criteria.join(','), days, q: search, page });
    apiFetch('/communities/discover?' + params).then(r => { if (alive) { setRows(r); setError(''); } }).catch(e => { if (alive) setError(e.message); }).finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
  }, [selected, criteria, days, search, page]);
  return <><div className="overline">EXPLORE O NOTEE</div><h1>Descobrir notas</h1><p className="muted mb24">Combine assuntos, periodo e criterios para encontrar publicacoes.</p>
    <div className="card pad stack mb24"><form className="row gap8" onSubmit={e => { e.preventDefault(); setSearch(q); setPage(1); }}><input aria-label="Buscar nas publicacoes" value={q} onChange={e => setQ(e.target.value)} maxLength={200} placeholder="O que voce quer aprender?" /><button className="btn">Buscar</button></form>
      <fieldset><legend>Comunidades</legend><div className="row gap8 wrap">{communities.map(c => <label key={c.id} className="row gap8"><input type="checkbox" checked={selected.includes(c.id)} disabled={!selected.includes(c.id) && selected.length >= 20} onChange={e => { setSelected(prev => e.target.checked ? [...prev, c.id] : prev.filter(id => id !== c.id)); setPage(1); }} />{c.name}</label>)}</div><p className="tiny muted">Sem selecao: todas as publicacoes publicas. Com selecao: notas de qualquer comunidade marcada.</p></fieldset>
      <label>Periodo<select value={days} onChange={e => { setDays(Number(e.target.value)); setPage(1); }}><option value={7}>Ultimos 7 dias</option><option value={30}>Ultimos 30 dias</option><option value={0}>Todo o periodo</option></select></label>
      <fieldset><legend>Combinar criterios</legend><div className="row gap8 wrap">{Object.entries(CRITERIA).map(([id, label]) => <label className="row gap8" key={id}><input type="checkbox" checked={criteria.includes(id)} disabled={criteria.length === 1 && criteria.includes(id)} onChange={e => { setCriteria(prev => e.target.checked ? [...prev, id] : prev.filter(x => x !== id)); setPage(1); }} />{label}</label>)}</div><p className="tiny muted">Os criterios escolhidos tem o mesmo peso. A pontuacao equilibra popularidade e idade da publicacao.</p></fieldset>
    </div>
    {loading ? <p role="status">Buscando notas...</p> : error ? <p role="alert">{error}</p> : <div className="stack">{!rows.length && <p>Nenhuma nota encontrada com esses filtros.</p>}{rows.map(n => <article className="card pad" key={n.id}><Link to={'/u/' + n.handle}>{n.displayName}</Link><h2><Link to={'/p/' + n.handle + '/' + n.slug}>{n.title}</Link></h2><p className="tiny muted">{n.likes} curtidas / {n.reposts} reposts no periodo</p></article>)}</div>}
    <div className="row gap8 mt16"><button className="btn" disabled={loading || page === 1} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Pagina {page}</span><button className="btn" disabled={loading || rows.length < 20} onClick={() => setPage(p => p + 1)}>Proxima</button></div></>;
}
