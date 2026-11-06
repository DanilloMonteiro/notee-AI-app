// src/pages/Note.jsx
import React, { useState, useEffect, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';

export function Note() {
  const { id } = useParams();
  const [note, setNote] = useState(null);
  const [doubts, setDoubts] = useState([]);

  // Estados para gerir a edição do texto
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [saving, setSaving] = useState(false);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  // 1. Estado para a largura do painel (começa com 316px, como no HTML)
  const [asideWidth, setAsideWidth] = useState(316);
  const [isResizing, setIsResizing] = useState(false);
  const dragRef = useRef(null);

  const [showModal, setShowModal] = useState(false);
  const [selectedText, setSelectedText] = useState('');
  const [questionText, setQuestionText] = useState('');
  const [submittingDoubt, setSubmittingDoubt] = useState(false);

  // 2. Função que dispara quando você clica na barra divisória
  const handlePointerDown = e => {
    setIsResizing(true);
    dragRef.current = {
      startX: e.clientX,
      startWidth: asideWidth,
    };
    document.body.classList.add('pane-is-resizing'); // Aplica o cursor de setinhas globais
  };

  // 3. Efeito global para escutar o rato a mover-se mesmo se sair de cima da barra
  useEffect(() => {
    const handlePointerMove = e => {
      if (!isResizing) return;
      const deltaX = e.clientX - dragRef.current.startX;
      // Calcula a nova largura e limita entre 270px e 900px
      const newWidth = Math.max(270, Math.min(dragRef.current.startWidth - deltaX, 900));
      setAsideWidth(newWidth);
    };

    const handlePointerUp = () => {
      if (isResizing) {
        setIsResizing(false);
        document.body.classList.remove('pane-is-resizing');
      }
    };

    if (isResizing) {
      window.addEventListener('pointermove', handlePointerMove);
      window.addEventListener('pointerup', handlePointerUp);
    }

    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [isResizing]);

  // Lê o texto selecionado no ecrã e abre o modal
  const handleMarkDoubt = () => {
    // window.getSelection() pega o que está sublinhado/selecionado no navegador
    const text = window.getSelection().toString().trim();
    if (!text) {
      alert('Por favor, selecione um pedaço de texto na sua nota primeiro.');
      return;
    }

    setSelectedText(text);
    // Sugestão automática de pergunta
    setQuestionText(`O que significa "${text}" neste contexto?`);
    setShowModal(true);
  };

  // Envia a dúvida para o C#
  const handleSubmitDoubt = async e => {
    e.preventDefault();
    setSubmittingDoubt(true);

    try {
      // O POST que construímos no ResearchEndpoints.cs
      await apiFetch('/doubts', {
        method: 'POST',
        body: JSON.stringify({
          noteId: id,
          blockId: 'main-content', // No MVP usamos um bloco único
          exactQuote: selectedText,
          startIndex: 0,
          endIndex: 0,
          question: questionText,
        }),
      });

      setShowModal(false);
      // Recarrega as dúvidas para mostrar a nova com status "Pending"
      await loadNoteAndDoubts();
    } catch (err) {
      alert('Erro ao criar a dúvida. Verifique a consola.');
    } finally {
      setSubmittingDoubt(false);
    }
  };

  useEffect(() => {
    loadNoteAndDoubts();
  }, [id]);

  // Atualiza as dúvidas automaticamente se houver alguma a ser processada
  useEffect(() => {
    // Verifica se alguma dúvida ainda está pendente ou a rodar
    const hasPendingDoubts = doubts.some(d => d.status === 'Pending' || d.status === 'Running');

    let intervalId;
    if (hasPendingDoubts) {
      // Configura um "relógio" para ir buscar as dúvidas a cada 3 segundos
      intervalId = setInterval(async () => {
        try {
          const doubtsData = await apiFetch(`/doubts/note/${id}`);
          setDoubts(doubtsData || []);
        } catch (err) {
          console.error('Erro ao verificar atualização das dúvidas:', err);
        }
      }, 3000);
    }

    // Limpa o relógio para não consumir memória quando a IA responder ou você sair da página
    return () => {
      if (intervalId) clearInterval(intervalId);
    };
  }, [id, doubts]); // O efeito reavalia sempre que a lista de dúvidas muda

  const loadNoteAndDoubts = async () => {
    try {
      // 1. Carrega os dados da Nota
      const noteData = await apiFetch(`/notes/${id}`);
      setNote(noteData);
      setTitle(noteData.title || '');
      // O seu HTML usava blocos ricos, mas no MVP vamos mapear para um campo de texto contínuo
      setContent(noteData.content || '');

      // 2. Lembra-se da rota GET /doubts/note/{id} que criámos no C#? Vamos usá-la!
      const doubtsData = await apiFetch(`/doubts/note/${id}`);
      setDoubts(doubtsData || []);
    } catch (err) {
      setError('Não foi possível carregar os dados da nota.');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      // Chama o método PUT que você construiu no NoteEndpoints.cs
      await apiFetch(`/notas/${id}`, {
        method: 'PUT',
        body: JSON.stringify({
          expectedVersion: note.version || 0, // Proteção de concorrência que você configurou!
          title: title,
          content: content,
          categoryId: note.categoryId || null,
          tagIds: note.tags?.map(t => t.id) || [],
        }),
      });

      // Atualizamos a referência local para o botão de salvar voltar a ficar inativo
      setNote({ ...note, title, content });
      alert('Nota salva com sucesso!');
    } catch (err) {
      alert('Erro ao salvar a nota. Verifique se a versão conflitou.');
    } finally {
      setSaving(false);
    }
  };

  if (loading)
    return (
      <div className="empty">
        <h3>A carregar...</h3>
      </div>
    );
  if (error || !note)
    return (
      <div className="empty">
        <h3>{error}</h3>
        <Link className="btn mt16" to="/notes">
          Voltar
        </Link>
      </div>
    );

  // Só ativamos o botão de guardar se houver alterações
  const isDirty = title !== note.title || content !== (note.content || '');

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">NOTAS / {note.categoryName || 'Sem categoria'}</div>
          <h1>{title || 'Sem título'}</h1>
          <p>Nota • {note.status}</p>
        </div>
        <div className="row gap8">
          <Link to="/notes" className="btn">
            <Icon name="back" /> Voltar
          </Link>
          <button className={`btn ${isDirty ? 'primary' : ''}`} onClick={handleSave} disabled={saving || !isDirty}>
            <Icon name="save" className="sm" /> {saving ? 'A salvar...' : 'Salvar alterações'}
          </button>
        </div>
      </div>

      <div className="editor-layout" style={{ '--editor-aside-width': `${asideWidth}px` }}>
        <section className="card editor-main-card">
          <div className="toolbar">
            <div className="row gap8">
              <span className="pill green">Modo de Edição</span>
            </div>
            {/* LIGAMOS O ONCLICK AQUI: */}
            <button className="btn compact" onClick={handleMarkDoubt}>
              <Icon name="question" className="sm" /> Marcar como dúvida
            </button>
          </div>

          <div className="editor-body">
            {/* O Input agora é editável! */}
            <input
              className="editor-title"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Dê um título à sua ideia..."
              aria-label="Título da nota"
            />

            <div className="editor-meta">
              {note.tags &&
                note.tags.map(tag => (
                  <span key={tag.id} className="tag">
                    {tag.name}
                  </span>
                ))}
            </div>

            {/* Trocamos a div estática por uma textarea para você poder escrever livremente */}
            <textarea
              className="editor-block paragraph"
              style={{ width: '100%', minHeight: '350px', border: 'none', resize: 'vertical', outline: 'none' }}
              value={content}
              onChange={e => setContent(e.target.value)}
              placeholder="Escreva as suas ideias aqui..."
            />
          </div>
        </section>
        <div
          className={`pane-resizer editor-resizer ${isResizing ? 'is-resizing' : ''}`}
          onPointerDown={handlePointerDown}
        ></div>
        {/* O painel de IA ganha vida com os dados reais do Gemini! */}
        <aside className="stack editor-side">
          {doubts.length > 0 ? (
            <div className="result-panel">
              <div className="row between mb16">
                <h4>Dúvidas desta nota</h4>
                <span className="badge-count">{doubts.length}</span>
              </div>

              {doubts.map(doubt => (
                <div key={doubt.id} className="card research-card" style={{ marginBottom: '12px' }}>
                  <span className={`pill ${doubt.status === 'Completed' ? 'green' : 'blue'}`}>
                    {doubt.status === 'Completed' ? 'Respondida' : 'A processar...'}
                  </span>

                  <blockquote className="quote-selection" style={{ fontSize: '12px', marginTop: '13px' }}>
                    {doubt.exactQuote || 'Trecho não selecionado'}
                  </blockquote>

                  <h3 className="mt16">{doubt.question}</h3>
                  {doubt.status === 'Completed' ? (
                    <>
                      {/* Cortamos o texto para caber bem na barra lateral */}
                      <p
                        style={{
                          marginTop: '10px',
                          fontSize: '12px',
                          lineHeight: '1.6',
                          display: '-webkit-box',
                          WebkitLineClamp: '3',
                          WebkitBoxOrient: 'vertical',
                          overflow: 'hidden',
                        }}
                      >
                        {doubt.answer}
                      </p>
                      {/* 👇 O NOVO BOTÃO DE LIGAÇÃO */}
                      <Link to={`/research/${doubt.id}`} className="btn compact ghost mt16" style={{ width: '100%' }}>
                        Ler detalhe completo <Icon name="arrow" className="sm" />
                      </Link>
                    </>
                  ) : (
                    <p className="muted small mt16">A aguardar o Worker de segundo plano...</p>
                  )}
                </div>
              ))}
            </div>
          ) : (
            <div className="empty" style={{ padding: '24px' }}>
              <Icon name="spark" />
              <h3 style={{ fontSize: '14px', marginTop: '10px' }}>Assistente de curiosidade</h3>
              <p style={{ fontSize: '12px', marginTop: '5px' }}>
                Nenhuma dúvida registada ainda. Escreva algo, selecione o texto e marque como dúvida (implementaremos a
                seleção no próximo passo).
              </p>
            </div>
          )}
        </aside>
      </div>
      {/* MODAL DE CRIAÇÃO DE DÚVIDA */}
      {showModal && (
        <div className="modal-overlay">
          <section className="modal" role="dialog">
            <div className="modal-title">
              <h2>O que você quer entender?</h2>
              <button className="icon-btn" onClick={() => setShowModal(false)}>
                <Icon name="close" />
              </button>
            </div>

            <form onSubmit={handleSubmitDoubt}>
              <div className="quote-selection">{selectedText}</div>

              <div className="field">
                <label htmlFor="doubt-question">Pergunta (você pode refinar)</label>
                <textarea
                  id="doubt-question"
                  required
                  value={questionText}
                  onChange={e => setQuestionText(e.target.value)}
                  style={{ minHeight: '80px' }}
                />
              </div>

              <p className="small muted">
                O trecho fica preservado. A resposta será processada pela IA em segundo plano.
              </p>

              <div className="modal-actions mt24">
                <button type="button" className="btn" onClick={() => setShowModal(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn primary" disabled={submittingDoubt}>
                  <Icon name="question" className="sm" />
                  {submittingDoubt ? 'A enviar...' : 'Salvar dúvida'}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  );
}
