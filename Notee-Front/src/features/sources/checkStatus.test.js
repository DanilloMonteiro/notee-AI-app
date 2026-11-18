import test from 'node:test';
import assert from 'node:assert/strict';
import { checkHint, checkLabel, checkStatusInfo, isCheckInProgress } from './checkStatus.js';

test('traduz status da API e mantém desconhecidos', () => {
  assert.deepEqual(checkStatusInfo('Unverifiable'), { label: 'Não deu para conferir', tone: '' });
  assert.equal(checkStatusInfo('pending').label, 'Conferindo…');
  assert.equal(checkStatusInfo('novo').label, 'novo');
});

test('conferência em andamento', () => {
  assert.equal(isCheckInProgress({ status: 'pending' }), true);
  assert.equal(isCheckInProgress({ status: 'running' }), true);
  assert.equal(isCheckInProgress({ status: 'confirmed' }), false);
});

test('análise por IA mostra o veredito', () => {
  assert.equal(checkLabel({ layer: 'ai', verdict: 'partially', status: 'foundwithdifferences' }), 'IA: apoia em parte');
  assert.equal(checkLabel({ layer: 'citation', status: 'quotenotfound' }), 'Trecho não encontrado');
});

test('dica só para conferência impossível', () => {
  assert.match(checkHint({ status: 'unverifiable', summary: 'Não há texto arquivado para conferir o trecho.' }), /PDF/);
  assert.match(checkHint({ status: 'unverifiable', summary: 'Informe o trecho da fonte para fazer a conferência.' }), /trecho da fonte/);
  assert.equal(checkHint({ status: 'confirmed', summary: 'ok' }), null);
});

test('explicação das etiquetas', async () => {
  const { checkStatusHelp } = await import('./checkStatus.js');
  assert.match(checkStatusHelp('FoundWithDifferences'), /título/);
  assert.match(checkStatusHelp('unverifiable'), /DOI, ISBN nem link/);
  assert.equal(checkStatusHelp('desconhecido'), null);
});
