import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { useToast } from '../../contexts/ToastContext';
import { sourcesApi } from './sourcesApi';
import { SourceForm } from './SourceForm';
import './sources.css';

const kinds = [['', 'Todos os tipos'], ['book', 'Livros'], ['article', 'Artigos'], ['video', 'Vídeos'], ['study', 'Estudos'], ['dataset', 'Dados'], ['document', 'Documentos'], ['website', 'Sites'], ['person', 'Pesquisadores'], ['interview', 'Entrevistas'], ['other', 'Outros']];
const statuses = [['', 'Todos os status'], ['pending', 'Pendente'], ['running', 'Em andamento'], ['confirmed', 'Confirmada'], ['foundwithdifferences', 'Com divergências'], ['quotenotfound', 'Trecho não localizado'], ['mismatch', 'Incompatível'], ['notfound', 'Não encontrada'], ['unverifiable', 'Não verificável'], ['failed', 'Falhou']];

function SourceFiles({ source }) {
  const [files, setFiles] = useState([]);
  const [checks, setChecks] = useState([]);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  const load = useCallback(() => Promise.all([sourcesApi.files(source.id), sourcesApi.sourceChecks(source.id)]), [source.id]);
  const refresh = async () => {
    try {
      const [fileRows, checkRows] = await load();
      setFiles(fileRows || []); setChecks(checkRows || []);
    } catch (err) { addToast(err.message || 'Não foi possível carregar arquivos e verificações.', 'warning'); }
  };
  useEffect(() => {
    let active = true;
    load().then(([fileRows, checkRows]) => {
      if (active) { setFiles(fileRows || []); setChecks(checkRows || []); }
    }).catch(err => { if (active) addToast(err.message || 'Não foi possível carregar arquivos e verificações.', 'warning'); });
    return () => { active = false; };
  }, [addToast, load]);
  const upload = async event => {
    const file = event.target.files?.[0];
    if (!file) return;
    setBusy(true);
    try { await sourcesApi.uploadFile(source.id, file); await refresh(); addToast('Arquivo privado salvo e trecho enviado para conferência.'); }
    catch (err) { addToast(err.message || 'Não foi possível enviar o arquivo.', 'warning'); }
    finally { setBusy(false); event.target.value = ''; }
  };
  const retry = async () => { setBusy(true); try { await sourcesApi.retrySourceCheck(source.id); addToast('Verificação agendada.'); } catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); } };
  const removeFile = async file => {
    if (!window.confirm('Excluir este arquivo privado e o texto extraído?')) return;
    setBusy(true); try { await sourcesApi.deleteFile(source.id, file.id); await refresh(); } catch (err) { addToast(err.message, 'warning'); } finally { setBusy(false); }
  };
  return <details className="source-files"><summary>Arquivos e verificação</summary>
    <p className="tiny muted">Arquivos privados, limite de 10 MB por arquivo e 200 MB por conta. Não são publicados.</p>
    <label className="btn compact">Enviar PDF ou TXT<input type="file" accept=".pdf,.txt,application/pdf,text/plain" disabled={busy} onChange={upload} hidden /></label>
    <button type="button" className="btn compact ghost" disabled={busy} onClick={retry}>Verificar metadados de novo</button>
    {files.map(file => <div className="row between source-file-row" key={file.id}><a href={file.downloadUrl} target="_blank" rel="noopener noreferrer">Abrir arquivo ({Math.ceil(file.sizeBytes / 1024)} KB, privado)</a><button type="button" className="btn compact ghost" disabled={busy} onClick={() => removeFile(file)}>Excluir</button></div>)}
    {checks.slice(0, 5).map(check => <div className="source-check-result" key={check.id}><strong>{check.status}</strong><p className="small">{check.summary}</p>{check.arguments?.map((arg, i) => <p className="tiny" key={i}>{arg.point}: {arg.evidence}</p>)}</div>)}
  </details>;
}

