// Modais do módulo de mapas: mapa, nova nota, conexão e referência.
import React, { useEffect, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { RELATION_TYPES } from './graph';

export function Modal({ title, onClose, children }) {
  const ref = useRef(null);
  const closeRef = useRef(onClose);
  useEffect(() => {
    closeRef.current = onClose;
  }, [onClose]);

  // Foco inicial e Esc registrados uma única vez (não refocar a cada render do pai).
  useEffect(() => {
    const onKey = e => e.key === 'Escape' && closeRef.current();
    document.addEventListener('keydown', onKey);
    ref.current?.querySelector('input, select, textarea')?.focus();
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className="modal-overlay" onMouseDown={e => e.target === e.currentTarget && onClose()}>
      <section className="modal" role="dialog" aria-modal="true" aria-label={title} ref={ref}>
        <div className="modal-title">
          <h2>{title}</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Fechar">
            <Icon name="close" />
          </button>
        </div>
        {children}
      </section>
    </div>
  );
}

function Actions({ busy, submitLabel, onClose, left }) {
  return (
    <div className="modal-actions">
      {left}
      <span className="grow" />
      <button type="button" className="btn" onClick={onClose}>
        Cancelar
      </button>
      <button type="submit" className="btn primary" disabled={busy}>
        {busy ? 'Salvando...' : submitLabel}
      </button>
    </div>
  );
}

export function BoardFormModal({ board, onSubmit, onDelete, onClose }) {
  const [title, setTitle] = useState(board?.title || '');
  const [description, setDescription] = useState(board?.description || '');
  const [busy, setBusy] = useState(false);

  const submit = async e => {
    e.preventDefault();
    setBusy(true);
    const done = await onSubmit({ title: title.trim(), description: description.trim() || null });
    if (!done) setBusy(false);
  };

  return (
    <Modal title={board ? 'Editar mapa' : 'Novo mapa de ideias'} onClose={onClose}>
      <form onSubmit={submit}>
        <div className="field">
          <label htmlFor="board-title">Nome do mapa</label>
          <input id="board-title" type="text" required maxLength={80} value={title} onChange={e => setTitle(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="board-description">Descrição</label>
          <textarea id="board-description" maxLength={240} value={description} onChange={e => setDescription(e.target.value)} />
        </div>
        <div className="callout neutral">
          <Icon name="info" className="sm" />
          <span>
            Todas as suas notas não arquivadas ficam disponíveis neste mapa. As posições e conexões são independentes dos outros mapas.
          </span>
        </div>
        <Actions
          busy={busy}
          submitLabel="Salvar mapa"
          onClose={onClose}
          left={
            board && onDelete ? (
              <button type="button" className="btn danger compact" onClick={onDelete}>
                Excluir mapa
              </button>
            ) : null
          }
        />
      </form>
    </Modal>
  );
}

export function NewNoteModal({ onSubmit, onClose }) {
  const [title, setTitle] = useState('');
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async e => {
    e.preventDefault();
    setBusy(true);
    const done = await onSubmit({ title: title.trim(), text: text.trim() });
    if (!done) setBusy(false);
  };

  return (
    <Modal title="Criar nota no mapa" onClose={onClose}>
      <form onSubmit={submit}>
        <div className="field">
          <label htmlFor="map-note-title">Título</label>
          <input
            id="map-note-title"
            maxLength={100}
            required
            placeholder="Qual ideia quer explorar?"
            value={title}
            onChange={e => setTitle(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="map-note-text">Primeiro pensamento</label>
          <textarea
            id="map-note-text"
            placeholder="Escreva uma ideia, observação ou pergunta..."
            value={text}
            onChange={e => setText(e.target.value)}
          />
        </div>
        <div className="callout neutral">
          <Icon name="note" className="sm" />
          <span>Esta será uma nota do Notee e também aparecerá em “Minhas notas” e nos outros mapas.</span>
        </div>
        <Actions busy={busy} submitLabel="Criar nota" onClose={onClose} />
      </form>
    </Modal>
  );
}

export function EdgeModal({ nodes, edge, from, to, onSubmit, onDelete, onClose }) {
  const [fromNoteId, setFrom] = useState(edge?.fromNoteId || from || nodes[0]?.noteId || '');
  const [toNoteId, setTo] = useState(edge?.toNoteId || to || nodes[1]?.noteId || '');
  const [type, setType] = useState(edge?.type || 'related');
  const [label, setLabel] = useState(edge?.label || '');
  const [busy, setBusy] = useState(false);
  const same = fromNoteId === toNoteId;

  const submit = async e => {
    e.preventDefault();
    if (same) return;
    setBusy(true);
    const done = await onSubmit({ fromNoteId, toNoteId, type, label: label.trim() || null });
    if (!done) setBusy(false);
  };

  const options = nodes.map(n => (
    <option key={n.noteId} value={n.noteId}>
      {n.title}
    </option>
  ));

  return (
    <Modal title={edge ? 'Editar relação' : 'Conectar duas notas'} onClose={onClose}>
      <form onSubmit={submit}>
        <p className="small muted mb16">
          As ligações representam a forma como <strong>você</strong> relaciona seus conceitos. Elas são do autor, não fatos verificados.
        </p>
        <div className="field">
          <label htmlFor="edge-from">Origem</label>
          <select id="edge-from" required value={fromNoteId} onChange={e => setFrom(e.target.value)}>
            {options}
          </select>
        </div>
        <div className="field">
          <label htmlFor="edge-to">Destino</label>
          <select id="edge-to" required value={toNoteId} onChange={e => setTo(e.target.value)}>
            {options}
          </select>
        </div>
        {same && <p className="tiny" style={{ color: '#a6452f' }}>Escolha notas diferentes para origem e destino.</p>}
        <div className="field">
          <label htmlFor="edge-type">Tipo de relação</label>
          <select id="edge-type" value={type} onChange={e => setType(e.target.value)}>
            {Object.entries(RELATION_TYPES).map(([value, text]) => (
              <option key={value} value={value}>
                {text}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="edge-label">Rótulo personalizado (opcional)</label>
          <input
            id="edge-label"
            maxLength={80}
            placeholder="Ex.: debate conceitual"
            value={label}
            onChange={e => setLabel(e.target.value)}
          />
        </div>
        <div className="callout neutral">
          <Icon name="info" className="sm" />
          <span>“Sem rótulo” deixa apenas a linha. Um texto personalizado prevalece sobre o tipo na visualização, mas o tipo permanece estruturado.</span>
        </div>
        <Actions
          busy={busy}
          submitLabel={edge ? 'Salvar conexão' : 'Criar conexão'}
          onClose={onClose}
          left={
            edge && onDelete ? (
              <button type="button" className="btn danger" onClick={onDelete}>
                Remover relação
              </button>
            ) : null
          }
        />
      </form>
    </Modal>
  );
}

export function ReferenceModal({ kind, onSubmit, onClose }) {
  const book = kind === 'book';
  const [title, setTitle] = useState('');
  const [person, setPerson] = useState('');
  const [year, setYear] = useState('');
  const [url, setUrl] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async e => {
    e.preventDefault();
    setBusy(true);
    const done = await onSubmit({
      kind,
      title: title.trim(),
      author: book ? person.trim() || null : null,
      publisher: book ? null : person.trim() || null,
      year: book ? year.trim() || null : null,
      url: url.trim() || null,
    });
    if (!done) setBusy(false);
  };

  return (
    <Modal title={book ? 'Adicionar livro' : 'Adicionar fonte'} onClose={onClose}>
      <form onSubmit={submit}>
        <div className="field">
          <label htmlFor="ref-title">Título</label>
          <input id="ref-title" maxLength={220} required value={title} onChange={e => setTitle(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="ref-person">{book ? 'Autor' : 'Editora, instituição ou veículo'}</label>
          <input id="ref-person" maxLength={140} value={person} onChange={e => setPerson(e.target.value)} />
        </div>
        {book && (
          <div className="field">
            <label htmlFor="ref-year">Ano (opcional)</label>
            <input id="ref-year" maxLength={15} value={year} onChange={e => setYear(e.target.value)} />
          </div>
        )}
        <div className="field">
          <label htmlFor="ref-url">Link (opcional)</label>
          <input id="ref-url" type="url" maxLength={2048} placeholder="https://" value={url} onChange={e => setUrl(e.target.value)} />
        </div>
        <div className="callout neutral">
          <Icon name="info" className="sm" />
          <span>A referência pertence à nota e aparece em todos os mapas. É uma indicação sua, não uma fonte verificada pela pesquisa.</span>
        </div>
        <Actions busy={busy} submitLabel="Adicionar" onClose={onClose} />
      </form>
    </Modal>
  );
}

export function QuestionModal({ noteTitle, onSubmit, onClose }) {
  const [question, setQuestion] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async e => {
    e.preventDefault();
    setBusy(true);
    const done = await onSubmit(question.trim());
    if (!done) setBusy(false);
  };

  return (
    <Modal title="Nova dúvida no mapa" onClose={onClose}>
      <form onSubmit={submit}>
        <p className="small muted mb16">
          A dúvida fica ligada à nota <strong>{noteTitle}</strong> e aparece em todos os mapas, sem trecho selecionado.
        </p>
        <div className="field">
          <label htmlFor="board-question">Pergunta</label>
          <textarea
            id="board-question"
            required
            maxLength={500}
            placeholder="O que você quer entender melhor?"
            value={question}
            onChange={e => setQuestion(e.target.value)}
          />
        </div>
        <div className="callout neutral">
          <Icon name="info" className="sm" />
          <span>A pesquisa usa o provedor permitido nas suas Configurações; sem autorização, a resposta é simulada e local.</span>
        </div>
        <Actions busy={busy} submitLabel="Criar dúvida" onClose={onClose} />
      </form>
    </Modal>
  );
}
