// src/pages/Note.jsx
import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { useParams, Link, useBlocker } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';
import { useToast } from '../contexts/ToastContext';
import { NoteOrganizer } from '../components/NoteOrganizer';
import { RevisionHistory } from '../components/RevisionHistory';
import { PublishPanel } from '../features/publishing/PublishPanel';
import { NoteSuggestions } from '../components/NoteSuggestions';
import { SourcesPanel } from '../features/sources/SourcesPanel';
import { STATUS_LABEL } from '../notes';
import { ChartView } from '../features/charts/ChartView';
import { ChartEditor } from '../features/charts/ChartEditor';
import { CHART_LIMITS, createEmptyChart, isChartBlock } from '../features/charts/chartModel';
import { splitPastedText } from '../features/charts/detectTables';
import { cleanBlock, hasStructure, organizeText } from '../features/notes/organizeText';
import { OrganizeAiModal } from '../features/notes/OrganizeAiModal';
import { locateQuote } from '../features/notes/anchors';
import { buildMarkedHtml, parseMarks } from '../features/notes/markSegments';
import { MarkPopover } from '../features/notes/MarkPopover';
import { BlockTypeMenu } from '../features/notes/BlockTypeMenu';
import { AnswerSources, AnswerText } from '../features/research/AnswerWithRefs';
import {
  BLOCK_TYPES,
  MAX_INDENT,
  adjustStylesForEdit,
  blockTypeKey,
  convertBlockType,
  createTextBlock,
  listLabels,
  splitBlockAt,
  toggleStyle,
  deleteTextRange,
  formatTextRange,
  orderPositions,
  textInRange,
} from '../features/notes/blockFormatting';

const generateId = () => 'b-' + Math.random().toString(36).substr(2, 9);
const HISTORY_LIMIT = 100; // passos guardados para Ctrl+Z
const PLACEHOLDERS = {
  paragraph: 'Escreva aqui…',
  heading: 'Título',
  list: 'Item da lista',
  quote: 'Citação',
  code: 'Código',
};
// Zoom de leitura do texto da nota (A− / A+), lembrado no navegador.
const TEXT_SCALE_KEY = 'notee-editor-text-scale';
const TEXT_SCALES = [0.85, 0.9, 1, 1.1, 1.2, 1.3, 1.4, 1.5];
// Largura do painel "Detalhes da nota" (px), ajustável pela divisória e lembrada no navegador.
const DETAILS_WIDTH_KEY = 'notee-editor-details-width';
const DETAILS_DEFAULT = 290;
const DETAILS_MIN = 240;
const DETAILS_MAX = 560;
const EDITOR_MIN = 420;
const RESIZER_WIDTH = 12;

// Marcações do texto (dúvidas, comentários e fontes juntas): ver features/notes/markSegments.js.

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
// Posição do cursor/seleção em caracteres do texto do bloco (ignora <mark>/<strong>/<em>).
function getSelectionOffsets(el) {
  const sel = window.getSelection();
  if (!el || !sel || sel.rangeCount === 0) return null;
  const range = sel.getRangeAt(0);
  if (!el.contains(range.startContainer) || !el.contains(range.endContainer)) return null;
  const pre = document.createRange();
  pre.selectNodeContents(el);
  pre.setEnd(range.startContainer, range.startOffset);
  const start = pre.toString().length;
  return { start, end: start + range.toString().length };
}

// Recoloca a seleção pelos mesmos offsets depois de redesenhar o bloco.
function setSelectionOffsets(el, start, end) {
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
  const range = document.createRange();
  let pos = 0;
  let startSet = false;
  let node;
  while ((node = walker.nextNode())) {
    const len = node.textContent.length;
    if (!startSet && start <= pos + len) { range.setStart(node, start - pos); startSet = true; }
    if (startSet && end <= pos + len) { range.setEnd(node, end - pos); break; }
    pos += len;
  }
  if (!startSet) { range.selectNodeContents(el); range.collapse(false); }
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
}

// Setas no limite do bloco: devolve { direction, x } quando o cursor deve passar para o bloco vizinho.
// ↑ só na primeira linha visual, ↓ só na última, ← no início, → no fim. Seleção não vazia segue o padrão.
function caretBoundary(el, key) {
  const sel = window.getSelection();
  if (!el || !sel || sel.rangeCount === 0 || !sel.isCollapsed) return null;
  const range = sel.getRangeAt(0);
  if (!el.contains(range.startContainer)) return null;
  const offsets = getSelectionOffsets(el);
  const length = el.textContent.length;
  if (key === 'ArrowLeft') return offsets?.start === 0 ? { direction: 'left' } : null;
  if (key === 'ArrowRight') return offsets?.start >= length ? { direction: 'right' } : null;

  const caret = range.getClientRects()[0] || range.getBoundingClientRect();
  const box = el.getBoundingClientRect();
  const style = getComputedStyle(el);
  const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.6 || 24;
  const x = caret && caret.height ? caret.left : box.left + parseFloat(style.paddingLeft || 0);
  // Bloco vazio, ou cursor sem posição medível: qualquer seta vertical sai do bloco.
  if (length === 0 || !caret || caret.height === 0) return { direction: key === 'ArrowUp' ? 'up' : 'down', x };
  const contentTop = box.top + parseFloat(style.paddingTop || 0);
  const contentBottom = box.bottom - parseFloat(style.paddingBottom || 0);
  if (key === 'ArrowUp' && caret.top - contentTop < lineHeight * 0.75) return { direction: 'up', x };
  if (key === 'ArrowDown' && contentBottom - caret.bottom < lineHeight * 0.75) return { direction: 'down', x };
  return null;
}

// Põe o cursor no ponto (x, y) da tela se ele cair dentro de `el`; devolve false se não der.
function placeCaretAtPoint(el, x, y) {
  let node = null;
  let offset = 0;
  if (document.caretPositionFromPoint) {
    const pos = document.caretPositionFromPoint(x, y);
    if (pos) ({ offsetNode: node, offset } = pos);
  } else if (document.caretRangeFromPoint) {
    const r = document.caretRangeFromPoint(x, y);
    if (r) { node = r.startContainer; offset = r.startOffset; }
  }
  if (!node || !el.contains(node)) return false;
  const range = document.createRange();
  range.setStart(node, offset);
  range.collapse(true);
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
  return true;
}

// --- Seleção entre vários blocos -------------------------------------------------------
// Cada bloco é um contentEditable próprio, e o navegador não deixa a seleção atravessar dois.
// Para selecionar entre blocos, os blocos ficam temporariamente NÃO editáveis (seleção de leitura normal)
// e a página trata apagar/digitar/recortar/formatar. Posições são { blockId, offset } no texto do bloco.

// Nó de texto + offset dentro do bloco para um offset em caracteres.
function nodeAtOffset(el, offset) {
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
  let pos = 0;
  let node;
  let last = null;
  while ((node = walker.nextNode())) {
    const len = node.textContent.length;
    if (offset <= pos + len) return { node, offset: Math.max(0, offset - pos) };
    pos += len;
    last = node;
  }
  return last ? { node: last, offset: last.textContent.length } : { node: el, offset: el.childNodes.length };
}

// Posição no texto (bloco + offset) para um ponto da tela; fora dos blocos, usa o bloco mais próximo na vertical.
function textPositionFromPoint(container, x, y) {
  let node = null;
  let offset = 0;
  if (document.caretPositionFromPoint) {
    const pos = document.caretPositionFromPoint(x, y);
    if (pos) ({ offsetNode: node, offset } = pos);
  } else if (document.caretRangeFromPoint) {
    const r = document.caretRangeFromPoint(x, y);
    if (r) { node = r.startContainer; offset = r.startOffset; }
  }
  const host = node && (node.nodeType === Node.TEXT_NODE ? node.parentElement : node)?.closest?.('[data-block-id]');
  if (host && container.contains(host)) {
    const range = document.createRange();
    range.selectNodeContents(host);
    range.setEnd(node, offset);
    return { blockId: host.dataset.blockId, offset: range.toString().length };
  }
  const els = [...container.querySelectorAll('[data-block-id]')];
  if (!els.length) return null;
  let best = els[0];
  for (const el of els) {
    const r = el.getBoundingClientRect();
    if (y >= r.top) best = el;
  }
  const r = best.getBoundingClientRect();
  return { blockId: best.dataset.blockId, offset: y > r.bottom || x > r.right ? best.textContent.length : 0 };
}

