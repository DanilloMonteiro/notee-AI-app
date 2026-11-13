const stopwords = new Set('sobre entre para pelos pelas desta deste esses essas muito ainda quando onde como mais menos todos todas texto nota notas dados uma uns nas nos das dos que por com sem são não foi era ser ter seu sua seus suas the and from'.split(' '));
const fold = value => String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
const words = value => fold(value).match(/[a-z0-9]{5,}/g) || [];
const containsName = (text, name) => {
  const normalized = fold(name).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`(^|[^a-z0-9])${normalized}(?=$|[^a-z0-9])`).test(fold(text));
};

export function suggestTaxonomy({ title = '', blocks = [], categories = [], tags = [], currentCategoryId = null, currentTagIds = [] }) {
  const headings = blocks.filter(b => b.type === 'heading').map(b => b.text || '').join(' ');
  const body = blocks.map(b => b.text || '').join(' ');
  const score = name => (containsName(title, name) ? 5 : 0) + (containsName(headings, name) ? 3 : 0) + (containsName(body, name) ? 1 : 0);
  const reason = name => containsName(title, name) ? 'Aparece no título da nota' : containsName(headings, name) ? 'Aparece em título de seção' : 'Aparece no texto';
  const existing = items => items.map(item => ({ name: item.name, existingId: item.id, isNew: false, reason: reason(item.name), score: score(item.name) })).filter(item => item.score > 0).sort((a, b) => b.score - a.score || a.name.localeCompare(b.name, 'pt-BR'));

  let category = currentCategoryId ? null : existing(categories)[0] || null;
  if (!category && !currentCategoryId) {
    const counts = new Map();
    words(headings).filter(w => !stopwords.has(w)).forEach(w => counts.set(w, (counts.get(w) || 0) + 1));
    const dominant = [...counts].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))[0];
    if (dominant) category = { name: dominant[0], isNew: true, reason: 'Nenhuma categoria existente combina', score: dominant[1] };
  }

  const selected = new Set(currentTagIds.map(String));
  const suggestions = existing(tags).filter(tag => !selected.has(String(tag.existingId)));
  const counts = new Map();
  const tokens = words(`${title} ${headings} ${body}`).filter(w => !stopwords.has(w));
  for (let i = 0; i < tokens.length; i++) {
    counts.set(tokens[i], (counts.get(tokens[i]) || 0) + 1);
    if (i + 1 < tokens.length) {
      const pair = `${tokens[i]} ${tokens[i + 1]}`;
      counts.set(pair, (counts.get(pair) || 0) + 1);
    }
  }
  const known = new Set(tags.map(t => fold(t.name)));
  const fresh = [...counts].filter(([name, count]) => count >= 2 && !known.has(name)).map(([name, count]) => ({ name, isNew: true, reason: `Termo recorrente (${count} ocorrências)`, score: count })).sort((a, b) => b.score - a.score || a.name.localeCompare(b.name));
  return { category, tags: [...suggestions, ...fresh].slice(0, 5) };
}