export function SourcesPage() {
  const [sources, setSources] = useState([]);
  const [query, setQuery] = useState('');
  const [kind, setKind] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState(null);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();

  const load = useCallback(() => sourcesApi.list({ query, kind, status }), [kind, query, status]);
  const refresh = useCallback(async () => {
    try { setSources(await load() || []); }
    catch (err) { addToast(err.message || 'Não foi possível carregar a biblioteca.', 'warning'); }
  }, [addToast, load]);

  useEffect(() => {
    let active = true;
    load().then(rows => { if (active) setSources(rows || []); })
      .catch(err => { if (active) addToast(err.message || 'Não foi possível carregar a biblioteca.', 'warning'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [addToast, load]);

  const save = async input => {
    setBusy(true);
    try {
      if (form?.id) await sourcesApi.update(form.id, input);
      else await sourcesApi.create(input);
      setForm(null);
      await refresh();
      addToast('Fonte salva.');
    } catch (err) { addToast(err.message || 'Não foi possível salvar a fonte.', 'warning'); }
    finally { setBusy(false); }
  };

  const remove = async source => {
    if (!window.confirm(`Excluir “${source.title}” da biblioteca?`)) return;
    setBusy(true);
    try { await sourcesApi.remove(source.id); await refresh(); addToast('Fonte excluída.'); }
    catch (err) { addToast(err.message || 'Não foi possível excluir a fonte.', 'warning'); }
    finally { setBusy(false); }
  };

  return (
    <div className="page-container sources-page">
      <header className="page-header row between wrap gap8">
        <div><h1>Biblioteca de fontes</h1><p className="muted">Fontes reutilizáveis e citações das suas notas.</p></div>
        <button type="button" className="btn primary" onClick={() => setForm({})}><Icon name="plus" className="sm" /> Adicionar fonte</button>
      </header>
      {form && <div className="card pad source-edit-card"><h2>{form.id ? 'Editar fonte' : 'Nova fonte'}</h2><SourceForm key={form.id || 'new'} initial={form.id ? form : null} busy={busy} onSave={save} onCancel={() => setForm(null)} /></div>}
      <div className="row wrap gap8 source-filters">
        <label className="grow">Buscar<input type="search" value={query} onChange={e => setQuery(e.target.value)} placeholder="Título, autor, DOI ou ISBN" /></label>
        <label>Tipo<select value={kind} onChange={e => setKind(e.target.value)}>{kinds.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <label>Status<select value={status} onChange={e => setStatus(e.target.value)}>{statuses.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      </div>
      {loading ? <p className="muted">Carregando biblioteca…</p> : sources.length === 0 ? <div className="empty"><h3>Nenhuma fonte encontrada</h3><p>Cadastre fontes ou cite um trecho no editor de uma nota.</p></div> : (
        <div className="stack source-library-list">
          {sources.map(source => <article className="card pad source-library-item" key={source.id}>
            <div className="row between wrap gap8"><div><span className="pill">{source.kind}</span> <span className="pill">{source.status === 'unverified' ? 'Não verificada' : source.status}</span><h3>{source.title}</h3></div><div className="row gap8"><button type="button" className="btn compact" onClick={() => setForm(source)}>Editar</button><button type="button" className="btn compact ghost" disabled={busy || source.citationCount > 0} title={source.citationCount ? 'Remova as citações antes de excluir a fonte.' : 'Excluir fonte'} onClick={() => remove(source)}>Excluir</button></div></div>
            <p className="small muted">{[source.authors?.join(', '), source.year, source.venue, source.doi && `DOI ${source.doi}`, source.isbn && `ISBN ${source.isbn}`].filter(Boolean).join(' · ')}</p>
            {source.url && <a href={source.url} target="_blank" rel="noopener noreferrer nofollow">Abrir link ↗</a>}
            {source.notes && <p>{source.notes}</p>}
            <SourceFiles source={source} />
            <div className="source-cited-notes"><strong className="small">Notas que citam: </strong>{source.citedNotes?.length ? source.citedNotes.map(note => <Link key={note.noteId} to={`/note/${note.noteId}`}>{note.title}</Link>) : <span className="small muted">Nenhuma</span>}</div>
            <p className="tiny muted">Metadados declarados; a verificação ainda não garante que a fonte sustente uma afirmação.</p>
          </article>)}
        </div>
      )}
    </div>
  );
}
