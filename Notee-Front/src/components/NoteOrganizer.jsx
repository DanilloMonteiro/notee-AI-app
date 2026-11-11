// Categoria e tags da nota no editor. As mudanças entram no "Salvar alterações" da nota.
import React, { useEffect, useState } from 'react';
import { Icon } from './Icon';
import { taxonomyApi } from '../notes';
import { useToast } from '../contexts/ToastContext';

export function NoteOrganizer({ categoryId, tagIds, onCategoryChange, onTagsChange, disabled }) {
  const [categories, setCategories] = useState([]);
  const [tags, setTags] = useState([]);
  const [newTag, setNewTag] = useState('');
  const [showTags, setShowTags] = useState(false);
  const [showCategory, setShowCategory] = useState(false);
  const [newCategory, setNewCategory] = useState('');
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();

  useEffect(() => {
    let alive = true;
    Promise.all([taxonomyApi.list('categories'), taxonomyApi.list('tags')])
      .then(([c, t]) => {
        if (!alive) return;
        setCategories(c || []);
        setTags(t || []);
      })
      .catch(() => {});
    return () => {
      alive = false;
    };
  }, []);

  const createCategory = async e => {
    e.preventDefault();
    const name = newCategory.trim();
    if (!name || busy) return;
    setBusy(true);
    try {
      const created = await taxonomyApi.create('categories', name.trim());
      setCategories(prev => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
      onCategoryChange(created.id);
      setNewCategory('');
      setShowCategory(false);
    } catch (err) {
      addToast(err.message || 'Não foi possível criar a categoria.', 'warning');
    } finally {
      setBusy(false);
    }
  };

  const addTag = async e => {
    e.preventDefault();
    const name = newTag.trim();
    if (!name) return;
    const existing = tags.find(t => t.name.toLowerCase() === name.toLowerCase());
    try {
      const tag = existing || (await taxonomyApi.create('tags', name));
      if (!existing) setTags(prev => [...prev, tag]);
      if (!tagIds.includes(tag.id)) onTagsChange([...tagIds, tag.id]);
      setNewTag('');
    } catch (err) {
      addToast(err.message || 'Não foi possível criar a tag.', 'warning');
    }
  };

  const selectedTags = tagIds.map(id => tags.find(t => t.id === id)).filter(Boolean);

  return (
    <div className="card pad mb24 row gap8 wrap" style={{ alignItems: 'center' }}>
      <label className="small muted" htmlFor="note-category">
        Categoria
      </label>
      <select
        id="note-category"
        value={categoryId || ''}
        disabled={disabled}
        onChange={e => onCategoryChange(e.target.value || null)}
        className="organizer-category-select"
      >
        <option value="">Sem categoria</option>
        {categories.map(c => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>
      <div className="organizer-category-picker" onKeyDown={e => { if (e.key === 'Escape') setShowCategory(false); }}>
      <button type="button" className="btn compact ghost" onClick={() => setShowCategory(!showCategory)} disabled={disabled} aria-expanded={showCategory} aria-controls="new-category-card">
        <Icon name="plus" className="sm" /> Categoria
      </button>
      {showCategory && !disabled && (
        <form id="new-category-card" className="organizer-category-form" onSubmit={createCategory}>
          <label htmlFor="new-category">Nova categoria</label>
          <p>Organize suas notas por assunto.</p>
          <input id="new-category" value={newCategory} onChange={e => setNewCategory(e.target.value)} placeholder="Nome da categoria" maxLength={60} autoFocus />
          <div className="organizer-category-actions">
          <button type="submit" className="btn primary compact" disabled={busy || !newCategory.trim()}>Criar</button>
          <button type="button" className="btn compact" onClick={() => setShowCategory(false)}>Cancelar</button>
          </div>
        </form>
      )}
      </div>

      <span className="small muted" style={{ marginLeft: 12 }}>
        Tags
      </span>
      {selectedTags.map(tag => (
        <span key={tag.id} className="tag organizer-tag">
          {tag.name}
          {!disabled && (
            <button
              type="button"
              className="organizer-tag-remove"
              aria-label={`Remover tag ${tag.name}`}
              onClick={() => onTagsChange(tagIds.filter(id => id !== tag.id))}
            >
              <Icon name="close" className="sm" />
            </button>
          )}
        </span>
      ))}
      {!disabled && (
        <div className="organizer-tag-picker" onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget)) setShowTags(false); }}>
        <form onSubmit={addTag} className="organizer-tag-form">
          <input
            onFocus={() => setShowTags(true)}
            onKeyDown={e => { if (e.key === 'Escape') setShowTags(false); }}
            aria-expanded={showTags}
            aria-controls="note-tag-options"
            value={newTag}
            maxLength={60}
            onChange={e => setNewTag(e.target.value)}
            placeholder="Adicionar tag"
            aria-label="Adicionar tag"
          />
          <button type="submit" disabled={!newTag.trim()} aria-label="Adicionar tag">
            <Icon name="plus" className="sm" /> Adicionar
          </button>
        </form>
        {showTags && (
          <div id="note-tag-options" className="organizer-tag-options" aria-label="Tags existentes">
            <div className="organizer-tag-options-title">Tags existentes</div>
            {tags.filter(t => !tagIds.includes(t.id) && t.name.toLowerCase().includes(newTag.trim().toLowerCase())).length ?
              tags.filter(t => !tagIds.includes(t.id) && t.name.toLowerCase().includes(newTag.trim().toLowerCase())).map(t => (
                <button type="button" key={t.id} onClick={() => { onTagsChange([...tagIds, t.id]); setNewTag(''); setShowTags(false); }}>{t.name}<span>Adicionar</span></button>
              )) : <p>Nenhuma tag disponível. Digite um nome para criar.</p>}
          </div>
        )}
        </div>
      )}
    </div>
  );
}
