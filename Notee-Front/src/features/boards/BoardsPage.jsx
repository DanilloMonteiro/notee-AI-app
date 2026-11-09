// Catálogo de mapas de ideias (/boards).
import React, { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { useToast } from '../../contexts/ToastContext';
import { boardsApi } from './boardsApi';
import { BoardFormModal } from './BoardModals';
import { errorMessage, formatDate } from './graph';

export function BoardsPage() {
  const [boards, setBoards] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [modal, setModal] = useState(null); // { board } | { board: null }
  const { addToast } = useToast();
  const navigate = useNavigate();

  const load = useCallback(async () => {
    try {
      setBoards((await boardsApi.list()) || []);
      setError('');
    } catch (err) {
      setError(errorMessage(err, 'Não foi possível carregar seus mapas. A API está em execução?'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const save = async values => {
    const editing = modal?.board;
    try {
      if (editing) {
        await boardsApi.update(editing.id, { ...values, expectedVersion: editing.version });
        addToast('Mapa atualizado.');
        setModal(null);
        load();
      } else {
        const created = await boardsApi.create(values);
        addToast('Mapa criado.');
        navigate(`/boards/${created.id}`);
      }
      return true;
    } catch (err) {
      addToast(errorMessage(err, 'Não foi possível salvar o mapa.'), 'warning');
      if (err?.status === 409) load();
      return false;
    }
  };

  const remove = async () => {
    const board = modal?.board;
    if (!board || !window.confirm(`Excluir o mapa “${board.title}”? As notas continuam em Minhas notas.`)) return;
    try {
      await boardsApi.remove(board.id);
      addToast('Mapa excluído. Nenhuma nota foi apagada.');
      setModal(null);
      load();
    } catch (err) {
      addToast(errorMessage(err, 'Não foi possível excluir o mapa.'), 'warning');
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">SEU CONHECIMENTO / MAPAS</div>
          <h1>Seus mapas de ideias</h1>
          <p>Escolha um espaço visual para desenvolver, conectar e revisitar seus raciocínios.</p>
        </div>
        <div className="row gap8">
          <button className="btn primary" onClick={() => setModal({ board: null })}>
            <Icon name="plus" className="sm" /> Novo mapa
          </button>
        </div>
      </div>

      <div className="graph-catalog-intro">
        <Icon name="link" className="sm" />
        <span>
          Cada mapa usa as notas de <strong>Minhas notas</strong>. O conteúdo é compartilhado; as posições e conexões são próprias de cada
          mapa.
        </span>
      </div>

      {error && (
        <div className="callout warning mb24">
          <Icon name="warning" />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="empty">
          <h3>A carregar...</h3>
        </div>
      ) : boards.length === 0 && !error ? (
        <div className="empty">
          <Icon name="map" />
          <h3>Nenhum mapa ainda</h3>
          <p>Crie um mapa para organizar visualmente suas notas, sem duplicar conteúdo.</p>
          <button className="btn primary" onClick={() => setModal({ board: null })}>
            <Icon name="plus" className="sm" /> Criar primeiro mapa
          </button>
        </div>
      ) : (
        <div className="graph-catalog-grid">
          {boards.map(board => (
            <article key={board.id} className="card graph-catalog-card">
              <Link className="graph-catalog-main" to={`/boards/${board.id}`} aria-label={`Abrir mapa ${board.title}`}>
                <div className="graph-catalog-illustration" aria-hidden="true">
                  <span className="graph-catalog-node node-one" />
                  <span className="graph-catalog-node node-two" />
                  <span className="graph-catalog-node node-three" />
                  <span className="graph-catalog-node node-four" />
                  <svg viewBox="0 0 300 88" preserveAspectRatio="none">
                    <path d="M73 41L140 23L213 46M140 23L259 20" fill="none" stroke="#93b08c" strokeWidth="1.7" strokeDasharray="4 4" />
                  </svg>
                </div>
                <div className="graph-catalog-meta">
                  <span className="pill green">
                    <Icon name="link" className="sm" /> Mapa de ideias
                  </span>
                  <span className="small muted">{formatDate(board.updatedAt)}</span>
                </div>
                <h2>{board.title}</h2>
                <p>{board.description || 'Um espaço para conectar ideias e explorar assuntos.'}</p>
                <div className="graph-catalog-footer">
                  <span>
                    <Icon name="note" className="sm" /> {board.noteCount} notas
                  </span>
                  <span>
                    <Icon name="link" className="sm" /> {board.edgeCount} conexões
                  </span>
                </div>
              </Link>
              <div className="graph-catalog-actions">
                <Link className="btn primary compact" to={`/boards/${board.id}`}>
                  Abrir mapa <Icon name="arrow" className="sm" />
                </Link>
                <button className="btn ghost compact" onClick={() => setModal({ board })} aria-label={`Editar mapa ${board.title}`}>
                  <Icon name="settings" className="sm" /> Editar
                </button>
              </div>
            </article>
          ))}
        </div>
      )}

      {modal && <BoardFormModal board={modal.board} onSubmit={save} onDelete={remove} onClose={() => setModal(null)} />}
    </div>
  );
}
