// src/pages/Home.jsx
import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';
import { NoteCard } from '../components/NoteCard';

export function Home() {
  const [recentNotes, setRecentNotes] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadDashboard();
  }, []);

  const loadDashboard = async () => {
    try {
      // Trazemos apenas as notas mais recentes para o ecrã inicial
      const data = await apiFetch('/notes?page=1&pageSize=4');
      setRecentNotes(data || []);
    } catch (err) {
      console.error('Erro ao carregar o dashboard:', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">BEM-VINDO AO NOTEE</div>
          <h1>Um lugar para continuar pensando.</h1>
          <p>As suas notas e perguntas estão por aqui.</p>
        </div>
        <div className="row gap8">
          <Link to="/notes" className="btn primary">
            <Icon name="plus" /> Nova nota
          </Link>
        </div>
      </div>

      <div className="hero-card">
        <div>
          <span className="pill green mb8">
            <Icon name="spark" className="sm" /> Curiosidade, organizada
          </span>
          <h2>Anote agora. Entenda depois.</h2>
          <p>
            Marque uma palavra ou frase como dúvida. A resposta ganha o seu próprio espaço, sem reescrever o que você
            pensou.
          </p>

          <Link to="/notes" className="btn compact mt16">
            Explorar as minhas notas <Icon name="arrow" className="sm" />
          </Link>
        </div>
        <div className="hero-ornament">
          <div className="mini-paper back"></div>
          <div className="mini-paper"></div>
          <span className="sparkle-round">
            <Icon name="spark" />
          </span>
        </div>
      </div>

      <div className="grid four">
        <div className="card stat">
          <div className="stat-top">
            Notas ativas <Icon name="note" />
          </div>
          {/* No futuro, podemos criar um endpoint para trazer estas contagens exatas do C# */}
          <div className="stat-number">{loading ? '-' : recentNotes.length}</div>
          <div className="stat-foot">Ideias que está a desenvolver</div>
        </div>
        <div className="card stat">
          <div className="stat-top">
            Para pesquisar <Icon name="question" />
          </div>
          <div className="stat-number">-</div>
          <div className="stat-foot">Perguntas à espera</div>
        </div>
        <div className="card stat">
          <div className="stat-top">
            Respostas disponíveis <Icon name="check" />
          </div>
          <div className="stat-number">-</div>
          <div className="stat-foot">Exemplos para revisar</div>
        </div>
        <div className="card stat">
          <div className="stat-top">
            Publicações <Icon name="globe" />
          </div>
          <div className="stat-number">0</div>
          <div className="stat-foot">O que decidiu partilhar</div>
        </div>
      </div>

      <div className="split mt32">
        <section>
          <div className="section-heading" style={{ marginTop: 0 }}>
            <h2>As suas notas recentes</h2>
            <Link className="btn ghost compact" to="/notes">
              Ver todas <Icon name="arrow" className="sm" />
            </Link>
          </div>

          {loading ? (
            <div className="empty">A carregar...</div>
          ) : recentNotes.length > 0 ? (
            <div className="grid two">
              {recentNotes.map(note => (
                <NoteCard key={note.id} note={note} />
              ))}
            </div>
          ) : (
            <div className="empty">
              <Icon name="note" />
              <h3>Nenhuma nota ainda</h3>
              <p>Comece a escrever a sua primeira ideia.</p>
            </div>
          )}
        </section>

        <aside>
          <div className="section-heading" style={{ marginTop: 0 }}>
            <h2>Vale a pena voltar aqui</h2>
            <Icon name="spark" />
          </div>

          <div className="callout neutral mt16">
            <Icon name="info" />
            <span>Mais tarde, ligaremos aqui as dúvidas que a IA acabou de responder para você rever rapidamente.</span>
          </div>
        </aside>
      </div>
    </div>
  );
}
