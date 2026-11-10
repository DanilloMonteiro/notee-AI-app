// src/components/AppShell.jsx
import React, { useState } from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Icon } from './Icon';
import { UserAvatar } from './UserAvatar';
import { logout } from '../api';
import { CurrentUserProvider, useCurrentUser } from '../contexts/CurrentUserContext';

export function AppShell() {
  return (
    <CurrentUserProvider>
      <AppShellLayout />
    </CurrentUserProvider>
  );
}

function AppShellLayout() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const { user, loading } = useCurrentUser();
  const displayName = user?.fullName || user?.email || (loading ? 'Carregando…' : 'Sua conta');
  const navigate = useNavigate(); // <-- Inicialize o hook

  // Função para limpar o token e sair
  const handleLogout = async () => {
    await logout(); // revoga o refresh token no servidor
    navigate('/login');
  };

  const navClass = ({ isActive }) => `nav-item ${isActive ? 'active' : ''}`;
  return (
    <div className={`app-shell ${isCollapsed ? 'sidebar-collapsed' : ''}`}>
      <aside className="sidebar">
        <div className="sidebar-brand">
          <Link className="logo" to="/home" aria-label="Notee, meu espaço">
            <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
          </Link>
          <button className="icon-btn sidebar-collapse-button" onClick={() => setIsCollapsed(!isCollapsed)}>
            <Icon name={isCollapsed ? 'panel-left-open' : 'panel-left-close'} />
          </button>
        </div>

        <div className="workspace row gap8">
          <UserAvatar name={user?.fullName || user?.email} imageUrl={user?.avatarUrl} />
          <span className="grow">
            <strong className="small">Meu espaço</strong>
            <span className="tiny muted" style={{ display: 'block' }}>
              Pessoal
            </span>
          </span>
          <Icon name="down" className="sm" />
        </div>

        <nav>
          <NavLink to="/feed" className={navClass}><Icon name="globe" /><span>Feed</span></NavLink>
          <NavLink to="/home" className={navClass}>
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
            <Link
              to="/settings?tab=account"
              className="user-card row gap8"
              aria-label={`${displayName}: abrir configurações da conta`}
              title="Configurações da conta"
            >
              <UserAvatar name={user?.fullName || user?.email} imageUrl={user?.avatarUrl} />
              <div className="grow">
                <strong className="small">{displayName}</strong>
                {user?.fullName && user?.email && <div className="tiny muted">{user.email}</div>}
              </div>
            </Link>

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
