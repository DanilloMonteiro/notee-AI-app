// Páginas públicas de leitura (sem login, sem sidebar): nota publicada, link secreto, perfil e biblioteca.
// O conteúdo é renderizado como texto pelo React (sem HTML do autor), evitando XSS.
import React, { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ReadingActions } from '../social/ReadingActions';
import { FollowButton } from '../social/Social';
import { publicApi } from './publishingApi';
import { ChartView } from '../charts/ChartView';
import { parseBlocks as blocksOf } from '../notes/noteBlocks';
import { apiFetch } from '../../api';
import { locateQuote } from '../notes/anchors';
import { formatBibliographyCitation } from '../sources/citationFormatting';
import { segmentText } from '../notes/markSegments';
import { listLabels } from '../notes/blockFormatting';

// Trecho [from, to) do texto com negrito/itálico do bloco (block.styles). Sem estilos, texto puro.
function styledSlice(text, styles, from, to = text.length) {
  if (!styles?.length) return text.slice(from, to);
  const local = styles
    .map(s => ({ ...s, start: Math.max(s.start, from) - from, end: Math.min(s.end, to) - from }))
    .filter(s => s.end > s.start);
  return segmentText(text.slice(from, to), [], local).map((seg, i) => {
    let node = seg.text;
    if (seg.italic) node = <em>{node}</em>;
    if (seg.bold) node = <strong>{node}</strong>;
    return <React.Fragment key={`${from}-${i}`}>{node}</React.Fragment>;
  });
}

function MarkedText({ block, comments, citations, onCommentClick }) {
  const text = block.text || '';
  const commentRanges = comments.filter(c => !c.parentId && c.anchorQuote && !c.isHidden && !c.isDeleted)
    .map(c => ({ kind: 'comment', comment: c, location: locateQuote([block], c) })).filter(x => x.location);
  const citationRanges = citations.map((citation, index) => ({ citation, number: index + 1, location: citation.blockId === block.id
    ? locateQuote([block], { blockId: citation.blockId, exactQuote: citation.exactQuote, startIndex: citation.startIndex }) : null }))
    .filter(x => x.location);
  const ranges = [...commentRanges, ...citationRanges.map(x => ({ ...x, kind: 'citation' }))]
    .sort((a, b) => a.location.startIndex - b.location.startIndex || (a.kind === b.kind ? 0 : a.kind === 'comment' ? -1 : 1));
  const parts = [];
  let cursor = 0;
  for (const item of ranges) {
    const { location } = item;
    if (location.startIndex < cursor) continue;
    parts.push(styledSlice(text, block.styles, cursor, location.startIndex));
    if (item.kind === 'citation') {
      parts.push(<React.Fragment key={`citation-${item.citation.id}`}><mark className="source-mark">{styledSlice(text, block.styles, location.startIndex, location.endIndex)}</mark><sup className="source-reference-number">[{item.number}]</sup></React.Fragment>);
    } else {
      parts.push(<mark key={item.comment.id} className="comment-mark" style={{ background: '#fef08a', color: '#713f12', cursor: 'pointer' }} onClick={() => onCommentClick?.(item.comment.id)}>{styledSlice(text, block.styles, location.startIndex, location.endIndex)}</mark>);
    }
    cursor = location.endIndex;
  }
  parts.push(styledSlice(text, block.styles, cursor));
  return parts;
}

function Block({ block, listLabel, comments = [], citations = [], onCommentClick }) {
  const content = <MarkedText block={block} comments={comments} citations={citations} onCommentClick={onCommentClick} />;
  switch (block.type) {
    case 'chart':
      // Dados do snapshot publicado; nada é buscado fora da publicação.
      return <ChartView chart={block} />;
    case 'heading':
      // level: 1 = seção principal, 2 = padrão, 3 = subtítulo (organizador de tópicos).
      if (block.level === 1) return <h2 style={{ fontSize: 24, margin: '30px 0 10px' }}>{content}</h2>;
      if (block.level === 3) return <h4 style={{ fontSize: 16, margin: '18px 0 6px' }}>{content}</h4>;
      return <h3 style={{ fontSize: 20, margin: '26px 0 10px' }}>{content}</h3>;
    case 'quote':
      return <blockquote className="quote-selection">{content}</blockquote>;
    case 'code':
      return (
        <pre style={{ background: '#f4f6f1', padding: 14, borderRadius: 8, overflow: 'auto' }}>
          <code>{content}</code>
        </pre>
      );
    case 'list':
      // Item de lista do editor novo (um bloco por item, com nível e rótulo 1. / 1.1 / a. / a.1).
      if (block.listStyle) {
        return (
          <p className="public-list-item" data-label={listLabel || '•'} style={{ '--indent': block.indent || 0 }}>
            {content}
          </p>
        );
      }
      return (
        <ul style={{ paddingLeft: 22 }}>
          {block.text.split('\n').map((line, i) => (
            <li key={i}>{line}</li>
          ))}
        </ul>
      );
    default:
      return <p style={{ lineHeight: 1.8, margin: '0 0 14px', whiteSpace: 'pre-wrap' }}>{content}</p>;
  }
}

