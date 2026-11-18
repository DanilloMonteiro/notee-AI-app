// Menu do "+" ao lado de cada bloco: escolhe o tipo do novo bloco, inserido logo abaixo.
import React, { useEffect, useRef } from 'react';
import { Icon } from '../../components/Icon';
import { BLOCK_TYPES } from './blockFormatting';

const ICONS = {
  paragraph: 'note',
  'heading-1': 'grid',
  'heading-2': 'grid',
  'heading-3': 'grid',
  'list-bullet': 'more',
  'list-number': 'more',
  'list-letter': 'more',
  quote: 'message',
  code: 'link',
  chart: 'map',
};

export function BlockTypeMenu({ onPick, onClose, allowChart = true }) {
  const ref = useRef(null);

  useEffect(() => {
    const onPointer = e => { if (ref.current && !ref.current.contains(e.target)) onClose(); };
    const onKey = e => { if (e.key === 'Escape') { e.stopPropagation(); onClose(); } };
    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey, true);
    ref.current?.querySelector('button')?.focus();
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey, true);
    };
  }, [onClose]);

  const items = allowChart ? [...BLOCK_TYPES, { key: 'chart', label: 'Gráfico' }] : BLOCK_TYPES;
  return (
    <div ref={ref} className="ne-block-menu" role="menu" aria-label="Inserir bloco abaixo">
      <div className="ne-block-menu-title">Inserir abaixo</div>
      {items.map(item => (
        <button key={item.key} type="button" role="menuitem" onMouseDown={e => e.preventDefault()} onClick={() => onPick(item.key)}>
          <Icon name={ICONS[item.key] || 'note'} className="sm" />
          {item.label}
        </button>
      ))}
    </div>
  );
}
