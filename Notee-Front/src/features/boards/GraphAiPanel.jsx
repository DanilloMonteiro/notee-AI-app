// Aba "IA e contexto" do mapa (MAP-04/MAP-05): escopo do contexto, prévia JSON
// e sugestões de conexão que só entram no grafo com aceite individual.
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { apiFetch } from '../../api';
import { useToast } from '../../contexts/ToastContext';
import { Modal } from './BoardModals';
import { errorMessage } from './graph';

export function GraphAiPanel({ boardId, scope, setScope, selection, clearSelection, nodeCount, internalEdgeCount, onGraphChanged }) {
  const [suggestions, setSuggestions] = useState([]);
  const [preview, setPreview] = useState(null);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();
  const toastRef = useRef(addToast);
  useEffect(() => {
    toastRef.current = addToast;
  }, [addToast]);

  const scopeBody = () =>
    scope === 'manual' ? { scope: 'selected', noteIds: [...selection] } : { scope: 'all' };
  const selectedCount = scope === 'manual' ? selection.size : nodeCount;
  const canUse = selectedCount > 0;

  const loadSuggestions = useCallback(async () => {
    try {
      setSuggestions((await apiFetch(`/boards/${boardId}/suggestions`)) || []);
    } catch (err) {
      toastRef.current(errorMessage(err, 'Não foi possível carregar as sugestões.'), 'warning');
    }
  }, [boardId]);

  useEffect(() => {
    loadSuggestions();
  }, [loadSuggestions]);

  const run = async fn => {
    setBusy(true);
    try {
      await fn();
    } catch (err) {
      addToast(errorMessage(err, 'Não foi possível concluir.'), 'warning');
      if (err?.status === 409) loadSuggestions();
    } finally {
      setBusy(false);
    }
  };

  const showPreview = () =>
    run(async () => setPreview(await apiFetch(`/boards/${boardId}/context-preview`, { method: 'POST', body: JSON.stringify(scopeBody()) })));

  const generate = () =>
    run(async () => {
      const list = await apiFetch(`/boards/${boardId}/suggestions`, { method: 'POST', body: JSON.stringify(scopeBody()) });
      setSuggestions(list || []);
      addToast(list?.length ? `${list.length} sugestão(ões) pendente(s) para revisar.` : 'Nenhuma conexão nova encontrada.', 'info');
    });

  const decide = (s, action) =>
    run(async () => {
      await apiFetch(`/boards/${boardId}/suggestions/${s.id}/${action}`, { method: 'POST' });
      setSuggestions(prev => prev.filter(x => x.id !== s.id));
      if (action === 'accept') {
        addToast('Conexão criada a partir da sugestão.');
        onGraphChanged();
      }
    });

  return (
    <>
      <div className="graph-ai-intro">
        <span className="graph-ai-icon">
          <Icon name="spark" />
        </span>
        <div>
          <h3>Assistente do mapa</h3>
          <p>Ideias propostas, nunca aplicadas sem sua aprovação.</p>
        </div>
      </div>
      <div className="callout neutral" style={{ fontSize: 11 }}>
        <Icon name="info" className="sm" />
        <span>As sugestões são calculadas localmente (tags, categoria e termos em comum). Nenhum dado do mapa é enviado a serviços externos.</span>
      </div>

      <div className="graph-panel-section">
        <div className="overline">CONTEXTO</div>
        <p className="small muted mt16">Defina exatamente quais notas e conexões podem ser usadas.</p>
        <div className="graph-scope-switch" role="group" aria-label="Escopo do contexto">
          <button className={scope === 'all' ? 'active' : ''} aria-pressed={scope === 'all'} onClick={() => setScope('all')}>
            Mapa inteiro
          </button>
          <button className={scope === 'manual' ? 'active' : ''} aria-pressed={scope === 'manual'} onClick={() => setScope('manual')}>
            Selecionar subgrafo
          </button>
        </div>
        {scope === 'manual' && (
          <>
            <p className="small muted mt16">Clique nas notas do mapa para marcar ou desmarcar. Só entram as ligações entre as notas escolhidas.</p>
            <button className="btn ghost compact" onClick={clearSelection} disabled={!selection.size}>
              Limpar seleção
            </button>
          </>
        )}
        <div className="graph-context-stat">
          <strong>{selectedCount}</strong> notas <span>·</span> <strong>{internalEdgeCount}</strong> relações
        </div>
        <button className="btn compact" style={{ width: '100%' }} onClick={showPreview} disabled={!canUse || busy}>
          <Icon name="eye" className="sm" /> Pré-visualizar contexto JSON
        </button>
      </div>

      <div className="graph-panel-section">
        <div className="overline">SUGESTÕES DE CONEXÃO</div>
        <button className="btn compact" style={{ marginTop: 12, width: '100%' }} onClick={generate} disabled={!canUse || busy}>
          <Icon name="spark" className="sm" /> Gerar sugestões
        </button>
        {suggestions.length ? (
          suggestions.map(s => (
            <div key={s.id} className="graph-suggestion">
              <span className="pill blue">Conexão sugerida</span>
              <strong>
                {s.fromTitle} → {s.toTitle}
              </strong>
              <p>{s.reason}</p>
              <div className="row gap8">
                <button className="btn primary compact" onClick={() => decide(s, 'accept')} disabled={busy}>
                  <Icon name="check" className="sm" /> Aceitar
                </button>
                <button className="btn compact" onClick={() => decide(s, 'dismiss')} disabled={busy}>
                  Descartar
                </button>
              </div>
            </div>
          ))
        ) : (
          <p className="small muted mt16">Nenhuma sugestão pendente.</p>
        )}
      </div>

      {preview && (
        <Modal title="Contexto que seria usado" onClose={() => setPreview(null)}>
          <p className="small muted mb16">
            {preview.noteCount} notas · {preview.edgeCount} relações{preview.truncated ? ' · conteúdo truncado pelos limites' : ''}. Hash{' '}
            <code>{preview.contextHash}</code>
          </p>
          <pre className="graph-json-preview" style={{ maxHeight: '55vh', overflow: 'auto' }}>
            {JSON.stringify(preview, null, 2)}
          </pre>
        </Modal>
      )}
    </>
  );
}