function PublicShell({ children }) {
  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg, #f7f8f4)' }}>
      <header className="row between" style={{ maxWidth: 760, margin: '0 auto', padding: '22px 20px' }}>
        <Link to="/library" className="logo" style={{ textDecoration: 'none' }}>
          <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
        </Link>
        <Link to="/library" className="small muted">
          Biblioteca
        </Link>
      </header>
      <main style={{ maxWidth: 760, margin: '0 auto', padding: '0 20px 60px' }}>{children}</main>
    </div>
  );
}

function useLoad(fn, deps) {
  const [state, setState] = useState({ loading: true, data: null, error: false });
  useEffect(() => {
    let alive = true;
    fn()
      .then(data => alive && setState({ loading: false, data, error: false }))
      .catch(() => alive && setState({ loading: false, data: null, error: true }));
    return () => {
      alive = false;
    };
  }, deps);
  return state;
}

function Article({ state, comments = [], onAnchor, onCommentClick, canComment = false }) {
  if (state.loading) return <p className="muted">A carregar...</p>;
  if (state.error || !state.data)
    return (
      <div className="empty">
        <h3>Publicação indisponível</h3>
        <p>Ela pode ter sido revogada, tornada privada ou o link está incorreto.</p>
      </div>
    );
  const note = state.data;
  const citations = note.citations || [];
  const publicListLabels = listLabels(blocksOf(note.content));
  return (
    <article className="card pad" style={{ padding: 34 }}>
      <h1 style={{ fontSize: 32, marginBottom: 8 }}>{note.title}</h1>
      <p className="small muted" style={{ marginBottom: 26 }}>
        {note.author ? (
          <>
            por <Link to={`/u/${note.author.handle}`}>{note.author.displayName}</Link> ·{' '}
          </>
        ) : null}
        publicado em {new Date(note.publishedAt).toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' })}
      </p>
      {blocksOf(note.content).map((b, i) => (
        <div key={b.id || i} data-block-id={b.id} onMouseUp={e => {
          if (!canComment || !b.id || !['paragraph', 'heading', 'quote', 'code'].includes(b.type)) return;
          const selection = window.getSelection();
          if (!selection || selection.isCollapsed || !selection.toString().trim() || selection.toString().length > 500) return;
          const range = selection.getRangeAt(0);
          if (!e.currentTarget.contains(range.startContainer) || !e.currentTarget.contains(range.endContainer)) return;
          const before = range.cloneRange(); before.selectNodeContents(e.currentTarget); before.setEnd(range.startContainer, range.startOffset);
          onAnchor({ blockId: b.id, exactQuote: selection.toString(), startIndex: before.toString().length });
        }}><Block block={b} listLabel={publicListLabels.get(b.id)} comments={comments} citations={citations} onCommentClick={onCommentClick} /></div>
      ))}
      {citations.length > 0 && <section className="public-bibliography"><h2>Referências</h2><ol>{citations.map(citation => <li key={citation.id}>
        <span>{formatBibliographyCitation(citation) || citation.title}</span> <span className="pill">{citation.verificationStatus === 'unverified' ? 'Não verificada' : citation.verificationStatus}</span>
        {citation.url && <> · <a href={citation.url} target="_blank" rel="noopener noreferrer nofollow">Abrir fonte</a></>}
        {citation.snapshotUrl && <> · <a href={citation.snapshotUrl} target="_blank" rel="noopener noreferrer nofollow">Snapshot do Internet Archive</a></>}
      </li>)}</ol><p className="tiny muted">Verificação automática não garante veracidade; confira as evidências.</p></section>}
    </article>
  );
}

export function PublicNotePage() {
  const { handle, slug } = useParams();
  const state = useLoad(() => publicApi.note(handle, slug), [handle, slug]);
  const [comments, setComments] = useState({ publicationId: null, items: [] });
  const [anchor, setAnchor] = useState(null);
  const [commentText, setCommentText] = useState('');
  const [commentVersion, setCommentVersion] = useState(0);
  const [commentBusy, setCommentBusy] = useState(false);
  const [canComment, setCanComment] = useState(false);
  const [showAnchorModal, setShowAnchorModal] = useState(false);
  useEffect(() => {
    if (!state.data?.commentsEnabled) return;
    let alive = true;
    apiFetch('/me/profile').then(profile => { if (alive) setCanComment(!!profile); }).catch(() => { if (alive) setCanComment(false); });
    return () => { alive = false; };
  }, [state.data]);
  useEffect(() => {
    if (!state.data?.id || !state.data.commentsEnabled) return;
    let alive = true;
    apiFetch(`/discussion/${state.data.id}?view=all`).then(rows => { if (alive) setComments({ publicationId: state.data.id, items: rows || [] }); }).catch(() => {});
    return () => { alive = false; };
  }, [state.data, commentVersion]);
  const submitAnchor = async e => {
    e.preventDefault(); setCommentBusy(true);
    try { await apiFetch(`/discussion/${state.data.id}`, { method: 'POST', body: JSON.stringify({ text: commentText, anchor }) }); setAnchor(null); setShowAnchorModal(false); setCommentText(''); setCommentVersion(v => v + 1); }
    catch (err) { alert(err.message || 'Não foi possível comentar.'); }
    finally { setCommentBusy(false); }
  };
  return (
    <PublicShell>
      <Article state={state} comments={comments.publicationId === state.data?.id ? comments.items : []} onAnchor={setAnchor} canComment={canComment && !!state.data?.commentsEnabled} onCommentClick={id => { window.location.hash = `comment-${id}`; document.getElementById(`comment-${id}`)?.scrollIntoView({ behavior: 'smooth' }); }} />
      {anchor && canComment && !showAnchorModal && <button type="button" className="btn primary" style={{ position: 'fixed', right: 24, bottom: 24, zIndex: 12 }} onClick={() => setShowAnchorModal(true)}>Comentar trecho</button>}
      {anchor && showAnchorModal && <div className="modal-overlay"><section className="modal" role="dialog" aria-modal="true" aria-label="Comentar trecho"><div className="modal-title"><h2>Comentar trecho</h2><button type="button" className="icon-btn" onClick={() => { setAnchor(null); setShowAnchorModal(false); }}>×</button></div><form onSubmit={submitAnchor}><blockquote className="quote-selection">{anchor.exactQuote}</blockquote><label htmlFor="public-comment-text">Seu comentário</label><textarea id="public-comment-text" value={commentText} onChange={e => setCommentText(e.target.value)} maxLength={2000} required /><div className="modal-actions mt24"><button type="button" className="btn" onClick={() => { setAnchor(null); setShowAnchorModal(false); }}>Cancelar</button><button className="btn primary" disabled={commentBusy || !commentText.trim()}>Comentar</button></div></form></section></div>}
      {state.data && !state.error && <ReadingActions key={handle + slug} handle={handle} slug={slug} blocks={blocksOf(state.data.content)} commentVersion={commentVersion} commentsEnabled={state.data.commentsEnabled} />}
    </PublicShell>
  );
}

export function SharedNotePage() {
  const { token } = useParams();
  return (
    <PublicShell>
      <Article state={useLoad(() => publicApi.shared(token), [token])} />
    </PublicShell>
  );
}

function NoteList({ notes, showAuthor }) {
  if (!notes.length) return <p className="muted">Nenhuma publicação ainda.</p>;
  return (
    <div className="stack">
      {notes.map(n => (
        <Link key={`${n.author.handle}/${n.slug}`} className="card pad" to={`/p/${n.author.handle}/${n.slug}`} style={{ display: 'block' }}>
          <h3>{n.title}</h3>
          <p className="tiny muted mt16">
            {showAuthor ? `${n.author.displayName} · ` : ''}
            {new Date(n.publishedAt).toLocaleDateString('pt-BR')}
          </p>
        </Link>
      ))}
    </div>
  );
}

export function PublicProfilePage() {
  const { handle } = useParams();
  const state = useLoad(() => publicApi.profile(handle), [handle]);
  return (
    <PublicShell>
      {state.loading ? (
        <p className="muted">A carregar...</p>
      ) : state.error ? (
        <div className="empty">
          <h3>Perfil não encontrado</h3>
        </div>
      ) : (
        <>
          <div className="overline">PERFIL</div>
          <h1 className="mb24">{state.data.author.displayName}</h1>
          <FollowButton handle={handle} />
          <NoteList notes={state.data.notes} />
        </>
      )}
    </PublicShell>
  );
}

export function LibraryPage() {
  const state = useLoad(() => publicApi.library(), []);
  return (
    <PublicShell>
      <div className="overline">BIBLIOTECA</div>
      <h1 className="mb24">Leituras publicadas</h1>
      {state.loading ? <p className="muted">A carregar...</p> : <NoteList notes={state.data || []} showAuthor />}
    </PublicShell>
  );
}
