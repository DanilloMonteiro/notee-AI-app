// "Nova nota" (ADR-044): título + três caminhos — escrever, descrever e gerar com IA, ou partir de outra nota.
// Os caminhos de IA mostram provedor e custo antes do envio e devolvem uma PRÉVIA; nada é salvo antes de "Criar nota".
import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { apiFetch } from '../../api';
import { taxonomyApi } from '../../notes';
import { sourcesApi } from '../sources/sourcesApi';
import { ChartView } from '../charts/ChartView';
import { PreviewBlock } from './OrganizeAiModal';
import { costText } from './aiCost';
import { createComposedNote, initialSelection, parseNoteLink, toggleChart, toggleOrigin, toggleSource } from './composeNote';

// Trechos do texto que a fonte/origem apoia (viram citações ancoradas ao criar a nota).
function SupportList({ supports }) {
  if (!supports?.length) return <span className="small muted"> · cita a nota inteira</span>;
  return (
    <span className="new-note-supports">
      {supports.map(quote => (
        <q key={quote}>{quote}</q>
      ))}
    </span>
  );
}

const MODES = [
  { id: 'manual', icon: 'note', label: 'Escrever eu mesmo', hint: 'Abre o editor com o título escolhido.' },
  { id: 'generate', icon: 'spark', label: 'Descrever e gerar com IA', hint: 'A IA escreve a nota e sugere categoria, tags, fontes e gráficos.' },
  { id: 'rewrite', icon: 'link', label: 'Partir de outra nota', hint: 'Cole o link de uma nota sua ou publicada e diga o que mudar.' },
];

const MAX_INSTRUCTIONS = 4000;

const api = {
  createNote: body => apiFetch('/notes', { method: 'POST', body: JSON.stringify(body) }),
  createTaxonomy: taxonomyApi.create,
  listTaxonomy: taxonomyApi.list,
  createSource: sourcesApi.create,
  listSources: query => sourcesApi.list({ query }),
  addCitation: sourcesApi.addCitation,
};

function errorMessage(err, fallback) {
  if (err?.status === 429) return 'Limite de criações com IA atingido nesta hora. Tente mais tarde ou escreva a nota você mesmo.';
  if (err?.status === 404) return err.message || 'A nota do link não foi encontrada.';
  if (err?.status === 502) return 'O provedor de IA não respondeu. Nenhuma nota foi criada.';
  if (err?.status === 422) return err.message || 'A resposta da IA foi recusada. Nenhuma nota foi criada.';
  return err?.message || fallback;
}

const authorsYear = s => [s.authors?.slice(0, 3).join(', '), s.year].filter(Boolean).join(' · ');

