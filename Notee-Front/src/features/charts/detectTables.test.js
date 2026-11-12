// Testes do detector de tabelas coladas. Runner nativo do Node: `npm test` (node --test).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseCell, splitPastedText } from './detectTables.js';

const USER_EXAMPLE = `5. Investimentos e produção industrial
O investimento é um dos indicadores mais importantes para entender o crescimento de longo prazo. Ele representa a ampliação da capacidade produtiva, por exemplo, com máquinas, equipamentos, instalações e infraestrutura.

Taxa de investimento — 2010 a 2018

Formação Bruta de Capital Fixo (FBCF) como proporção do PIB

Fonte: IBGE, Sistema de Contas Nacionais.

Entre 2013 e 2017, o investimento caiu de 20,9% para 14,6% do PIB, uma redução de 6,3 pontos percentuais. A queda foi particularmente acentuada em 2015 e 2016.

A produção industrial também recuou fortemente. Segundo a Pesquisa Industrial Mensal do IBGE:
Ano    Produção industrial

2014    −3,0%
2015    −8,3%
2016    −6,4%
2017    +2,5%
2018    +1,1%


A indústria já apresentava contração em 2014, antes da fase mais intensa da recessão. Isso é relevante para avaliar se os problemas começaram somente após a eleição de 2014.

Em 2015, a produção de bens de capital — máquinas e equipamentos usados para produzir outros bens — caiu 25,5%, um sinal de retração especialmente grave dos investimentos produtivos.`;

test('exemplo do usuário vira texto + gráfico de barras + texto', () => {
  const segments = splitPastedText(USER_EXAMPLE);
  assert.deepEqual(
    segments.map(s => s.type),
    ['text', 'chart', 'text'],
  );

  const [before, { chart, source }, after] = segments;
  assert.equal(chart.type, 'chart');
  assert.equal(chart.chartType, 'bar');
  assert.equal(chart.title, 'Produção industrial');
  assert.equal(chart.xAxisLabel, 'Ano');
  assert.equal(chart.unit, '%');
  assert.deepEqual(chart.labels, ['2014', '2015', '2016', '2017', '2018']);
  assert.equal(chart.series.length, 1);
  assert.equal(chart.series[0].name, 'Produção industrial');
  assert.deepEqual(chart.series[0].values, [-3, -8.3, -6.4, 2.5, 1.1]);
  assert.deepEqual(chart.sources, [], 'fonte nunca é inferida do texto em volta');

  assert.ok(before.text.endsWith('Segundo a Pesquisa Industrial Mensal do IBGE:'));
  assert.ok(before.text.includes('Taxa de investimento — 2010 a 2018'), 'texto preservado sem normalização');
  assert.ok(after.text.startsWith('A indústria já apresentava contração'));
  assert.ok(after.text.endsWith('investimentos produtivos.'));
  assert.ok(source.startsWith('Ano    Produção industrial') && source.includes('2018    +1,1%'));
});

test('prosa com percentuais não vira gráfico', () => {
  const text = 'Entre 2013 e 2017, o investimento caiu de 20,9% para 14,6% do PIB.\nEm 2015 caiu 25,5%, um sinal grave.';
  assert.deepEqual(splitPastedText(text), [{ type: 'text', text }]);
});

test('uma única linha numérica não vira gráfico', () => {
  const text = 'Intro\n2014    -3,0%\nFim';
  assert.equal(splitPastedText(text).filter(s => s.type === 'chart').length, 0);
});

test('tabela separada por tab com várias séries e anos vira linha', () => {
  const text = 'Ano\tPIB\tInflação\n2019\t1,2\t4,31\n2020\t3,3\t4,52\n2021\t4,8\t10,06';
  const [{ chart }] = splitPastedText(text);
  assert.equal(chart.chartType, 'line');
  assert.deepEqual(chart.series.map(s => s.name), ['PIB', 'Inflação']);
  assert.deepEqual(chart.series[1].values, [4.31, 4.52, 10.06]);
});

test('célula ausente vira null e não zero', () => {
  const text = 'Região;A;B\nNorte;5;3\nSul;—;4\nLeste;2;n/d';
  const [{ chart }] = splitPastedText(text);
  assert.deepEqual(chart.series[0].values, [5, null, 2]);
  assert.deepEqual(chart.series[1].values, [3, 4, null]);
  assert.equal(chart.chartType, 'bar');
});

test('sem cabeçalho usa nomes genéricos', () => {
  const [{ chart }] = splitPastedText('Norte  10\nSul  20');
  assert.equal(chart.series[0].name, 'Valores');
  assert.equal(chart.title, '');
});

test('mais de 8 colunas de valores não converte', () => {
  const row = n => `${n}  1  2  3  4  5  6  7  8  9`;
  assert.equal(splitPastedText(`${row('a')}\n${row('b')}`).filter(s => s.type === 'chart').length, 0);
});

test('parseCell entende formatos pt-BR e en', () => {
  assert.equal(parseCell('1.234,5').value, 1234.5);
  assert.equal(parseCell('1234.5').value, 1234.5);
  assert.equal(parseCell('1.234').value, 1234);
  assert.equal(parseCell('+2,5%').value, 2.5);
  assert.equal(parseCell('10 %').percent, true);
  assert.equal(parseCell('abc'), null);
  assert.equal(parseCell('—').missing, true);
});
