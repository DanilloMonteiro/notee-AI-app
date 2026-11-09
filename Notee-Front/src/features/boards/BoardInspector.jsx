// Painel lateral do mapa: aba "Nota" (resumo, livros, fontes, dúvidas, conexões)
// e aba "IA e contexto" (ainda não implementada — nada é enviado a serviços externos).
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { useToast } from '../../contexts/ToastContext';
import { referencesApi } from './boardsApi';
import { QuestionModal, ReferenceModal } from './BoardModals';
import { apiFetch } from '../../api';
import { edgeName, errorMessage } from './graph';

const DOUBT_STATUS = {
  Pending: { label: 'Pendente', cls: 'amber' },
  Running: { label: 'A processar', cls: 'blue' },
  Completed: { label: 'Respondida', cls: 'green' },
  Failed: { label: 'Falhou', cls: 'red' },
  Cancelled: { label: 'Cancelada', cls: '' },
};

const isHttpUrl = url => /^https?:\/\//i.test(String(url || '').trim());

function ReferenceList({ items, kind, onRemove }) {
  const what = kind === 'book' ? 'livro' : 'fonte';
  if (!items.length) return <p className="tiny muted">Nenhum {what} cadastrado.</p>;
  return (
    <div className="graph-ref-list">
      {items.map(ref => (
        <div key={ref.id} className="graph-ref-item">
          <div className="grow">
            <strong>{ref.title}</strong>
            <span className="tiny muted">
              {kind === 'book' ? [ref.author, ref.year].filter(Boolean).join(' · ') : ref.publisher || 'Sem instituição informada'}
            </span>
            {isHttpUrl(ref.url) && (
              <a href={ref.url} target="_blank" rel="noopener noreferrer" className="graph-ref-url">
                Abrir {what} <Icon name="external" className="sm" />
              </a>
            )}
          </div>
          <button type="button" className="icon-btn" onClick={() => onRemove(ref)} aria-label={`Remover ${what}`}>
            <Icon name="trash" className="sm" />
          </button>
        </div>
      ))}
    </div>
  );
}

function NotePanel({ node, doubts, edges, nodesById, onSelectDoubt, onEditEdge, onConnectFrom, onReferencesChanged }) {
  const [refs, setRefs] = useState([]);
  const [loadingRefs, setLoadingRefs] = useState(true);
  const [refModal, setRefModal] = useState(null); // 'book' | 'source'
  const [questionOpen, setQuestionOpen] = useState(false);

  // AC09: dúvida livre ligada à nota, sem âncora (origem BoardQuestion).
  const addQuestion = async question => {
    try {
      await apiFetch(`/notes/${node.noteId}/doubts`, { method: 'POST', body: JSON.stringify({ question }) });
      setQuestionOpen(false);
      onReferencesChanged(); // recarrega o mapa (satélites e contagens)
      return true;
    } catch (err) {
      toastRef.current(errorMessage(err, 'Não foi possível criar a dúvida.'), 'warning');
      return false;
    }
  };
  const { addToast } = useToast();
  // addToast muda a cada render do provider; a ref evita recarregar (ou entrar em loop) por isso.
  const toastRef = useRef(addToast);
  useEffect(() => {
    toastRef.current = addToast;
  }, [addToast]);

  const loadRefs = useCallback(async () => {
    try {
      setRefs((await referencesApi.list(node.noteId)) || []);
    } catch (err) {
      toastRef.current(errorMessage(err, 'Não foi possível carregar as referências.'), 'warning');
    } finally {
      setLoadingRefs(false);
    }
  }, [node.noteId]);

  // O painel é montado com key={noteId}, então "loading" já começa verdadeiro para cada nota.
  useEffect(() => {
    loadRefs();
  }, [loadRefs]);

  const addRef = async values => {
    try {
      await referencesApi.create(node.noteId, values);
      setRefModal(null);
      await loadRefs();
      onReferencesChanged();
      return true;
    } catch (err) {
      addToast(errorMessage(err, 'Não foi possível adicionar.'), 'warning');
      return false;
    }
  };

  const removeRef = async ref => {
    if (!window.confirm(`Remover “${ref.title}” desta nota? Ela some de todos os mapas.`)) return;
    try {
      await referencesApi.remove(node.noteId, ref.id);
      await loadRefs();
      onReferencesChanged();
    } catch (err) {
      addToast(errorMessage(err, 'Não foi possível remover.'), 'warning');
    }
  };

  const books = refs.filter(r => r.kind === 'book');
  const sources = refs.filter(r => r.kind === 'source');
  const noteDoubts = doubts.filter(d => d.noteId === node.noteId);
  const neighbors = edges.filter(e => e.fromNoteId === node.noteId || e.toNoteId === node.noteId);

  return (
    <>
      <div className="graph-inspector-top">
        <span className="pill green">
          <Icon name="note" className="sm" /> Nota compartilhada
        </span>
        <Link to={`/note/${node.noteId}`} className="btn compact">
          Editor completo <Icon name="arrow" className="sm" />
        </Link>
      </div>
      <h3 className="graph-panel-title">{node.title}</h3>
      <div className="row wrap gap8">
        {node.tags.slice(0, 3).map(tag => (
          <span key={tag.id} className="tag">
            {tag.name}
          </span>
        ))}
      </div>

      <div className="graph-panel-section">
        <div className="overline">O QUE ESTÁ ESCRITO</div>
        <p className="graph-panel-preview">{node.excerpt || 'Sem texto por enquanto.'}</p>
      </div>

      <div className="graph-panel-section">
        <div className="row between">
          <h4>
            Livros <span className="pill">{books.length}</span>
          </h4>
          <button className="btn compact" onClick={() => setRefModal('book')}>
            <Icon name="plus" className="sm" /> Adicionar
          </button>
        </div>
        {loadingRefs ? <p className="tiny muted">A carregar...</p> : <ReferenceList items={books} kind="book" onRemove={removeRef} />}
      </div>

      <div className="graph-panel-section">
        <div className="row between">
          <h4>
            Fontes <span className="pill">{sources.length}</span>
          </h4>
          <button className="btn compact" onClick={() => setRefModal('source')}>
            <Icon name="plus" className="sm" /> Adicionar
          </button>
        </div>
        {loadingRefs ? <p className="tiny muted">A carregar...</p> : <ReferenceList items={sources} kind="source" onRemove={removeRef} />}
      </div>

      <div className="graph-panel-section">
        <div className="row between">
          <h4>
            Dúvidas <span className="pill">{noteDoubts.length}</span>
          </h4>
          <button className="btn compact" onClick={() => setQuestionOpen(true)}>
            <Icon name="plus" className="sm" /> Nova
          </button>
        </div>
        {noteDoubts.length ? (
          noteDoubts.map(d => (
            <button key={d.id} className="graph-list-question" onClick={() => onSelectDoubt(d.id)}>
              <Icon name="question" className="sm" />
              <span>{d.question}</span>
            </button>
          ))
        ) : (
          <p className="tiny muted">Nenhuma dúvida nesta nota.</p>
        )}
      </div>

      <div className="graph-panel-section">
        <h4>{neighbors.length} ligações neste mapa</h4>
        {neighbors.length ? (
          neighbors.map(edge => {
            const otherId = edge.fromNoteId === node.noteId ? edge.toNoteId : edge.fromNoteId;
            const outgoing = edge.fromNoteId === node.noteId;
            return (
              <button key={edge.id} className="graph-relation-item" onClick={() => onEditEdge(edge)}>
                {outgoing ? '→ ' : '← '}
                {nodesById.get(otherId)?.title || 'Nota indisponível'}
                <span className="tiny muted">{edgeName(edge)}</span>
              </button>
            );
          })
        ) : (
          <p className="tiny muted">Conecte esta nota a outra para formar uma relação.</p>
        )}
        <button className="btn compact" style={{ marginTop: 12 }} onClick={() => onConnectFrom(node.noteId)}>
          <Icon name="link" className="sm" /> Conectar a outra nota
        </button>
      </div>

      {refModal && <ReferenceModal kind={refModal} onSubmit={addRef} onClose={() => setRefModal(null)} />}
      {questionOpen && <QuestionModal noteTitle={node.title} onSubmit={addQuestion} onClose={() => setQuestionOpen(false)} />}
    </>
  );
}

