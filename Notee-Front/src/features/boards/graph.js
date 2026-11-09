// Regras visuais e de layout do canvas (portadas do protótipo v1.2, sem estado global).

export const RELATION_TYPES = {
  none: 'Sem rótulo',
  related: 'Relacionado a',
  causes: 'Causa',
  example: 'Exemplo de',
  contradicts: 'Contradiz',
  depends: 'Depende de',
  influences: 'Influencia',
  custom: 'Personalizado',
};

export const CARD = { width: 206, height: 124 };
export const DOUBT = { width: 174, height: 52, gapX: 14, offsetY: 75, step: 61 };
export const STAGE_SIZE = 6400;
export const COORD_MIN = -1000;
export const COORD_MAX = 7000;
export const ZOOM_MIN = 0.3;
export const ZOOM_MAX = 2.2;

export const edgeName = edge =>
  (edge.label || '').trim() || RELATION_TYPES[edge.type] || RELATION_TYPES.none;

/** Rótulo exibido sobre a linha: vazio quando o tipo é "none" e não há texto. */
export const edgeDisplayLabel = edge => (edge.type === 'none' && !edge.label ? '' : edgeName(edge));

/** Zoom mantendo fixo o ponto (px, py) da superfície. */
export function zoomAround(view, nextScale, px, py) {
  const scale = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, nextScale));
  return {
    scale,
    tx: px - (px - view.tx) * (scale / view.scale),
    ty: py - (py - view.ty) * (scale / view.scale),
  };
}

export const clampCoord =v => Math.min(COORD_MAX, Math.max(COORD_MIN, Math.round(v)));

/** Curva Bézier da origem para o destino, saindo pela lateral mais próxima. */
export function edgeGeometry(from, to) {
  const forward = to.x >= from.x;
  const x1 = from.x + (forward ? CARD.width : 0);
  const y1 = from.y + CARD.height * 0.5;
  const x2 = to.x + (forward ? 0 : CARD.width);
  const y2 = to.y + CARD.height * 0.5;
  const spread = Math.max(55, Math.abs(x2 - x1) * 0.45);
  const path = `M ${x1} ${y1} C ${x1 + (forward ? spread : -spread)} ${y1}, ${x2 - (forward ? spread : -spread)} ${y2}, ${x2} ${y2}`;
  return { path, mx: (x1 + x2) / 2, my: (y1 + y2) / 2 - 13 };
}

/** Posição do satélite de dúvida i de uma nota. */
export const doubtPosition = (node, index) => ({
  x: node.x + CARD.width + DOUBT.gapX,
  y: node.y + DOUBT.offsetY + index * DOUBT.step,
});

/** Enquadra todos os cartões (e satélites) dentro da superfície visível. */
export function fitView(nodes, doubtCountByNote, bounds) {
  if (!nodes.length || !bounds.width) return { tx: 100, ty: 100, scale: 1 };
  const minX = Math.min(...nodes.map(n => n.x)) - 35;
  const minY = Math.min(...nodes.map(n => n.y)) - 55;
  const maxX = Math.max(...nodes.map(n => n.x + CARD.width + (doubtCountByNote.get(n.noteId) ? DOUBT.width + DOUBT.gapX + 6 : 0))) + 35;
  const maxY = Math.max(
    ...nodes.map(n => {
      const count = doubtCountByNote.get(n.noteId) || 0;
      return Math.max(n.y + CARD.height, count ? n.y + DOUBT.offsetY + (count - 1) * DOUBT.step + DOUBT.height : 0);
    }),
  ) + 35;
  const scale = Math.min(1.05, Math.max(0.35, Math.min((bounds.width - 45) / (maxX - minX), (bounds.height - 72) / (maxY - minY))));
  return {
    scale,
    tx: (bounds.width - (maxX - minX) * scale) / 2 - minX * scale,
    ty: (bounds.height - (maxY - minY) * scale) / 2 - minY * scale,
  };
}

/** Layout automático por vizinhança (BFS a partir das notas mais conectadas), em 3 colunas. */
export function autoLayout(nodes, edges) {
  const ids = new Set(nodes.map(n => n.noteId));
  const titles = new Map(nodes.map(n => [n.noteId, n.title]));
  const degree = new Map(nodes.map(n => [n.noteId, 0]));
  const adjacency = new Map(nodes.map(n => [n.noteId, new Set()]));
  for (const e of edges) {
    if (!ids.has(e.fromNoteId) || !ids.has(e.toNoteId)) continue;
    adjacency.get(e.fromNoteId).add(e.toNoteId);
    adjacency.get(e.toNoteId).add(e.fromNoteId);
    degree.set(e.fromNoteId, degree.get(e.fromNoteId) + 1);
    degree.set(e.toNoteId, degree.get(e.toNoteId) + 1);
  }
  const seen = new Set();
  const ordered = [];
  while (seen.size < ids.size) {
    const root = [...ids]
      .filter(id => !seen.has(id))
      .sort((a, b) => degree.get(b) - degree.get(a) || titles.get(a).localeCompare(titles.get(b)))[0];
    if (!root) break;
    let pending = [root];
    seen.add(root);
    while (pending.length) {
      ordered.push(...pending);
      const next = [];
      for (const id of pending) {
        for (const related of [...adjacency.get(id)].sort()) {
          if (!seen.has(related)) {
            seen.add(related);
            next.push(related);
          }
        }
      }
      pending = next;
    }
  }
  return ordered.map((noteId, i) => ({ noteId, x: 105 + (i % 3) * 405, y: 115 + Math.floor(i / 3) * 245 }));
}

export const formatDate = iso => {
  try {
    return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' });
  } catch {
    return '';
  }
};

export const errorMessage = (err, fallback) => err?.problem?.detail || err?.message || fallback;
