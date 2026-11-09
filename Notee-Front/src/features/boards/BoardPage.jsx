// Canvas de um mapa (/boards/:boardId): toolbar, superfície, painel lateral redimensionável.
import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { useToast } from '../../contexts/ToastContext';
import { boardsApi, createNote } from './boardsApi';
import { BoardCanvas } from './BoardCanvas';
import { BoardInspector } from './BoardInspector';
import { BoardFormModal, EdgeModal, NewNoteModal } from './BoardModals';
import { autoLayout, errorMessage, fitView, zoomAround } from './graph';

// Preferências visuais apenas (nunca dados de notas ou autorização) — UX v1.2.
const PREFS_KEY = 'notee-ui-layout-v1';
const ASIDE_MIN = 270;
const ASIDE_MAX = 950;

function loadPrefs() {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) || '{}');
    return {
      graphAsideWidth: Math.min(ASIDE_MAX, Math.max(ASIDE_MIN, Number(saved.graphAsideWidth) || 330)),
      graphAsideHidden: Boolean(saved.graphAsideHidden),
    };
  } catch {
    return { graphAsideWidth: 330, graphAsideHidden: false };
  }
}

function savePrefs(prefs) {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) || '{}');
    localStorage.setItem(PREFS_KEY, JSON.stringify({ ...saved, ...prefs }));
  } catch {
    // armazenamento opcional
  }
}

// key={boardId}: trocar de mapa remonta a view e zera seleção, modo de conexão e zoom.
export function BoardPage() {
  const { boardId } = useParams();
  return <BoardView key={boardId} boardId={boardId} />;
}

