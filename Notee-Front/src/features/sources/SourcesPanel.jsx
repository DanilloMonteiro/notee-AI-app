import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useToast } from '../../contexts/ToastContext';
import { Icon } from '../../components/Icon';
import { sourcesApi } from './sourcesApi';
import { SourceForm } from './SourceForm';
import { detectCitations } from './detectCitations';
import { formatLocator, parseTimestamp } from './citationFormatting';
import { checkStatusHelp, checkStatusInfo } from './checkStatus';
import { OriginBadge, SourceComposition } from '../research/AnswerWithRefs';
import { blockableDomain } from '../research/sourceMix';
import { sourceRulesApi } from '../research/sourceRulesApi';

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
  const [formOpen, setFormOpen] = useState(null);
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

  // Formulário recolhido por padrão; abre sozinho com trecho selecionado ou quando a nota ainda não tem fontes.
  const formShown = formOpen ?? (!!selectedAnchor?.quote || citations.length === 0);

  // "Não usar este site": vale para as próximas pesquisas; a citação atual continua até ser removida.
  const blockSite = async domain => {
    try {
      await sourceRulesApi.save(domain, 'blocked');
      addToast(`${domain} não será mais usado como fonte nas próximas pesquisas.`);
    } catch (err) {
      addToast(err.message || 'Não foi possível bloquear o site.', 'warning');
    }
  };

  return (
    <div className="result-panel sources-panel">
      <div className="row between wrap gap8">
        <h4>Fontes desta nota</h4>
        <Link className="btn compact ghost" to="/sources">Biblioteca</Link>
      </div>
      {/* ADR-047: quanto das fontes desta nota é artigo acadêmico e quanto é página da internet. */}
      <SourceComposition sources={Object.values(grouped).map(g => g.source)} />
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
        <button type="button" className="source-form-toggle" aria-expanded={formShown} onClick={() => setFormOpen(!formShown)}>
          <strong>Adicionar citação</strong><span className="tiny muted">{formShown ? 'Ocultar' : 'Mostrar'}</span>
        </button>
        {formShown && <>
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
        </>}
      </div>

      {loading ? <p className="small muted">Carregando fontes…</p> : Object.values(grouped).length === 0 ? <p className="small muted">Nenhuma fonte citada nesta nota.</p> : Object.values(grouped).map(({ source, citations: items }) => {
        const sourceStatus = checkStatusInfo(source.status);
        const meta = [source.authors?.join(', '), source.year, source.venue].filter(Boolean).join(' · ');
        return (
        <section className="source-citation-group" key={source.id}>
          <div className="source-group-head">
            {/* Nome da fonte em destaque; com link, o próprio nome abre a fonte (como no card do trecho). */}
            {source.url
              ? <a className="source-group-name" href={source.url} target="_blank" rel="noopener noreferrer" title={`${source.title} — abrir fonte`}>{source.title} <Icon name="external" className="sm" /></a>
              : <strong className="source-group-name" title={source.title}>{source.title}</strong>}
            <span className="source-group-status">
              <span className={`pill ${sourceStatus.tone}`}>{sourceStatus.label}</span>
              {checkStatusHelp(source.status) && <span className="source-status-help">
                <button type="button" className="source-status-help-btn" aria-label={`O que significa “${sourceStatus.label}”`} aria-describedby={`status-help-${source.id}`}>
                  <Icon name="info" className="sm" />
                </button>
                <span role="tooltip" id={`status-help-${source.id}`} className="source-status-tooltip">{checkStatusHelp(source.status)}</span>
              </span>}
            </span>
          </div>
          <div className="source-group-meta">
            <OriginBadge source={source} />
            {meta && <span>{meta}</span>}
            {blockableDomain(source) && (
              <button type="button" className="answer-sources-block" title={`Nunca usar ${blockableDomain(source)} como fonte nas próximas pesquisas`} onClick={() => blockSite(blockableDomain(source))}>
                Não usar este site
              </button>
            )}
          </div>
          {/* Observação da fonte (ex.: por que a IA sugeriu a fonte) inteira e em destaque. */}
          {source.notes && <p className="source-group-note">{source.notes}</p>}
          {items.map(citation => {
            const locator = formatLocator(citation);
            const detail = [locator !== 'Nota inteira' ? locator : '', citation.sourceQuote ? `“${citation.sourceQuote}”` : ''].filter(Boolean).join(' · ');
            return <div className="source-citation-row" key={citation.id}>
              {/* Mesmo número [n] mostrado no texto da nota (ordem das citações). */}
              <span className="source-citation-n">[{citations.indexOf(citation) + 1}]</span>
              <div className="source-citation-body">
                <span className="source-citation-quote" title={citation.exactQuote || locator}>{citation.exactQuote || locator}</span>
                {citation.exactQuote && detail && <span className="tiny muted source-citation-detail" title={detail}>{detail}</span>}
              </div>
              <button type="button" className="icon-btn source-citation-remove" disabled={busy} title="Remover citação" aria-label="Remover citação" onClick={() => removeCitation(citation.id)}>
                <Icon name="trash" className="sm" />
              </button>
            </div>;
          })}
        </section>
        );
      })}
      <p className="tiny muted">As fontes são referências declaradas. A presença de metadados ainda não confirma o conteúdo da fonte.</p>
    </div>
  );
}
