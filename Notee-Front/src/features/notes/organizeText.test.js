// Testes do organizador local de tópicos (`npm test`).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { hasStructure, organizeText } from './organizeText.js';

const FIXTURE = readFileSync(new URL('./fixtures/economia-2009-2018.md', import.meta.url), 'utf8');

const words = s => (String(s).toLowerCase().normalize('NFC').match(/[\p{L}\p{N}]+/gu) || []);
const counts = list => list.reduce((m, w) => m.set(w, (m.get(w) || 0) + 1), new Map());

test('texto completo do usuário vira tópicos, destaques e gráficos', () => {
  const { blocks, title } = organizeText(FIXTURE, { currentTitle: 'Uma nova ideia' });

  assert.equal(title, 'Economia brasileira no governo Dilma Rousseff: análise histórica de 2009 a 2018');

  const sections = blocks.filter(b => b.type === 'heading' && b.level === 2).map(b => b.text);
  assert.equal(sections.length, 11);
  assert.equal(sections[0], '1. Os períodos analisados');
  assert.equal(sections[10], '11. Conclusão: o que os números permitem afirmar?');

  const subheadings = blocks.filter(b => b.type === 'heading' && b.level === 3).map(b => b.text);
  assert.ok(subheadings.includes('2009–10 · Lula II — período anterior'));
  assert.ok(subheadings.includes('Por que a inflação disparou em 2015?'));

  const stats = blocks.filter(b => b.type === 'list' && b.text.includes('Recuperação forte'));
  assert.equal(stats.length, 1);
  assert.deepEqual(stats[0].text.split('\n'), [
    '2010 — antes: +7,5% — Recuperação forte',
    '2014 — Dilma I: +0,5% — Quase estagnação',
    '2015 — Dilma II: −3,5% — Recessão profunda',
  ]);

  const charts = blocks.filter(b => b.type === 'chart').map(b => b.chart);
  assert.deepEqual(
    charts.map(c => c.title),
    ['Produção industrial', 'Evolução da pobreza', '10. Tabela consolidada: todos os principais indicadores'],
  );
  assert.deepEqual(charts[1].series.map(s => s.name), ['Pobreza', 'Extrema pobreza']);
  assert.match(charts[1].sources[0].label, /^IBGE, Síntese de Indicadores Sociais 2019/);
  assert.equal(charts[2].series.length, 6);
  assert.equal(charts[2].series[2].values[0], null, 'desemprego 2009 "—" fica sem dado');
  assert.equal(charts[2].chartType, 'line');
  assert.deepEqual(charts[0].sources, [], 'fonte acima/longe da tabela não é atribuída');

  assert.ok(!blocks.some(b => typeof b.text === 'string' && /\*\*|^---$|^#/m.test(b.text)), 'sem marcação markdown');
});

test('nenhuma palavra é perdida ou inventada', () => {
  const { blocks, title } = organizeText(FIXTURE, { currentTitle: '' });
  const out = [title, ...blocks.map(b => (b.type === 'chart' ? b.source : b.text))].join('\n');
  const input = counts(words(FIXTURE));
  const output = counts(words(out));
  for (const [w, n] of input) assert.ok((output.get(w) || 0) >= n, `palavra perdida: ${w}`);
  for (const [w, n] of output) assert.ok((input.get(w) || 0) >= n, `palavra inventada: ${w}`);
});

test('não troca o título se a nota já tem um', () => {
  const { title, blocks } = organizeText('# Meu título\n\nTexto', { currentTitle: 'Notas da aula' });
  assert.equal(title, undefined);
  assert.deepEqual(blocks[0], { type: 'heading', level: 1, text: 'Meu título' });
});

test('texto simples continua um parágrafo', () => {
  const { blocks } = organizeText('Só uma frase sem marcação.');
  assert.deepEqual(blocks, [{ type: 'paragraph', text: 'Só uma frase sem marcação.' }]);
  assert.equal(hasStructure('Só uma frase.'), false);
  assert.equal(hasStructure('## Título'), true);
});

test('listas numeradas, citações e código', () => {
  const { blocks } = organizeText('1. um\n2. dois\n\n> citado\n\n```\nx = 1\n```');
  assert.deepEqual(
    blocks.map(b => [b.type, b.text]),
    [
      ['list', 'um\ndois'],
      ['quote', 'citado'],
      ['code', 'x = 1'],
    ],
  );
});