function BoardView({ boardId }) {
  const navigate = useNavigate();
  const { addToast } = useToast();
  const toastRef = useRef(addToast);
  useEffect(() => {
    toastRef.current = addToast;
  }, [addToast]);
  const toast = useCallback((msg, type) => toastRef.current(msg, type), []);

  const [board, setBoard] = useState(null);
  const [boards, setBoards] = useState([]);
  const [status, setStatus] = useState('loading'); // loading | ready | notfound | error
  const [errorText, setErrorText] = useState('');
  const [view, setView] = useState({ tx: 100, ty: 100, scale: 1 });
  const [fitPending, setFitPending] = useState(true);
  const [selectedNoteId, setSelectedNoteId] = useState(null);
  const [selectedDoubtId, setSelectedDoubtId] = useState(null);
  const [linkMode, setLinkMode] = useState(false);
  const [linkFromId, setLinkFromId] = useState(null);
  const [tab, setTab] = useState('note');
  const [modal, setModal] = useState(null); // { type: 'board' | 'note' | 'edge', ... }
  const [prefs, setPrefs] = useState(loadPrefs);
  const [resizing, setResizing] = useState(false);

  const surfaceRef = useRef(null);
  const layoutRef = useRef(null);
  // Versão de posição mais recente por nota (atualizada de forma síncrona, para encadear movimentos).
  const versionRef = useRef(new Map());
  const moveQueueRef = useRef(new Map());

  const applyBoard = useCallback(data => {
    versionRef.current = new Map(data.nodes.map(n => [n.noteId, n.positionVersion]));
    setBoard(data);
  }, []);

  const load = useCallback(
    async ({ fit = false } = {}) => {
      try {
        applyBoard(await boardsApi.get(boardId));
        setStatus('ready');
        if (fit) setFitPending(true);
      } catch (err) {
        setStatus(err?.status === 404 ? 'notfound' : 'error');
        setErrorText(errorMessage(err, 'Não foi possível carregar o mapa.'));
      }
    },
    [boardId, applyBoard],
  );

  const loadBoards = useCallback(() => {
    boardsApi
      .list()
      .then(list => setBoards(list || []))
      .catch(() => {});
  }, []);

  useEffect(() => {
    load({ fit: true });
    loadBoards();
  }, [load, loadBoards]);

  const doubtCountByNote = useMemo(() => {
    const map = new Map();
    for (const d of board?.doubts || []) map.set(d.noteId, (map.get(d.noteId) || 0) + 1);
    return map;
  }, [board]);

  const nodesById = useMemo(() => new Map((board?.nodes || []).map(n => [n.noteId, n])), [board]);

  const fitAll = useCallback(() => {
    if (!board || !surfaceRef.current) return;
    setView(fitView(board.nodes, doubtCountByNote, surfaceRef.current.getBoundingClientRect()));
  }, [board, doubtCountByNote]);

  useLayoutEffect(() => {
    if (fitPending && status === 'ready' && board && surfaceRef.current) {
      fitAll();
      setFitPending(false);
    }
  }, [fitPending, status, board, fitAll]);

  // Esc cancela o modo de conexão.
  useEffect(() => {
    const onKey = e => {
      if (e.key === 'Escape' && linkMode && !modal) {
        setLinkMode(false);
        setLinkFromId(null);
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [linkMode, modal]);

  const updatePrefs = next => {
    setPrefs(p => {
      const merged = { ...p, ...next };
      savePrefs(merged);
      return merged;
    });
  };

  // ---- Seleção e conexão ----
  const exitLinkMode = () => {
    setLinkMode(false);
    setLinkFromId(null);
  };

  const selectNote = noteId => {
    if (linkMode) {
      if (!linkFromId) {
        setLinkFromId(noteId);
        setSelectedNoteId(noteId);
      } else if (linkFromId === noteId) {
        toast('Escolha uma segunda nota para conectar.', 'info');
      } else {
        setModal({ type: 'edge', from: linkFromId, to: noteId });
      }
      return;
    }
    setSelectedNoteId(noteId);
    setSelectedDoubtId(null);
    setTab('note');
  };

  const selectDoubt = doubtId => {
    setSelectedDoubtId(doubtId);
    setTab('note');
  };

  const connectFrom = noteId => {
    setLinkMode(true);
    setLinkFromId(noteId);
    setSelectedNoteId(noteId);
    toast('Clique na nota de destino para criar a conexão.', 'info');
  };

  // ---- Mover cartão: otimista, um PUT por soltura, encadeado por nota ----
  const moveEnd = (noteId, x, y) => {
    setBoard(b => ({ ...b, nodes: b.nodes.map(n => (n.noteId === noteId ? { ...n, x, y } : n)) }));
    const previous = moveQueueRef.current.get(noteId) || Promise.resolve();
    const next = previous.then(async () => {
      try {
        const saved = await boardsApi.movePlacement(boardId, noteId, { x, y, expectedVersion: versionRef.current.get(noteId) ?? null });
        versionRef.current.set(noteId, saved.positionVersion);
        setBoard(b => ({
          ...b,
          nodes: b.nodes.map(n => (n.noteId === noteId ? { ...n, positionVersion: saved.positionVersion } : n)),
        }));
      } catch (err) {
        toast(errorMessage(err, 'Não foi possível salvar a posição.'), 'warning');
        load(); // recarrega o estado real (conflito 409 ou erro)
      }
    });
    moveQueueRef.current.set(noteId, next);
  };

  // ---- Comandos ----
  const reorganize = async () => {
    if (!board?.nodes.length) return;
    const placements = autoLayout(board.nodes, board.edges).map(p => ({
      ...p,
      expectedVersion: versionRef.current.get(p.noteId) ?? null,
    }));
    try {
      await boardsApi.movePlacements(boardId, placements);
      await load({ fit: true });
      toast('Mapa reorganizado. Os vínculos e o conteúdo das notas foram preservados.');
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível reorganizar.'), 'warning');
      load();
    }
  };

  const zoomBy = factor => {
    const rect = surfaceRef.current?.getBoundingClientRect();
    if (!rect) return;
    setView(v => zoomAround(v, v.scale * factor, rect.width / 2, rect.height / 2));
  };

  const saveBoard = async values => {
    try {
      await boardsApi.update(boardId, { ...values, expectedVersion: board.version });
      toast('Mapa atualizado.');
      setModal(null);
      load();
      loadBoards();
      return true;
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível salvar o mapa.'), 'warning');
      if (err?.status === 409) load();
      return false;
    }
  };

  const createBoard = async values => {
    try {
      const created = await boardsApi.create(values);
      setModal(null);
      toast('Mapa criado.');
      navigate(`/boards/${created.id}`);
      return true;
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível criar o mapa.'), 'warning');
      return false;
    }
  };

  const deleteBoard = async () => {
    if (!window.confirm(`Excluir o mapa “${board.title}”? As notas continuam em Minhas notas.`)) return;
    try {
      await boardsApi.remove(boardId);
      toast('Mapa excluído. Nenhuma nota foi apagada.');
      navigate('/boards');
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível excluir o mapa.'), 'warning');
    }
  };

  const saveNote = async values => {
    try {
      const note = await createNote(values);
      setModal(null);
      await load();
      setSelectedNoteId(note.id);
      setSelectedDoubtId(null);
      setTab('note');
      toast('Nota criada. Ela também aparece em Minhas notas e nos outros mapas.');
      return true;
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível criar a nota.'), 'warning');
      return false;
    }
  };

  const saveEdge = async values => {
    const editing = modal?.edge;
    try {
      if (editing) {
        await boardsApi.updateEdge(boardId, editing.id, { ...values, expectedVersion: editing.version });
        toast('Relação atualizada.');
      } else {
        await boardsApi.createEdge(boardId, values);
        toast('Conexão criada neste mapa.');
      }
      setModal(null);
      exitLinkMode();
      await load();
      return true;
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível salvar a conexão.'), 'warning');
      if (err?.status === 409 && editing) load();
      return false;
    }
  };

  const deleteEdge = async () => {
    const edge = modal?.edge;
    if (!edge) return;
    try {
      await boardsApi.removeEdge(boardId, edge.id);
      setModal(null);
      toast('Relação removida somente deste mapa.');
      await load();
    } catch (err) {
      toast(errorMessage(err, 'Não foi possível remover a relação.'), 'warning');
    }
  };

  const closeModal = () => {
    // Cancelar a criação de uma conexão também encerra o modo "Conectar".
    if (modal?.type === 'edge' && !modal.edge) exitLinkMode();
    setModal(null);
  };

  // ---- Divisor redimensionável do painel ----
  const onResizerPointerDown = e => {
    e.currentTarget.setPointerCapture(e.pointerId);
    setResizing(true);
  };
  const onResizerPointerMove = e => {
    if (!resizing || !layoutRef.current) return;
    const rect = layoutRef.current.getBoundingClientRect();
    const width = Math.min(ASIDE_MAX, Math.max(ASIDE_MIN, rect.right - e.clientX - 5));
    setPrefs(p => ({ ...p, graphAsideWidth: Math.round(width) }));
  };
  const onResizerPointerUp = () => {
    setResizing(false);
    savePrefs({ graphAsideWidth: prefs.graphAsideWidth });
  };
  const onResizerKeyDown = e => {
    const delta = e.key === 'ArrowLeft' ? 20 : e.key === 'ArrowRight' ? -20 : 0;
    if (!delta) return;
    e.preventDefault();
    updatePrefs({ graphAsideWidth: Math.min(ASIDE_MAX, Math.max(ASIDE_MIN, prefs.graphAsideWidth + delta)) });
  };

  // ---- Renderização ----
  if (status === 'loading' && !board) {
    return (
      <div className="empty">
        <h3>A carregar o mapa...</h3>
      </div>
    );
  }

  if (status === 'notfound' || (status === 'error' && !board)) {
    return (
      <div className="empty">
        <Icon name="map" />
        <h3>{status === 'notfound' ? 'Mapa não encontrado' : 'Não foi possível abrir o mapa'}</h3>
        <p>{status === 'notfound' ? 'Ele pode ter sido excluído ou não pertence à sua conta.' : errorText}</p>
        <Link className="btn primary" to="/boards">
          <Icon name="back" className="sm" /> Todos os mapas
        </Link>
      </div>
    );
  }

  const selectedNode = selectedNoteId ? nodesById.get(selectedNoteId) : null;
  const selectedDoubt = selectedDoubtId ? board.doubts.find(d => d.id === selectedDoubtId) : null;
  const linkSource = linkFromId ? nodesById.get(linkFromId) : null;
  const hidden = prefs.graphAsideHidden;

  return (
    <div className="graph-page">
      <div className="page-heading">
        <div>
          <div className="overline">SEU CONHECIMENTO / MAPAS</div>
          <h1>Ideias que se conectam.</h1>
          <p>Pense visualmente, explore relações e preserve cada nota em sua biblioteca.</p>
        </div>
        <div className="row gap8">
          <Link className="btn compact" to="/boards">
            <Icon name="back" className="sm" /> Todos os mapas
          </Link>
          <button className="btn compact" onClick={() => setModal({ type: 'newBoard' })}>
            <Icon name="plus" className="sm" /> Novo mapa
          </button>
        </div>
      </div>

      <div className="graph-board-head">
        <div>
          <div className="row gap8">
            <select
              className="graph-board-select"
              aria-label="Escolher mapa"
              value={boardId}
              onChange={e => navigate(`/boards/${e.target.value}`)}
            >
              {(boards.some(b => b.id === boardId) ? boards : [{ id: boardId, title: board.title }, ...boards]).map(b => (
                <option key={b.id} value={b.id}>
                  {b.id === boardId ? board.title : b.title}
                </option>
              ))}
            </select>
            <button className="icon-btn" onClick={() => setModal({ type: 'board' })} aria-label="Editar mapa" title="Editar mapa">
              <Icon name="settings" className="sm" />
            </button>
          </div>
          {board.description && <p className="small muted">{board.description}</p>}
        </div>
        <div className="row gap8 wrap">
          <span className="pill">{board.nodes.length} notas</span>
          <span className="pill">{board.edges.length} relações</span>
          <span className="pill amber">{board.doubts.length} dúvidas</span>
        </div>
      </div>

      <div className="graph-toolbar">
        <div className="row gap8 wrap">
          <button className="btn primary compact" onClick={() => setModal({ type: 'note' })}>
            <Icon name="plus" className="sm" /> Nova nota
          </button>
          <button
            className={`btn compact ${linkMode ? 'graph-tool-active' : ''}`}
            aria-pressed={linkMode}
            onClick={() => (linkMode ? exitLinkMode() : setLinkMode(true))}
            disabled={board.nodes.length < 2}
          >
            <Icon name="link" className="sm" /> Conectar
          </button>
          <button className="btn compact" onClick={reorganize} disabled={!board.nodes.length}>
            <Icon name="grid" className="sm" /> Reorganizar
          </button>
          <button className="btn compact" onClick={fitAll}>
            <Icon name="refresh" className="sm" /> Enquadrar tudo
          </button>
          {linkMode && (
            <span className="graph-connection-hint">
              {linkSource ? (
                <>
                  Conectando de <strong>{linkSource.title}</strong> · clique no destino
                </>
              ) : (
                'Clique na nota de origem · Esc cancela'
              )}
            </span>
          )}
        </div>
        <div className="row gap8">
          <button
            className="btn compact graph-toggle-side"
            aria-expanded={!hidden}
            aria-controls="graph-side-panel"
            onClick={() => updatePrefs({ graphAsideHidden: !hidden })}
            title="Mostrar ou ocultar painel de Nota e IA"
          >
            <Icon name={hidden ? 'panel-left-open' : 'panel-left-close'} className="sm" /> {hidden ? 'Mostrar painel' : 'Ocultar painel'}
          </button>
          <span className="tiny muted">{Math.round(view.scale * 100)}%</span>
          <button className="icon-btn graph-zoom-btn" onClick={() => zoomBy(1 / 1.15)} title="Reduzir zoom" aria-label="Reduzir zoom">
            −
          </button>
          <button className="icon-btn graph-zoom-btn" onClick={() => zoomBy(1.15)} title="Ampliar zoom" aria-label="Ampliar zoom">
            +
          </button>
        </div>
      </div>

      <div
        ref={layoutRef}
        className={`graph-layout ${hidden ? 'is-side-hidden' : ''}`}
        style={{ '--graph-aside-width': `${prefs.graphAsideWidth}px` }}
      >
        {board.nodes.length === 0 ? (
          <section className="graph-surface" ref={surfaceRef} style={{ display: 'grid', placeItems: 'center' }}>
            <div className="empty" style={{ background: 'transparent', border: 0 }}>
              <Icon name="note" />
              <h3>Nenhuma nota disponível</h3>
              <p>Crie uma nota para começar a conectar ideias. Notas arquivadas não aparecem nos mapas.</p>
              <button className="btn primary" onClick={() => setModal({ type: 'note' })}>
                <Icon name="plus" className="sm" /> Nova nota
              </button>
            </div>
          </section>
        ) : (
          <BoardCanvas
            surfaceRef={surfaceRef}
            nodes={board.nodes}
            edges={board.edges}
            doubts={board.doubts}
            view={view}
            setView={setView}
            selectedNoteId={selectedNoteId}
            selectedDoubtId={selectedDoubtId}
            linkFromId={linkFromId}
            onSelectNote={selectNote}
            onSelectDoubt={selectDoubt}
            onEditEdge={edge => setModal({ type: 'edge', edge })}
            onMoveEnd={moveEnd}
          />
        )}

        <div
          className={`pane-resizer graph-resizer ${resizing ? 'is-resizing' : ''}`}
          tabIndex={0}
          role="separator"
          aria-orientation="vertical"
          aria-label="Redimensionar painel de Nota e IA"
          aria-valuemin={ASIDE_MIN}
          aria-valuemax={ASIDE_MAX}
          aria-valuenow={prefs.graphAsideWidth}
          title="Arraste para ajustar ou use as teclas de seta"
          onPointerDown={onResizerPointerDown}
          onPointerMove={onResizerPointerMove}
          onPointerUp={onResizerPointerUp}
          onKeyDown={onResizerKeyDown}
        />

        <BoardInspector
          tab={tab}
          setTab={setTab}
          selectedNode={selectedNode}
          selectedDoubt={selectedDoubt}
          nodesById={nodesById}
          doubts={board.doubts}
          edges={board.edges}
          onSelectDoubt={selectDoubt}
          onEditEdge={edge => setModal({ type: 'edge', edge })}
          onConnectFrom={connectFrom}
          onReferencesChanged={() => load()}
        />
      </div>

      <div className="graph-bottom-note">
        <Icon name="shield" className="sm" /> Mapa privado da sua conta. Posições e conexões são salvas na API; a IA não é consultada neste
        mapa.
        <span>As notas de “Minhas notas” são as mesmas notas deste mapa.</span>
      </div>

      {modal?.type === 'board' && <BoardFormModal board={board} onSubmit={saveBoard} onDelete={deleteBoard} onClose={closeModal} />}
      {modal?.type === 'newBoard' && <BoardFormModal board={null} onSubmit={createBoard} onClose={closeModal} />}
      {modal?.type === 'note' && <NewNoteModal onSubmit={saveNote} onClose={closeModal} />}
      {modal?.type === 'edge' && (
        <EdgeModal
          nodes={board.nodes}
          edge={modal.edge}
          from={modal.from}
          to={modal.to}
          onSubmit={saveEdge}
          onDelete={modal.edge ? deleteEdge : null}
          onClose={closeModal}
        />
      )}
    </div>
  );
}
