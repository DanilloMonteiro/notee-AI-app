// src/App.jsx
import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { PrivateRoute } from './components/PrivateRoute';
import { Login } from './pages/Login';
import { Landing } from './pages/Landing';
import { Register } from './pages/Register';
import { Notes } from './pages/Notes';
import { Note } from './pages/Note';
import { CommunitiesPage, CommunityPage } from './features/social/Communities';
import { FeedPage } from './features/social/Social';
import { Home } from './pages/Home';
import { ResearchDetail } from './pages/ResearchDetails';
import { Research } from './pages/Research';
import { Settings } from './pages/Settings';
import { Search } from './pages/Search';
import { ToastProvider } from './contexts/ToastContext';
import { BoardsPage } from './features/boards/BoardsPage';
import { BoardPage } from './features/boards/BoardPage';
import { MyPublicationsPage } from './features/publishing/MyPublicationsPage';
import { AdminAiPage, AdminLoginPage } from './features/admin/AdminPages';
import { ADMIN_PATH } from './features/admin/adminApi';
import { LibraryPage, PublicNotePage, PublicProfilePage, SharedNotePage } from './features/publishing/PublicPages';

function App() {
  return (
    <ToastProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Landing />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          {/* Leitura pública: sem login */}
          <Route path="/library" element={<LibraryPage />} />
          <Route path="/p/:handle/:slug" element={<PublicNotePage />} />
          <Route path="/s/:token" element={<SharedNotePage />} />
          <Route path="/u/:handle" element={<PublicProfilePage />} />
          {/* Administração: rota não divulgada (VITE_ADMIN_PATH); acesso exige conta admin */}
          <Route path={ADMIN_PATH} element={<AdminLoginPage />} />
          <Route path={`${ADMIN_PATH}/ia`} element={<AdminAiPage />} />

          <Route element={<PrivateRoute />}>
            <Route element={<AppShell />}>
              <Route path="/communities" element={<CommunitiesPage />} />
              <Route path="/communities/:id" element={<CommunityPage />} />
              <Route path="/feed" element={<FeedPage />} />
              <Route path="/home" element={<Home />} />
              <Route path="/notes" element={<Notes />} />
              <Route path="/note/:id" element={<Note />} />
              <Route path="/research/:id" element={<ResearchDetail />} />
              <Route path="/research" element={<Research />} />
              <Route path="/settings" element={<Settings />} />
              <Route path="/search" element={<Search />} />
              <Route path="/boards" element={<BoardsPage />} />
              <Route path="/boards/:boardId" element={<BoardPage />} />
              <Route path="/publications" element={<MyPublicationsPage />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </ToastProvider>
  );
}

export default App;
