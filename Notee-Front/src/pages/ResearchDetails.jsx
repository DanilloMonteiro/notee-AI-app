// src/pages/ResearchDetail.jsx
import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Icon } from '../components/Icon';
import { apiFetch } from '../api';

export function ResearchDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [doubt, setDoubt] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadDoubt = async () => {
      try {
        // Usa o endpoint que já criámos no ResearchEndpoints.cs
        const data = await apiFetch(`/doubts/${id}`);
        setDoubt(data);
      } catch (err) {
        console.error('Erro ao carregar a dúvida:', err);
      } finally {
        setLoading(false);
      }
    };
    loadDoubt();
  }, [id]);

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

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">DÚVIDAS / DETALHE</div>
          <h1>Da pergunta à informação</h1>
          <p>O que foi pesquisado e o resultado processado pela IA.</p>
        </div>
        <div className="row gap8">
          {/* Volta para a página anterior (o Editor da Nota) */}
          <button className="btn" onClick={() => navigate(-1)}>
            <Icon name="back" /> Voltar à nota
          </button>
        </div>
      </div>

      <div className="split">
        <section className="card pad">
          <span className={`pill ${doubt.status === 'Completed' ? 'green' : 'blue'}`}>
            {doubt.status === 'Completed' ? 'Pesquisa Concluída' : 'A processar...'}
          </span>

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
                <span>Resposta gerada pela IA (Google Gemini).</span>
              </div>

              {/* O split separa as quebras de linha da IA em parágrafos reais no HTML */}
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

              <div className="callout warning mt16">
                <Icon name="info" />
                <span>
                  <strong>Aviso:</strong> A IA pode inventar factos (alucinação). Verifique sempre as informações
                  importantes.
                </span>
              </div>
            </>
          ) : (
            <p className="muted mt16">
              A aguardar a resposta da Inteligência Artificial. O Worker em C# está a trabalhar...
            </p>
          )}
        </section>

        <aside className="stack">
          <div className="card pad">
            <h3>Contexto preservado</h3>
            <p className="small muted mt16">
              A resposta é um registo separado. Editar a nota original não reescreve a pesquisa já concluída.
            </p>
          </div>
        </aside>
      </div>
    </div>
  );
}
