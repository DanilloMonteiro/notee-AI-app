// src/pages/ResearchDetails.jsx
// Detalhe da dúvida: estado do job durável, provider usado, evidência ("Sem fonte"),
// nova pesquisa (202 + Idempotency-Key) e cancelamento.
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';
import { useToast } from '../contexts/ToastContext';

const DOUBT_STATUS = {
  Pending: { label: 'Na fila', cls: 'amber' },
  Running: { label: 'Pesquisando...', cls: 'blue' },
  Completed: { label: 'Respondida', cls: 'green' },
  Failed: { label: 'Falhou', cls: 'red' },
  Cancelled: { label: 'Cancelada', cls: '' },
};

const PROVIDER = { fake: 'Simulado (local, nada foi enviado)', gemini: 'Google Gemini (externo)' };
const isActive = d => d?.latestJob && ['Queued', 'Running'].includes(d.latestJob.status);

export function ResearchDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const [doubt, setDoubt] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const pollRef = useRef(null);

  const load = useCallback(async () => {
    try {
      setDoubt(await apiFetch(`/doubts/${id}`));
    } catch {
      setDoubt(null);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  // Acompanha o job enquanto estiver na fila ou em execução.
  useEffect(() => {
    clearInterval(pollRef.current);
    if (isActive(doubt) || doubt?.status === 'Running') {
      pollRef.current = setInterval(load, 2000);
    }
    return () => clearInterval(pollRef.current);
  }, [doubt, load]);

  const research = async () => {
    setBusy(true);
    try {
      await apiFetch(`/doubts/${id}/research-jobs`, {
        method: 'POST',
        headers: { 'Idempotency-Key': `${id}-${Date.now()}` },
      });
      addToast('Pesquisa enviada para a fila.');
      await load();
    } catch (err) {
      addToast(err.message || 'Não foi possível iniciar a pesquisa.', 'warning');
    } finally {
      setBusy(false);
    }
  };

  const cancel = async () => {
    setBusy(true);
    try {
      await apiFetch(`/research-jobs/${doubt.latestJob.id}/cancel`, { method: 'POST' });
      addToast('Cancelamento solicitado.');
      await load();
    } catch (err) {
      addToast(err.message || 'Não foi possível cancelar.', 'warning');
    } finally {
      setBusy(false);
    }
  };

  if (loading)
    return (
      <div className="empty">
        <h3>A carregar...</h3>
      </div>
    );

  if (!doubt) {
    return (
      <div className="empty">
        <h3>Dúvida não encontrada</h3>
        <button className="btn mt16" onClick={() => navigate(-1)}>
          Voltar
        </button>
      </div>
    );
  }

  const status = DOUBT_STATUS[doubt.status] || { label: doubt.status, cls: '' };
  const job = doubt.latestJob;

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">DÚVIDAS / DETALHE</div>
          <h1>Da pergunta à informação</h1>
          <p>O que foi pesquisado, por qual provedor e com que evidência.</p>
        </div>
        <div className="row gap8">
          <button className="btn" onClick={() => navigate(-1)}>
            <Icon name="back" /> Voltar
          </button>
          <Link className="btn" to={`/note/${doubt.noteId}`}>
            <Icon name="note" className="sm" /> Abrir nota
          </Link>
        </div>
      </div>

      <div className="split">
        <section className="card pad">
          <div className="row gap8 wrap">
            <span className={`pill ${status.cls}`}>{status.label}</span>
            {doubt.origin === 'BoardQuestion' && <span className="pill">Pergunta do mapa</span>}
            {doubt.evidence === 'NoSource' && <span className="pill amber">Sem fonte</span>}
            {doubt.evidence === 'Sourced' && <span className="pill green">{doubt.sources.length} fonte(s)</span>}
          </div>

          <h3 className="mt16">{doubt.question}</h3>

          {doubt.exactQuote && (
            <blockquote className="quote-selection" style={{ fontSize: '12px', marginTop: '13px' }}>
              {doubt.exactQuote}
            </blockquote>
          )}

          {doubt.status === 'Completed' ? (
            <>
              <div className="callout neutral mb16 mt16">
                <Icon name="info" className="sm" />
                <span>Resposta de: {PROVIDER[doubt.answerProvider] || doubt.answerProvider || 'provedor desconhecido'}.</span>
              </div>
              <div style={{ fontSize: '14px', lineHeight: '1.7', color: 'var(--ink)' }}>
                {doubt.answer?.split('\n').map((paragraph, i) =>
                  paragraph.trim() ? (
                    <p key={i} style={{ marginBottom: '14px' }}>
                      {paragraph}
                    </p>
                  ) : null,
                )}
              </div>
              <div className="divider"></div>
              {doubt.sources?.length > 0 ? (
                <div className="mt16">
                  <h4 className="mb16">Fontes devolvidas pelo provedor</h4>
                  <ol style={{ paddingLeft: 20, lineHeight: 1.8 }}>
                    {doubt.sources.map(s => (
                      <li key={s.url}>
                        <a href={s.url} target="_blank" rel="noopener noreferrer">
                          {s.title}
                        </a>{' '}
                        <span className="tiny muted">{new URL(s.url).hostname}</span>
                      </li>
                    ))}
                  </ol>
                  <p className="tiny muted">Fontes indicadas pelo provedor; confira se elas sustentam a resposta.</p>
                </div>
              ) : (
                <div className="callout warning mt16">
                  <Icon name="info" />
                  <span>
                    <strong>Sem fonte verificada.</strong> A resposta não traz citações conferíveis; a IA pode errar. Confirme informações
                    importantes.
                  </span>
                </div>
              )}
            </>
          ) : (
            <p className="muted mt16">
              {doubt.status === 'Failed'
                ? 'A pesquisa falhou depois das tentativas automáticas. Você pode tentar novamente.'
                : doubt.status === 'Cancelled'
                  ? 'A pesquisa foi cancelada.'
                  : job
                    ? 'A pesquisa está na fila ou em andamento. Esta página atualiza sozinha.'
                    : 'Ainda não há pesquisa para esta dúvida (a pesquisa pode estar desligada em Configurações).'}
            </p>
          )}

          <div className="row gap8 mt16">
            {isActive(doubt) ? (
              <button className="btn" onClick={cancel} disabled={busy}>
                <Icon name="close" className="sm" /> Cancelar pesquisa
              </button>
            ) : (
              <button className="btn primary" onClick={research} disabled={busy}>
                <Icon name="refresh" className="sm" /> {doubt.status === 'Completed' ? 'Pesquisar novamente' : 'Pesquisar'}
              </button>
            )}
          </div>
        </section>

        <aside className="stack">
          <div className="card pad">
            <h3>Última pesquisa</h3>
            {job ? (
              <ul className="small muted mt16" style={{ lineHeight: 1.8, listStyle: 'none', padding: 0 }}>
                <li>Estado: {job.status}</li>
                <li>
                  Tentativa: {job.attempt} de {job.maxAttempts}
                </li>
                {job.provider && <li>Provedor: {job.provider}</li>}
                {job.lastError && <li>Último erro: {job.lastError}</li>}
                {job.inputTokens != null && (
                  <li>
                    Tokens: {job.inputTokens} entrada / {job.outputTokens} saída
                  </li>
                )}
                <li>Criada: {new Date(job.createdAt).toLocaleString('pt-BR')}</li>
                {job.completedAt && <li>Concluída: {new Date(job.completedAt).toLocaleString('pt-BR')}</li>}
              </ul>
            ) : (
              <p className="small muted mt16">Nenhuma pesquisa registrada.</p>
            )}
          </div>
          <div className="card pad">
            <h3>Contexto preservado</h3>
            <p className="small muted mt16">
              A resposta é um registro separado. Editar a nota original não reescreve a pesquisa já concluída.
            </p>
          </div>
        </aside>
      </div>
    </div>
  );
}
