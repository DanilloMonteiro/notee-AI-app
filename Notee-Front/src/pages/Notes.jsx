// src/pages/Notes.jsx
// Minhas notas: abas Ativas / Arquivadas / Lixeira, filtros por categoria e tag e busca no servidor.
import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { NoteCard } from '../components/NoteCard';
import { useToast } from '../contexts/ToastContext';
import { apiFetch } from '../api';
import { notesApi, taxonomyApi } from '../notes';

const TABS = [
  { id: 'active', label: 'Ativas' },
  { id: 'archived', label: 'Arquivadas' },
  { id: 'trashed', label: 'Lixeira' },
];
const PAGE_SIZE = 30;
const DONE = { archive: 'Nota arquivada.', trash: 'Nota movida para a lixeira.', restore: 'Nota restaurada.' };

export function Notes() {
  const [tab, setTab] = useState('active');
  const [query, setQuery] = useState('');
  const [debounced, setDebounced] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [tagId, setTagId] = useState('');
  const [notes, setNotes] = useState([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [categories, setCategories] = useState([]);
  const [tags, setTags] = useState([]);
  const navigate = useNavigate();
  const { addToast } = useToast();

  useEffect(() => {
    Promise.all([taxonomyApi.list('categories'), taxonomyApi.list('tags')])
      .then(([c, t]) => {
        setCategories(c || []);
        setTags(t || []);
      })
      .catch(() => {});
  }, []);

  // Busca com atraso curto para não consultar a cada tecla.
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(query.trim()), 300);
    return () => clearTimeout(timer);
  }, [query]);

  const load = useCallback(
    async (nextPage = 1) => {
      try {
        const items = (await notesApi.list({ status: tab, query: debounced, categoryId, tagId, page: nextPage, pageSize: PAGE_SIZE })) || [];
        setNotes(prev => (nextPage === 1 ? items : [...prev, ...items]));
        setHasMore(items.length === PAGE_SIZE);
        setPage(nextPage);
        setError('');
      } catch {
        setError('Não foi possível carregar as suas notas. A API está em execução?');
      } finally {
        setLoading(false);
      }
    },
    [tab, debounced, categoryId, tagId],
  );

  useEffect(() => {
    load(1);
  }, [load]);

  const handleCreateNote = async () => {
    setCreating(true);
    try {
      const newNote = await apiFetch('/notes', {
        method: 'POST',
        body: JSON.stringify({ title: 'Uma nova ideia', content: '[]' }),
      });
      navigate(`/note/${newNote.id}`);
    } catch {
      addToast('Erro ao criar a nota.', 'warning');
      setCreating(false);
    }
  };

  const act = async (note, action) => {
    try {
      if (action === 'delete') {
        if (!window.confirm(`Excluir “${note.title}” definitivamente? Esta ação não pode ser desfeita.`)) return;
        await notesApi.remove(note.id);
        addToast('Nota excluída definitivamente.');
      } else {
        await notesApi.lifecycle(note.id, action, note.version);
        addToast(DONE[action]);
      }
      setNotes(prev => prev.filter(n => n.id !== note.id));
    } catch (err) {
      addToast(err?.message || 'Não foi possível concluir a ação.', 'warning');
      load(1);
    }
  };

  const actionsFor = note => {
    const btn = (action, icon, label, extra = '') => (
      <button key={action} className={`btn compact ${extra}`} onClick={() => act(note, action)}>
        <Icon name={icon} className="sm" /> {label}
      </button>
    );
    if (tab === 'active') return [btn('archive', 'archive', 'Arquivar', 'ghost'), btn('trash', 'trash', 'Lixeira', 'ghost')];
    if (tab === 'archived') return [btn('restore', 'refresh', 'Restaurar'), btn('trash', 'trash', 'Lixeira', 'ghost')];
    return [btn('restore', 'refresh', 'Restaurar'), btn('delete', 'trash', 'Excluir de vez', 'danger')];
  };

  const filtered = Boolean(debounced || categoryId || tagId);

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">MEU ESPAÇO</div>
          <h1>Minhas notas</h1>
          <p>Pequenas anotações, perguntas importantes e ideias em construção.</p>
        </div>
        <div className="row gap8">
          <button className="btn primary" onClick={handleCreateNote} disabled={creating}>
            <Icon name="plus" /> {creating ? 'A criar...' : 'Nova nota'}
          </button>
        </div>
      </div>

      <div className="tabbar" role="tablist">
        {TABS.map(t => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            className={`tab ${tab === t.id ? 'active' : ''}`}
            onClick={() => {
              setLoading(true);
              setTab(t.id);
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="filters">
        <input
          type="text"
          placeholder="Buscar por título ou texto..."
          aria-label="Buscar notas"
          value={query}
          onChange={e => setQuery(e.target.value)}
        />
        <select aria-label="Filtrar por categoria" value={categoryId} onChange={e => setCategoryId(e.target.value)}>
          <option value="">Todas as categorias</option>
          {categories.map(c => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <select aria-label="Filtrar por tag" value={tagId} onChange={e => setTagId(e.target.value)}>
          <option value="">Todas as tags</option>
          {tags.map(t => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <span className="grow"></span>
        <span className="small muted">{notes.length} notas</span>
      </div>

      {tab === 'trashed' && (
        <div className="callout neutral mb24">
          <Icon name="info" />
          <span>Notas na lixeira não aparecem nos mapas nem na busca. Excluir de vez remove também as dúvidas da nota.</span>
        </div>
      )}

      {error && (
        <div className="callout warning mb24">
          <Icon name="info" />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="empty">
          <h3>A carregar...</h3>
        </div>
      ) : notes.length > 0 ? (
        <>
          <div className="grid three">
            {notes.map(note => (
              <NoteCard key={note.id} note={note} actions={actionsFor(note)} />
            ))}
          </div>
          {hasMore && (
            <div className="row mt16" style={{ justifyContent: 'center' }}>
              <button className="btn" onClick={() => load(page + 1)}>
                Carregar mais
              </button>
            </div>
          )}
        </>
      ) : (
        <div className="empty">
          <Icon name={tab === 'trashed' ? 'trash' : 'search'} />
          <h3>{filtered ? 'Nenhuma nota encontrada' : tab === 'active' ? 'Nenhuma nota ainda' : 'Nada por aqui'}</h3>
          <p>{tab === 'active' && !filtered ? 'Crie a sua primeira nota para começar.' : 'Ajuste os filtros ou troque de aba.'}</p>
        </div>
      )}
    </div>
  );
}
