// src/components/AppShell.jsx
import React, { useEffect, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Icon } from './Icon';
import { UserAvatar } from './UserAvatar';
import { apiFetch, logout } from '../api';
import { CurrentUserProvider, useCurrentUser } from '../contexts/CurrentUserContext';

export function AppShell() {
  return (
    <CurrentUserProvider>
      <AppShellLayout />
    </CurrentUserProvider>
  );
}

const PAGE_NAMES = {
  '/discover': 'Descobrir notas',
  '/communities': 'Comunidades',
  '/feed': 'Seu feed',
  '/home': 'Visão geral',
  '/notes': 'Minhas notas',
  '/sources': 'Fontes',
  '/boards': 'Mapas de ideias',
  '/research': 'Dúvidas e pesquisas',
  '/publications': 'Publicações',
  '/search': 'Buscar nas notas',
  '/settings': 'Configurações',
};

// Páginas internas mostram a seção de origem como link de volta (ex.: nota → "Minhas notas").
const PARENT_PAGES = [
  { prefix: '/note/', to: '/notes', name: 'Minhas notas' },
  { prefix: '/research/', to: '/research', name: 'Dúvidas e pesquisas' },
  { prefix: '/boards/', to: '/boards', name: 'Mapas de ideias' },
  { prefix: '/communities/', to: '/communities', name: 'Comunidades' },
];

// Fecha um popover ao clicar fora dele ou apertar Esc.
function useDismiss(open, setOpen, ref) {
  useEffect(() => {
    if (!open) return;
    const onPointer = e => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    const onKey = e => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open, setOpen, ref]);
}