function DoubtPanel({ doubt, note }) {
  const status = DOUBT_STATUS[doubt.status] || { label: doubt.status, cls: '' };
  return (
    <>
      <div className="graph-inspector-top">
        <span className="pill amber">
          <Icon name="question" className="sm" /> Dúvida vinculada
        </span>
        <Link to={`/research/${doubt.id}`} className="btn compact">
          Abrir pesquisa <Icon name="arrow" className="sm" />
        </Link>
      </div>
      <h3 className="graph-panel-title">{doubt.question}</h3>
      <p className="small muted">Nota: {note?.title || 'Nota indisponível'}</p>
      <div className="graph-panel-section">
        <span className={`pill ${status.cls}`}>{status.label}</span>
        <p className="small muted mt16">A dúvida pertence à nota e aparece em todos os mapas, sem duplicação.</p>
      </div>
    </>
  );
}

function AiPanel() {
  return (
    <>
      <div className="graph-ai-intro">
        <span className="graph-ai-icon">
          <Icon name="spark" />
        </span>
        <div>
          <h3>Assistente do mapa</h3>
          <p>Ideias propostas, nunca aplicadas sem sua aprovação.</p>
        </div>
      </div>
      <div className="callout neutral" style={{ fontSize: 12 }}>
        <Icon name="info" className="sm" />
        <span>
          Ainda não disponível nesta versão. A prévia de contexto e as sugestões com IA (MAP-04 e MAP-05) dependem de consentimento e de
          jobs duráveis. Nenhum dado do mapa é enviado a serviços externos.
        </span>
      </div>
    </>
  );
}

export function BoardInspector(props) {
  const { tab, setTab, selectedNode, selectedDoubt, nodesById } = props;

  let content;
  if (tab === 'ai') content = <AiPanel />;
  else if (selectedDoubt) content = <DoubtPanel doubt={selectedDoubt} note={nodesById.get(selectedDoubt.noteId)} />;
  else if (selectedNode) content = <NotePanel key={selectedNode.noteId} node={selectedNode} {...props} />;
  else
    content = (
      <div className="graph-inspector-empty">
        <Icon name="note" className="lg" />
        <h3>Uma ideia de cada vez.</h3>
        <p>Selecione uma nota no mapa para ver o resumo, referências e dúvidas.</p>
      </div>
    );

  return (
    <aside className="graph-side" id="graph-side-panel">
      <div className="graph-side-tabs" role="tablist">
        <button role="tab" aria-selected={tab === 'note'} className={tab === 'note' ? 'active' : ''} onClick={() => setTab('note')}>
          <Icon name="note" className="sm" /> Nota
        </button>
        <button role="tab" aria-selected={tab === 'ai'} className={tab === 'ai' ? 'active' : ''} onClick={() => setTab('ai')}>
          <Icon name="spark" className="sm" /> IA e contexto
        </button>
      </div>
      <div className="graph-side-scroll">{content}</div>
    </aside>
  );
}
