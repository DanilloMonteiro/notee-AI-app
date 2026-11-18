// Resposta de dúvida com [n] clicáveis e lista de fontes (ADR-045/047).
// [n] conferido → link para a fonte; [n] de origem só declarada pela IA → marcador sem link, com aviso.
import React from 'react';
import { Icon } from '../../components/Icon';
import { UNVERIFIED_LABEL, isHighAuthority, isVerifiedSource, sourceHost, sourceMeta, splitAnswerRefs } from './answerRefs';
import { blockableDomain, formatComposition, shortAuthors, sourceComposition, sourceOriginOf } from './sourceMix';

function RefMark({ n, source }) {
  if (isVerifiedSource(source)) {
    return (
      <a className="answer-ref" href={source.url} target="_blank" rel="noopener noreferrer" title={source.title}>
        [{n}]
      </a>
    );
  }
  return (
    <span className="answer-ref is-unverified" title={`${source.title} · ${UNVERIFIED_LABEL}`}>
      [{n}]
    </span>
  );
}

/** Texto da resposta (parágrafos separados por linha) com os marcadores [n] ligados às fontes. */
export function AnswerText({ answer, sources = [], className = '', paragraphStyle }) {
  const paragraphs = String(answer || '').split('\n').filter(p => p.trim());
  return (
    <div className={`answer-text ${className}`.trim()}>
      {paragraphs.map((paragraph, i) => (
        <p key={i} style={paragraphStyle}>
          {splitAnswerRefs(paragraph, sources).map((part, j) =>
            part.type === 'ref' ? <RefMark key={j} n={part.n} source={part.source} /> : <React.Fragment key={j}>{part.value}</React.Fragment>,
          )}
        </p>
      ))}
    </div>
  );
}

/** "Fontes: 75% artigos · 25% internet" com barra proporcional; nada quando não há fontes de origem conhecida. */
export function SourceComposition({ sources = [], className = '' }) {
  const c = sourceComposition(sources);
  const text = formatComposition(c);
  if (!text) return null;
  return (
    <div className={`source-composition ${className}`.trim()} title={`${c.academic} artigo(s) acadêmico(s) e ${c.web} página(s) da internet`}>
      <span className="source-composition-bar" aria-hidden="true">
        <span className="is-academic" style={{ width: `${c.academicPercent}%` }} />
        <span className="is-web" style={{ width: `${c.webPercent}%` }} />
      </span>
      <span className="tiny">Fontes: {text}</span>
    </div>
  );
}

/** Selo de origem: Artigo (acadêmico) ou Site (internet). */
export function OriginBadge({ source }) {
  const origin = sourceOriginOf(source);
  if (!origin) return null;
  return <span className={`source-origin-badge is-${origin}`}>{origin === 'academic' ? 'Artigo' : 'Site'}</span>;
}

/**
 * Lista numerada das fontes: conferidas com link; declaradas com o aviso "não conferida".
 * onBlockSite(domínio): quando passado, páginas da internet ganham a ação "Não usar este site".
 */
export function AnswerSources({ sources = [], compact = false, onBlockSite }) {
  if (!sources.length) return null;
  return (
    <ol className={`answer-sources ${compact ? 'is-compact' : ''}`}>
      {sources.map((s, i) => {
        const domain = onBlockSite ? blockableDomain(s) : null;
        const authors = sourceOriginOf(s) === 'academic' ? shortAuthors(s.authors) : '';
        return (
          <li key={`${s.url || s.title}-${i}`}>
            <span className="answer-sources-n">[{i + 1}]</span>
            {isVerifiedSource(s) ? (
              <span>
                <OriginBadge source={s} />{' '}
                <a href={s.url} target="_blank" rel="noopener noreferrer">
                  {s.title} <Icon name="external" className="sm" />
                </a>{' '}
                {authors && <span className="answer-sources-host">{authors} · </span>}
                <span className="answer-sources-host">{sourceMeta(s) || sourceHost(s.url)}</span>
                {s.trusted && (
                  <span className="answer-sources-trusted" title="Site que você marcou como confiável">
                    confiável
                  </span>
                )}
                {isHighAuthority(s) && (
                  <span className="answer-sources-authority" title="O buscador classifica esta fonte como de alta autoridade">
                    alta autoridade
                  </span>
                )}
                {domain && (
                  <button type="button" className="answer-sources-block" title={`Nunca usar ${domain} como fonte`} onClick={() => onBlockSite(domain)}>
                    Não usar este site
                  </button>
                )}
              </span>
            ) : (
              <span>
                {s.title} <span className="answer-sources-unverified">{UNVERIFIED_LABEL}</span>
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}
