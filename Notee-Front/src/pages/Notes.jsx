// src/pages/Notes.jsx
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';
import { NoteCard } from '../components/NoteCard';

export function Notes() {
  const [notes, setNotes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false); // <-- Estado para o botão de criar

  const navigate = useNavigate(); // <-- 2. Inicializar o hook de navegação

  // O useEffect com array vazio [] roda apenas uma vez, ao montar a página
  useEffect(() => {
    loadNotes();
  }, []);

  const loadNotes = async () => {
    try {
      // Chama o endpoint GET /notas que configurámos no C#
      const response = await apiFetch('/notes?page=1&pageSize=50');
      // O C# retorna uma lista (Array). Guardamo-la no state.
      setNotes(response || []);
    } catch (err) {
      setError('Não foi possível carregar as suas notas. A API C# está a correr?');
    } finally {
      setLoading(false);
    }
  };

  // 👇 3. A NOVA FUNÇÃO PARA CRIAR NOTAS
  const handleCreateNote = async () => {
    setCreating(true);
    try {
      // Faz o POST para o C# criar uma nota em branco
      const newNote = await apiFetch('/notes', {
        method: 'POST',
        body: JSON.stringify({
          title: 'Uma nova ideia',
          content: 'Comece a escrever por aqui...',
          categoryId: null,
          tagIds: [],
        }),
      });

      // Se o C# devolver a nova nota com o ID, navegamos diretamente para o editor dela!
      if (newNote && newNote.id) {
        navigate(`/note/${newNote.id}`);
      }
    } catch (err) {
      alert('Erro ao criar a nota. Verifique se o endpoint POST /notas está configurado corretamente.');
      setCreating(false);
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">MEU ESPAÇO</div>
          <h1>Minhas notas</h1>
          <p>Pequenas anotações, perguntas importantes e ideias em construção.</p>
        </div>
        <div className="row gap8">
          {/* 👇 4. LIGAR A FUNÇÃO AO BOTÃO */}
          <button className="btn primary" onClick={handleCreateNote} disabled={creating}>
            <Icon name="plus" /> {creating ? 'A criar...' : 'Nova nota'}
          </button>
        </div>
      </div>

      <div className="filters">
        <input type="text" placeholder="Filtrar notas..." aria-label="Filtrar notas" />
        <span className="grow"></span>
        <span className="small muted">{notes.length} notas</span>
      </div>

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
        <div className="grid three">
          {notes.map(note => (
            <NoteCard key={note.id} note={note} />
          ))}
        </div>
      ) : (
        <div className="empty">
          <Icon name="search" />
          <h3>Nenhuma nota encontrada</h3>
          <p>Crie a sua primeira nota para começar.</p>
        </div>
      )}
    </div>
  );
}
