// src/components/AppShell.jsx
import React, { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Icon } from './Icon';

export function AppShell() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const navigate = useNavigate(); // <-- Inicialize o hook

  // Função para limpar o token e sair
  const handleLogout = () => {
    localStorage.removeItem('notee_token');
    navigate('/login');
  };

  const navClass = ({ isActive }) => `nav-item ${isActive ? 'active' : ''}`;
  return (
    <div className={`app-shell ${isCollapsed ? 'sidebar-collapsed' : ''}`}>
      <aside className="sidebar">
        <div className="sidebar-brand">
          <a className="logo" href="/" aria-label="Notee, início">
            <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
          </a>
          <button className="icon-btn sidebar-collapse-button" onClick={() => setIsCollapsed(!isCollapsed)}>
            <Icon name={isCollapsed ? 'panel-left-open' : 'panel-left-close'} />
          </button>
        </div>

        <div className="workspace row gap8">
          <span className="avatar">AC</span>
          <span className="grow">
            <strong className="small">Meu espaço</strong>
            <span className="tiny muted" style={{ display: 'block' }}>
              Pessoal
            </span>
          </span>
          <Icon name="down" className="sm" />
        </div>

        <nav>
          <NavLink to="/" className={navClass} end>
            <Icon name="home" />
            <span>Visão geral</span>
          </NavLink>
          <NavLink to="/notes" className={navClass}>
            <Icon name="note" />
            <span>Minhas notas</span>
          </NavLink>
          <NavLink to="/boards" className={navClass} title="Mapas de ideias">
            <Icon name="map" />
            <span>Mapas de ideias</span>
          </NavLink>
          <NavLink to="/research" className={navClass}>
            <Icon name="spark" />
            <span>Dúvidas e pesquisas</span>
          </NavLink>
          <NavLink to="/publications" className={navClass}>
            <Icon name="globe" />
            <span>Publicações</span>
          </NavLink>
          <NavLink to="/search" className={navClass}>
            <Icon name="search" />
            <span>Buscar nas notas</span>
          </NavLink>

          <div className="nav-label">Organizar</div>
          <NavLink to="/settings" className={navClass}>
            <Icon name="settings" />
            <span>Configurações</span>
          </NavLink>
        </nav>
        <div className="sidebar-bottom">
          <div className="user-switch row gap8">
            <span className="avatar">AC</span>
            <div className="grow">
              <strong className="small">Alex Costa</strong>
              <div className="tiny muted">Conta de desenvolvimento</div>
            </div>

            {/* Adicione o evento onClick no botão! */}
            <button className="icon-btn" onClick={handleLogout} aria-label="Sair da conta" title="Sair">
              <Icon name="logout" />
            </button>
          </div>
        </div>
      </aside>
      <div className="main-wrap">
        <header className="topbar">
          <div className="crumb">
            Meu espaço <span>/</span> <strong>Aplicação React</strong>
          </div>
          <div className="row">
            <button className="icon-btn">
              <Icon name="bell" />
            </button>
          </div>
        </header>

        <main className="page" id="main-content">
          {/* É aqui que as páginas vão aparecer! */}
          <Outlet />
        </main>
      </div>
    </div>
  );
}
