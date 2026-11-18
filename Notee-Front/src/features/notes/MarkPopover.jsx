// Card que aparece ao passar o mouse (ou clicar) num trecho marcado do texto da nota.
// Mostra tudo o que está ancorado no trecho: dúvida com resposta, comentário e fonte.
// Usa só dados já carregados pela página; nenhuma chamada nova à API.
import React, { useLayoutEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { formatLocator } from '../sources/citationFormatting';
import { AnswerSources, AnswerText } from '../research/AnswerWithRefs';

const DOUBT_STATUS = {
  Completed: { label: 'Respondida', className: 'green' },
  Running: { label: 'Pesquisando…', className: 'blue' },
  Pending: { label: 'Pendente', className: 'blue' },
};
// Status vem de SourceCheckStatus (API) em minúsculas, ou "unverified".
const SOURCE_STATUS = {
  unverified: 'Não verificada',
  pending: 'Conferindo…',
  running: 'Conferindo…',
  confirmed: 'Confirmada',
  foundwithdifferences: 'Encontrada com diferenças',
  quotenotfound: 'Trecho não encontrado',
  mismatch: 'Não confere',
  notfound: 'Não encontrada',
  unverifiable: 'Não verificável',
  failed: 'Falha na conferência',
};
const GAP = 8;

function DoubtSection({ doubt, onShowPanel }) {
  const status = DOUBT_STATUS[doubt.status] || { label: doubt.status, className: 'blue' };
  return (
    <section className="nm-card-section is-doubt">
      <header>
        <span className="nm-card-kind"><Icon name="question" className="sm" /> Dúvida</span>
        <span className={`pill ${status.className}`}>{status.label}</span>
      </header>
      <strong className="nm-card-title">{doubt.question}</strong>
      {doubt.status === 'Completed' && doubt.answer ? (
        <AnswerText answer={doubt.answer} sources={doubt.sources || []} className="nm-card-answer" />
      ) : (
        <p className="nm-card-muted">Aguardando resposta…</p>
      )}
      {doubt.status === 'Completed' &&
        (doubt.sources?.length > 0 ? (
          <AnswerSources sources={doubt.sources} compact />
        ) : (
          <p className="nm-card-muted">Sem fonte: a IA não indicou de onde veio a resposta.</p>
        ))}
      <footer>
        {doubt.status === 'Completed' && <Link to={`/research/${doubt.id}`}>Ler resposta completa</Link>}
        <button type="button" onClick={() => onShowPanel('doubts')}>Ver no painel</button>
      </footer>
    </section>
  );
}

function CommentSection({ comment, onShowPanel }) {
  const isPublic = !!comment.isPublic;
  return (
    <section className="nm-card-section is-comment">
      <header>
        <span className="nm-card-kind"><Icon name="message" className="sm" /> Comentário</span>
        <span className={`pill ${isPublic ? 'blue' : ''}`}>{isPublic ? 'Público' : 'Só eu'}</span>
      </header>
      <p className="nm-card-text">{comment.isDeleted ? 'Comentário removido.' : comment.text}</p>
      <footer>
        <button type="button" onClick={() => onShowPanel('comments')}>Ver no painel</button>
      </footer>
    </section>
  );
}

function SourceSection({ citation, onShowPanel }) {
  const source = citation.source || {};
  const meta = [source.authors?.join(', '), source.year, source.venue].filter(Boolean).join(' · ');
  const locator = formatLocator(citation);
  return (
    <section className="nm-card-section is-source">
      <header>
        <span className="nm-card-kind"><Icon name="book" className="sm" /> Fonte [{citation.referenceNumber}]</span>
        {source.status && <span className="pill">{SOURCE_STATUS[String(source.status).toLowerCase()] || source.status}</span>}
      </header>
      <strong className="nm-card-title">{source.title || 'Fonte sem título'}</strong>
      {meta && <p className="nm-card-muted">{meta}</p>}
      {locator !== 'Nota inteira' && <p className="nm-card-muted">{locator}</p>}
      {/* Ex.: "Origem informada pela IA na criação da nota — não conferida." */}
      {source.notes && <p className="nm-card-muted nm-card-note">{source.notes}</p>}
      {citation.sourceQuote && <blockquote className="nm-card-quote">“{citation.sourceQuote}”</blockquote>}
      <footer>
        {source.url && (
          <a href={source.url} target="_blank" rel="noreferrer">
            Abrir fonte <Icon name="external" className="sm" />
          </a>
        )}
        <button type="button" onClick={() => onShowPanel('sources')}>Ver no painel</button>
      </footer>
    </section>
  );
}

/**
 * anchorRect: retângulo (getBoundingClientRect) do trecho marcado.
 * items: [{ kind: 'doubt'|'comment'|'source', data }] já resolvidos pela página.
 */
export function MarkPopover({ anchorRect, items, onShowPanel, onMouseEnter, onMouseLeave }) {
  const ref = useRef(null);
  const [pos, setPos] = useState({ top: -9999, left: -9999 });

  // Abaixo do trecho; acima se não couber. Sempre dentro da janela.
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el || !anchorRect) return;
    const { width, height } = el.getBoundingClientRect();
    const below = anchorRect.bottom + GAP;
    const top = below + height > window.innerHeight - GAP && anchorRect.top - GAP - height > GAP ? anchorRect.top - GAP - height : below;
    const left = Math.min(Math.max(GAP, anchorRect.left), window.innerWidth - width - GAP);
    setPos({ top, left });
  }, [anchorRect, items]);

  if (!items.length) return null;
  return (
    <div
      ref={ref}
      className="nm-card"
      role="tooltip"
      style={{ top: pos.top, left: pos.left }}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
    >
      {items.map(item =>
        item.kind === 'doubt' ? (
          <DoubtSection key={`d-${item.data.id}`} doubt={item.data} onShowPanel={onShowPanel} />
        ) : item.kind === 'comment' ? (
          <CommentSection key={`c-${item.data.id}`} comment={item.data} onShowPanel={onShowPanel} />
        ) : (
          <SourceSection key={`s-${item.data.id}`} citation={item.data} onShowPanel={onShowPanel} />
        ),
      )}
    </div>
  );
}
