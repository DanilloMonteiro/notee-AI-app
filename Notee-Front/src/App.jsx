// src/App.jsx
import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { PrivateRoute } from './components/PrivateRoute';
import { Login } from './pages/Login';
import { Notes } from './pages/Notes';
import { Note } from './pages/Note';
import { Home } from './pages/Home';
import { ResearchDetail } from './pages/ResearchDetails';
import { Research } from './pages/Research';
import { Settings } from './pages/Settings';
import { ToastProvider } from './context/ToastContext';

function App() {
  return (
    <ToastProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />

          <Route element={<PrivateRoute />}>
            <Route element={<AppShell />}>
              <Route path="/" element={<Home />} />
              <Route path="/notes" element={<Notes />} />
              <Route path="/note/:id" element={<Note />} />
              <Route path="/research/:id" element={<ResearchDetail />} />
              <Route path="/research" element={<Research />} />
              <Route path="/settings" element={<Settings />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </ToastProvider>
  );
}

export default App;
