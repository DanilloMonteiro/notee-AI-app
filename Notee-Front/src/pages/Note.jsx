// src/pages/Note.jsx
import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';
import { useToast } from '../contexts/ToastContext';
import { NoteOrganizer } from '../components/NoteOrganizer';
import { RevisionHistory } from '../components/RevisionHistory';
import { PublishPanel } from '../features/publishing/PublishPanel';
import { NoteSuggestions } from '../components/NoteSuggestions';
import { STATUS_LABEL } from '../notes';

const generateId = () => 'b-' + Math.random().toString(36).substr(2, 9);

// --- HELPERS DE HIGHLIGHT -------------------------------------------------

const escapeHtml = s =>
  s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

// Encontra a ocorrência de `quote` mais próxima de `hint` (startIndex gravado na dúvida).
// Assim, se o mesmo trecho aparecer várias vezes, marcamos o que foi selecionado.
const findQuote = (text, quote, hint = 0) => {
  if (!quote) return -1;
  let best = -1;
  let idx = text.indexOf(quote);
  while (idx !== -1) {
    if (best === -1 || Math.abs(idx - hint) < Math.abs(best - hint)) best = idx;
    idx = text.indexOf(quote, idx + 1);
  }
  return best;
};

// Fonte única da verdade para "a citação ainda existe no bloco?"
const isQuotePresent = (text, quote) => !!quote && (text || '').includes(quote);

// Constrói o HTML a partir do texto CRU + intervalos (nunca faz replace sobre HTML já gerado,
// o que evitaria marcar texto dentro de atributos como style="...").
const buildHighlightedHtml = (text, blockDoubts) => {
  const ranges = blockDoubts
    .map(d => {
      const start = findQuote(text, d.exactQuote, d.startIndex || 0);
      return start === -1 ? null : { start, end: start + d.exactQuote.length };
    })
    .filter(Boolean)
    .sort((a, b) => a.start - b.start);

  // Junta intervalos sobrepostos para não gerar <mark> aninhados
  const merged = [];
  for (const r of ranges) {
    const last = merged[merged.length - 1];
    if (last && r.start <= last.end) last.end = Math.max(last.end, r.end);
    else merged.push({ ...r });
  }

  let html = '';
  let cursor = 0;
  for (const r of merged) {
    html += escapeHtml(text.slice(cursor, r.start));
    html += `<mark class="doubt-mark" style="${MARK_STYLE}">${escapeHtml(text.slice(r.start, r.end))}</mark>`;
    cursor = r.end;
  }
  return html + escapeHtml(text.slice(cursor));
};

// Sem padding: padding no <mark> faz o texto "saltar" quando o highlight é removido/reposto.
const MARK_STYLE = 'background-color:#dbeafe;color:#1e40af;border-radius:3px;';

// "plaintext-only" impede HTML colado e <div>/<b> gerados pelo browser.
// Fallback para browsers antigos que não o suportam.
const PLAINTEXT_SUPPORTED = (() => {
  try {
    const d = document.createElement('div');
    d.contentEditable = 'plaintext-only';
    return d.contentEditable === 'plaintext-only';
  } catch {
    return false;
  }
})();

