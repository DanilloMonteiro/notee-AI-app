// Análise assistida da nota (M10): sugestões de tag, categoria e dúvida com aceite individual,
// e a configuração de pesquisa automática desta nota (sobrepõe a da conta).
import React, { useCallback, useEffect, useState } from 'react';
import { Icon } from './Icon';
import { apiFetch } from '../api';
import { useToast } from '../contexts/ToastContext';

const KIND = { tag: 'Tag', category: 'Categoria', doubt: 'Dúvida' };
const AUTO = { inherit: 'Seguir a conta', on: 'Sempre pesquisar', off: 'Não pesquisar automaticamente' };

export function NoteSuggestions({ noteId, isDirty, disabled, onNoteChanged }) {
  const [items, setItems] = useState([]);
  const [settings, setSettings] = useState(null);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();

  const load = useCallback(async () => {
    try {
      const [list, s] = await Promise.all([apiFetch(`/notes/${noteId}/suggestions`), apiFetch(`/notes/${noteId}/settings`)]);
      setItems(list || []);
      setSettings(s);
    } catch {
      // painel opcional
    }
  }, [noteId]);

  useEffect(() => {
    load();
  }, [load]);

  const run = async (fn, okMsg) => {
    setBusy(true);
    try {
      await fn();
      if (okMsg) addToast(okMsg);
    } catch (err) {
      addToast(err.message || 'Não foi possível concluir.', 'warning');
    } finally {
      setBusy(false);
      load();
    }
  };

  const analyze = () =>
    run(async () => {
      const list = await apiFetch(`/notes/${noteId}/analysis-runs`, { method: 'POST' });
      if (!list?.length) addToast('Nenhuma sugestão nova para esta versão.', 'info');
    });

  const accept = s =>
    run(async () => {
      await apiFetch(`/suggestions/${s.id}/accept`, { method: 'POST' });
      onNoteChanged();
    }, `${KIND[s.kind]} aplicada.`);

  const dismiss = s => run(() => apiFetch(`/suggestions/${s.id}/dismiss`, { method: 'POST' }));

  const setAuto = value =>
    run(async () => setSettings(await apiFetch(`/notes/${noteId}/settings`, { method: 'PATCH', body: JSON.stringify({ autoResearch: value }) })));

  return (
    <div className="card pad mb24">
      <div className="row between wrap gap8">
        <div>
          <strong>Sugestões para esta nota</strong>
          <div id="note-analysis-description" className="tiny muted">
            Analisa localmente a versão salva da nota e pode sugerir tags, categoria e dúvidas. Nada é enviado para fora durante esta análise.
          </div>
          <div className="tiny"><strong>Você decide:</strong> aceite ou descarte cada sugestão individualmente.</div>
          {isDirty && <div className="tiny muted">Salve as alterações para analisar a versão atual da nota.</div>}
        </div>
        <div className="row gap8 wrap">
          {settings && (
            <select
              aria-label="Pesquisa automática nesta nota"
              value={settings.autoResearch}
              disabled={busy || disabled}
              onChange={e => setAuto(e.target.value)}
              style={{ width: 'auto' }}
              title={settings.effectiveAutoResearch ? 'Novas dúvidas são pesquisadas automaticamente' : 'Novas dúvidas ficam pendentes'}
            >
              {Object.entries(AUTO).map(([v, label]) => (
                <option key={v} value={v}>
                  {label}
                </option>
              ))}
            </select>
          )}
          <button className="btn compact" onClick={analyze} disabled={busy || disabled || isDirty} aria-describedby="note-analysis-description" title={isDirty ? 'Salve a nota antes' : undefined}>
            <Icon name="spark" className="sm" /> Analisar nota
          </button>
        </div>
      </div>

      {items.length > 0 && (
        <div className="stack mt16">
          {items.map(s => (
            <div key={s.id} className="row between gap8 wrap" style={{ borderTop: '1px solid var(--line)', paddingTop: 10 }}>
              <div style={{ minWidth: 0, flex: 1 }}>
                <span className="pill blue">{KIND[s.kind]}</span> <strong>{s.value}</strong>
                <div className="tiny muted">{s.reason}</div>
              </div>
              <div className="row gap8">
                <button className="btn primary compact" disabled={busy || isDirty || disabled} onClick={() => accept(s)}>
                  <Icon name="check" className="sm" /> Aceitar
                </button>
                <button className="btn compact" disabled={busy} onClick={() => dismiss(s)}>
                  Descartar
                </button>
              </div>
            </div>
          ))}
          {isDirty && <p className="tiny muted">Salve a nota para aceitar sugestões (elas valem para a versão salva).</p>}
        </div>
      )}
    </div>
  );
}
