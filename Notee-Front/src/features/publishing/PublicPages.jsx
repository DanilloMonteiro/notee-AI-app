// Páginas públicas de leitura (sem login, sem sidebar): nota publicada, link secreto, perfil e biblioteca.
// O conteúdo é renderizado como texto pelo React (sem HTML do autor), evitando XSS.
import React, { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ReadingActions } from '../social/ReadingActions';
import { FollowButton } from '../social/Social';
import { publicApi } from './publishingApi';

function blocksOf(content) {
  try {
    const parsed = JSON.parse(content || '[]');
    if (Array.isArray(parsed)) return parsed.filter(b => b && typeof b.text === 'string');
  } catch {
    // texto puro
  }
  return content ? [{ id: 'p', type: 'paragraph', text: content }] : [];
}

function Block({ block }) {
  switch (block.type) {
    case 'heading':
      return <h2 style={{ fontSize: 22, margin: '26px 0 10px' }}>{block.text}</h2>;
    case 'quote':
      return <blockquote className="quote-selection">{block.text}</blockquote>;
    case 'code':
      return (
        <pre style={{ background: '#f4f6f1', padding: 14, borderRadius: 8, overflow: 'auto' }}>
          <code>{block.text}</code>
        </pre>
      );
    case 'list':
      return (
        <ul style={{ paddingLeft: 22 }}>
          {block.text.split('\n').map((line, i) => (
            <li key={i}>{line}</li>
          ))}
        </ul>
      );
    default:
      return <p style={{ lineHeight: 1.8, margin: '0 0 14px', whiteSpace: 'pre-wrap' }}>{block.text}</p>;
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

function Article({ state }) {
  if (state.loading) return <p className="muted">A carregar...</p>;
  if (state.error || !state.data)
    return (
      <div className="empty">
        <h3>Publicação indisponível</h3>
        <p>Ela pode ter sido revogada, tornada privada ou o link está incorreto.</p>
      </div>
    );
  const note = state.data;
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
        <Block key={b.id || i} block={b} />
      ))}
    </article>
  );
}

export function PublicNotePage() {
  const { handle, slug } = useParams();
  const state = useLoad(() => publicApi.note(handle, slug), [handle, slug]);
  return (
    <PublicShell>
      <Article state={state} />
      {state.data && !state.error && <ReadingActions key={handle + slug} handle={handle} slug={slug} />}
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