// --- COMPONENTE DO EDITOR (contentEditable) -------------------------------
function ContentEditableBlock({ block, doubts, onChange, onSelect }) {
  const divRef = useRef(null);

  const blockDoubts = useMemo(
    () => doubts.filter(d => d.blockId === block.id && d.exactQuote),
    [doubts, block.id],
  );

  const paint = () => {
    if (divRef.current) divRef.current.innerHTML = buildHighlightedHtml(block.text || '', blockDoubts);
  };

  // Só reescreve o DOM quando o bloco NÃO está em foco -> nunca mexe no cursor durante a digitação.
  useEffect(() => {
    if (divRef.current && document.activeElement !== divRef.current) paint();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [block.text, blockDoubts]);

  // Durante a digitação: apenas o texto cru (sem <mark>) sobe para o estado React.
  const handleInput = e => onChange(e.currentTarget.innerText);

  // Ao sair do bloco: redesenha (repõe highlights restaurados / remove os quebrados).
  const handleBlur = () => paint();

  const handlePaste = e => {
    if (PLAINTEXT_SUPPORTED) return;
    e.preventDefault();
    document.execCommand('insertText', false, e.clipboardData.getData('text/plain'));
  };

  const handleSelect = () => {
    const el = divRef.current;
    const sel = window.getSelection();
    if (!el || !sel || sel.rangeCount === 0) return;

    if (sel.isCollapsed) {
      onSelect(null);
      return;
    }

    const range = sel.getRangeAt(0);
    // Ignora seleções que atravessam vários blocos
    if (!el.contains(range.startContainer) || !el.contains(range.endContainer)) return;

    const quote = sel.toString();
    if (!quote.trim()) return;

    const pre = document.createRange();
    pre.selectNodeContents(el);
    pre.setEnd(range.startContainer, range.startOffset);
    const startIndex = pre.toString().length;

    onSelect({ quote, startIndex, endIndex: startIndex + quote.length });
  };

  return (
    <div
      ref={divRef}
      contentEditable={PLAINTEXT_SUPPORTED ? 'plaintext-only' : true}
      suppressContentEditableWarning
      className={`editor-block ${block.type}`}
      onInput={handleInput}
      onBlur={handleBlur}
      onPaste={handlePaste}
      onMouseUp={handleSelect}
      onKeyUp={handleSelect}
      style={{
        width: '100%',
        border: 'none',
        outline: 'none',
        minHeight: block.type === 'heading' ? '40px' : '80px',
        fontFamily: block.type === 'code' ? 'monospace' : 'inherit',
        padding: block.type === 'code' || block.type === 'quote' ? '12px' : '4px',
        backgroundColor: block.type === 'code' ? '#f1f4ed' : 'transparent',
        borderLeft: block.type === 'quote' ? '3px solid #b8d794' : 'none',
        whiteSpace: 'pre-wrap',
      }}
      data-placeholder={`Escreva o seu ${block.type}...`}
    />
  );
}
// -------------------------------------------------------------

const keptStorageKey = noteId => `notee_kept_doubts_${noteId}`;

const loadKeptDoubts = noteId => {
  try {
    const raw = localStorage.getItem(keptStorageKey(noteId));
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

export function Note() {
  const { id } = useParams();
  const { addToast } = useToast();

  const [note, setNote] = useState(null);
  const [doubts, setDoubts] = useState([]);

  const [title, setTitle] = useState('');
  const [categoryId, setCategoryId] = useState(null);
  const [tagIds, setTagIds] = useState([]);
  const [showHistory, setShowHistory] = useState(false);
  const closeHistory = useCallback(() => setShowHistory(false), []);
  const [showPublish, setShowPublish] = useState(false);
  const closePublish = useCallback(() => setShowPublish(false), []);
  const [blocks, setBlocks] = useState([]);
  const [newBlockType, setNewBlockType] = useState('paragraph');

  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [asideWidth, setAsideWidth] = useState(316);
  const [isResizing, setIsResizing] = useState(false);
  const dragRef = useRef(null);

  // Estados da seleção
  const [showModal, setShowModal] = useState(false);
  const [selection, setSelection] = useState(null); // { blockId, quote, startIndex, endIndex }
  const [questionText, setQuestionText] = useState('');
  const [submittingDoubt, setSubmittingDoubt] = useState(false);

  // Dúvidas em que o utilizador escolheu "Manter mesmo assim" (persistidas por nota no browser)
  const [keptDoubts, setKeptDoubts] = useState(() => loadKeptDoubts(id));

  useEffect(() => {
    setKeptDoubts(loadKeptDoubts(id));
  }, [id]);

  const updateKeptDoubts = updater => {
    setKeptDoubts(prev => {
      const next = updater(prev);
      try {
        localStorage.setItem(keptStorageKey(id), JSON.stringify(next));
      } catch {
        /* storage indisponível: fica só em memória */
      }
      return next;
    });
  };

  const loadDoubts = async () => {
    const doubtsData = await apiFetch(`/doubts/note/${id}`);
    setDoubts(doubtsData || []);
  };

  // Aplica uma nota vinda da API ao estado do editor (carga inicial e restauração de revisão).
  const applyLoadedNote = noteData => {
    setNote(noteData);
    setTitle(noteData.title || '');
    setCategoryId(noteData.categoryId ?? null);
    setTagIds((noteData.tags || []).map(t => t.id));
    try {
      const parsedBlocks = JSON.parse(noteData.content || '[]');
      setBlocks(
        Array.isArray(parsedBlocks) && parsedBlocks.length > 0
          ? parsedBlocks
          : [{ id: generateId(), type: 'paragraph', text: noteData.content || '' }],
      );
    } catch {
      setBlocks([{ id: generateId(), type: 'paragraph', text: noteData.content || '' }]);
    }
  };

  const loadNoteAndDoubts = async () => {
    try {
      applyLoadedNote(await apiFetch(`/notes/${id}`));
      await loadDoubts();
    } catch (err) {
      setError('Não foi possível carregar os dados da nota.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadNoteAndDoubts();
  }, [id]);

  useEffect(() => {
    const hasPendingDoubts = doubts.some(d => d.status === 'Pending' || d.status === 'Running');
    let intervalId;
    if (hasPendingDoubts) {
      intervalId = setInterval(async () => {
        try {
          await loadDoubts();
        } catch (err) {
          console.error(err);
        }
      }, 3000);
    }
    return () => {
      if (intervalId) clearInterval(intervalId);
    };
  }, [id, doubts]);

  const handlePointerDown = e => {
    setIsResizing(true);
    dragRef.current = { startX: e.clientX, startWidth: asideWidth };
    document.body.classList.add('pane-is-resizing');
  };

  useEffect(() => {
    const handlePointerMove = e => {
      if (!isResizing) return;
      setAsideWidth(Math.max(270, Math.min(dragRef.current.startWidth - (e.clientX - dragRef.current.startX), 900)));
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

  const handleSave = async () => {
    setSaving(true);
    try {
      const payload = {
        expectedVersion: note.version || 0,
        title: title,
        content: JSON.stringify(blocks),
        categoryId: categoryId || null,
        tagIds,
      };
      // A API devolve a nota salva (com a nova versão e a categoria resolvida).
      const saved = await apiFetch(`/notes/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
      setNote(saved);
      addToast('Alterações salvas.', 'check');
    } catch (err) {
      addToast(
        err?.status === 409 ? 'A nota foi alterada em outro lugar. Copie seu texto e recarregue a página.' : err?.message || 'Falha ao salvar.',
        'warning',
      );
    } finally {
      setSaving(false);
    }
  };

  const handleAddBlock = () => setBlocks(prev => [...prev, { id: generateId(), type: newBlockType, text: '' }]);
  const handleUpdateBlock = (blockId, newText) =>
    setBlocks(prev => prev.map(b => (b.id === blockId ? { ...b, text: newText } : b)));

  const handleTextSelect = (blockId, sel) => setSelection(sel ? { blockId, ...sel } : null);

  const handleMarkDoubt = () => {
    if (!selection) return addToast('Selecione um pedaço de texto dentro do editor.', 'info');
    setQuestionText(`O que significa "${selection.quote}" neste contexto?`);
    setShowModal(true);
  };

  const handleSubmitDoubt = async e => {
    e.preventDefault();
    if (!selection) return;
    setSubmittingDoubt(true);
    try {
      await apiFetch('/doubts', {
        method: 'POST',
        body: JSON.stringify({
          noteId: id,
          question: questionText,
          blockId: selection.blockId,
          exactQuote: selection.quote,
          startIndex: selection.startIndex,
          endIndex: selection.endIndex,
        }),
      });
      setShowModal(false);
      setSelection(null);
      // Recarrega SÓ as dúvidas: recarregar a nota apagaria as edições ainda não salvas.
      await loadDoubts();
      addToast('Dúvida enviada.', 'spark');
    } catch (err) {
      addToast('Falha ao enviar.', 'info');
    } finally {
      setSubmittingDoubt(false);
    }
  };

  const handleDeleteDoubt = async doubtId => {
    try {
      await apiFetch(`/doubts/${doubtId}`, { method: 'DELETE' });
      setDoubts(prev => prev.filter(d => d.id !== doubtId));
      updateKeptDoubts(prev => prev.filter(k => k !== doubtId));
      addToast('Dúvida apagada.', 'check');
    } catch (err) {
      addToast('Erro ao apagar.', 'info');
    }
  };

  const handleKeepDoubt = doubtId => updateKeptDoubts(prev => (prev.includes(doubtId) ? prev : [...prev, doubtId]));

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

  const savedTagIds = (note.tags || []).map(t => t.id);
  const isDirty =
    title !== note.title ||
    JSON.stringify(blocks) !== (note.content || '[]') ||
    (categoryId || null) !== (note.categoryId || null) ||
    tagIds.length !== savedTagIds.length ||
    tagIds.some(t => !savedTagIds.includes(t));
  const isTrashed = note.status === 'Trashed';

  const handleLifecycle = async action => {
    if (isDirty) {
      addToast('Salve as alterações antes de mudar o estado da nota.', 'warning');
      return;
    }
    try {
      const updated = await apiFetch(`/notes/${id}/${action}`, {
        method: 'POST',
        body: JSON.stringify({ expectedVersion: note.version }),
      });
      setNote(updated);
      const labels = { archive: 'Nota arquivada.', trash: 'Nota movida para a lixeira.', restore: 'Nota restaurada.' };
      addToast(labels[action]);
    } catch (err) {
      addToast(err?.message || 'Não foi possível concluir a ação.', 'warning');
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">NOTAS / {note.categoryName || 'Sem categoria'}</div>
          <h1>{title || 'Sem título'}</h1>
          <p>Nota • {STATUS_LABEL[note.status] || note.status}</p>
        </div>
        <div className="row gap8 wrap">
          <Link to="/notes" className="btn">
            <Icon name="back" /> Voltar
          </Link>
          {note.status === 'Archived' || isTrashed ? (
            <button className="btn" onClick={() => handleLifecycle('restore')}>
              <Icon name="refresh" className="sm" /> Restaurar
            </button>
          ) : (
            <button className="btn" onClick={() => handleLifecycle('archive')}>
              <Icon name="archive" className="sm" /> Arquivar
            </button>
          )}
          <button className="btn" onClick={() => setShowPublish(true)}>
            <Icon name="globe" className="sm" /> Publicar
          </button>
          <button className="btn" onClick={() => setShowHistory(true)}>
            <Icon name="clock" className="sm" /> Histórico
          </button>
          {!isTrashed && (
            <button className="btn" onClick={() => handleLifecycle('trash')}>
              <Icon name="trash" className="sm" /> Lixeira
            </button>
          )}
          <button className={`btn ${isDirty ? 'primary' : ''}`} onClick={handleSave} disabled={saving || !isDirty || isTrashed}>
            <Icon name="save" className="sm" /> {saving ? 'A salvar...' : 'Salvar alterações'}
          </button>
        </div>
      </div>

      {isTrashed && (
        <div className="callout warning mb24">
          <Icon name="warning" />
          <span>Esta nota está na lixeira e não pode ser editada. Restaure-a para continuar escrevendo.</span>
        </div>
      )}

      {showPublish && (
        <PublishPanel noteId={id} noteTitle={note.title} isDirty={isDirty} disabled={isTrashed} onClose={closePublish} />
      )}

      {showHistory && (
        <RevisionHistory
          noteId={id}
          currentVersion={note.version}
          isDirty={isDirty}
          disabled={isTrashed}
          onClose={closeHistory}
          onRestored={(restored, fromVersion) => {
            setShowHistory(false);
            applyLoadedNote(restored);
            addToast(`Versão ${fromVersion} restaurada como v${restored.version}.`);
          }}
        />
      )}

      <NoteOrganizer
        categoryId={categoryId}
        tagIds={tagIds}
        onCategoryChange={setCategoryId}
        onTagsChange={setTagIds}
        disabled={isTrashed}
      />

      <NoteSuggestions noteId={id} isDirty={isDirty} disabled={isTrashed} onNoteChanged={loadNoteAndDoubts} />

      <div className="editor-layout" style={{ '--editor-aside-width': `${asideWidth}px` }}>
        <section className="card editor-main-card">
          <div className="toolbar">
            <div className="row gap8">
              <select
                value={newBlockType}
                onChange={e => setNewBlockType(e.target.value)}
                style={{ padding: '6px', borderRadius: '4px' }}
              >
                <option value="paragraph">Parágrafo</option>
                <option value="heading">Título</option>
                <option value="list">Lista</option>
                <option value="quote">Citação</option>
                <option value="code">Código</option>
              </select>
              <button className="icon-btn" onClick={handleAddBlock}>
                <Icon name="plus" />
              </button>
            </div>
            {/* onMouseDown preventDefault: não rouba o foco nem apaga a seleção do editor */}
            <button className="btn compact" onMouseDown={e => e.preventDefault()} onClick={handleMarkDoubt}>
              <Icon name="question" className="sm" /> Marcar dúvida no texto
            </button>
          </div>

          <div className="editor-body">
            <input
              className="editor-title"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Sem título"
            />
            <div className="editor-meta mb24">
              {note.tags &&
                note.tags.map(tag => (
                  <span key={tag.id} className="tag">
                    {tag.name}
                  </span>
                ))}
            </div>

            <div id="editor-blocks" style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {blocks.map(block => (
                <ContentEditableBlock
                  key={block.id}
                  block={block}
                  doubts={doubts}
                  onChange={newText => handleUpdateBlock(block.id, newText)}
                  onSelect={sel => handleTextSelect(block.id, sel)}
                />
              ))}
            </div>
          </div>
        </section>

        <div
          className={`pane-resizer editor-resizer ${isResizing ? 'is-resizing' : ''}`}
          onPointerDown={handlePointerDown}
        ></div>

        <aside className="stack editor-side">
          {doubts.length > 0 ? (
            <div className="result-panel">
              <div className="row between mb16">
                <h4>Dúvidas desta nota</h4>
                <span className="badge-count">{doubts.length}</span>
              </div>

              {doubts.map(d => {
                // Monitorização em tempo real: recalculada a cada tecla, porque `blocks` muda no onInput.
                const block = blocks.find(b => b.id === d.blockId);
                const isReferenceMissing = !!d.exactQuote && !isQuotePresent(block?.text, d.exactQuote);
                const showWarning = isReferenceMissing && !keptDoubts.includes(d.id);

                return (
                  <div
                    key={d.id}
                    className="card research-card"
                    style={{
                      marginBottom: '12px',
                      border: showWarning ? '2px solid #f59e0b' : '1px solid var(--border)',
                    }}
                  >
                    {showWarning ? (
                      <div>
                        <div className="row gap8 mb16">
                          <Icon name="warning" style={{ color: '#f59e0b' }} />
                          <h4 style={{ color: '#f59e0b', margin: 0 }}>Dúvida sem referência no texto</h4>
                        </div>
                        <p className="small muted">
                          O trecho <strong>"{d.exactQuote}"</strong> foi alterado ou apagado do texto original.
                        </p>
                        <div className="row gap8 mt16">
                          <button className="btn compact primary" onClick={() => handleDeleteDoubt(d.id)}>
                            <Icon name="trash" className="sm" /> Apagar
                          </button>
                          <button className="btn compact ghost" onClick={() => handleKeepDoubt(d.id)}>
                            Manter mesmo assim
                          </button>
                        </div>
                      </div>
                    ) : (
                      <>
                        <div className="row between mb16">
                          <span className={`pill ${d.status === 'Completed' ? 'green' : 'blue'}`}>
                            {d.status === 'Completed' ? 'Respondida' : 'A processar...'}
                          </span>
                          <button className="icon-btn" onClick={() => handleDeleteDoubt(d.id)} title="Apagar dúvida">
                            <Icon name="trash" className="sm" />
                          </button>
                        </div>

                        {d.exactQuote && (
                          <blockquote
                            className="quote-selection"
                            style={{ fontSize: '12px', opacity: isReferenceMissing ? 0.6 : 1 }}
                            title={isReferenceMissing ? 'Trecho já não existe no texto' : undefined}
                          >
                            {d.exactQuote}
                          </blockquote>
                        )}
                        <h3 className="mt16">{d.question}</h3>

                        {d.status === 'Completed' ? (
                          <>
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
                              {d.answer}
                            </p>
                            <Link to={`/research/${d.id}`} className="btn compact ghost mt16" style={{ width: '100%' }}>
                              Ler detalhe <Icon name="arrow" className="sm" />
                            </Link>
                          </>
                        ) : (
                          <p className="muted small mt16">A aguardar resposta...</p>
                        )}
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="empty" style={{ padding: '24px' }}>
              <Icon name="spark" />
              <p style={{ fontSize: '12px', marginTop: '10px' }}>
                Nenhuma dúvida. Selecione texto e marque uma dúvida.
              </p>
            </div>
          )}
        </aside>
      </div>

      {showModal && selection && (
        <div className="modal-overlay">
          <section className="modal" role="dialog">
            <div className="modal-title">
              <h2>O que você quer entender?</h2>
              <button className="icon-btn" onClick={() => setShowModal(false)}>
                <Icon name="close" />
              </button>
            </div>
            <form onSubmit={handleSubmitDoubt}>
              <div className="quote-selection">{selection.quote}</div>
              <div className="field">
                <label>Pergunta (você pode refinar)</label>
                <textarea
                  required
                  value={questionText}
                  onChange={e => setQuestionText(e.target.value)}
                  style={{ minHeight: '80px' }}
                />
              </div>
              <div className="modal-actions mt24">
                <button type="button" className="btn" onClick={() => setShowModal(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn primary" disabled={submittingDoubt}>
                  <Icon name="question" className="sm" /> {submittingDoubt ? 'A enviar...' : 'Salvar dúvida'}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  );
}
