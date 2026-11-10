import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { SocialHeading, SocialState, PublicationCard, SocialPagination } from './SocialUI';
import { apiFetch } from '../../api';

const CRITERIA = { likes: 'Mais úteis', recent: 'Mais recentes', reposts: 'Mais repostadas', views: 'Mais vistas' };
export function DiscoverPage() {
  const [communities, setCommunities] = useState([]);
  const [selected, setSelected] = useState([]);
  const [criteria, setCriteria] = useState(['likes', 'recent', 'reposts', 'views']);
  const [days, setDays] = useState(30);
  const [q, setQ] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [communityError, setCommunityError] = useState('');
  const [reload, setReload] = useState(0);
  useEffect(() => { let alive = true; apiFetch('/communities').then(c => { if (alive) setCommunities(c); }).catch(e => { if (alive) setCommunityError(e.message); }); return () => { alive = false; }; }, []);
  useEffect(() => {
    let alive = true; setLoading(true);
    const params = new URLSearchParams({ communities: selected.join(','), criteria: criteria.join(','), days, q: search, page });
    apiFetch('/communities/discover?' + params).then(r => { if (alive) { setRows(r); setError(''); } }).catch(e => { if (alive) setError(e.message); }).finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
  }, [selected, criteria, days, search, page, reload]);
  return <div className="social-page">
    <SocialHeading eyebrow="EXPLORE O NOTEE" title="Descobrir notas" description="Encontre novas perspectivas. Combine assuntos e escolha o que importa para você."><Link className="btn" to="/communities"><Icon name="globe" />Comunidades</Link></SocialHeading>
    <div className="social-layout">
      <aside className="card social-sidebar" aria-label="Filtros da descoberta">
        <div><strong>Refinar descoberta</strong><p className="social-hint">Combine os filtros para encontrar sua próxima leitura.</p></div>
        <fieldset><legend>Comunidades</legend>{communityError ? <p className="small" role="alert">{communityError}</p> : <div className="social-filter-list">{communities.map(c => <label key={c.id} className="check"><input type="checkbox" checked={selected.includes(c.id)} disabled={!selected.includes(c.id) && selected.length >= 20} onChange={e => { setSelected(prev => e.target.checked ? [...prev, c.id] : prev.filter(id => id !== c.id)); setPage(1); }} />{c.name}</label>)}{!communities.length && <p className="tiny muted">Nenhuma comunidade disponível.</p>}</div>}<p className="social-hint">Sem seleção, todas as publicações públicas aparecem.</p></fieldset>
        <div className="field"><label htmlFor="discover-period">Período</label><select id="discover-period" value={days} onChange={e => { setDays(Number(e.target.value)); setPage(1); }}><option value={7}>Últimos 7 dias</option><option value={30}>Últimos 30 dias</option><option value={0}>Todo o período</option></select></div>
        <fieldset><legend>O que você quer encontrar?</legend><div className="social-filter-list">{Object.entries(CRITERIA).map(([id, label]) => <label className="check" key={id}><input type="checkbox" checked={criteria.includes(id)} disabled={criteria.length === 1 && criteria.includes(id)} onChange={e => { setCriteria(prev => e.target.checked ? [...prev, id] : prev.filter(x => x !== id)); setPage(1); }} />{label}</label>)}</div><p className="social-hint">Os critérios selecionados têm o mesmo peso. Mantenha pelo menos um ativo.</p></fieldset>
        <button className="btn compact" onClick={() => { setSelected([]); setCriteria(['likes', 'recent', 'reposts', 'views']); setDays(30); setQ(''); setSearch(''); setPage(1); }}>Limpar filtros</button>
      </aside>
      <section aria-label="Resultados da descoberta">
        <form className="social-toolbar" onSubmit={e => { e.preventDefault(); setSearch(q); setPage(1); }}><div className="social-search"><input type="text" aria-label="Buscar nas publicações" value={q} onChange={e => setQ(e.target.value)} maxLength={200} placeholder="Buscar por título ou conteúdo…" /><button className="btn primary"><Icon name="search" className="sm" />Buscar</button></div></form>
        {loading || error || !rows.length ? <SocialState loading={loading} error={error} retry={() => setReload(r => r + 1)} title="Nenhuma nota encontrada" description="Tente outra busca ou amplie as comunidades e o período selecionados." /> : <div className="stack">{rows.map(n => <PublicationCard note={n} period key={n.id} />)}</div>}
        {!error && <SocialPagination page={page} loading={loading} hasNext={rows.length === 20} onChange={setPage} />}
      </section>
    </div>
  </div>;
}
