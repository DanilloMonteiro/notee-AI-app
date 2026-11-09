// Categoria e tags da nota no editor. As mudanças entram no "Salvar alterações" da nota.
import React, { useEffect, useState } from 'react';
import { Icon } from './Icon';
import { taxonomyApi } from '../notes';
import { useToast } from '../contexts/ToastContext';

export function NoteOrganizer({ categoryId, tagIds, onCategoryChange, onTagsChange, disabled }) {
  const [categories, setCategories] = useState([]);
  const [tags, setTags] = useState([]);
  const [newTag, setNewTag] = useState('');
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

  const createCategory = async () => {
    const name = window.prompt('Nome da nova categoria');
    if (!name?.trim()) return;
    try {
      const created = await taxonomyApi.create('categories', name.trim());
      setCategories(prev => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
      onCategoryChange(created.id);
    } catch (err) {
      addToast(err.message || 'Não foi possível criar a categoria.', 'warning');
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
        style={{ padding: '6px', borderRadius: '6px', width: 'auto', minWidth: 180 }}
      >
        <option value="">Sem categoria</option>
        {categories.map(c => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>
      <button type="button" className="btn compact ghost" onClick={createCategory} disabled={disabled}>
        <Icon name="plus" className="sm" /> Categoria
      </button>

      <span className="small muted" style={{ marginLeft: 12 }}>
        Tags
      </span>
      {selectedTags.map(tag => (
        <span key={tag.id} className="tag row gap8">
          {tag.name}
          {!disabled && (
            <button
              type="button"
              className="icon-btn"
              style={{ width: 18, height: 18 }}
              aria-label={`Remover tag ${tag.name}`}
              onClick={() => onTagsChange(tagIds.filter(id => id !== tag.id))}
            >
              <Icon name="close" className="sm" />
            </button>
          )}
        </span>
      ))}
      {!disabled && (
        <form onSubmit={addTag} className="row gap8">
          <input
            list="note-tag-options"
            value={newTag}
            maxLength={60}
            onChange={e => setNewTag(e.target.value)}
            placeholder="Adicionar tag"
            aria-label="Adicionar tag"
            style={{ padding: '6px', borderRadius: '6px', width: 140 }}
          />
          <datalist id="note-tag-options">
            {tags.map(t => (
              <option key={t.id} value={t.name} />
            ))}
          </datalist>
        </form>
      )}
    </div>
  );
}