function AppShellLayout() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [showNotifications, setShowNotifications] = useState(false);
  const [showUserMenu, setShowUserMenu] = useState(false);
  const notificationsRef = useRef(null);
  const userMenuRef = useRef(null);
  useDismiss(showNotifications, setShowNotifications, notificationsRef);
  useDismiss(showUserMenu, setShowUserMenu, userMenuRef);

  useEffect(() => {
    let alive = true;
    const load = () => apiFetch('/me/notifications').then(rows => { if (alive) setNotifications(rows || []); }).catch(() => {});
    load();
    const interval = setInterval(load, 60000);
    window.addEventListener('focus', load);
    return () => { alive = false; clearInterval(interval); window.removeEventListener('focus', load); };
  }, []);
  const unread = notifications.filter(n => !n.readAt).length;
  const openNotifications = async () => {
    setShowNotifications(open => !open);
    setShowUserMenu(false);
    if (unread > 0) {
      try { await apiFetch('/me/notifications/read', { method: 'POST', body: JSON.stringify({ ids: notifications.filter(n => !n.readAt).map(n => n.id) }) }); setNotifications(prev => prev.map(n => ({ ...n, readAt: n.readAt || new Date().toISOString() }))); }
      catch { /* mantém avisos não lidos */ }
    }
  };

  const { user, loading } = useCurrentUser();
  const displayName = user?.fullName || user?.email || (loading ? 'Carregando…' : 'Sua conta');
  const { pathname, search } = useLocation();
  const parent = PARENT_PAGES.find(p => pathname.startsWith(p.prefix));
  const isAccountPage = pathname === '/settings' && search.includes('tab=account');
  const pageName = isAccountPage ? 'Minha conta' : PAGE_NAMES[pathname] || parent?.name || '';
  const navigate = useNavigate();

  const handleLogout = async () => {
    setShowUserMenu(false);
    await logout(); // revoga o refresh token no servidor
    navigate('/login');
  };

  const navClass = ({ isActive }) => `nav-item ${isActive ? 'active' : ''}`;
  return (
    <div className={`app-shell ${isCollapsed ? 'sidebar-collapsed' : ''}`}>
      <aside className="sidebar">
        <div className="sidebar-brand">
          <Link className="logo" to="/home" aria-label="Notee, visão geral">
            <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
          </Link>
          <button className="icon-btn sidebar-collapse-button" onClick={() => setIsCollapsed(!isCollapsed)} aria-label={isCollapsed ? 'Expandir menu' : 'Recolher menu'}>
            <Icon name={isCollapsed ? 'panel-left-open' : 'panel-left-close'} />
          </button>
        </div>

        <nav>
          <div className="nav-label">Explorar</div>
          <NavLink to="/discover" className={navClass}><Icon name="search" /><span>Descobrir</span></NavLink>
          <NavLink to="/communities" className={navClass}><Icon name="user" /><span>Comunidades</span></NavLink>
          <NavLink to="/feed" className={navClass}><Icon name="book" /><span>Feed</span></NavLink>
          <div className="nav-divider" role="separator" />
          <NavLink to="/home" className={navClass}>
            <Icon name="home" />
            <span>Visão geral</span>
          </NavLink>
          <NavLink to="/notes" className={navClass}>
            <Icon name="note" />
            <span>Minhas notas</span>
          </NavLink>
          <NavLink to="/sources" className={navClass}>
            <Icon name="book" />
            <span>Fontes</span>
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
        </nav>

        {/* Configurações do sistema (IA e pesquisas). As da conta ficam no menu do usuário, no topo. */}
        <div className="sidebar-bottom">
          <NavLink to="/settings" end className={({ isActive }) => `nav-item ${isActive && !isAccountPage ? 'active' : ''}`} title="Configurações do sistema">
            <Icon name="settings" />
            <span>Configurações</span>
          </NavLink>
        </div>
      </aside>
      <div className="main-wrap">
        <header className="topbar">
          <div className="topbar-title">
            {parent ? (
              <Link to={parent.to} className="topbar-back" title={`Voltar para ${parent.name}`}>
                <Icon name="back" className="sm" /> {parent.name}
              </Link>
            ) : (
              <strong>{pageName}</strong>
            )}
          </div>
          <div className="topbar-right">
            <div className="topbar-popover-anchor" ref={notificationsRef}>
              <button className="icon-btn" aria-label={`Avisos: ${unread} não lidos`} aria-expanded={showNotifications} onClick={openNotifications}>
                <Icon name="bell" />
                {unread > 0 && <span className="badge-count">{unread}</span>}
              </button>
              {showNotifications && (
                <div className="topbar-popover topbar-notifications">
                  <strong>Avisos</strong>
                  {notifications.length ? notifications.map(n => (
                    <Link key={n.id} className="row gap8 mt16 small" to={`${n.path}#comment-${n.commentId}`} onClick={() => setShowNotifications(false)}>
                      {n.kind === 'reply' ? 'Nova resposta' : 'Novo comentário'} · {new Date(n.createdAt).toLocaleString('pt-BR')}
                    </Link>
                  )) : <p className="small muted mt16">Nenhum aviso.</p>}
                </div>
              )}
            </div>

            <div className="topbar-popover-anchor" ref={userMenuRef}>
              <button
                type="button"
                className="topbar-user"
                aria-haspopup="menu"
                aria-expanded={showUserMenu}
                onClick={() => { setShowUserMenu(open => !open); setShowNotifications(false); }}
              >
                <UserAvatar name={user?.fullName || user?.email} imageUrl={user?.avatarUrl} />
                <span className="topbar-user-name">{displayName}</span>
                <Icon name="down" className="sm" />
              </button>
              {showUserMenu && (
                <div className="topbar-popover topbar-user-menu" role="menu">
                  <div className="topbar-user-menu-head">
                    <UserAvatar name={user?.fullName || user?.email} imageUrl={user?.avatarUrl} />
                    <div>
                      <strong>{displayName}</strong>
                      {user?.fullName && user?.email && <span>{user.email}</span>}
                    </div>
                  </div>
                  <Link role="menuitem" to="/settings?tab=account" onClick={() => setShowUserMenu(false)}>
                    <Icon name="user" className="sm" /> Editar perfil
                  </Link>
                  <button role="menuitem" type="button" className="danger" onClick={handleLogout}>
                    <Icon name="logout" className="sm" /> Sair da conta
                  </button>
                </div>
              )}
            </div>
          </div>
        </header>

        <main className="page" id="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
