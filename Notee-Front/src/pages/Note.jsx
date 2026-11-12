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
import { ChartView } from '../features/charts/ChartView';
import { ChartEditor } from '../features/charts/ChartEditor';
import { CHART_LIMITS, createEmptyChart, isChartBlock } from '../features/charts/chartModel';
import { splitPastedText } from '../features/charts/detectTables';
import { cleanBlock, hasStructure, organizeText } from '../features/notes/organizeText';
import { OrganizeAiModal } from '../features/notes/OrganizeAiModal';

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
function ContentEditableBlock({ block, doubts, onChange, onSelect, onPasteTables, onPasteStructured, onRemoveEmpty }) {
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
    const pasted = e.clipboardData?.getData('text/plain') || '';
    // Tabelas numéricas no texto colado viram gráficos (exceto em blocos de código).
    if (onPasteTables && block.type !== 'code') {
      const segments = splitPastedText(pasted);
      if (segments.some(s => s.type === 'chart')) {
        e.preventDefault();
        const el = divRef.current;
        const sel = window.getSelection();
        let start = (block.text || '').length;
        let end = start;
        if (el && sel && sel.rangeCount > 0 && el.contains(sel.getRangeAt(0).startContainer)) {
          const range = sel.getRangeAt(0);
          const pre = document.createRange();
          pre.selectNodeContents(el);
          pre.setEnd(range.startContainer, range.startOffset);
          start = pre.toString().length;
          end = start + range.toString().length;
        }
        onPasteTables({ start, end, pasted, segments });
        el?.blur();
        return;
      }
      // Sem tabelas, mas com títulos/listas/negritos: cola normalmente e oferece "Organizar em tópicos".
      if (onPasteStructured && hasStructure(pasted)) onPasteStructured();
    }
    if (PLAINTEXT_SUPPORTED) return;
    e.preventDefault();
    document.execCommand('insertText', false, pasted);
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

  // Backspace num bloco vazio remove o bloco (evita faixas em branco na nota).
  const handleKeyDown = e => {
    if (e.key !== 'Backspace' || !onRemoveEmpty) return;
    const text = (divRef.current?.innerText || '').replace(/\n/g, '');
    if (text === '') {
      e.preventDefault();
      onRemoveEmpty();
    }
  };

  return (
    <div
      ref={divRef}
      data-block-id={block.id}
      contentEditable={PLAINTEXT_SUPPORTED ? 'plaintext-only' : true}
      suppressContentEditableWarning
      className={`editor-block ${block.type}${block.type === 'heading' ? ` level-${block.level || 2}` : ''}`}
      onInput={handleInput}
      onBlur={handleBlur}
      onPaste={handlePaste}
      onKeyDown={handleKeyDown}
      onMouseUp={handleSelect}
      onKeyUp={handleSelect}
      style={{
        width: '100%',
        border: 'none',
        outline: 'none',
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
  // Gráfico em edição: { id, isNew } — cancelar um gráfico recém-criado o remove.
  const [editingChart, setEditingChart] = useState(null);
  // Aviso com "Desfazer": { message, restore: { blocks, title } | null, offerOrganize } (colagem, organizar, IA).
  const [notice, setNotice] = useState(null);
  const [showOrganizeAi, setShowOrganizeAi] = useState(false);

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
    setEditingChart(null);
    setNotice(null);
    try {
      const parsedBlocks = JSON.parse(noteData.content || '[]');
      setBlocks(
        Array.isArray(parsedBlocks) && parsedBlocks.length > 0
          ? parsedBlocks
          : [{ id: generateId(), type: 'paragraph', text: Array.isArray(parsedBlocks) ? '' : noteData.content || '' }],
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
    if (editingChart) {
      addToast('Conclua ou cancele a edição do gráfico antes de salvar.', 'info');
      return;
    }
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
      setNotice(null);
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

  const handleAddBlock = () => {
    if (newBlockType !== 'chart') {
      setBlocks(prev => [...prev, { id: generateId(), type: newBlockType, text: '' }]);
      return;
    }
    if (editingChart) return addToast('Conclua o gráfico em edição antes de adicionar outro.', 'info');
    if (blocks.filter(isChartBlock).length >= CHART_LIMITS.maxCharts)
      return addToast(`Uma nota pode ter no máximo ${CHART_LIMITS.maxCharts} gráficos.`, 'warning');
    const chart = createEmptyChart();
    setBlocks(prev => [...prev, chart]);
    setEditingChart({ id: chart.id, isNew: true });
  };
  const handleChartDone = updated => {
    setBlocks(prev => prev.map(b => (b.id === updated.id ? updated : b)));
    setEditingChart(null);
  };
  const handleChartCancel = () => {
    if (editingChart?.isNew) setBlocks(prev => prev.filter(b => b.id !== editingChart.id));
    setEditingChart(null);
  };
  // Colagem com tabelas: o bloco atual fica com "texto antes + 1º trecho"; gráficos e demais trechos
  // entram logo depois; o último trecho recebe o texto que estava após o cursor. O id do bloco é mantido.
  const handlePasteTables = (blockId, { start, end, pasted, segments }) => {
    const index = blocks.findIndex(b => b.id === blockId);
    if (index < 0) return;
    const current = blocks[index];
    const original = current.text || '';
    const before = original.slice(0, start);
    const after = original.slice(end);

    let available = CHART_LIMITS.maxCharts - blocks.filter(isChartBlock).length;
    let skipped = 0;
    // Gráficos acima do limite voltam a ser o texto original da tabela.
    const merged = [];
    for (const seg of segments) {
      const piece =
        seg.type === 'chart' && available-- <= 0 ? (skipped++, { type: 'text', text: seg.source }) : seg;
      const last = merged[merged.length - 1];
      if (piece.type === 'text' && last?.type === 'text') last.text = `${last.text}\n${piece.text}`;
      else merged.push(piece.type === 'text' ? { ...piece } : piece);
    }

    if (merged[0]?.type === 'text') merged[0].text = before + merged[0].text;
    else merged.unshift({ type: 'text', text: before });
    if (merged[merged.length - 1].type === 'text') merged[merged.length - 1].text += after;
    else if (after) merged.push({ type: 'text', text: after });

    const created = merged
      .map((seg, i) => {
        if (seg.type === 'chart') return { ...seg.chart, id: createEmptyChart().id };
        if (i === 0) return { ...current, text: seg.text };
        return { id: generateId(), type: 'paragraph', text: seg.text };
      })
      // O bloco onde se colou some se ficou vazio (sem texto, sem dúvidas possíveis).
      .filter((b, i) => !(i === 0 && !b.text && merged.length > 1));

    const count = created.filter(isChartBlock).length;
    const rawBlocks = blocks.map(b => (b.id === blockId ? { ...b, text: before + pasted + after } : b));
    setBlocks([...blocks.slice(0, index), ...created, ...blocks.slice(index + 1)]);
    setNotice({
      message:
        (count === 1 ? '1 tabela do texto colado virou gráfico.' : `${count} tabelas do texto colado viraram gráficos.`) +
        ' Revise os dados e a fonte em “Editar dados”.' +
        (skipped > 0 ? ` ${skipped} tabela(s) ficaram como texto (limite de ${CHART_LIMITS.maxCharts} gráficos).` : ''),
      restore: { blocks: rawBlocks, title },
      undoLabel: 'Conversão desfeita: o texto colado ficou como estava.',
      offerOrganize: hasStructure(pasted),
    });
  };

  const handleUndo = () => {
    if (!notice?.restore) return;
    setBlocks(notice.restore.blocks);
    setTitle(notice.restore.title);
    setEditingChart(null);
    addToast(notice.undoLabel || 'Alteração desfeita.', 'info');
    setNotice(null);
  };

  // Converte itens do organizador (local ou IA) em blocos do editor, respeitando o limite de gráficos.
  const itemsToBlocks = (items, firstId, budget) => {
    const out = [];
    items.forEach(item => {
      let block;
      if (item.type === 'chart') {
        if (budget.available-- > 0) block = { ...item.chart, id: createEmptyChart().id };
        else {
          budget.skipped++;
          block = { id: generateId(), type: 'paragraph', text: item.source || '' };
        }
      } else {
        block = { ...cleanBlock(item), id: generateId() };
      }
      if (out.length === 0 && firstId && !isChartBlock(block)) block.id = firstId;
      out.push(block);
    });
    return out;
  };

  // "Organizar em tópicos" (regras locais): reestrutura cada parágrafo; demais blocos ficam como estão.
  const handleOrganize = () => {
    if (editingChart) return addToast('Conclua a edição do gráfico antes de organizar.', 'info');
    const budget = { available: CHART_LIMITS.maxCharts - blocks.filter(isChartBlock).length, skipped: 0 };
    let newTitle;
    let changed = false;
    const next = [];
    for (const block of blocks) {
      if (block.type !== 'paragraph' || !block.text?.trim()) {
        next.push(block);
        continue;
      }
      const result = organizeText(block.text, { currentTitle: newTitle ?? title });
      const same =
        !result.title && result.blocks.length === 1 && result.blocks[0].type === 'paragraph' && result.blocks[0].text === block.text;
      if (same) {
        next.push(block);
        continue;
      }
      changed = true;
      if (result.title && newTitle === undefined) newTitle = result.title;
      next.push(...itemsToBlocks(result.blocks, block.id, budget));
    }
    // Gráficos sem título (ex.: tabelas de várias séries vindas da colagem) recebem o título da seção anterior.
    let lastHeading = null;
    for (let i = 0; i < next.length; i++) {
      if (next[i].type === 'heading') lastHeading = next[i].text;
      else if (isChartBlock(next[i]) && !next[i].title?.trim() && lastHeading) {
        next[i] = { ...next[i], title: lastHeading.slice(0, CHART_LIMITS.title) };
        changed = true;
      }
    }
    if (!changed) return addToast('A nota já está organizada.', 'info');
    if (next.length === 0) next.push({ id: generateId(), type: 'paragraph', text: '' });

    const charts = next.filter(isChartBlock).length - blocks.filter(isChartBlock).length;
    setNotice({
      message:
        `Nota organizada em ${next.filter(b => b.type === 'heading').length} títulos` +
        (charts > 0 ? ` e ${charts} gráfico(s)` : '') +
        '. Confira e salve.' +
        (budget.skipped ? ` ${budget.skipped} tabela(s) ficaram como texto (limite de gráficos).` : ''),
      restore: { blocks, title },
      undoLabel: 'Organização desfeita.',
    });
    setBlocks(next);
    if (newTitle) setTitle(newTitle);
  };

  // Resultado aceito do "Organizar com IA": substitui os blocos (gráficos existentes voltam pelas referências).
  const handleApplyAi = ({ items, title: aiTitle, costLabel }) => {
    const budget = { available: CHART_LIMITS.maxCharts, skipped: 0 };
    const next = itemsToBlocks(items, null, budget);
    setNotice({
      message: `Organização por IA aplicada${costLabel ? ` (${costLabel})` : ''}. Confira e salve.`,
      restore: { blocks, title },
      undoLabel: 'Organização por IA desfeita.',
    });
    setBlocks(next.length ? next : [{ id: generateId(), type: 'paragraph', text: '' }]);
    if (aiTitle) setTitle(aiTitle);
    setShowOrganizeAi(false);
  };

  // Remove o bloco e parágrafos vazios que fiquem colados a ele (sem faixas em branco).
  const handleRemoveBlock = blockId => {
    setBlocks(prev => {
      const index = prev.findIndex(b => b.id === blockId);
      if (index < 0) return prev;
      const isEmptyText = b => b && !isChartBlock(b) && b.type === 'paragraph' && !(b.text || '').trim();
      let start = index;
      let end = index;
      while (isEmptyText(prev[start - 1])) start--;
      while (isEmptyText(prev[end + 1])) end++;
      const next = [...prev.slice(0, start), ...prev.slice(end + 1)];
      return next.length ? next : [{ id: generateId(), type: 'paragraph', text: '' }];
    });
    if (editingChart?.id === blockId) setEditingChart(null);
    addToast('Gráfico removido. Salve para confirmar.', 'info');
  };

  // Backspace em bloco vazio: remove e põe o cursor no fim do bloco de texto anterior.
  const handleRemoveEmpty = blockId => {
    const index = blocks.findIndex(b => b.id === blockId);
    if (index < 0 || blocks.length <= 1) return;
    const previous = blocks
      .slice(0, index)
      .reverse()
      .find(b => !isChartBlock(b));
    setBlocks(prev => prev.filter(b => b.id !== blockId));
    if (previous) {
      setTimeout(() => {
        const el = document.querySelector(`[data-block-id="${previous.id}"]`);
        if (!el) return;
        el.focus();
        const range = document.createRange();
        range.selectNodeContents(el);
        range.collapse(false);
        const sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
      }, 0);
    }
  };
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
                <option value="chart">Gráfico</option>
              </select>
              <button className="icon-btn" onClick={handleAddBlock} disabled={isTrashed} aria-label="Adicionar bloco" title="Adicionar bloco">
                <Icon name="plus" />
              </button>
            </div>
            <div className="editor-toolbar-group">
              <button
                className="btn compact"
                onClick={handleOrganize}
                disabled={isTrashed}
                title="Organiza títulos, listas, destaques e tabelas com regras locais (nada sai do Notee)"
              >
                <Icon name="grid" className="sm" /> Organizar em tópicos
              </button>
              <button
                className="btn compact"
                onClick={() => setShowOrganizeAi(true)}
                disabled={isTrashed || !!editingChart}
                title="Envia o texto para o provedor de IA configurado, com custo e confirmação"
              >
                <Icon name="spark" className="sm" /> Organizar com IA
              </button>
              {/* onMouseDown preventDefault: não rouba o foco nem apaga a seleção do editor */}
              <button className="btn compact" onMouseDown={e => e.preventDefault()} onClick={handleMarkDoubt}>
                <Icon name="question" className="sm" /> Marcar dúvida no texto
              </button>
            </div>
          </div>

          {showOrganizeAi && (
            <OrganizeAiModal
              noteId={id}
              blocks={blocks}
              title={title}
              onApply={handleApplyAi}
              onClose={() => setShowOrganizeAi(false)}
            />
          )}

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

            {notice && (
              <div className="callout neutral mb16 paste-chart-notice" role="status">
                <Icon name="spark" />
                <span>{notice.message}</span>
                <div className="row gap8">
                  {notice.offerOrganize && (
                    <button className="btn compact primary" onClick={handleOrganize}>
                      <Icon name="spark" className="sm" /> Organizar em tópicos
                    </button>
                  )}
                  {notice.restore && (
                    <button className="btn compact" onClick={handleUndo}>
                      <Icon name="back" className="sm" /> Desfazer
                    </button>
                  )}
                  <button className="btn compact ghost" onClick={() => setNotice(null)}>
                    OK
                  </button>
                </div>
              </div>
            )}

            <div id="editor-blocks" style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              {blocks.map(block =>
                isChartBlock(block) ? (
                  <div key={block.id} className="editor-chart-block">
                    {editingChart?.id === block.id ? (
                      <ChartEditor chart={block} onDone={handleChartDone} onCancel={handleChartCancel} />
                    ) : (
                      <>
                        <ChartView chart={block} />
                        {!isTrashed && (
                          <div className="row gap8 editor-chart-actions">
                            <button
                              className="btn compact"
                              onClick={() => setEditingChart({ id: block.id, isNew: false })}
                              disabled={!!editingChart}
                            >
                              <Icon name="grid" className="sm" /> Editar dados
                            </button>
                            <button className="btn compact ghost" onClick={() => handleRemoveBlock(block.id)}>
                              <Icon name="trash" className="sm" /> Remover gráfico
                            </button>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                ) : (
                <ContentEditableBlock
                  key={block.id}
                  block={block}
                  doubts={doubts}
                  onChange={newText => handleUpdateBlock(block.id, newText)}
                  onSelect={sel => handleTextSelect(block.id, sel)}
                  onPasteTables={isTrashed ? undefined : payload => handlePasteTables(block.id, payload)}
                  onPasteStructured={() =>
                    setNotice({ message: 'Texto com títulos, listas ou negritos colado.', restore: null, offerOrganize: true })
                  }
                  onRemoveEmpty={isTrashed ? undefined : () => handleRemoveEmpty(block.id)}
                />
                ),
              )}
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
