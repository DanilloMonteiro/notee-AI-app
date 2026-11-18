// Resposta de dúvida com [n] clicáveis e lista de fontes (ADR-045).
// [n] conferido → link para a fonte; [n] de origem só declarada pela IA → marcador sem link, com aviso.
import React from 'react';
import { Icon } from '../../components/Icon';
import { UNVERIFIED_LABEL, isHighAuthority, isVerifiedSource, sourceHost, sourceMeta, splitAnswerRefs } from './answerRefs';

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

/** Lista numerada das fontes: conferidas com link; declaradas com o aviso "não conferida". */
export function AnswerSources({ sources = [], compact = false }) {
  if (!sources.length) return null;
  return (
    <ol className={`answer-sources ${compact ? 'is-compact' : ''}`}>
      {sources.map((s, i) => (
        <li key={`${s.url || s.title}-${i}`}>
          <span className="answer-sources-n">[{i + 1}]</span>
          {isVerifiedSource(s) ? (
            <span>
              <a href={s.url} target="_blank" rel="noopener noreferrer">
                {s.title} <Icon name="external" className="sm" />
              </a>{' '}
              <span className="answer-sources-host">{sourceMeta(s) || sourceHost(s.url)}</span>
              {isHighAuthority(s) && (
                <span className="answer-sources-authority" title="O buscador classifica esta fonte como de alta autoridade">
                  alta autoridade
                </span>
              )}
            </span>
          ) : (
            <span>
              {s.title} <span className="answer-sources-unverified">{UNVERIFIED_LABEL}</span>
            </span>
          )}
        </li>
      ))}
    </ol>
  );
}