export function NewNoteModal({ onCreated, onClose }) {
  const [title, setTitle] = useState('');
  const [mode, setMode] = useState('manual');
  const [instructions, setInstructions] = useState('');
  const [link, setLink] = useState('');
  const [state, setState] = useState('form'); // form | estimating | ready | sending | preview | saving | error
  const [error, setError] = useState('');
  const [estimate, setEstimate] = useState(null);
  const [result, setResult] = useState(null);
  const [selection, setSelection] = useState(null);

  const parsedLink = mode === 'rewrite' ? parseNoteLink(link) : null;
  const trimmedTitle = title.trim();
  const formError = !trimmedTitle
    ? 'Dê um título para a nota.'
    : trimmedTitle.length > 100
      ? 'O título deve ter até 100 caracteres.'
      : mode === 'generate' && !instructions.trim()
        ? 'Descreva o que você quer na nota.'
        : mode === 'rewrite' && !parsedLink
          ? 'Cole um link de nota (/note/…, /p/…/… ou /s/…).'
          : '';
  const busy = ['estimating', 'sending', 'saving'].includes(state);

  const requestBody = extra => ({
    mode,
    title: trimmedTitle,
    instructions: instructions.trim(),
    baseLink: parsedLink?.url ?? null,
    ...extra,
  });

  const fail = (err, fallback) => {
    setError(errorMessage(err, fallback));
    setState('error');
  };

  const handleContinue = async e => {
    e.preventDefault();
    if (formError || busy) return;
    if (mode === 'manual') {
      setState('saving');
      try {
        const note = await api.createNote({ title: trimmedTitle, content: '[]' });
        onCreated(note, []);
      } catch (err) {
        fail(err, 'Erro ao criar a nota.');
      }
      return;
    }
    setState('estimating');
    try {
      setEstimate(await apiFetch('/notes/compose-ai/estimate', { method: 'POST', body: JSON.stringify(requestBody()) }));
      setState('ready');
    } catch (err) {
      fail(err, 'Não foi possível calcular a estimativa.');
    }
  };

  const handleGenerate = async () => {
    setState('sending');
    try {
      const r = await apiFetch('/notes/compose-ai', { method: 'POST', body: JSON.stringify(requestBody({ confirm: true })) });
      setResult(r);
      setSelection(initialSelection(r));
      setState('preview');
    } catch (err) {
      fail(err, 'Falha ao gerar a nota. Nenhuma nota foi criada.');
    }
  };

  const handleCreate = async () => {
    setState('saving');
    try {
      const { note, warnings } = await createComposedNote({ api, title: trimmedTitle, result, selection });
      onCreated(note, warnings);
    } catch (err) {
      fail(err, 'Erro ao criar a nota. Nada foi salvo.');
    }
  };

  const toggle = (key, index) =>
    setSelection(prev =>
      key === 'category' ? { ...prev, category: !prev.category } : { ...prev, [key]: prev[key].map((v, i) => (i === index ? !v : v)) },
    );

  const external = estimate?.external;

  return (
    <div className="modal-overlay">
      <section className="modal organize-ai-modal" role="dialog" aria-modal="true" aria-labelledby="new-note-title">
        <div className="modal-title">
          <h2 id="new-note-title">Nova nota</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Fechar" disabled={state === 'saving'}>
            <Icon name="close" />
          </button>
        </div>

        {(state === 'form' || state === 'estimating' || (state === 'saving' && !result)) && (
          <form onSubmit={handleContinue}>
            <div className="field">
              <label htmlFor="new-note-title-input">Título</label>
              <input
                id="new-note-title-input"
                type="text"
                value={title}
                maxLength={100}
                autoFocus
                onChange={e => setTitle(e.target.value)}
                placeholder="Sobre o que é esta nota?"
              />
            </div>

            <div className="new-note-modes" role="radiogroup" aria-label="Como começar">
              {MODES.map(m => (
                <label key={m.id} className={`new-note-mode ${mode === m.id ? 'selected' : ''}`}>
                  <input type="radio" name="new-note-mode" value={m.id} checked={mode === m.id} onChange={() => setMode(m.id)} />
                  <Icon name={m.icon} className="sm" />
                  <span>
                    <strong>{m.label}</strong>
                    <span className="small muted">{m.hint}</span>
                  </span>
                </label>
              ))}
            </div>

            {mode === 'rewrite' && (
              <div className="field mt16">
                <label htmlFor="new-note-link">Link da nota</label>
                <input
                  id="new-note-link"
                  type="text"
                  value={link}
                  onChange={e => setLink(e.target.value)}
                  placeholder="https://…/note/… , /p/autor/slug ou /s/token"
                />
                {parsedLink && parsedLink.kind !== 'own' && (
                  <span className="small muted">Se a nota for de outra pessoa, o link entra como fonte para dar o crédito.</span>
                )}
              </div>
            )}

            {mode !== 'manual' && (
              <div className={`field ${mode === 'generate' ? 'mt16' : ''}`}>
                <label htmlFor="new-note-instructions">{mode === 'generate' ? 'O que você quer nesta nota?' : 'O que mudar? (opcional)'}</label>
                <textarea
                  id="new-note-instructions"
                  rows={5}
                  maxLength={MAX_INSTRUCTIONS}
                  value={instructions}
                  onChange={e => setInstructions(e.target.value)}
                  placeholder={
                    mode === 'generate'
                      ? 'Ex.: Resumo da crise econômica de 2015 com dados de PIB e inflação, para revisar antes da prova.'
                      : 'Ex.: Deixar mais curto, focar em juros e acrescentar uma seção de dúvidas.'
                  }
                />
                <span className="small muted">
                  {instructions.length}/{MAX_INSTRUCTIONS}
                </span>
              </div>
            )}

            {formError && trimmedTitle && <p className="small muted">{formError}</p>}

            <div className="modal-actions mt16">
              <button type="button" className="btn" onClick={onClose} disabled={busy}>
                Cancelar
              </button>
              <button type="submit" className="btn primary" disabled={Boolean(formError) || busy}>
                {mode === 'manual' ? (
                  <>
                    <Icon name="plus" className="sm" /> {state === 'saving' ? 'Criando…' : 'Criar e escrever'}
                  </>
                ) : (
                  <>
                    <Icon name="arrow" className="sm" /> {state === 'estimating' ? 'Calculando…' : 'Continuar'}
                  </>
                )}
              </button>
            </div>
          </form>
        )}

        {(state === 'ready' || state === 'sending') && estimate && (
          <>
            <div className={`callout ${external ? 'warning' : 'neutral'} mb16`}>
              <Icon name={external ? 'warning' : 'info'} />
              <span>
                {external ? (
                  <>
                    O título, a sua descrição{estimate.baseNote ? ', o texto da nota base' : ''} e os nomes das suas categorias e tags
                    serão enviados para <strong>{estimate.providerName}</strong>.
                  </>
                ) : (
                  <>
                    {estimate.externalConfigured ? 'Sua conta não autorizou o uso de IA externa' : 'Nenhum provedor de IA externo está configurado'}
                    , então será usada a <strong>simulação local</strong> (sem custo, nada sai do Notee). Ela monta um rascunho simples e
                    não sugere fontes nem gráficos. {estimate.externalConfigured && <Link to="/settings">Autorizar em Configurações</Link>}
                  </>
                )}
              </span>
            </div>
            <dl className="organize-ai-facts">
              <dt>Provedor</dt>
              <dd>{estimate.providerName}</dd>
              {estimate.baseNote && (
                <>
                  <dt>Nota base</dt>
                  <dd>
                    {estimate.baseNote.title} {!estimate.baseNote.isOwn && <span className="pill">de outra pessoa</span>}
                  </dd>
                </>
              )}
              <dt>Custo estimado</dt>
              <dd>{costText(estimate.estimatedCostUsd, estimate.estimatedInputTokens + estimate.estimatedOutputTokens, external)}</dd>
              <dt>Limite</dt>
              <dd>{estimate.maxPerHour} criações por hora</dd>
            </dl>
            <p className="small muted">
              Fontes e gráficos sugeridos só aparecem se o Notee encontrar a fonte (DOI, ISBN ou URL). Você revisa tudo antes de criar a nota.
            </p>
            <div className="modal-actions mt16">
              <button className="btn" onClick={() => setState('form')} disabled={state === 'sending'}>
                Voltar
              </button>
              <button className="btn primary" onClick={handleGenerate} disabled={state === 'sending'}>
                <Icon name="spark" className="sm" /> {state === 'sending' ? 'Gerando… (pode levar até 1 min)' : external ? 'Enviar e gerar' : 'Gerar (simulação)'}
              </button>
            </div>
          </>
        )}

        {(state === 'preview' || (state === 'saving' && result)) && result && selection && (
          <>
            <p className="small muted mb16">
              Prévia · {costText(result.costUsd, (result.inputTokens || 0) + (result.outputTokens || 0), result.external)}
              {result.discardedSources > 0 && ` · ${result.discardedSources} referência(s) não encontrada(s): aparecem abaixo como não conferidas`}
              {result.discardedCharts > 0 && ` · ${result.discardedCharts} gráfico(s) descartado(s) por falta de fonte verificável`}
            </p>
            <div className="organize-ai-preview">
              <div style={{ fontSize: 22, fontWeight: 700, marginBottom: 6 }}>{trimmedTitle}</div>
              {result.blocks.map((b, i) => {
                if (b.type !== 'chart') return <PreviewBlock key={i} item={b.type === 'table' ? { type: 'paragraph', text: b.text } : b} />;
                const chart = result.charts[b.ref];
                const source = chart.origin === 'generated' ? result.sources[chart.sourceIndex] : null;
                return (
                  <div key={i} className={`new-note-chart ${selection.charts[b.ref] ? '' : 'off'}`}>
                    <label className="new-note-check">
                      <input type="checkbox" checked={selection.charts[b.ref]} onChange={() => setSelection(s => toggleChart(s, result, b.ref))} />
                      {chart.origin === 'generated' ? 'Incluir gráfico gerado' : 'Manter gráfico da nota base'}
                    </label>
                    <ChartView chart={chart.chart} embedded />
                    {source && (
                      <p className="small muted">
                        <Icon name="warning" className="sm" /> Dados atribuídos pela IA a <strong>{source.resolved.title}</strong> — a fonte
                        existe, mas confira os números nela antes de usar.
                      </p>
                    )}
                  </div>
                );
              })}
            </div>

            {(result.category || result.tags.length > 0) && (
              <div className="new-note-group">
                <h3>Organização sugerida</h3>
                {result.category && (
                  <label className="new-note-check">
                    <input type="checkbox" checked={selection.category} onChange={() => toggle('category')} />
                    Categoria: <strong>{result.category.name}</strong>{' '}
                    <span className={`pill ${result.category.isNew ? 'amber' : 'green'}`}>{result.category.isNew ? 'nova' : 'existente'}</span>
                    <span className="small muted"> {result.category.reason}</span>
                  </label>
                )}
                {result.tags.map((tag, i) => (
                  <label key={tag.name} className="new-note-check">
                    <input type="checkbox" checked={selection.tags[i]} onChange={() => toggle('tags', i)} />
                    Tag: <strong>{tag.name}</strong> <span className={`pill ${tag.isNew ? 'amber' : 'green'}`}>{tag.isNew ? 'nova' : 'existente'}</span>
                    <span className="small muted"> {tag.reason}</span>
                  </label>
                ))}
              </div>
            )}

            <div className="new-note-group">
              <h3>Fontes</h3>
              {result.attribution && (
                <label className="new-note-check">
                  <input type="checkbox" checked disabled />
                  <span>
                    <strong>{result.attribution.title}</strong> <span className="pill blue">nota original · obrigatória</span>
                    <span className="small muted"> {authorsYear(result.attribution)}</span>
                  </span>
                </label>
              )}
              {result.sources.map((s, i) => (
                <label key={s.identifier} className="new-note-check">
                  <input type="checkbox" checked={selection.sources[i]} onChange={() => setSelection(sel => toggleSource(sel, result, i))} />
                  <span>
                    <strong>{s.resolved.title}</strong> <span className="pill green">conferida pelo sistema · revise</span>
                    <span className="small muted">
                      {' '}
                      {authorsYear(s.resolved)} · {s.reason}
                    </span>
                    {s.resolved.url && (
                      <a className="small" href={s.resolved.url} target="_blank" rel="noreferrer noopener">
                        {' '}
                        abrir <Icon name="external" className="sm" />
                      </a>
                    )}
                    <SupportList supports={s.supports} />
                  </span>
                </label>
              ))}
              {!result.attribution && result.sources.length === 0 && !(result.origins || []).length && (
                <p className="small muted">Sem fonte. Nenhuma fonte sugerida pôde ser conferida; você pode adicionar fontes na aba Fontes da nota.</p>
              )}
            </div>

            {(result.origins || []).length > 0 && (
              <div className="new-note-group">
                <h3>Origens informadas pela IA (não conferidas)</h3>
                <p className="small muted">
                  A IA disse que estas informações vieram daqui, mas o Notee não conseguiu conferir. Elas entram na nota como fontes
                  marcadas “não conferida”, sem link.
                </p>
                {result.origins.map((o, i) => (
                  <label key={`${o.label}-${i}`} className="new-note-check">
                    <input type="checkbox" checked={!!selection.origins?.[i]} onChange={() => setSelection(sel => toggleOrigin(sel, i))} />
                    <span>
                      <strong>{o.label}</strong>{' '}
                      <span className="pill amber">{o.unresolvedIdentifier ? 'referência não encontrada' : 'não conferida'}</span>
                      <SupportList supports={o.supports} />
                    </span>
                  </label>
                ))}
              </div>
            )}

            <div className="modal-actions mt16">
              <button className="btn" onClick={onClose} disabled={state === 'saving'}>
                Descartar
              </button>
              <button className="btn primary" onClick={handleCreate} disabled={state === 'saving'}>
                <Icon name="check" className="sm" /> {state === 'saving' ? 'Criando…' : 'Criar nota'}
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
              <button className="btn primary" onClick={() => setState(result ? 'preview' : 'form')}>
                Voltar
              </button>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
