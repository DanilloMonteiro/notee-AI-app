import React, { useState } from 'react';
import { sourcesApi } from './sourcesApi';

const kinds = ['book', 'article', 'video', 'study', 'dataset', 'document', 'website', 'person', 'interview', 'other'];
const labels = { book: 'Livro', article: 'Artigo', video: 'Vídeo', study: 'Estudo', dataset: 'Conjunto de dados', document: 'Documento', website: 'Site', person: 'Pesquisador(a)', interview: 'Entrevista', other: 'Outro' };
const empty = { kind: 'book', title: '', authors: [], year: '', venue: '', language: '', doi: '', isbn: '', url: '', license: 'unknown', notes: '' };

export function SourceForm({ initial, onSave, onCancel, busy = false }) {
  const [draft, setDraft] = useState(() => ({ ...empty, ...(initial || {}), authorsText: (initial?.authors || []).join('\n') }));
  const [resolverInput, setResolverInput] = useState('');
  const [resolving, setResolving] = useState(false);
  const [resolveMessage, setResolveMessage] = useState('');
  const change = e => setDraft(prev => ({ ...prev, [e.target.name]: e.target.value }));
  const submit = async e => {
    e.preventDefault();
    const year = draft.year ? Number(draft.year) : null;
    await onSave({
      kind: draft.kind,
      title: draft.title.trim(),
      authors: draft.authorsText.split(/[\n,;]/).map(s => s.trim()).filter(Boolean),
      year,
      venue: draft.venue.trim() || null,
      language: draft.language.trim() || null,
      doi: draft.doi.trim() || null,
      isbn: draft.isbn.trim() || null,
      url: draft.url.trim() || null,
      license: draft.license,
      notes: draft.notes.trim() || null,
    });
  };
  const resolve = async () => {
    if (!resolverInput.trim()) return;
    setResolving(true);
    setResolveMessage('');
    try {
      const result = await sourcesApi.resolve(resolverInput.trim());
      setDraft(prev => ({ ...prev, ...result, authorsText: (result.authors || []).join('\n'), year: result.year || '', license: result.license || 'unknown' }));
      setResolveMessage(result.message || `Metadados sugeridos por ${result.provider}. Confira os campos antes de salvar.`);
    } catch (err) { setResolveMessage(err.message || 'Não foi possível resolver. Você ainda pode preencher os dados manualmente.'); }
    finally { setResolving(false); }
  };

  return (
    <form className="source-form stack" onSubmit={submit}>
      <div className="row gap8 source-resolver">
        <label className="grow">Preencher por DOI, ISBN ou link<input value={resolverInput} onChange={e => setResolverInput(e.target.value)} maxLength={2048} placeholder="10.1234/abc, ISBN ou https://…" /></label>
        <button type="button" className="btn compact" disabled={busy || resolving || !resolverInput.trim()} onClick={resolve}>{resolving ? 'Buscando…' : 'Buscar metadados'}</button>
      </div>
      {resolveMessage && <p className="tiny muted" role="status">{resolveMessage}</p>}
      <div className="row wrap gap8">
        <label className="grow">Tipo
          <select name="kind" value={draft.kind} onChange={change}>{kinds.map(kind => <option key={kind} value={kind}>{labels[kind]}</option>)}</select>
        </label>
        <label className="grow">Título
          <input name="title" value={draft.title} onChange={change} maxLength={300} required />
        </label>
      </div>
      <label>Autores (um por linha ou separados por vírgula)
        <textarea name="authorsText" value={draft.authorsText} onChange={change} rows={2} placeholder="Nome do autor" />
      </label>
      <div className="row wrap gap8">
        <label>Ano
          <input name="year" type="number" min="1000" max="9999" value={draft.year || ''} onChange={change} />
        </label>
        <label className="grow">Editora, periódico ou canal
          <input name="venue" value={draft.venue || ''} onChange={change} maxLength={300} />
        </label>
        <label>Idioma
          <input name="language" value={draft.language || ''} onChange={change} maxLength={32} />
        </label>
      </div>
      <div className="row wrap gap8">
        <label className="grow">DOI
          <input name="doi" value={draft.doi || ''} onChange={change} maxLength={256} placeholder="10.xxxx/..." />
        </label>
        <label>ISBN
          <input name="isbn" value={draft.isbn || ''} onChange={change} maxLength={20} />
        </label>
      </div>
      <label>Link
        <input name="url" type="url" value={draft.url || ''} onChange={change} maxLength={2048} placeholder="https://…" />
      </label>
      <div className="row wrap gap8">
        <label>Licença declarada
          <select name="license" value={draft.license || 'unknown'} onChange={change}>
            <option value="unknown">Desconhecida</option><option value="public-domain">Domínio público</option>
            <option value="cc-by">CC BY</option><option value="cc-by-sa">CC BY-SA</option>
            <option value="open-access">Acesso aberto</option><option value="proprietary">Proprietária</option>
          </select>
        </label>
        <label className="grow">Notas privadas
          <input name="notes" value={draft.notes || ''} onChange={change} maxLength={2000} />
        </label>
      </div>
      <div className="row gap8">
        <button className="btn primary compact" disabled={busy || !draft.title.trim()}>{busy ? 'A guardar…' : 'Guardar fonte'}</button>
        {onCancel && <button type="button" className="btn compact" onClick={onCancel}>Cancelar</button>}
      </div>
    </form>
  );
}
