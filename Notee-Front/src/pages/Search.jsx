// src/pages/Search.jsx
// Busca no servidor (título e texto das notas não arquivadas), isolada pela conta.
import React, { useState } from 'react';
import { Icon } from '../components/Icon';
import { NoteCard } from '../components/NoteCard';
import { notesApi } from '../notes';

export function Search() {
  const [searchTerm, setSearchTerm] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [matches, setMatches] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSearch = async e => {
    e.preventDefault();
    const term = searchTerm.trim();
    setSubmitted(term);
    if (!term) {
      setMatches([]);
      return;
    }
    setLoading(true);
    try {
      setMatches((await notesApi.list({ query: term, pageSize: 50 })) || []);
      setError('');
    } catch {
      setError('Não foi possível buscar agora. A API está em execução?');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">BUSCA</div>
          <h1>Encontre o fio da ideia.</h1>
          <p>Busque por título ou texto. Somente as suas notas ativas aparecem aqui.</p>
        </div>
      </div>

      <form className="row mb24" onSubmit={handleSearch}>
        <input
          type="text"
          value={searchTerm}
          onChange={e => setSearchTerm(e.target.value)}
          placeholder="O que você está procurando?"
          aria-label="Pesquisar nas notas"
          style={{ padding: '15px', fontSize: '15px', flex: 1 }}
        />
        <button className="btn primary" type="submit" disabled={loading}>
          <Icon name="search" /> {loading ? 'Buscando...' : 'Buscar'}
        </button>
      </form>

      {error && (
        <div className="callout warning mb24">
          <Icon name="warning" />
          <span>{error}</span>
        </div>
      )}

      {!submitted ? (
        <div className="empty">
          <Icon name="search" />
          <h3>Uma palavra pode ser o começo.</h3>
          <p>Experimente pesquisar por um termo que aparece no título ou no texto de uma nota.</p>
        </div>
      ) : loading ? (
        <div className="empty">
          <h3>Buscando...</h3>
        </div>
      ) : matches.length > 0 ? (
        <>
          <div className="small muted mb16">
            {matches.length} resultado(s) para "{submitted}"
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
          <p>Tente uma palavra menos específica. Notas arquivadas e na lixeira não entram nesta busca.</p>
        </div>
      )}
    </div>
  );
}
