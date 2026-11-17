// Criação de nota assistida por IA (ADR-044): funções puras da prévia e a gravação do que foi aceito.
// Nada é salvo antes de "Criar nota"; cada sugestão (gráfico, categoria, tag, fonte) tem aceite próprio.

const NOTE_ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const SEGMENT = /^[A-Za-z0-9_-]{1,200}$/;

const randomPart = () =>
  typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).slice(2, 11);

/**
 * Reconhece o link de outra nota: /note/:id, /p/:handle/:slug ou /s/:token (URL completa ou caminho).
 * Devolve { kind, url } com a URL absoluta (usada como atribuição) ou null.
 */
export function parseNoteLink(input, origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost') {
  const value = String(input || '').trim();
  if (!value) return null;
  let url;
  try {
    url = value.startsWith('/') ? new URL(value, origin) : new URL(value);
  } catch {
    return null;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return null;
  const parts = url.pathname.split('/').filter(Boolean);
  const absolute = `${url.origin}${url.pathname}`;
  if (parts.length === 2 && parts[0] === 'note' && NOTE_ID.test(parts[1])) return { kind: 'own', url: absolute };
  if (parts.length === 3 && parts[0] === 'p' && SEGMENT.test(parts[1]) && SEGMENT.test(parts[2])) return { kind: 'public', url: absolute };
  if (parts.length === 2 && parts[0] === 's' && SEGMENT.test(parts[1])) return { kind: 'shared', url: absolute };
  return null;
}

/**
 * Estado inicial das escolhas da prévia: tudo sugerido começa marcado, e a pessoa desmarca o que não quer.
 * A fonte de atribuição (nota de outra pessoa) é obrigatória e não aparece como escolha.
 */
export function initialSelection(result) {
  return {
    charts: (result.charts || []).map(() => true),
    category: Boolean(result.category),
    tags: (result.tags || []).map(() => true),
    sources: (result.sources || []).map(() => true),
  };
}

/** Desmarcar a fonte de um gráfico gerado desmarca o gráfico (gráfico sempre com fonte). */
export function toggleSource(selection, result, index) {
  const sources = selection.sources.map((v, i) => (i === index ? !v : v));
  const charts = selection.charts.map((v, i) => {
    const chart = result.charts[i];
    return chart.origin === 'generated' && chart.sourceIndex === index && !sources[index] ? false : v;
  });
  return { ...selection, sources, charts };
}

/** Marcar um gráfico gerado marca a fonte dele. */
export function toggleChart(selection, result, index) {
  const charts = selection.charts.map((v, i) => (i === index ? !v : v));
  const chart = result.charts[index];
  const sources = [...selection.sources];
  if (charts[index] && chart.origin === 'generated' && chart.sourceIndex !== null && chart.sourceIndex !== undefined) {
    sources[chart.sourceIndex] = true;
  }
  return { ...selection, charts, sources };
}

/** Converte os blocos da resposta em blocos do editor; gráficos desmarcados saem. Tabela fica como texto (não vira gráfico sem fonte). */
export function buildBlocks(result, selection) {
  const blocks = [];
  for (const b of result.blocks || []) {
    if (b.type === 'chart') {
      const chart = result.charts[b.ref];
      if (chart && selection.charts[b.ref]) blocks.push({ ...chart.chart, id: `c-${randomPart()}` });
      continue;
    }
    const type = b.type === 'table' ? 'paragraph' : b.type;
    const block = { id: `b-${randomPart()}`, type, text: b.text };
    if (type === 'heading') block.level = b.level || 2;
    blocks.push(block);
  }
  return blocks;
}

/** Converte a fonte resolvida no corpo de POST /sources. */
export function sourceInput(resolved, notes = null) {
  return {
    kind: resolved.kind || 'website',
    // Limites de Source (Sources.cs): título/veículo 300, autor 140.
    title: String(resolved.title || '').slice(0, 300),
    authors: (resolved.authors || []).map(a => String(a).slice(0, 140)),
    year: resolved.year || null,
    venue: resolved.venue ? String(resolved.venue).slice(0, 300) : null,
    language: null,
    doi: resolved.doi || null,
    isbn: resolved.isbn || null,
    url: resolved.url || null,
    license: resolved.license || 'unknown',
    notes,
  };
}

/** Fontes aceitas + atribuição obrigatória, no formato de POST /sources. */
export function acceptedSources(result, selection) {
  const list = (result.sources || [])
    .filter((_, i) => selection.sources[i])
    .map(s => sourceInput(s.resolved, `Sugerida na criação da nota: ${s.reason}`.slice(0, 1000)));
  if (result.attribution) list.unshift(sourceInput(result.attribution, 'Nota original usada como base.'));
  return list;
}

/**
 * Grava o que foi aceito: categoria/tags novas → nota (já com taxonomia) → fontes e citações.
 * Falhas depois de criar a nota não desfazem a nota: voltam em `warnings`.
 */
export async function createComposedNote({ api, title, result, selection }) {
  const warnings = [];
  let categoryId = null;
  if (selection.category && result.category) {
    categoryId = result.category.existingId || (await ensureTaxonomy(api, 'categories', result.category.name));
  }
  const tagIds = [];
  for (const [i, tag] of (result.tags || []).entries()) {
    if (!selection.tags[i]) continue;
    tagIds.push(tag.existingId || (await ensureTaxonomy(api, 'tags', tag.name)));
  }

  const note = await api.createNote({
    title,
    content: JSON.stringify(buildBlocks(result, selection)),
    categoryId,
    tagIds,
  });

  for (const source of acceptedSources(result, selection)) {
    try {
      const sourceId = await ensureSource(api, source);
      await api.addCitation(note.id, { sourceId });
    } catch {
      warnings.push(source.title);
    }
  }
  return { note, warnings };
}

async function ensureTaxonomy(api, kind, name) {
  try {
    return (await api.createTaxonomy(kind, name)).id;
  } catch (err) {
    if (err?.status !== 409) throw err;
    const existing = (await api.listTaxonomy(kind)).find(x => x.name.toLowerCase() === name.toLowerCase());
    if (!existing) throw err;
    return existing.id;
  }
}

/** Mesma fonte já na biblioteca (DOI/ISBN repetido → 409): reaproveita em vez de duplicar. */
async function ensureSource(api, source) {
  try {
    return (await api.createSource(source)).id;
  } catch (err) {
    if (err?.status !== 409) throw err;
    const key = source.doi || source.isbn;
    const existing = (await api.listSources(key || source.title)).find(
      s => (source.doi && s.doi?.toLowerCase() === source.doi.toLowerCase()) || (source.isbn && s.isbn === source.isbn),
    );
    if (!existing) throw err;
    return existing.id;
  }
}
