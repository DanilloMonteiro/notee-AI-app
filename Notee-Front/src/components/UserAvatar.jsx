// src/components/UserAvatar.jsx
// Avatar da conta: mostra a foto quando houver `imageUrl` (upload ainda não implementado na API)
// e, sem foto ou se ela falhar, as iniciais do nome.
import React, { useState } from 'react';
import { Icon } from './Icon';

function initialsOf(name) {
  const parts = (name || '').trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '';
  const first = parts[0][0];
  const last = parts.length > 1 ? parts[parts.length - 1][0] : '';
  return (first + last).toUpperCase();
}

export function UserAvatar({ name, imageUrl, className = '' }) {
  const [failedUrl, setFailedUrl] = useState(null);
  const showImage = imageUrl && failedUrl !== imageUrl;
  const initials = initialsOf(name);

  return (
    <span className={`avatar ${className}`.trim()} aria-hidden="true">
      {showImage ? (
        <img src={imageUrl} alt="" onError={() => setFailedUrl(imageUrl)} />
      ) : (
        initials || <Icon name="user" className="sm" />
      )}
    </span>
  );
}
