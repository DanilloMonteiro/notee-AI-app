// src/contexts/CurrentUserContext.jsx
// Conta logada (GET /me), buscada uma vez e compartilhada pela sidebar e pelas páginas.
// Campos: id, email, fullName, phoneNumber e, futuramente, avatarUrl (ainda não devolvido pela API).
import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { apiFetch } from '../api';

const CurrentUserContext = createContext({ user: null, loading: true, error: null, reload: () => Promise.resolve() });

export function CurrentUserProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const reload = useCallback(
    () =>
      apiFetch('/me')
        .then(me => {
          setUser(me);
          setError(null);
        })
        .catch(setError)
        .finally(() => setLoading(false)),
    [],
  );

  useEffect(() => {
    let alive = true;
    apiFetch('/me')
      .then(me => alive && setUser(me))
      .catch(err => alive && setError(err))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, []);

  return <CurrentUserContext.Provider value={{ user, loading, error, reload }}>{children}</CurrentUserContext.Provider>;
}

export function useCurrentUser() {
  return useContext(CurrentUserContext);
}
