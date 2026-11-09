// Superfície do mapa: pan (arrastar fundo), zoom (scroll/botões), arrastar cartões,
// arestas SVG dirigidas e dúvidas satélites. Posição só é enviada ao soltar o cartão.
import React, { useEffect, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { CARD, STAGE_SIZE, clampCoord, doubtPosition, edgeDisplayLabel, edgeGeometry, edgeName, zoomAround } from './graph';

const DRAG_THRESHOLD = 3;

export function BoardCanvas({
  surfaceRef,
  nodes,
  edges,
  doubts,
  view,
  setView,
  selectedNoteId,
  selectedDoubtId,
  linkFromId,
  onSelectNote,
  onSelectDoubt,
  onEditEdge,
  onMoveEnd,
}) {
  const [live, setLive] = useState(null); // { noteId, x, y } durante o arrasto
  const [panning, setPanning] = useState(false);
  const dragRef = useRef(null);
  const panRef = useRef(null);
  const suppressClickRef = useRef(false);
  const viewRef = useRef(view);
  useEffect(() => {
    viewRef.current = view;
  }, [view]);

  // Zoom com a roda do mouse: listener nativo (não passivo) para poder cancelar o scroll da página.
  useEffect(() => {
    const surface = surfaceRef.current;
    if (!surface) return undefined;
    const onWheel = e => {
      e.preventDefault();
      const rect = surface.getBoundingClientRect();
      const factor = Math.exp(-e.deltaY * 0.0015);
      setView(zoomAround(viewRef.current, viewRef.current.scale * factor, e.clientX - rect.left, e.clientY - rect.top));
    };
    surface.addEventListener('wheel', onWheel, { passive: false });
    return () => surface.removeEventListener('wheel', onWheel);
  }, [surfaceRef, setView]);

  const positionOf = node => (live && live.noteId === node.noteId ? live : node);
  const nodeById = new Map(nodes.map(n => [n.noteId, positionOf(n)]));

  // ---- Pan do fundo ----
  const onSurfacePointerDown = e => {
    if (e.button !== 0 || e.target.closest('.graph-note, .graph-doubt, .graph-edge')) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    panRef.current = { x: e.clientX, y: e.clientY, tx: view.tx, ty: view.ty };
    setPanning(true);
  };
  const onSurfacePointerMove = e => {
    const pan = panRef.current;
    if (!pan) return;
    setView(v => ({ ...v, tx: pan.tx + e.clientX - pan.x, ty: pan.ty + e.clientY - pan.y }));
  };
  const endPan = () => {
    panRef.current = null;
    setPanning(false);
  };

  // ---- Arrasto de cartões ----
  const onNodePointerDown = (e, node) => {
    if (e.button !== 0) return;
    e.stopPropagation();
    e.currentTarget.setPointerCapture(e.pointerId);
    dragRef.current = { noteId: node.noteId, sx: e.clientX, sy: e.clientY, ox: node.x, oy: node.y, moved: false };
  };
  const onNodePointerMove = e => {
    const drag = dragRef.current;
    if (!drag) return;
    const dx = (e.clientX - drag.sx) / view.scale;
    const dy = (e.clientY - drag.sy) / view.scale;
    if (!drag.moved && Math.abs(dx) + Math.abs(dy) < DRAG_THRESHOLD) return;
    drag.moved = true;
    drag.x = clampCoord(drag.ox + dx);
    drag.y = clampCoord(drag.oy + dy);
    setLive({ noteId: drag.noteId, x: drag.x, y: drag.y });
  };
  const onNodePointerUp = () => {
    const drag = dragRef.current;
    dragRef.current = null;
    if (!drag?.moved) return;
    // O click que segue o pointerup não deve selecionar; o timeout limpa a flag caso ele não ocorra.
    suppressClickRef.current = true;
    setTimeout(() => {
      suppressClickRef.current = false;
    }, 0);
    onMoveEnd(drag.noteId, drag.x, drag.y); // o pai aplica a posição de forma otimista
    setLive(null);
  };
  const onNodeClick = noteId => {
    if (suppressClickRef.current) {
      suppressClickRef.current = false;
      return;
    }
    onSelectNote(noteId);
  };

  // Dúvidas agrupadas por nota, na ordem recebida.
  const doubtsByNote = new Map();
  for (const d of doubts) {
    if (!doubtsByNote.has(d.noteId)) doubtsByNote.set(d.noteId, []);
    doubtsByNote.get(d.noteId).push(d);
  }

  return (
    <section
      ref={surfaceRef}
      className={`graph-surface ${panning ? 'is-panning' : ''}`}
      aria-label="Quadro visual de notas. Arraste para mover, use rolagem para zoom."
      onPointerDown={onSurfacePointerDown}
      onPointerMove={onSurfacePointerMove}
      onPointerUp={endPan}
      onPointerCancel={endPan}
      style={{
        backgroundPosition: `${view.tx}px ${view.ty}px`,
        backgroundSize: `${23 * view.scale}px ${23 * view.scale}px`,
      }}
    >
      <div className="graph-stage" style={{ transform: `translate(${view.tx}px, ${view.ty}px) scale(${view.scale})` }}>
        <svg
          className="graph-links"
          width={STAGE_SIZE}
          height={STAGE_SIZE}
          viewBox={`0 0 ${STAGE_SIZE} ${STAGE_SIZE}`}
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            <marker id="graph-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto" markerUnits="userSpaceOnUse">
              <path d="M0 0 L7 3.5 L0 7Z" fill="#97ae8f" />
            </marker>
          </defs>

          {edges.map(edge => {
            const from = nodeById.get(edge.fromNoteId);
            const to = nodeById.get(edge.toNoteId);
            if (!from || !to) return null;
            const { path, mx, my } = edgeGeometry(from, to);
            const label = edgeDisplayLabel(edge);
            const w = Math.min(164, Math.max(57, label.length * 6 + 18));
            const related = selectedNoteId === edge.fromNoteId || selectedNoteId === edge.toNoteId;
            return (
              <g key={edge.id} className={`graph-edge ${related ? 'edge-related' : ''}`} onClick={() => onEditEdge(edge)}>
                <path d={path} className="graph-edge-line" markerEnd="url(#graph-arrow)" />
                <path d={path} className="graph-edge-hit" />
                {label && (
                  <>
                    <rect x={mx - w / 2} y={my - 13} width={w} height="24" rx="10" className="graph-edge-label-bg" />
                    <text x={mx} y={my + 2} textAnchor="middle" className="graph-edge-text">
                      {label.slice(0, 24)}
                    </text>
                  </>
                )}
                <title>{`${from.title} → ${to.title}: ${label || edgeName(edge)}`}</title>
              </g>
            );
          })}

          {[...doubtsByNote.entries()].flatMap(([noteId, list]) => {
            const node = nodeById.get(noteId);
            if (!node) return [];
            return list.map((d, i) => {
              const p = doubtPosition(node, i);
              return (
                <path
                  key={`wire-${d.id}`}
                  className="graph-doubt-wire"
                  d={`M${node.x + CARD.width} ${node.y + 88} Q${p.x - 4} ${node.y + 88} ${p.x} ${p.y + 25}`}
                />
              );
            });
          })}
        </svg>

        {nodes.map(raw => {
          const node = positionOf(raw);
          const selected = selectedNoteId === node.noteId || linkFromId === node.noteId;
          return (
            <button
              key={node.noteId}
              type="button"
              className={`graph-note ${selected ? 'selected' : ''}`}
              style={{ left: node.x, top: node.y }}
              aria-label={`Selecionar ${node.title}`}
              aria-pressed={selected}
              onPointerDown={e => onNodePointerDown(e, node)}
              onPointerMove={onNodePointerMove}
              onPointerUp={onNodePointerUp}
              onPointerCancel={() => {
                dragRef.current = null;
                setLive(null);
              }}
              onClick={() => onNodeClick(node.noteId)}
            >
              <span className="graph-note-top">
                <span className="graph-note-symbol">
                  <Icon name="note" className="sm" />
                </span>
                <span className="graph-note-category">{node.categoryName || 'Sem categoria'}</span>
              </span>
              <strong className="graph-note-title">{node.title}</strong>
              <span className="graph-note-summary">{(node.excerpt || '').slice(0, 84) || 'Clique para escrever nesta nota.'}</span>
              <span className="graph-note-foot">
                <span>
                  <Icon name="book" className="sm" /> {node.referenceCount} refs
                </span>
                <span>
                  <Icon name="question" className="sm" /> {node.doubtCount} dúvidas
                </span>
              </span>
              <span className="graph-connector" title="Conectar esta nota" />
            </button>
          );
        })}

        {[...doubtsByNote.entries()].flatMap(([noteId, list]) => {
          const node = nodeById.get(noteId);
          if (!node) return [];
          return list.map((d, i) => {
            const p = doubtPosition(node, i);
            return (
              <button
                key={d.id}
                type="button"
                className={`graph-doubt ${selectedDoubtId === d.id ? 'selected' : ''}`}
                style={{ left: p.x, top: p.y }}
                title={d.question}
                onClick={() => onSelectDoubt(d.id)}
              >
                <span className="graph-doubt-symbol">
                  <Icon name="question" className="sm" />
                </span>
                <span className="graph-doubt-content">
                  <strong>Dúvida</strong>
                  <small>{d.question}</small>
                </span>
              </button>
            );
          });
        })}
      </div>

      <div className="graph-viewport-help">
        <Icon name="info" className="sm" /> Arraste os cartões para organizar · arraste o fundo para navegar · scroll para zoom
      </div>
      <div className="graph-map-legend">
        <span className="legend-dot" /> Nota <span className="legend-dot amber" /> Dúvida <span className="legend-line" /> Relação
      </div>
    </section>
  );
}
