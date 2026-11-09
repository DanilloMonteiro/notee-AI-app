// src/pages/Research.jsx
import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';

export function Research() {
  const [doubts, setDoubts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    loadAllDoubts();
  }, []);

  const loadAllDoubts = async () => {
    try {
      // Chama o nosso novo endpoint global de dúvidas
      const response = await apiFetch('/doubts');
      setDoubts(response || []);
    } catch (err) {
      setError('Não foi possível carregar o histórico de pesquisas.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">DÚVIDAS E PESQUISAS</div>
          <h1>Toda a pergunta merece um lugar.</h1>
          <p>Encontre as suas dúvidas, acompanhe pesquisas e revise as respostas.</p>
        </div>
      </div>

      <div className="callout neutral mb24">
        <Icon name="shield" />
        <span>As dúvidas são pesquisadas em segundo plano. Sem autorização em Configurações, as respostas são simuladas e nada sai do Notee.</span>
      </div>

      {error && (
        <div className="callout warning mb24">
          <Icon name="info" />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="empty">
          <h3>A carregar pesquisas...</h3>
        </div>
      ) : doubts.length > 0 ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>DÚVIDA</th>
                <th>ESTADO</th>
                <th>AÇÃO</th>
              </tr>
            </thead>
            <tbody>
              {doubts.map(doubt => (
                <tr key={doubt.id}>
                  <td style={{ maxWidth: '400px' }}>
                    <strong>{doubt.question}</strong>
                  </td>
                  <td>
                    <span className={`pill ${doubt.status === 'Completed' ? 'green' : 'blue'}`}>
                      {doubt.status === 'Completed' ? 'Respondida' : 'A processar'}
                    </span>
                  </td>
                  <td>
                    <Link className="btn compact" to={`/research/${doubt.id}`}>
                      Abrir detalhe <Icon name="arrow" className="sm" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="empty">
          <Icon name="question" />
          <h3>Nenhuma dúvida registada</h3>
          <p>Abra uma nota, selecione um texto e marque-o como dúvida para começar.</p>
          <Link to="/notes" className="btn mt16">
            Abrir as minhas notas
          </Link>
        </div>
      )}
    </div>
  );
}
