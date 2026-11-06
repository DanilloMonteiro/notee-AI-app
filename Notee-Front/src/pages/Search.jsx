// src/pages/Search.jsx
import React, { useState, useEffect } from 'react';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';
import { NoteCard } from '../components/NoteCard';

export function Search() {
  const [searchTerm, setSearchTerm] = useState('');
  const [notes, setNotes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [hasSearched, setHasSearched] = useState(false);

  // Carrega todas as notas no início (para a busca local)
  useEffect(() => {
    const fetchNotes = async () => {
      try {
        const response = await apiFetch('/notes?page=1&pageSize=100');
        setNotes(response || []);
      } catch (err) {
        console.error('Erro ao carregar as notas para a busca:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchNotes();
  }, []);

  const handleSearch = e => {
    e.preventDefault();
    setHasSearched(true);
  };

  // Lógica de filtragem local (tal como no seu protótipo HTML)
  const term = searchTerm.toLowerCase();
  const matches = notes.filter(n => {
    if (!term) return false;
    const title = (n.title || '').toLowerCase();
    const content = (n.content || '').toLowerCase();
    const cat = (n.categoryName || '').toLowerCase();
    return title.includes(term) || content.includes(term) || cat.includes(term);
  });

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">BUSCA</div>
          <h1>Encontre o fio da ideia.</h1>
          <p>Busque por título, texto ou categoria. Somente as suas notas ativas aparecem aqui.</p>
        </div>
      </div>

      <form className="row mb24" onSubmit={handleSearch}>
        <input
          type="text"
          value={searchTerm}
          onChange={e => {
            setSearchTerm(e.target.value);
            if (e.target.value === '') setHasSearched(false);
          }}
          placeholder="O que você está procurando?"
          aria-label="Pesquisar nas notas"
          style={{ padding: '15px', fontSize: '15px', flex: 1 }}
        />
        <button className="btn primary" type="submit">
          <Icon name="search" /> Buscar
        </button>
      </form>

      <p className="tiny muted mb24">
        Protótipo: busca textual local. PostgreSQL Full Text Search está no planeamento futuro.
      </p>

      {loading ? (
        <div className="empty">
          <h3>A carregar índice...</h3>
        </div>
      ) : !hasSearched && !term ? (
        <div className="empty">
          <Icon name="search" />
          <h3>Uma palavra pode ser o começo.</h3>
          <p>Experimente pesquisar pelo título de uma nota que já criou.</p>
        </div>
      ) : matches.length > 0 ? (
        <>
          <div className="small muted mb16">
            {matches.length} resultado(s) para "{searchTerm}"
          </div>
          <div className="grid two">
            {matches.map(note => (
              <NoteCard key={note.id} note={note} />
            ))}
          </div>
        </>
      ) : (
        <div className="empty">
          <Icon name="search" />
          <h3>Nenhum resultado</h3>
          <p>Tente uma palavra menos específica. Notas arquivadas não entram nesta busca.</p>
        </div>
      )}
    </div>
  );
}
