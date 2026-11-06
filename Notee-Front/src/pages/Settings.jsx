// src/pages/Settings.jsx
import React, { useState, useEffect } from 'react';
import { Icon } from '../components/Icon';

export function Settings() {
  const [activeTab, setActiveTab] = useState('research');

  // Preferências locais
  const [autoResearch, setAutoResearch] = useState(false);
  const [researchEnabled, setResearchEnabled] = useState(true);

  // Carrega as preferências guardadas ao abrir a página
  useEffect(() => {
    const savedAuto = localStorage.getItem('notee_pref_auto') === 'true';
    const savedEnabled = localStorage.getItem('notee_pref_enabled') !== 'false';
    setAutoResearch(savedAuto);
    setResearchEnabled(savedEnabled);
  }, []);

  // Guarda as preferências sempre que os switches mudam
  const toggleAuto = () => {
    setAutoResearch(!autoResearch);
    localStorage.setItem('notee_pref_auto', !autoResearch);
  };

  const toggleEnabled = () => {
    setResearchEnabled(!researchEnabled);
    localStorage.setItem('notee_pref_enabled', !researchEnabled);
  };

  return (
    <div>
      <div className="page-heading">
        <div>
          <div className="overline">CONFIGURAÇÕES</div>
          <h1>Um assistente do seu jeito.</h1>
          <p>Configure a organização e a pesquisa sem perder o controle sobre suas notas.</p>
        </div>
      </div>

      <div className="tabbar">
        <button className={`tab ${activeTab === 'research' ? 'active' : ''}`} onClick={() => setActiveTab('research')}>
          Inteligência e pesquisas
        </button>
        <button className={`tab ${activeTab === 'account' ? 'active' : ''}`} onClick={() => setActiveTab('account')}>
          Conta
        </button>
      </div>

      <div style={{ maxWidth: '900px' }}>
        {activeTab === 'research' && (
          <div className="card settings-section">
            <h2 className="mb24">O seu ritmo. As suas escolhas.</h2>

            <div className="settings-row">
              <div>
                <h4>Permitir pesquisas</h4>
                <p>Chave geral. Desligada, bloqueia novas pesquisas em qualquer nota.</p>
              </div>
              <button
                className={`switch ${researchEnabled ? 'on' : ''}`}
                role="switch"
                onClick={toggleEnabled}
              ></button>
            </div>

            <div className="settings-row">
              <div>
                <h4>Pesquisa automática padrão</h4>
                <p>O Worker do Gemini pesquisa a dúvida assim que você a marca no editor.</p>
              </div>
              <button className={`switch ${autoResearch ? 'on' : ''}`} role="switch" onClick={toggleAuto}></button>
            </div>

            <div className="settings-row">
              <div>
                <h4>Modelo de Inteligência Artificial</h4>
                <p>A API C# processa as dúvidas usando o Google Gemini 1.5 Flash.</p>
              </div>
              <span className="pill green">Gemini API</span>
            </div>
          </div>
        )}

        {activeTab === 'account' && (
          <div className="card settings-section">
            <h2 className="mb24">O seu perfil local</h2>
            <div className="field">
              <label>Nome</label>
              <input type="text" value="Alex Costa" readOnly />
            </div>
            <div className="field">
              <label>E-mail de desenvolvimento</label>
              <input type="email" value="alex@example.test" readOnly />
            </div>
            <p className="tiny muted mt16">
              Para atualizar estes dados, deverá implementar o endpoint de atualização no Identity do .NET.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
