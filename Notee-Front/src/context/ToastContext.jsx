// src/contexts/ToastContext.jsx
import React, { createContext, useContext, useState } from 'react';
import { Icon } from '../components/Icon';

// 1. Criamos o Contexto
const ToastContext = createContext();

// 2. Criamos o Provider (que vai embrulhar a nossa aplicação)
export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  // Função para adicionar uma nova notificação
  const addToast = (message, type = 'check') => {
    const id = Date.now();
    setToasts(prev => [...prev, { id, message, type }]);

    // Remove a notificação automaticamente após 4.5 segundos (igual ao seu HTML)
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 4500);
  };

  return (
    <ToastContext.Provider value={{ addToast }}>
      {children}

      {/* O container de toasts que flutua no canto inferior direito */}
      <div className="toast-container" id="toasts" role="status" aria-live="polite">
        {toasts.map(t => (
          <div key={t.id} className="toast" style={{ animation: 'slide .2s ease-out' }}>
            <Icon name={t.type} />
            <span>{t.message}</span>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

// 3. Hook customizado para usarmos facilmente em qualquer página
export const useToast = () => useContext(ToastContext);
