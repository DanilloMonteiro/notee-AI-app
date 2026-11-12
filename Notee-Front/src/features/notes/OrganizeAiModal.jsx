// "Organizar com IA" (ADR-037): mostra provedor e custo estimado, envia só com confirmação explícita
// e devolve uma PRÉVIA — a nota só muda quando a pessoa clica em "Aplicar" (com Desfazer no editor).
import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { apiFetch } from '../../api';
import { ChartView } from '../charts/ChartView';
import { blocksToText, responseToItems } from './aiOrganize';

const DEFAULT_TITLES = new Set(['', 'sem título', 'uma nova ideia']);

const usd = value =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(value);

function costText(cost, tokens, external) {
  if (!external) return 'sem custo (simulação local)';
  if (cost === null || cost === undefined) return `preço não configurado · ~${tokens.toLocaleString('pt-BR')} tokens`;
  return `${usd(cost)} · ~${tokens.toLocaleString('pt-BR')} tokens`;
}

function PreviewBlock({ item }) {
  if (item.type === 'chart') return <ChartView chart={item.chart} />;
  if (item.type === 'heading') {
    const size = item.level === 1 ? 19 : item.level === 3 ? 14 : 16;
    return <div style={{ fontSize: size, fontWeight: 650, margin: '10px 0 2px' }}>{item.text}</div>;
  }
  if (item.type === 'list')
    return (
      <ul style={{ margin: '2px 0', paddingLeft: 20 }}>
        {item.text.split('\n').map((l, i) => (
          <li key={i}>{l}</li>
        ))}
      </ul>
    );
  if (item.type === 'quote') return <blockquote className="quote-selection">{item.text}</blockquote>;
  if (item.type === 'code')
    return (
      <pre style={{ background: '#f4f6f1', padding: 10, borderRadius: 8, overflow: 'auto' }}>
        <code>{item.text}</code>
      </pre>
    );
  return <p style={{ margin: '4px 0', whiteSpace: 'pre-wrap' }}>{item.text}</p>;
}

export function OrganizeAiModal({ noteId, blocks, title, onApply, onClose }) {
  const { text, charts } = useMemo(() => blocksToText(blocks), [blocks]);
  const [estimate, setEstimate] = useState(null);
  const [state, setState] = useState('estimating'); // estimating | ready | sending | preview | error
  const [error, setError] = useState('');
  const [result, setResult] = useState(null);

  useEffect(() => {
    let alive = true;
    if (!text.trim()) {
      setState('error');
      setError('A nota está vazia.');
      return undefined;
    }
    apiFetch(`/notes/${noteId}/organize-ai/estimate`, { method: 'POST', body: JSON.stringify({ text }) })
      .then(e => {
        if (!alive) return;
        setEstimate(e);
        setState('ready');
      })
      .catch(err => {
        if (!alive) return;
        setError(err?.message || 'Não foi possível calcular a estimativa.');
        setState('error');
      });
    return () => {
      alive = false;
    };
  }, [noteId, text]);

  const handleSend = async () => {
    setState('sending');
    try {
      const r = await apiFetch(`/notes/${noteId}/organize-ai`, { method: 'POST', body: JSON.stringify({ text, confirm: true }) });
      setResult({ ...r, items: responseToItems(r.blocks, charts) });
      setState('preview');
    } catch (err) {
      setError(
        err?.status === 429
          ? 'Limite de organizações por IA atingido nesta hora. Tente mais tarde ou use “Organizar em tópicos”.'
          : err?.message || 'Falha ao organizar com IA. Nada foi alterado.',
      );
      setState('error');
    }
  };

  const handleApply = () => {
    const keepTitle = !DEFAULT_TITLES.has(String(title).trim().toLowerCase());
    const real = result.costUsd;
    const label = !result.external
      ? 'simulação local, sem custo'
      : real === null || real === undefined
        ? `${(result.inputTokens || 0) + (result.outputTokens || 0)} tokens`
        : usd(real);
    onApply({ items: result.items, title: keepTitle ? null : result.title, costLabel: label });
  };

  const external = estimate?.external;

  return (
    <div className="modal-overlay">
      <section className="modal organize-ai-modal" role="dialog" aria-modal="true" aria-labelledby="organize-ai-title">
        <div className="modal-title">
          <h2 id="organize-ai-title">Organizar com IA</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Fechar">
            <Icon name="close" />
          </button>
        </div>

        {state === 'estimating' && <p className="muted">Calculando estimativa…</p>}

        {(state === 'ready' || state === 'sending') && estimate && (
          <>
            <div className={`callout ${external ? 'warning' : 'neutral'} mb16`}>
              <Icon name={external ? 'warning' : 'info'} />
              <span>
                {external ? (
                  <>
                    O texto desta nota (cerca de {text.length.toLocaleString('pt-BR')} caracteres) será enviado para{' '}
                    <strong>{estimate.providerName}</strong>. A IA só reorganiza a estrutura; se ela omitir ou inventar conteúdo, a
                    resposta é recusada e nada muda.
                  </>
                ) : (
                  <>
                    {estimate.externalConfigured
                      ? 'Sua conta não autorizou o uso de IA externa'
                      : 'Nenhum provedor de IA externo está configurado'}
                    , então será usada a <strong>simulação local</strong> (sem custo, nada sai do Notee).{' '}
                    {estimate.externalConfigured && <Link to="/settings">Autorizar em Configurações</Link>}
                  </>
                )}
              </span>
            </div>
            <dl className="organize-ai-facts">
              <dt>Provedor</dt>
              <dd>{estimate.providerName}</dd>
              <dt>Custo estimado</dt>
              <dd>{costText(estimate.estimatedCostUsd, estimate.estimatedInputTokens + estimate.estimatedOutputTokens, external)}</dd>
              <dt>Limite</dt>
              <dd>{estimate.maxPerHour} organizações por hora</dd>
            </dl>
            <p className="small muted">Você verá uma prévia antes de aplicar. A nota só muda quando você clicar em “Aplicar”.</p>
            <div className="modal-actions mt16">
              <button className="btn" onClick={onClose} disabled={state === 'sending'}>
                Cancelar
              </button>
              <button className="btn primary" onClick={handleSend} disabled={state === 'sending'}>
                <Icon name="spark" className="sm" />{' '}
                {state === 'sending' ? 'Organizando…' : external ? 'Enviar e organizar' : 'Organizar (simulação)'}
              </button>
            </div>
          </>
        )}

        {state === 'preview' && result && (
          <>
            <p className="small muted mb16">
              Prévia · {result.items.length} blocos ·{' '}
              {costText(result.costUsd, (result.inputTokens || 0) + (result.outputTokens || 0), result.external)}
              {result.title && ` · título sugerido: “${result.title}”`}
            </p>
            <div className="organize-ai-preview">
              {result.items.map((item, i) => (
                <PreviewBlock key={i} item={item} />
              ))}
            </div>
            <div className="modal-actions mt16">
              <button className="btn" onClick={onClose}>
                Descartar
              </button>
              <button className="btn primary" onClick={handleApply}>
                <Icon name="check" className="sm" /> Aplicar na nota
              </button>
            </div>
          </>
        )}

        {state === 'error' && (
          <>
            <div className="callout warning mb16" role="alert">
              <Icon name="warning" />
              <span>{error}</span>
            </div>
            <div className="modal-actions">
              <button className="btn" onClick={onClose}>
                Fechar
              </button>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
