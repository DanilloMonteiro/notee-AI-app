import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useToast } from '../../contexts/ToastContext';
import { sourcesApi } from './sourcesApi';
import { SourceForm } from './SourceForm';
import { detectCitations } from './detectCitations';
import { formatLocator, parseTimestamp } from './citationFormatting';

const locatorKinds = ['none', 'page', 'pages', 'timestamp', 'section', 'paragraph'];
const locatorLabels = { none: 'Nota inteira', page: 'Página', pages: 'Páginas', timestamp: 'Minuto', section: 'Seção', paragraph: 'Parágrafo' };

export function SourcesPanel({ noteId, noteBlocks = [], isDirty, selectedAnchor, onClearAnchor, onCitationsChange }) {
  const [sources, setSources] = useState([]);
  const [citations, setCitations] = useState([]);
  const [selectedSourceId, setSelectedSourceId] = useState('');
  const [createMode, setCreateMode] = useState(false);
  const [locatorKind, setLocatorKind] = useState('none');
  const [pageStart, setPageStart] = useState('');
  const [pageEnd, setPageEnd] = useState('');
  const [timeStart, setTimeStart] = useState('');
  const [timeEnd, setTimeEnd] = useState('');
  const [locatorText, setLocatorText] = useState('');
  const [sourceQuote, setSourceQuote] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [suggestions, setSuggestions] = useState([]);
  const [accepted, setAccepted] = useState([]);
  const [checkHistory, setCheckHistory] = useState({});
  const [expandedCheck, setExpandedCheck] = useState(null);
  const { addToast } = useToast();

  const refresh = async () => {
    const [sourceRows, citationRows] = await Promise.all([sourcesApi.list(), sourcesApi.citations(noteId)]);
    setSources(sourceRows || []);
    setCitations(citationRows || []);
    onCitationsChange?.(citationRows || []);
    if (!selectedSourceId && sourceRows?.length) setSelectedSourceId(sourceRows[0].id);
  };

  useEffect(() => {
    let alive = true;
    Promise.all([sourcesApi.list(), sourcesApi.citations(noteId)])
      .then(([sourceRows, citationRows]) => {
        if (!alive) return;
        setSources(sourceRows || []);
        setCitations(citationRows || []);
        onCitationsChange?.(citationRows || []);
        setSelectedSourceId(sourceRows?.[0]?.id || '');
      })
      .catch(err => addToast(err.message || 'Não foi possível carregar as fontes.', 'warning'))
      .finally(() => alive && setLoading(false));
    return () => { alive = false; };
  }, [addToast, noteId, onCitationsChange]);

  const grouped = useMemo(() => citations.reduce((all, citation) => {
    const key = citation.sourceId;
    if (!all[key]) all[key] = { source: citation.source, citations: [] };
    all[key].citations.push(citation);
    return all;
  }, {}), [citations]);

  const citationBody = sourceId => {
    const isTimestamp = locatorKind === 'timestamp';
    const start = isTimestamp ? parseTimestamp(timeStart) : null;
    const end = isTimestamp ? parseTimestamp(timeEnd) : null;
    if (isTimestamp && (start == null || Number.isNaN(start) || (timeEnd.trim() && Number.isNaN(end)))) throw new Error('Use mm:ss ou hh:mm:ss, por exemplo 12:34.');
    if (selectedAnchor?.quote && isDirty) throw new Error('Salve a nota antes de citar o trecho selecionado.');
    return {
      sourceId,
      blockId: selectedAnchor?.quote ? selectedAnchor.blockId : null,
      exactQuote: selectedAnchor?.quote || null,
      startIndex: selectedAnchor?.quote ? selectedAnchor.startIndex : null,
      endIndex: selectedAnchor?.quote ? selectedAnchor.endIndex : null,
      locatorKind,
      locatorText: locatorText.trim() || null,
      pageStart: ['page', 'pages'].includes(locatorKind) && pageStart ? Number(pageStart) : null,
      pageEnd: locatorKind === 'pages' && pageEnd ? Number(pageEnd) : null,
      timeStartSec: start,
      timeEndSec: end,
      sourceQuote: sourceQuote.trim() || null,
    };
  };

  const addCitation = async sourceId => {
    setBusy(true);
    try {
      await sourcesApi.addCitation(noteId, citationBody(sourceId));
      await refresh();
      onClearAnchor?.();
      setSourceQuote('');
      addToast('Citação salva.');
    } catch (err) { addToast(err.message || 'Não foi possível salvar a citação.', 'warning'); }
    finally { setBusy(false); }
  };

  const createAndCite = async input => {
    if (selectedAnchor?.quote && isDirty) {
      addToast('Salve a nota antes de citar o trecho selecionado.', 'info');
      return;
    }
    setBusy(true);
    try {
      const source = await sourcesApi.create(input);
      setCreateMode(false);
      await sourcesApi.addCitation(noteId, citationBody(source.id));
      await refresh();
      onClearAnchor?.();
      addToast('Fonte criada e citada.');
    } catch (err) {
      try { await refresh(); } catch { /* mantém a mensagem original */ }
      addToast(err.message || 'Não foi possível criar a fonte.', 'warning');
    } finally { setBusy(false); }
  };

  const removeCitation = async id => {
    setBusy(true);
    try { await sourcesApi.removeCitation(id); await refresh(); addToast('Citação removida da nota.'); }
    catch (err) { addToast(err.message || 'Não foi possível remover a citação.', 'warning'); }
    finally { setBusy(false); }
  };

  const acceptSuggestion = async suggestion => {
    if (isDirty) { addToast('Salve a nota antes de aceitar sugestões de citação.', 'info'); return; }
    setBusy(true);
    try {
      let resolved = null;
      if (suggestion.identifier) {
        try { resolved = await sourcesApi.resolve(suggestion.identifier); } catch { /* mantém os dados reconhecidos localmente */ }
      }
      const source = await sourcesApi.create({
        kind: resolved?.kind || suggestion.kind,
        title: resolved?.title?.trim() || suggestion.title,
        authors: resolved?.authors?.length ? resolved.authors : suggestion.authors,
        year: resolved?.year || suggestion.year,
        venue: resolved?.venue || suggestion.venue,
        language: resolved?.language || null,
        doi: resolved?.doi || (suggestion.type === 'doi' ? suggestion.identifier : null),
        isbn: resolved?.isbn || (suggestion.type === 'isbn' ? suggestion.identifier : null),
        url: resolved?.url || (suggestion.type === 'url' ? suggestion.identifier : null),
        license: resolved?.license || 'unknown',
        notes: 'Sugestão detectada no texto e aceita pela pessoa autora.',
      });
      try {
        await sourcesApi.addCitation(noteId, {
          sourceId: source.id, blockId: suggestion.blockId, exactQuote: suggestion.quote,
          startIndex: suggestion.startIndex, endIndex: suggestion.endIndex, locatorKind: 'none',
          locatorText: null, pageStart: null, pageEnd: null, timeStartSec: null, timeEndSec: null, sourceQuote: null,
        });
      } catch (error) { await sourcesApi.remove(source.id).catch(() => {}); throw error; }
      setAccepted(items => [...items, suggestion.id]);
      await refresh();
      addToast('Sugestão aceita; a fonte e a citação foram criadas.');
    } catch (err) { addToast(err.message || 'Não foi possível aceitar a sugestão.', 'warning'); }
    finally { setBusy(false); }
  };

  const showChecks = async citation => {
    try {
      const rows = await sourcesApi.citationChecks(citation.id);
      setCheckHistory(prev => ({ ...prev, [citation.id]: rows || [] }));
      setExpandedCheck(citation.id);
    } catch (err) { addToast(err.message || 'Não foi possível carregar verificações.', 'warning'); }
  };

  const retryQuoteCheck = async citation => {
    try { await sourcesApi.retryCitationCheck(citation.id); addToast('Conferência do trecho agendada.'); await showChecks(citation); }
    catch (err) { addToast(err.message || 'Não foi possível agendar a conferência.', 'warning'); }
  };

  const analyzeWithAi = async citation => {
    try {
      const estimate = await sourcesApi.estimateAiCheck(citation.id);
      const cost = estimate.estimatedCostUsd == null ? 'não configurado' : `$${Number(estimate.estimatedCostUsd).toFixed(6)} USD`;
      const disclosure = estimate.hasArchive
        ? `Dados enviados:\nAfirmação/contexto: ${estimate.claim}\n\nTrecho da fonte: ${estimate.sourceExcerpt}\n\nProvedor: ${estimate.provider}. Custo estimado: ${cost}. Deseja continuar?`
        : 'Não há texto arquivado. O Notee registrará “não verificável” sem chamar a IA. Continuar?';
      if (!window.confirm(disclosure)) return;
      await sourcesApi.runAiCheck(citation.id);
      await showChecks(citation);
    } catch (err) { addToast(err.message || 'Não foi possível analisar a citação.', 'warning'); }
  };

  return (
    <div className="result-panel sources-panel">
      <div className="row between wrap gap8">
        <h4>Fontes desta nota</h4>
        <Link className="btn compact ghost" to="/sources">Biblioteca</Link>
      </div>
      {selectedAnchor?.quote && <blockquote className="quote-selection source-selection">{selectedAnchor.quote}</blockquote>}
      {selectedAnchor?.quote && isDirty && <p className="small muted">Salve a nota antes de citar este trecho.</p>}

      <section className="card pad source-suggestions">
        <div className="row between wrap gap8"><div><strong>Sugestões no texto</strong><p className="tiny muted">A detecção é local. Cada aceite cria uma fonte e uma citação que você pode revisar.</p></div>
          <button type="button" className="btn compact" onClick={() => { setAccepted([]); setSuggestions(detectCitations(noteBlocks)); }}>Detectar citações</button>
        </div>
        {suggestions.length > 0 && <div className="stack">
          {suggestions.map(item => <div className="source-suggestion" key={item.id}>
            <div><strong>{item.quote}</strong><div className="tiny muted">{item.type.replaceAll('-', ' ')}{item.year ? ` · ${item.year}` : ''}</div></div>
            <button type="button" className="btn compact primary" disabled={busy || isDirty || accepted.includes(item.id)} onClick={() => acceptSuggestion(item)}>
              {accepted.includes(item.id) ? 'Aceita' : 'Aceitar'}
            </button>
          </div>)}
          {isDirty && <p className="tiny muted">Salve a nota para aceitar as sugestões sobre a versão atual.</p>}
        </div>}
      </section>

      <div className="card pad source-citation-form">
        <strong>Adicionar citação</strong>
        {!createMode ? <>
          {sources.length > 0 && <label>Fonte
            <select value={selectedSourceId} onChange={e => setSelectedSourceId(e.target.value)}>
              {sources.map(source => <option key={source.id} value={source.id}>{source.title}</option>)}
            </select>
          </label>}
          <button type="button" className="btn compact" onClick={() => setCreateMode(true)}>Criar fonte e citar</button>
          {sources.length > 0 && <button type="button" className="btn primary compact" disabled={busy || !selectedSourceId || (isDirty && !!selectedAnchor?.quote)} onClick={() => addCitation(selectedSourceId)}>Citar fonte selecionada</button>}
        </> : <SourceForm busy={busy} onCancel={() => setCreateMode(false)} onSave={createAndCite} />}

        <div className="stack source-locator">
          <label>Localização
            <select value={locatorKind} onChange={e => setLocatorKind(e.target.value)}>{locatorKinds.map(kind => <option key={kind} value={kind}>{locatorLabels[kind]}</option>)}</select>
          </label>
          {['page', 'pages'].includes(locatorKind) && <div className="row gap8">
            <label>Página inicial<input type="number" min="1" value={pageStart} onChange={e => setPageStart(e.target.value)} /></label>
            {locatorKind === 'pages' && <label>Página final<input type="number" min="1" value={pageEnd} onChange={e => setPageEnd(e.target.value)} /></label>}
          </div>}
          {locatorKind === 'timestamp' && <div className="row gap8">
            <label>De (mm:ss)<input value={timeStart} onChange={e => setTimeStart(e.target.value)} placeholder="12:34" /></label>
            <label>Até (mm:ss)<input value={timeEnd} onChange={e => setTimeEnd(e.target.value)} placeholder="13:10" /></label>
          </div>}
          {['section', 'paragraph'].includes(locatorKind) && <label>Seção ou parágrafo<input value={locatorText} onChange={e => setLocatorText(e.target.value)} maxLength={200} /></label>}
          <label>Trecho da fonte (opcional)<textarea value={sourceQuote} onChange={e => setSourceQuote(e.target.value)} maxLength={2000} rows={2} /></label>
        </div>
      </div>

      {loading ? <p className="small muted">Carregando fontes…</p> : Object.values(grouped).length === 0 ? <p className="small muted">Nenhuma fonte citada nesta nota.</p> : Object.values(grouped).map(({ source, citations: items }) => (
        <section className="source-citation-group" key={source.id}>
          <div className="row between wrap gap8"><div><strong>{source.title}</strong><div className="tiny muted">{source.authors?.join(', ')}{source.year ? ` · ${source.year}` : ''}{source.venue ? ` · ${source.venue}` : ''}</div></div><span className="pill">{source.status === 'unverified' ? 'Não verificada' : source.status}</span></div>
          {items.map(citation => <div className="source-citation-item" key={citation.id}>
            {citation.exactQuote && <blockquote className="quote-selection">{citation.exactQuote}</blockquote>}
            <p className="small muted">{formatLocator(citation)}</p>
            {citation.sourceQuote && <p className="tiny">“{citation.sourceQuote}”</p>}
            <div className="row wrap gap8">
              <button type="button" className="btn compact" disabled={busy} onClick={() => retryQuoteCheck(citation)}>Conferir trecho</button>
              <button type="button" className="btn compact" disabled={busy} onClick={() => analyzeWithAi(citation)}>Analisar com IA (pode ter custo)</button>
              <button type="button" className="btn compact ghost" onClick={() => expandedCheck === citation.id ? setExpandedCheck(null) : showChecks(citation)}>Histórico e evidências</button>
            </div>
            {expandedCheck === citation.id && <div className="source-check-history">
              {(checkHistory[citation.id] || []).length === 0 ? <p className="tiny muted">Nenhuma conferência concluída. Aguarde o job ou atualize o histórico.</p> : (checkHistory[citation.id] || []).map(check => <article key={check.id}>
                <div className="row between wrap gap8"><strong>{check.layer === 'ai' ? `IA: ${check.verdict || check.status}` : check.status}</strong><span className="tiny muted">{check.provider || 'local'}{check.costUsd != null ? ` · $${Number(check.costUsd).toFixed(6)} USD` : ''}</span></div>
                <p className="small">{check.summary}</p>
                {check.arguments?.map((arg, index) => <div className="source-evidence-row" key={index}><div><strong>{arg.point}</strong></div><blockquote>{arg.evidence}</blockquote></div>)}
              </article>)}
            </div>}
            <button type="button" className="btn compact ghost" disabled={busy} onClick={() => removeCitation(citation.id)}>Remover citação</button>
          </div>)}
        </section>
      ))}
      <p className="tiny muted">As fontes são referências declaradas. A presença de metadados ainda não confirma o conteúdo da fonte.</p>
    </div>
  );
}