// anchors: todas as marcações da nota ({ kind, id, blockId, exactQuote, startIndex, referenceNumber }).
// listLabel: rótulo calculado da lista ('1.', 'a.1'…), desenhado por CSS (não entra no texto).
// repaintToken: muda depois de negrito/itálico para redesenhar o bloco mesmo em foco, mantendo a seleção.
// editable = false: seleção entre blocos em andamento (bloco vira texto de leitura até a seleção acabar).
// onSelectAll: Ctrl+A com o bloco inteiro já selecionado (ou vazio) seleciona a nota toda.
function ContentEditableBlock({ block, anchors, listLabel, repaintToken, editable = true, onChange, onSelect, onFocusBlock, onFormat, onListKey, onNavigate, onSelectAll, onPasteTables, onPasteStructured, onRemoveEmpty }) {
  const divRef = useRef(null);

  // Chave estável: as âncoras são recalculadas a cada render da página, mas só repintamos se mudarem.
  const anchorsKey = JSON.stringify(
    anchors
      .filter(a => a.blockId === block.id && a.exactQuote)
      .map(a => [a.kind, a.id, a.exactQuote, a.startIndex || 0, a.referenceNumber || 0]),
  );
  const blockAnchors = useMemo(
    () => JSON.parse(anchorsKey).map(([kind, id, exactQuote, startIndex, referenceNumber]) => ({ kind, id, exactQuote, startIndex, referenceNumber })),
    [anchorsKey],
  );

  const stylesKey = JSON.stringify(block.styles || []);

  const paint = () => {
    if (divRef.current) divRef.current.innerHTML = buildMarkedHtml(block.text || '', blockAnchors, block.styles || []);
  };

  // Só reescreve o DOM quando o bloco NÃO está em foco -> nunca mexe no cursor durante a digitação.
  useEffect(() => {
    if (divRef.current && document.activeElement !== divRef.current) paint();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [block.text, blockAnchors, stylesKey]);

  // Negrito/itálico aplicado com o bloco em foco: redesenha e devolve a seleção ao mesmo trecho.
  useEffect(() => {
    if (!repaintToken || !divRef.current) return;
    if (document.activeElement !== divRef.current) return paint();
    const offsets = getSelectionOffsets(divRef.current);
    paint();
    if (offsets) setSelectionOffsets(divRef.current, offsets.start, offsets.end);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [repaintToken]);

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

  const handleKeyDown = e => {
    const el = divRef.current;
    const text = (el?.innerText || '').replace(/\n/g, '');
    const isList = block.type === 'list';

    // Ctrl+A: 1ª vez seleciona o bloco (padrão do navegador); com o bloco já todo selecionado, a nota inteira.
    if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 'a' && onSelectAll) {
      const offsets = getSelectionOffsets(el);
      const length = el?.textContent.length || 0;
      if (length === 0 || (offsets && offsets.start === 0 && offsets.end >= length)) {
        e.preventDefault();
        onSelectAll();
        return;
      }
    }

    // Ctrl+B / Ctrl+I: negrito e itálico no trecho selecionado.
    if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && ['b', 'i'].includes(e.key.toLowerCase())) {
      e.preventDefault();
      const offsets = getSelectionOffsets(el);
      if (offsets && offsets.end > offsets.start) onFormat?.(e.key.toLowerCase() === 'b' ? 'bold' : 'italic', offsets);
      return;
    }

    // Setas: no limite do bloco, o cursor continua no bloco vizinho (como num texto corrido).
    if (onNavigate && !e.shiftKey && !e.ctrlKey && !e.metaKey && !e.altKey && e.key.startsWith('Arrow')) {
      const move = caretBoundary(el, e.key);
      if (move) {
        e.preventDefault();
        onNavigate(move.direction, move.x);
        return;
      }
    }

    if (isList && onListKey) {
      // Enter: próximo item (divide no cursor); item vazio sai da lista ou sobe um nível.
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        onListKey('enter', { empty: text === '', offset: getSelectionOffsets(el)?.start ?? (block.text || '').length, text: el?.innerText || '' });
        return;
      }
      // Tab / Shift+Tab: aumenta ou diminui o nível (1. → 1.1 → 1.1.1).
      if (e.key === 'Tab') {
        e.preventDefault();
        onListKey(e.shiftKey ? 'outdent' : 'indent');
        return;
      }
      // Backspace em item vazio: sobe um nível; no primeiro nível vira parágrafo.
      if (e.key === 'Backspace' && text === '') {
        e.preventDefault();
        onListKey('backspace-empty');
        return;
      }
    }

    // Backspace num bloco vazio remove o bloco (evita faixas em branco na nota).
    if (e.key === 'Backspace' && onRemoveEmpty && text === '') {
      e.preventDefault();
      onRemoveEmpty();
    }
  };

  const listClass = block.type === 'list' ? ` list-${block.listStyle || 'bullet'}` : '';
  return (
    <div
      ref={divRef}
      data-block-id={block.id}
      data-label={block.type === 'list' ? listLabel || '•' : undefined}
      contentEditable={!editable ? 'false' : PLAINTEXT_SUPPORTED ? 'plaintext-only' : true}
      suppressContentEditableWarning
      className={`editor-block ${block.type}${block.type === 'heading' ? ` level-${block.level || 2}` : ''}${listClass}`}
      onInput={handleInput}
      onBlur={handleBlur}
      onFocus={() => onFocusBlock?.(block.id)}
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
        '--indent': block.type === 'list' ? block.indent || 0 : undefined,
      }}
      data-placeholder={PLACEHOLDERS[block.type] || 'Escreva aqui…'}
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
  const [privateComments, setPrivateComments] = useState([]);
  const [publicComments, setPublicComments] = useState([]);
  const [sourceCitations, setSourceCitations] = useState([]);
  const [publications, setPublications] = useState([]);
  const [commentVisibility, setCommentVisibility] = useState('private');
  const [activeAside, setActiveAside] = useState(() => { try { const tab = localStorage.getItem('notee-editor-aside'); return ['comments', 'sources'].includes(tab) ? tab : 'doubts'; } catch { return 'doubts'; } });
  const [showCommentModal, setShowCommentModal] = useState(false);
  const [commentText, setCommentText] = useState('');
  const [commentBusy, setCommentBusy] = useState(false);
  const [selectedCommentId, setSelectedCommentId] = useState(null);

  const [title, setTitle] = useState('');
  const [categoryId, setCategoryId] = useState(null);
  const [tagIds, setTagIds] = useState([]);
  const [showHistory, setShowHistory] = useState(false);
  const closeHistory = useCallback(() => setShowHistory(false), []);
  const [showPublish, setShowPublish] = useState(false);
  const closePublish = useCallback(() => setShowPublish(false), []);
  const wasPublishOpen = useRef(false);
  const [blocks, setBlocks] = useState([]);
  // Bloco de texto com o cursor (ou o último que teve): o seletor de tipo e o "+" da barra agem nele.
  const [focusedBlockId, setFocusedBlockId] = useState(null);
  // Bloco cujo menu "+" está aberto ('__toolbar' = "+" da barra).
  const [blockMenuFor, setBlockMenuFor] = useState(null);
  // Muda após negrito/itálico para o bloco formatado redesenhar mantendo a seleção.
  const [formatTick, setFormatTick] = useState({ id: null, n: 0 });
  const [textScale, setTextScale] = useState(() => {
    try {
      const saved = Number(localStorage.getItem(TEXT_SCALE_KEY));
      return TEXT_SCALES.includes(saved) ? saved : 1;
    } catch {
      return 1;
    }
  });
  const changeTextScale = direction =>
    setTextScale(current => {
      const i = TEXT_SCALES.indexOf(current);
      const next = direction === 0 ? 1 : TEXT_SCALES[Math.max(0, Math.min(TEXT_SCALES.length - 1, (i < 0 ? 2 : i) + direction))];
      try { localStorage.setItem(TEXT_SCALE_KEY, String(next)); } catch { /* armazenamento indisponível */ }
      return next;
    });
  // Gráfico em edição: { id, isNew } — cancelar um gráfico recém-criado o remove.
  const [editingChart, setEditingChart] = useState(null);
  // Aviso com "Desfazer": { message, restore: { blocks, title } | null, offerOrganize } (colagem, organizar, IA).
  const [notice, setNotice] = useState(null);
  const [showOrganizeAi, setShowOrganizeAi] = useState(false);

  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  // Painel "Detalhes da nota": fixo à direita em telas largas; gaveta em telas estreitas.
  const [detailsOpen, setDetailsOpen] = useState(false);
  // Abas Dúvidas / Comentários / Fontes no rodapé da nota.
  const bottomRef = useRef(null);
  // Histórico do desfazer/refazer (ver "Desfazer / refazer" mais abaixo).
  const historyRef = useRef({ past: [], present: null, future: [] });
  const historyTimerRef = useRef(null);
  const applyingHistoryRef = useRef(false);
  const latestRef = useRef(null);
  const historyActionsRef = useRef({ move: () => {}, enabled: false });

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
  const loadPrivateComments = async () => setPrivateComments(await apiFetch(`/notes/${id}/comments`) || []);
  const loadSourceCitations = async () => setSourceCitations(await apiFetch(`/notes/${id}/citations`) || []);
  const loadPublicComments = useCallback(async () => {
    const [comments, pubs] = await Promise.all([apiFetch(`/notes/${id}/public-comments`), apiFetch(`/notes/${id}/publications`)]);
    setPublicComments(comments || []); setPublications(pubs || []);
  }, [id]);
  useEffect(() => {
    if (wasPublishOpen.current && !showPublish) loadPublicComments().catch(() => {});
    wasPublishOpen.current = showPublish;
  }, [showPublish, loadPublicComments]);

  // Aplica uma nota vinda da API ao estado do editor (carga inicial e restauração de revisão).
  const applyLoadedNote = noteData => {
    // Nota carregada ou versão restaurada: o histórico de desfazer recomeça a partir dela.
    clearTimeout(historyTimerRef.current);
    historyTimerRef.current = null;
    historyRef.current = { past: [], present: null, future: [] };
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
      await loadPrivateComments();
      await loadPublicComments();
      await loadSourceCitations();
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

  // Troca a aba do rodapé (dúvidas/comentários/fontes) e lembra a escolha no browser.
  const selectAside = tab => {
    setActiveAside(tab);
    try { localStorage.setItem('notee-editor-aside', tab); } catch { /* armazenamento indisponível */ }
  };

  // Esc fecha a gaveta de detalhes em telas estreitas.
  useEffect(() => {
    if (!detailsOpen) return;
    const onKey = e => { if (e.key === 'Escape') setDetailsOpen(false); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [detailsOpen]);

  // --- Divisória entre o texto e "Detalhes da nota" ----------------------------------
  // Arrastar (ou ←/→ com foco na divisória) muda a largura do painel; duplo clique volta ao padrão.
  const [detailsWidth, setDetailsWidth] = useState(() => {
    try {
      const saved = Number(localStorage.getItem(DETAILS_WIDTH_KEY));
      return saved >= DETAILS_MIN && saved <= DETAILS_MAX ? saved : DETAILS_DEFAULT;
    } catch {
      return DETAILS_DEFAULT;
    }
  });
  const [isResizing, setIsResizing] = useState(false);
  const layoutRef = useRef(null);
  const dragRef = useRef(null);

  // Limita a largura do painel e garante espaço mínimo para o texto.
  const clampDetailsWidth = useCallback(width => {
    const layoutWidth = layoutRef.current?.clientWidth || Infinity;
    const max = Math.min(DETAILS_MAX, layoutWidth - EDITOR_MIN - RESIZER_WIDTH);
    return Math.round(Math.max(DETAILS_MIN, Math.min(width, max)));
  }, []);

  useEffect(() => {
    if (isResizing) return;
    try { localStorage.setItem(DETAILS_WIDTH_KEY, String(detailsWidth)); } catch { /* armazenamento indisponível */ }
  }, [detailsWidth, isResizing]);

  useEffect(() => {
    if (!isResizing) return;
    const onMove = e => setDetailsWidth(clampDetailsWidth(dragRef.current.startWidth - (e.clientX - dragRef.current.startX)));
    const onUp = () => {
      setIsResizing(false);
      document.body.classList.remove('pane-is-resizing');
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    return () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
  }, [isResizing, clampDetailsWidth]);

  const handleResizerPointerDown = e => {
    e.preventDefault();
    dragRef.current = { startX: e.clientX, startWidth: detailsWidth };
    document.body.classList.add('pane-is-resizing');
    setIsResizing(true);
  };
  const handleResizerKeyDown = e => {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
    e.preventDefault();
    // Seta para a esquerda aumenta o painel (a divisória anda para a esquerda).
    setDetailsWidth(w => clampDetailsWidth(w + (e.key === 'ArrowLeft' ? 16 : -16)));
  };

  // --- Card das marcações (passar o mouse ou clicar num trecho marcado) ---------------
  const [markCard, setMarkCard] = useState(null); // { marks: [{ kind, id }], rect, key }
  const markHideTimerRef = useRef(null);
  const cancelHideMarkCard = () => clearTimeout(markHideTimerRef.current);
  const scheduleHideMarkCard = () => {
    clearTimeout(markHideTimerRef.current);
    markHideTimerRef.current = setTimeout(() => setMarkCard(null), 200);
  };
  const showMarkCard = markEl => {
    cancelHideMarkCard();
    const key = markEl.dataset.marks;
    setMarkCard(prev => (prev?.key === key && prev.el === markEl ? prev : { key, el: markEl, marks: parseMarks(key), rect: markEl.getBoundingClientRect() }));
  };
  const handleBlocksMouseOver = e => {
    // Durante a seleção entre blocos o card atrapalharia: não abre.
    if (selectDragRef.current?.active || multiSelRef.current) return;
    const markEl = e.target.closest?.('mark[data-marks]');
    if (markEl) showMarkCard(markEl);
  };
  const handleBlocksMouseOut = e => {
    const markEl = e.target.closest?.('mark[data-marks]');
    if (markEl && !markEl.contains(e.relatedTarget)) scheduleHideMarkCard();
  };
  const handleBlocksClick = e => {
    const markEl = e.target.closest?.('mark[data-marks]');
    if (!markEl) return;
    showMarkCard(markEl);
    const comment = parseMarks(markEl.dataset.marks).find(m => m.kind === 'comment');
    if (comment) setSelectedCommentId(comment.id);
  };
  // Esc, rolar a página ou começar a digitar fecham o card.
  useEffect(() => {
    if (!markCard) return;
    const close = () => setMarkCard(null);
    const onKey = e => { if (!['Shift', 'Control', 'Alt', 'Meta'].includes(e.key)) close(); };
    window.addEventListener('keydown', onKey);
    window.addEventListener('scroll', close, true);
    window.addEventListener('resize', close);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('scroll', close, true);
      window.removeEventListener('resize', close);
    };
  }, [markCard]);
  useEffect(() => () => clearTimeout(markHideTimerRef.current), []);

  // --- Seleção que atravessa vários blocos (arrastar, Shift+clique, Ctrl+A duas vezes) --------
  // multiSel = { anchor, focus } em { blockId, offset }. Enquanto existe, os blocos ficam não editáveis
  // (a seleção do navegador passa entre eles) e as teclas abaixo agem sobre o trecho todo.
  const [multiSel, setMultiSel] = useState(null);
  const multiSelRef = useRef(null);
  multiSelRef.current = multiSel;
  const blocksRef = useRef(blocks);
  blocksRef.current = blocks;
  const selectDragRef = useRef(null);
  const readOnly = note?.status === 'Trashed';

  const caretPositionNow = () => {
    const el = document.activeElement;
    if (!el?.dataset?.blockId) return null;
    const offsets = getSelectionOffsets(el);
    return offsets ? { blockId: el.dataset.blockId, offset: offsets.start } : null;
  };
  const enterMultiSelect = (anchor, focus) => {
    document.activeElement?.blur?.();
    setMarkCard(null);
    setSelection(null);
    setMultiSel({ anchor, focus });
  };
  const focusTextPosition = pos => {
    setTimeout(() => {
      const el = pos && document.querySelector(`[data-block-id="${pos.blockId}"]`);
      if (!el) return;
      el.focus();
      setSelectionOffsets(el, pos.offset, pos.offset);
      setFocusedBlockId(pos.blockId);
    }, 0);
  };
  const exitMultiSelect = collapseTo => {
    setMultiSel(null);
    if (collapseTo) focusTextPosition(collapseTo);
  };
  const selectAllBlocks = () => {
    const textBlocks = blocksRef.current.filter(b => !isChartBlock(b));
    if (!textBlocks.length) return;
    const last = textBlocks[textBlocks.length - 1];
    enterMultiSelect({ blockId: textBlocks[0].id, offset: 0 }, { blockId: last.id, offset: (last.text || '').length });
  };
  // Apaga o trecho (e opcionalmente digita `insert` no lugar), juntando o primeiro e o último bloco.
  const deleteMultiSelection = (insert = '') => {
    const sel = multiSelRef.current;
    if (!sel) return;
    const { blocks: next, caret } = deleteTextRange(blocksRef.current, sel.anchor, sel.focus, insert);
    setBlocks(next);
    exitMultiSelect(caret);
  };
  const formatMultiSelection = kind => {
    const sel = multiSelRef.current;
    if (sel) setBlocks(prev => formatTextRange(prev, sel.anchor, sel.focus, kind));
  };

  // Reaplica a seleção do navegador depois de cada render (inclusive após os blocos se redesenharem).
  // useEffect do pai roda depois dos efeitos dos blocos, então a pintura dos blocos já aconteceu.
  useEffect(() => {
    if (!multiSel) return;
    const a = document.querySelector(`[data-block-id="${multiSel.anchor.blockId}"]`);
    const f = document.querySelector(`[data-block-id="${multiSel.focus.blockId}"]`);
    if (!a || !f) return setMultiSel(null);
    const start = nodeAtOffset(a, multiSel.anchor.offset);
    const end = nodeAtOffset(f, multiSel.focus.offset);
    try {
      window.getSelection().setBaseAndExtent(start.node, start.offset, end.node, end.offset);
    } catch {
      /* posição inválida após edição: a próxima interação corrige */
    }
  }, [multiSel, blocks]);

  // Arrastar o mouse de um bloco para outro entra no modo de seleção entre blocos.
  useEffect(() => {
    const onMove = e => {
      const drag = selectDragRef.current;
      if (!drag || !(e.buttons & 1)) return;
      const pos = textPositionFromPoint(drag.container, e.clientX, e.clientY);
      if (!pos) return;
      if (!drag.active) {
        if (pos.blockId === drag.anchor.blockId) return; // ainda dentro do mesmo bloco: seleção normal
        drag.active = true;
        enterMultiSelect(drag.anchor, pos);
        return;
      }
      e.preventDefault();
      setMultiSel(cur => (cur && (cur.focus.blockId !== pos.blockId || cur.focus.offset !== pos.offset) ? { ...cur, focus: pos } : cur));
    };
    const onUp = () => { selectDragRef.current = null; };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    return () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Teclas com a seleção entre blocos ativa.
  useEffect(() => {
    if (!multiSel) return;
    const onKey = e => {
      const mod = e.ctrlKey || e.metaKey;
      const key = e.key.toLowerCase();
      if (mod && key === 'c') return; // cópia tratada no evento "copy"
      if (mod && (key === 'z' || key === 'y')) return setMultiSel(null); // o desfazer global cuida do resto
      if (mod && key === 'a') { e.preventDefault(); return selectAllBlocks(); }
      if (mod && (key === 'b' || key === 'i')) { e.preventDefault(); return formatMultiSelection(key === 'b' ? 'bold' : 'italic'); }
      if (mod && key === 'x') {
        e.preventDefault();
        navigator.clipboard?.writeText(textInRange(blocksRef.current, multiSel.anchor, multiSel.focus)).catch(() => {});
        return deleteMultiSelection();
      }
      if (mod || e.altKey) return;
      if (e.key === 'Backspace' || e.key === 'Delete' || e.key === 'Enter') { e.preventDefault(); return deleteMultiSelection(); }
      if (e.key.length === 1) { e.preventDefault(); return deleteMultiSelection(e.key); } // digitar substitui a seleção
      if (e.key === 'Escape' || e.key.startsWith('Arrow')) {
        e.preventDefault();
        const [start, end] = orderPositions(blocksRef.current, multiSel.anchor, multiSel.focus);
        exitMultiSelect(e.key === 'ArrowLeft' || e.key === 'ArrowUp' ? start : end);
      }
    };
    // Copiar: texto puro dos blocos (sem rótulos de lista nem [n] desenhados por CSS).
    const onCopy = e => {
      e.clipboardData?.setData('text/plain', textInRange(blocksRef.current, multiSel.anchor, multiSel.focus));
      e.preventDefault();
    };
    // Clique fora da nota encerra a seleção.
    const onPointerDown = e => { if (!e.target.closest?.('#editor-blocks, .ne-tools')) setMultiSel(null); };
    document.addEventListener('keydown', onKey, true);
    document.addEventListener('copy', onCopy);
    document.addEventListener('pointerdown', onPointerDown);
    return () => {
      document.removeEventListener('keydown', onKey, true);
      document.removeEventListener('copy', onCopy);
      document.removeEventListener('pointerdown', onPointerDown);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [multiSel]);

  const handleBlocksPointerDown = e => {
    if (e.button !== 0 || readOnly) return;
    if (e.target.closest('button, input, select, textarea, a, .editor-chart-block, .ne-block-gutter')) return;
    const container = e.currentTarget;
    const pos = textPositionFromPoint(container, e.clientX, e.clientY);
    if (!pos) return;
    // Shift+clique: estende do cursor atual (ou da seleção em andamento) até o ponto clicado.
    if (e.shiftKey) {
      const anchor = multiSelRef.current?.anchor || caretPositionNow();
      if (anchor && anchor.blockId !== pos.blockId) {
        e.preventDefault();
        enterMultiSelect(anchor, pos);
        selectDragRef.current = { container, anchor, active: true };
        return;
      }
    }
    if (multiSelRef.current) setMultiSel(null); // clique comum encerra a seleção e volta a editar
    selectDragRef.current = { container, anchor: pos, active: false };
  };

  // Devolve true quando salvou (usado por "Salvar e sair" no aviso de alterações não salvas).
  const handleSave = async () => {
    if (editingChart) {
      addToast('Conclua ou cancele a edição do gráfico antes de salvar.', 'info');
      return false;
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
      return true;
    } catch (err) {
      addToast(
        err?.status === 409 ? 'A nota foi alterada em outro lugar. Copie seu texto e recarregue a página.' : err?.message || 'Falha ao salvar.',
        'warning',
      );
      return false;
    } finally {
      setSaving(false);
    }
  };

  // Põe o cursor num bloco de texto (no início ou no fim).
  const focusBlock = (blockId, atEnd = false) => {
    setTimeout(() => {
      const el = document.querySelector(`[data-block-id="${blockId}"]`);
      if (!el) return;
      el.focus();
      const range = document.createRange();
      range.selectNodeContents(el);
      range.collapse(!atEnd);
      const sel = window.getSelection();
      sel.removeAllRanges();
      sel.addRange(range);
    }, 0);
  };

  // Setas entre blocos: ↑/↓ mantêm a coluna (x) e vão para a última/primeira linha do vizinho;
  // ←/→ vão para o fim/início. Gráficos são pulados; ↑ no primeiro bloco volta ao título.
  const navigateFromBlock = (blockId, direction, x) => {
    const textBlocks = blocks.filter(b => !isChartBlock(b));
    const index = textBlocks.findIndex(b => b.id === blockId);
    const target = direction === 'up' || direction === 'left' ? textBlocks[index - 1] : textBlocks[index + 1];
    if (!target) {
      if (direction === 'up' || direction === 'left') {
        const titleInput = document.querySelector('.ne-title');
        titleInput?.focus();
        titleInput?.setSelectionRange(titleInput.value.length, titleInput.value.length);
      }
      return;
    }
    const el = document.querySelector(`[data-block-id="${target.id}"]`);
    if (!el) return;
    el.focus();
    setFocusedBlockId(target.id);
    if (direction === 'left' || direction === 'right') return setSelectionOffsets(el, direction === 'left' ? el.textContent.length : 0, direction === 'left' ? el.textContent.length : 0);
    el.scrollIntoView({ block: 'nearest' });
    const box = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    const lineHeight = parseFloat(style.lineHeight) || 24;
    const y = direction === 'up'
      ? box.bottom - parseFloat(style.paddingBottom || 0) - lineHeight / 2
      : box.top + parseFloat(style.paddingTop || 0) + lineHeight / 2;
    const left = Math.max(box.left + parseFloat(style.paddingLeft || 0) + 1, Math.min(x ?? 0, box.right - 2));
    if (!placeCaretAtPoint(el, left, y)) {
      const end = direction === 'up' ? el.textContent.length : 0;
      setSelectionOffsets(el, end, end);
    }
  };
  const handleTitleKeyDown = e => {
    if ((e.key === 'ArrowDown' || e.key === 'Enter') && !e.shiftKey) {
      const first = blocks.find(b => !isChartBlock(b));
      if (!first) return;
      e.preventDefault();
      setFocusedBlockId(first.id);
      focusBlock(first.id);
    }
  };

  // "+" ao lado de um bloco (ou na barra): insere o novo bloco LOGO ABAIXO dele.
  // Sem bloco de referência, insere no fim da nota.
  const insertBlockAfter = (afterId, key) => {
    setBlockMenuFor(null);
    const index = afterId ? blocks.findIndex(b => b.id === afterId) : -1;
    const at = index >= 0 ? index + 1 : blocks.length;
    if (key === 'chart') {
      if (editingChart) return addToast('Conclua o gráfico em edição antes de adicionar outro.', 'info');
      if (blocks.filter(isChartBlock).length >= CHART_LIMITS.maxCharts)
        return addToast(`Uma nota pode ter no máximo ${CHART_LIMITS.maxCharts} gráficos.`, 'warning');
      const chart = createEmptyChart();
      setBlocks(prev => [...prev.slice(0, at), chart, ...prev.slice(at)]);
      setEditingChart({ id: chart.id, isNew: true });
      return;
    }
    const block = createTextBlock(key, generateId(), index >= 0 ? blocks[index] : null);
    setBlocks(prev => [...prev.slice(0, at), block, ...prev.slice(at)]);
    setFocusedBlockId(block.id);
    focusBlock(block.id);
  };

  // Seletor de tipo da barra: converte o bloco em foco (ex.: parágrafo → título).
  const changeBlockType = (blockId, key) => {
    if (!blockId) return addToast('Clique num trecho do texto para escolher o tipo dele.', 'info');
    setBlocks(prev => prev.flatMap(b => (b.id === blockId && !isChartBlock(b) ? convertBlockType(b, key, generateId) : [b])));
    focusBlock(blockId, true);
  };

  // Negrito/itálico no trecho [start, end) do bloco; aplicar de novo remove.
  const applyFormat = (blockId, { start, end }, kind) => {
    if (!blockId || end <= start) return addToast('Selecione um trecho do texto para formatar.', 'info');
    setBlocks(prev =>
      prev.map(b => {
        if (b.id !== blockId) return b;
        const styles = toggleStyle(b.styles || [], start, end, kind, (b.text || '').length);
        const next = { ...b };
        if (styles.length) next.styles = styles;
        else delete next.styles;
        return next;
      }),
    );
    setFormatTick(prev => ({ id: blockId, n: prev.n + 1 }));
  };
  const formatSelection = kind =>
    multiSel ? formatMultiSelection(kind) : selection ?applyFormat(selection.blockId, { start: selection.startIndex, end: selection.endIndex }, kind) : addToast('Selecione um trecho do texto para formatar.', 'info');

  // Teclas especiais dentro de um item de lista.
  const handleListKey = (blockId, action, info = {}) => {
    const block = blocks.find(b => b.id === blockId);
    if (!block) return;
    const indent = block.indent || 0;
    const update = patch => setBlocks(prev => prev.map(b => (b.id === blockId ? { ...b, ...patch } : b)));
    if (action === 'indent') return update({ indent: Math.min(MAX_INDENT, indent + 1) });
    if (action === 'outdent') return update({ indent: Math.max(0, indent - 1) });
    if (action === 'backspace-empty' || (action === 'enter' && info.empty)) {
      // Item vazio: sobe um nível; no primeiro nível sai da lista e vira parágrafo.
      if (indent > 0) return update({ indent: indent - 1 });
      setBlocks(prev => prev.flatMap(b => (b.id === blockId ? convertBlockType(b, 'paragraph', generateId) : [b])));
      return;
    }
    if (action === 'enter') {
      // Divide no cursor: o texto depois do cursor vai para o próximo item.
      const newId = generateId();
      const current = { ...block, text: info.text ?? block.text };
      const [head, tail] = splitBlockAt(current, info.offset, newId);
      document.activeElement?.blur?.();
      setBlocks(prev => prev.flatMap(b => (b.id === blockId ? [head, tail] : [b])));
      setFocusedBlockId(newId);
      focusBlock(newId);
    }
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
  // Digitação: atualiza o texto e desloca negrito/itálico junto com a edição.
  const handleUpdateBlock = (blockId, newText) =>
    setBlocks(prev =>
      prev.map(b => {
        if (b.id !== blockId) return b;
        const next = { ...b, text: newText };
        if (b.styles?.length) {
          const styles = adjustStylesForEdit(b.text || '', newText, b.styles);
          if (styles.length) next.styles = styles;
          else delete next.styles;
        }
        return next;
      }),
    );

  const handleTextSelect = (blockId, sel) => setSelection(sel ? { blockId, ...sel } : null);

  const handleMarkDoubt = () => {
    if (!selection) return addToast('Selecione um pedaço de texto dentro do editor.', 'info');
    selectAside('doubts');
    setQuestionText(`O que significa "${selection.quote}" neste contexto?`);
    setShowModal(true);
  };

  const handleComment = () => {
    if (!selection) return addToast('Selecione um trecho do texto.', 'info');
    if (isDirty) return addToast('Salve a nota antes de comentar o trecho.', 'info');
    selectAside('comments');
    setCommentText(''); setCommentVisibility('private'); setShowCommentModal(true);
  };

  const handleCite = () => {
    selectAside('sources');
    if (selection && isDirty) addToast('Salve a nota antes de citar o trecho selecionado.', 'info');
    // O painel de fontes fica no rodapé: rola até ele para o usuário concluir a citação.
    setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 0);
  };

  const handleSubmitComment = async e => {
    e.preventDefault();
    if (!selection) return;
    setCommentBusy(true);
    try {
      if (commentVisibility === 'public') {
        const pub = publications.find(p => p.status === 'Published' && p.visibility === 'public' && p.commentsEnabled);
        if (!pub) throw new Error('Publique a nota com comentários ligados para comentar publicamente.');
        await apiFetch(`/discussion/${pub.id}`, { method: 'POST', body: JSON.stringify({ text: commentText, anchor: { blockId: selection.blockId, exactQuote: selection.quote, startIndex: selection.startIndex } }) });
        await loadPublicComments();
      } else {
        await apiFetch(`/notes/${id}/comments`, { method: 'POST', body: JSON.stringify({ blockId: selection.blockId, exactQuote: selection.quote, startIndex: selection.startIndex, endIndex: selection.endIndex, text: commentText }) });
        await loadPrivateComments();
      }
      setShowCommentModal(false); setSelection(null);
      addToast('Comentário salvo.');
    } catch (err) { addToast(err.message || 'Falha ao comentar.', 'warning'); }
    finally { setCommentBusy(false); }
  };

  const deletePrivateComment = async commentId => {
    try { await apiFetch(`/note-comments/${commentId}`, { method: 'DELETE' }); setPrivateComments(prev => prev.filter(c => c.id !== commentId)); }
    catch (err) { addToast(err.message || 'Falha ao excluir comentário.', 'warning'); }
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

  // Calculado antes dos retornos antecipados porque os hooks abaixo dependem dele.
  const savedTagIds = (note?.tags || []).map(t => t.id);
  const isDirty =
    !!note &&
    (title !== note.title ||
      JSON.stringify(blocks) !== (note.content || '[]') ||
      (categoryId || null) !== (note.categoryId || null) ||
      tagIds.length !== savedTagIds.length ||
      tagIds.some(t => !savedTagIds.includes(t)));

  // --- Desfazer / refazer (Ctrl+Z, Ctrl+Y ou Ctrl+Shift+Z) -------------------------------
  // Histórico de { blocks, title }. A digitação é agrupada: um passo a cada pausa de 500 ms.
  const commitHistory = snap => {
    const h = historyRef.current;
    if (!h.present || (h.present.blocks === snap.blocks && h.present.title === snap.title)) return;
    h.past.push(h.present);
    if (h.past.length > HISTORY_LIMIT) h.past.shift();
    h.present = snap;
    h.future = [];
  };

  useEffect(() => {
    const snap = { blocks, title };
    latestRef.current = snap;
    const h = historyRef.current;
    if (applyingHistoryRef.current || !h.present) {
      applyingHistoryRef.current = false;
      h.present = snap;
      return;
    }
    clearTimeout(historyTimerRef.current);
    historyTimerRef.current = setTimeout(() => commitHistory(snap), 500);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [blocks, title]);

  const applySnapshot = snap => {
    // Tira o foco do bloco para ele poder ser redesenhado; depois devolve o cursor ao mesmo bloco.
    const active = document.activeElement;
    const focusedBlockId = active?.dataset?.blockId;
    const focusedTitle = active?.classList?.contains('ne-title');
    if (focusedBlockId) active.blur();
    applyingHistoryRef.current = true;
    setBlocks(snap.blocks);
    setTitle(snap.title);
    setTimeout(() => {
      if (focusedTitle) return document.querySelector('.ne-title')?.focus();
      const el = focusedBlockId && document.querySelector(`[data-block-id="${focusedBlockId}"]`);
      if (!el) return;
      el.focus();
      const range = document.createRange();
      range.selectNodeContents(el);
      range.collapse(false);
      const sel = window.getSelection();
      sel.removeAllRanges();
      sel.addRange(range);
    }, 0);
  };

  const moveHistory = direction => {
    const h = historyRef.current;
    // Digitação ainda não registrada vira um passo antes de desfazer.
    if (historyTimerRef.current) {
      clearTimeout(historyTimerRef.current);
      historyTimerRef.current = null;
      commitHistory(latestRef.current);
    }
    const from = direction === 'undo' ? h.past : h.future;
    const to = direction === 'undo' ? h.future : h.past;
    if (!from.length) return;
    to.push(h.present);
    h.present = from.pop();
    applySnapshot(h.present);
  };

  historyActionsRef.current = {
    move: moveHistory,
    enabled: !!note && note.status !== 'Trashed' && !editingChart,
  };

  useEffect(() => {
    const onKeyDown = e => {
      if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
      const key = e.key.toLowerCase();
      const isUndo = key === 'z' && !e.shiftKey;
      const isRedo = key === 'y' || (key === 'z' && e.shiftKey);
      if (!isUndo && !isRedo) return;
      const target = e.target instanceof Element ? e.target : null;
      // Campos fora do texto da nota (tags, modais, gráficos) mantêm o desfazer nativo do navegador.
      const inNote = target?.closest('.ne-title, #editor-blocks');
      const inOtherField = target?.closest('input, textarea, select, [contenteditable], .modal-overlay');
      if (inOtherField && !inNote) return;
      if (!historyActionsRef.current.enabled) return;
      e.preventDefault();
      historyActionsRef.current.move(isUndo ? 'undo' : 'redo');
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  // --- Aviso ao sair com alterações não salvas -----------------------------------------
  // Navegação dentro do app: modal próprio. Fechar/recarregar a aba: aviso nativo do navegador
  // (o texto desse aviso não pode ser personalizado).
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) => isDirty && currentLocation.pathname !== nextLocation.pathname,
  );
  useEffect(() => {
    if (!isDirty) return;
    const onBeforeUnload = e => {
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [isDirty]);
  const [leaveBusy, setLeaveBusy] = useState(false);
  const saveAndLeave = async () => {
    setLeaveBusy(true);
    const ok = await handleSave();
    setLeaveBusy(false);
    if (ok) blocker.proceed?.();
    else blocker.reset?.();
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

  const isTrashed = note.status === 'Trashed';
  const sourceMarks = sourceCitations.map((c, index) => {
    const found = locateQuote(blocks, c);
    return found ? { ...c, referenceNumber: index + 1, blockId: found.blockId, startIndex: found.startIndex } : { ...c, referenceNumber: index + 1 };
  });
  const commentMarks = [...privateComments, ...publicComments
    .filter(c => c.anchorQuote && !c.isHidden && !c.isDeleted)
    .map(c => ({ ...c, isPublic: true, exactQuote: c.anchorQuote, blockId: c.anchorBlockId, startIndex: c.anchorStart }))]
    .map(c => { const found = locateQuote(blocks, c); return found ? { ...c, blockId: found.blockId, startIndex: found.startIndex } : c; });
  const doubtMarks = doubts.map(d => { const found = locateQuote(blocks, d); return found ? { ...d, blockId: found.blockId, startIndex: found.startIndex } : d; });
  const labels = listLabels(blocks);
  const focusedBlock = blocks.find(b => b.id === focusedBlockId && !isChartBlock(b)) || null;

  // Todas as marcações aparecem juntas no texto, qualquer que seja a aba ativa.
  const toAnchor = kind => m => ({ kind, id: m.id, blockId: m.blockId, exactQuote: m.exactQuote, startIndex: m.startIndex, referenceNumber: m.referenceNumber });
  const allAnchors = [
    ...doubtMarks.map(toAnchor('doubt')),
    ...commentMarks.map(toAnchor('comment')),
    ...sourceMarks.map(toAnchor('source')),
  ];
  const markLookup = { doubt: doubtMarks, comment: commentMarks, source: sourceMarks };
  const markCardItems = markCard
    ? markCard.marks
        .map(m => {
          const data = markLookup[m.kind]?.find(x => String(x.id) === m.id);
          return data ? { kind: m.kind, data } : null;
        })
        .filter(Boolean)
    : [];
  const showInPanel = kind => {
    setMarkCard(null);
    selectAside(kind === 'doubt' ? 'doubts' : kind === 'comment' ? 'comments' : kind === 'source' ? 'sources' : kind);
    setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 0);
  };

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

  const isArchivedOrTrashed = note.status === 'Archived' || isTrashed;
  const saveStateLabel = saving ? 'Salvando…' : isDirty ? 'Alterações não salvas' : 'Alterações salvas';
  const wordCount = blocks
    .filter(b => !isChartBlock(b))
    .reduce((total, b) => total + ((b.text || '').trim().match(/\S+/g)?.length || 0), 0);
  const lastEdited = note.updatedAt
    ? new Date(note.updatedAt).toLocaleString('pt-BR', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
    : null;
  const commentCount = privateComments.length + publicComments.length;
  const asideTabs = [
    { key: 'doubts', label: 'Dúvidas', icon: 'question', count: doubts.length },
    { key: 'comments', label: 'Comentários', icon: 'message', count: commentCount },
    { key: 'sources', label: 'Fontes', icon: 'book', count: sourceCitations.length },
  ];
  // Clique no espaço vazio da folha (fora dos blocos) põe o cursor no fim do último bloco de texto.
  const focusDocumentEnd = e => {
    if (isTrashed || (e.target !== e.currentTarget && e.target.id !== 'editor-blocks')) return;
    const textBlocks = e.currentTarget.querySelectorAll('[data-block-id]');
    const last = textBlocks[textBlocks.length - 1];
    if (!last) return;
    e.preventDefault();
    last.focus();
    const range = document.createRange();
    range.selectNodeContents(last);
    range.collapse(false);
    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);
  };
  const openTagInput = () => {
    setDetailsOpen(true);
    setTimeout(() => document.getElementById('ne-tag-input')?.focus(), 0);
  };

  return (
    <div className="ne-page">
      <header className="ne-topbar">
        <div className="ne-topbar-left">
          <span className="ne-save-state" role="status">
            <span className={`ne-status-dot ${isDirty || saving ? 'unsaved' : ''}`} />
            {saveStateLabel}
          </span>
        </div>
        <div className="ne-topbar-actions">
          <button className="icon-btn" onClick={() => setShowHistory(true)} title="Histórico de versões" aria-label="Ver histórico de versões">
            <Icon name="clock" />
          </button>
          {isArchivedOrTrashed ? (
            <button className="icon-btn" onClick={() => handleLifecycle('restore')} title="Restaurar nota" aria-label="Restaurar nota">
              <Icon name="refresh" />
            </button>
          ) : (
            <button className="icon-btn" onClick={() => handleLifecycle('archive')} title="Arquivar nota" aria-label="Arquivar nota">
              <Icon name="archive" />
            </button>
          )}
          <button className="btn compact ne-details-toggle" onClick={() => setDetailsOpen(open => !open)} aria-expanded={detailsOpen} aria-controls="ne-details">
            Detalhes <Icon name="down" className="sm" />
          </button>
          {(isDirty || saving) && !isTrashed && (
            <button className="btn compact" onClick={handleSave} disabled={saving}>
              <Icon name="save" className="sm" /> {saving ? 'Salvando…' : 'Salvar'}
            </button>
          )}
          <button className="btn compact primary" onClick={() => setShowPublish(true)}>
            <Icon name="globe" className="sm" /> Publicar
          </button>
        </div>
      </header>

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

      <div className="ne-layout" ref={layoutRef} style={{ '--ne-details-width': `${detailsWidth}px` }}>
        <article className="ne-editor" style={{ '--ne-text-scale': textScale }}>
          <div className="ne-context">
            <span className="ne-category-dot" />
            {note.categoryName || 'Sem categoria'}
            <span aria-hidden="true">•</span>
            <span>{STATUS_LABEL[note.status] || note.status}</span>
          </div>

          <input
            className="ne-title"
            value={title}
            onChange={e => setTitle(e.target.value)}
            onKeyDown={handleTitleKeyDown}
            placeholder="Sem título"
            aria-label="Título da nota"
          />

          <div className="ne-quick-tags" aria-label="Tags da nota">
            {(note.tags || []).map((tag, index) => (
              <span key={tag.id}>
                {index === 0 && <Icon name="tag" className="sm" />} {tag.name}
              </span>
            ))}
            {!isTrashed && (
              <button type="button" onClick={openTagInput} aria-label="Adicionar tag" title="Adicionar tag">
                <Icon name="plus" className="sm" />
              </button>
            )}
          </div>

          <div className="ne-tools" role="toolbar" aria-label="Ferramentas da nota">
            {/* Tipo do bloco onde está o cursor; trocar aqui converte o bloco (ex.: parágrafo → título). */}
            <select
              value={blockTypeKey(focusedBlock)}
              onChange={e => changeBlockType(focusedBlock?.id, e.target.value)}
              disabled={isTrashed || !focusedBlock}
              aria-label="Tipo do texto onde está o cursor"
              title={focusedBlock ? 'Tipo do texto onde está o cursor' : 'Clique num trecho do texto para trocar o tipo'}
            >
              {BLOCK_TYPES.map(t => (
                <option key={t.key} value={t.key}>{t.label}</option>
              ))}
            </select>
            <button className="ne-format-btn" onMouseDown={e => e.preventDefault()} onClick={() => formatSelection('bold')} disabled={isTrashed} aria-label="Negrito" title="Negrito (Ctrl+B)">
              <strong>B</strong>
            </button>
            <button className="ne-format-btn" onMouseDown={e => e.preventDefault()} onClick={() => formatSelection('italic')} disabled={isTrashed} aria-label="Itálico" title="Itálico (Ctrl+I)">
              <em>I</em>
            </button>
            <div className="ne-block-menu-anchor">
              <button
                onMouseDown={e => e.preventDefault()}
                onClick={() => setBlockMenuFor(m => (m === '__toolbar' ? null : '__toolbar'))}
                disabled={isTrashed}
                aria-label="Inserir bloco abaixo do cursor"
                aria-expanded={blockMenuFor === '__toolbar'}
                title="Inserir bloco abaixo de onde está o cursor"
              >
                <Icon name="plus" className="sm" />
              </button>
              {blockMenuFor === '__toolbar' && (
                <BlockTypeMenu onPick={key => insertBlockAfter(focusedBlock?.id || blocks[blocks.length - 1]?.id, key)} onClose={() => setBlockMenuFor(null)} />
              )}
            </div>
            <span className="ne-tool-divider" />
            {/* onMouseDown preventDefault: não rouba o foco nem apaga a seleção do editor */}
            <button onMouseDown={e => e.preventDefault()} onClick={handleMarkDoubt} disabled={isTrashed} title="Selecione um trecho e marque como dúvida">
              <Icon name="question" className="sm" /> Dúvida
            </button>
            <button onMouseDown={e => e.preventDefault()} onClick={handleComment} disabled={isTrashed} title="Selecione um trecho para comentar">
              <Icon name="message" className="sm" /> Comentar
            </button>
            <button onMouseDown={e => e.preventDefault()} onClick={handleCite} disabled={isTrashed} title="Selecione um trecho e vincule uma fonte">
              <Icon name="book" className="sm" /> Citar
            </button>
            <span className="ne-tool-spacer" />
            {/* Zoom de leitura: aumenta ou diminui todo o texto da nota na mesma proporção. */}
            <div className="ne-zoom" role="group" aria-label="Tamanho do texto">
              <button onClick={() => changeTextScale(-1)} disabled={textScale <= TEXT_SCALES[0]} aria-label="Diminuir texto" title="Diminuir texto">
                A−
              </button>
              <button className="ne-zoom-value" onClick={() => changeTextScale(0)} title="Voltar ao tamanho padrão">
                {Math.round(textScale * 100)}%
              </button>
              <button onClick={() => changeTextScale(1)} disabled={textScale >= TEXT_SCALES[TEXT_SCALES.length - 1]} aria-label="Aumentar texto" title="Aumentar texto">
                A+
              </button>
            </div>
            <span className="ne-tool-divider" />
            <button
              onClick={handleOrganize}
              disabled={isTrashed}
              title="Organiza títulos, listas, destaques e tabelas com regras locais (nada sai do Notee)"
            >
              <Icon name="grid" className="sm" /> Organizar em tópicos
            </button>
            <button
              className="ne-ai-tool"
              onClick={() => setShowOrganizeAi(true)}
              disabled={isTrashed || !!editingChart}
              title="Envia o texto para o provedor de IA configurado, com custo e confirmação"
            >
              <Icon name="spark" className="sm" /> Organizar com IA
            </button>
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

          {/* data-mode: cor da seleção de texto acompanha a aba ativa (dúvida, comentário ou fonte). */}
          <div className={`ne-document ${isTrashed ? 'is-readonly' : ''} ${multiSel ? 'is-multi-select' : ''}`} data-mode={activeAside} onMouseDown={focusDocumentEnd}>

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

            <div
              id="editor-blocks"
              style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}
              onMouseOver={handleBlocksMouseOver}
              onMouseOut={handleBlocksMouseOut}
              onClick={handleBlocksClick}
              onPointerDown={handleBlocksPointerDown}
            >
              {blocks.map(block => (
                <div key={block.id} className={`ne-block-row ${blockMenuFor === block.id ? 'menu-open' : ''}`}>
                  {/* "+" à esquerda do bloco (aparece no hover): insere o novo bloco logo abaixo deste. */}
                  {!isTrashed && (
                    <div className="ne-block-gutter">
                      <button
                        type="button"
                        className="ne-block-add"
                        onMouseDown={e => e.preventDefault()}
                        onClick={() => setBlockMenuFor(m => (m === block.id ? null : block.id))}
                        aria-label="Inserir bloco abaixo"
                        aria-expanded={blockMenuFor === block.id}
                        title="Inserir bloco abaixo"
                      >
                        <Icon name="plus" className="sm" />
                      </button>
                      {blockMenuFor === block.id && (
                        <BlockTypeMenu onPick={key => insertBlockAfter(block.id, key)} onClose={() => setBlockMenuFor(null)} />
                      )}
                    </div>
                  )}
                {isChartBlock(block) ? (
                  <div className="editor-chart-block">
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
                  block={block}
                  anchors={allAnchors}
                  listLabel={labels.get(block.id)}
                  repaintToken={formatTick.id === block.id ? formatTick.n : 0}
                  onChange={newText => handleUpdateBlock(block.id, newText)}
                  onSelect={sel => handleTextSelect(block.id, sel)}
                  onFocusBlock={setFocusedBlockId}
                  onFormat={isTrashed ? undefined : (kind, offsets) => applyFormat(block.id, offsets, kind)}
                  onListKey={isTrashed ? undefined : (action, info) => handleListKey(block.id, action, info)}
                  onNavigate={(direction, x) => navigateFromBlock(block.id, direction, x)}
                  editable={!multiSel}
                  onSelectAll={isTrashed ? undefined : selectAllBlocks}
                  onPasteTables={isTrashed ? undefined : payload => handlePasteTables(block.id, payload)}
                  onPasteStructured={() =>
                    setNotice({ message: 'Texto com títulos, listas ou negritos colado.', restore: null, offerOrganize: true })
                  }
                  onRemoveEmpty={isTrashed ? undefined : () => handleRemoveEmpty(block.id)}
                />
                )}
                </div>
              ))}
            </div>
          </div>

          <section className="ne-bottom" ref={bottomRef} aria-label="Dúvidas, comentários e fontes da nota">
            <div className="ne-tabs" role="tablist" aria-label="Painel da nota">
              {asideTabs.map(tab => (
                <button
                  key={tab.key}
                  type="button"
                  role="tab"
                  aria-selected={activeAside === tab.key}
                  className={activeAside === tab.key ? 'active' : ''}
                  onClick={() => selectAside(tab.key)}
                >
                  <Icon name={tab.icon} className="sm" /> {tab.label}
                  {tab.count > 0 && <span className="ne-tab-count">{tab.count}</span>}
                </button>
              ))}
            </div>
            <div className="ne-tab-panel" role="tabpanel">
          {activeAside === 'sources' ? <SourcesPanel noteId={id} noteBlocks={blocks} isDirty={isDirty} selectedAnchor={selection} onClearAnchor={() => setSelection(null)} onCitationsChange={setSourceCitations} /> : activeAside === 'comments' ? <div className="result-panel">
            <h4>Comentários desta nota</h4>
            {privateComments.length + publicComments.length === 0 && <p className="small muted">Selecione um trecho e clique em Comentar trecho.</p>}
            {privateComments.map(c => <div key={c.id} className="card research-card" style={{ marginBottom: 12, border: c.id === selectedCommentId ? '2px solid #eab308' : undefined }}>
              <span className="pill">🔒 Só eu</span>
              <blockquote className="quote-selection">{c.exactQuote}</blockquote>
              {!locateQuote(blocks, c) && <p className="small muted">Trecho alterado</p>}
              <p>{c.text}</p>
              <button type="button" className="btn compact ghost" onClick={() => deletePrivateComment(c.id)}>Excluir</button>
            </div>)}
            {publicComments.map(c => <div key={c.id} className="card research-card" style={{ marginBottom: 12, border: c.id === selectedCommentId ? '2px solid #eab308' : undefined }}><span className="pill blue">Público</span>{c.isHidden && <span className="pill">Oculto</span>}{c.anchorQuote && <blockquote className="quote-selection">{c.anchorQuote}</blockquote>}{c.anchorQuote && !locateQuote(blocks, c) && <p className="small muted">Comentário sobre versão anterior</p>}<p>{c.isDeleted ? 'Comentário removido.' : c.text}</p></div>)}
          </div> : (doubts.length > 0 ? (
            <div className="result-panel">
              <div className="row between mb16">
                <h4>Dúvidas desta nota</h4>
                <span className="badge-count">{doubts.length}</span>
              </div>

              {doubts.map(d => {
                // Monitorização em tempo real: recalculada a cada tecla, porque `blocks` muda no onInput.
                const isReferenceMissing = !!d.exactQuote && !locateQuote(blocks, d);
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
                            {/* Resposta com [n] ligados às fontes (link quando conferida). */}
                            <AnswerText answer={d.answer} sources={d.sources || []} className="ne-doubt-answer" />
                            {d.sources?.length > 0 ? (
                              <AnswerSources sources={d.sources} compact />
                            ) : (
                              <p className="small muted">Sem fonte: a IA não indicou de onde veio a resposta.</p>
                            )}
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
            <div className="ne-empty-state">
              <Icon name="question" />
              <span>
                <strong>Nenhuma dúvida marcada</strong>
                Selecione um trecho do texto e clique em “Dúvida” para transformá-lo em uma pergunta.
              </span>
            </div>
          ))}
            </div>
          </section>
        </article>

        <div
          className={`pane-resizer ne-resizer ${isResizing ? 'is-resizing' : ''}`}
          role="separator"
          aria-orientation="vertical"
          aria-label="Ajustar largura entre o texto e os detalhes da nota"
          aria-controls="ne-details"
          aria-valuemin={DETAILS_MIN}
          aria-valuemax={DETAILS_MAX}
          aria-valuenow={detailsWidth}
          tabIndex={0}
          title="Arraste para ajustar a largura · duplo clique volta ao padrão"
          onPointerDown={handleResizerPointerDown}
          onKeyDown={handleResizerKeyDown}
          onDoubleClick={() => setDetailsWidth(clampDetailsWidth(DETAILS_DEFAULT))}
        />

        {detailsOpen && <button type="button" className="ne-details-backdrop" onClick={() => setDetailsOpen(false)} aria-label="Fechar detalhes" />}
        <aside id="ne-details" className={`ne-details ${detailsOpen ? 'is-open' : ''}`} aria-label="Detalhes da nota">
          <div className="ne-details-heading">
            <div>
              <strong>Detalhes da nota</strong>
              <p>Organize e acompanhe</p>
            </div>
            <button type="button" className="icon-btn ne-details-close" onClick={() => setDetailsOpen(false)} aria-label="Fechar detalhes">
              <Icon name="close" />
            </button>
          </div>

          <div className="ne-detail-section">
            <NoteOrganizer
              variant="panel"
              noteId={id}
              noteVersion={note.version}
              title={title}
              blocks={blocks}
              categoryId={categoryId}
              tagIds={tagIds}
              onCategoryChange={setCategoryId}
              onTagsChange={setTagIds}
              disabled={isTrashed}
              isDirty={isDirty}
            />
          </div>

          <div className="ne-detail-section ne-note-info">
            {lastEdited && (
              <div>
                <Icon name="clock" className="sm" />
                <span><small>Última edição salva</small>{lastEdited}</span>
              </div>
            )}
            <div>
              <Icon name="note" className="sm" />
              <span><small>Extensão</small>{wordCount} {wordCount === 1 ? 'palavra' : 'palavras'}</span>
            </div>
            <div>
              <Icon name="folder" className="sm" />
              <span><small>Local</small>Meu espaço · v{note.version}</span>
            </div>
          </div>

          <div className="ne-detail-section ne-suggestions">
            <NoteSuggestions noteId={id} isDirty={isDirty} disabled={isTrashed} onNoteChanged={loadNoteAndDoubts} />
          </div>

          {!isTrashed && (
            <div className="ne-danger-zone">
              <button type="button" onClick={() => handleLifecycle('trash')}>
                <Icon name="trash" className="sm" /> Mover para a lixeira
              </button>
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
      {markCard && markCardItems.length > 0 && (
        <MarkPopover
          anchorRect={markCard.rect}
          items={markCardItems}
          onShowPanel={showInPanel}
          onMouseEnter={cancelHideMarkCard}
          onMouseLeave={scheduleHideMarkCard}
        />
      )}
      {blocker.state === 'blocked' && (
        <div className="modal-overlay">
          <section className="modal ne-leave-modal" role="alertdialog" aria-modal="true" aria-labelledby="ne-leave-title" aria-describedby="ne-leave-text">
            <div className="ne-leave-icon">
              <Icon name="warning" />
            </div>
            <h2 id="ne-leave-title">Sair sem salvar?</h2>
            <p id="ne-leave-text">
              Você tem alterações não salvas em <strong>{title || 'Sem título'}</strong>. Se sair agora, elas serão perdidas.
            </p>
            <div className="ne-leave-actions">
              <button type="button" className="btn" onClick={() => blocker.reset()} disabled={leaveBusy} autoFocus>
                Continuar editando
              </button>
              <button type="button" className="btn danger" onClick={() => blocker.proceed()} disabled={leaveBusy}>
                Sair sem salvar
              </button>
              <button type="button" className="btn primary" onClick={saveAndLeave} disabled={leaveBusy || isTrashed}>
                {leaveBusy ? 'Salvando…' : 'Salvar e sair'}
              </button>
            </div>
          </section>
        </div>
      )}
      {showCommentModal && selection && <div className="modal-overlay"><section className="modal" role="dialog" aria-modal="true" aria-label="Comentar trecho">
        <div className="modal-title"><h2>Comentar trecho</h2><button type="button" className="icon-btn" onClick={() => setShowCommentModal(false)} aria-label="Fechar"><Icon name="close" /></button></div>
        <form onSubmit={handleSubmitComment}>
          <blockquote className="quote-selection">{selection.quote}</blockquote>
          <label htmlFor="private-comment-text">Comentário</label>
          <textarea id="private-comment-text" required maxLength={2000} value={commentText} onChange={e => setCommentText(e.target.value)} />
          <label htmlFor="comment-visibility">Visibilidade</label>
          <select id="comment-visibility" value={commentVisibility} onChange={e => setCommentVisibility(e.target.value)}><option value="private">Só eu</option><option value="public" disabled={!publications.some(p => p.status === 'Published' && p.visibility === 'public' && p.commentsEnabled)}>Público na publicação</option></select>
          {!publications.some(p => p.status === 'Published' && p.visibility === 'public' && p.commentsEnabled) && <p className="small muted">Publique a nota como Pública e ligue os comentários para usar a opção pública.</p>}
          <div className="modal-actions mt24"><button type="button" className="btn" onClick={() => setShowCommentModal(false)}>Cancelar</button><button type="submit" className="btn primary" disabled={commentBusy || !commentText.trim()}>Salvar comentário</button></div>
        </form>
      </section></div>}
    </div>
  );
}
