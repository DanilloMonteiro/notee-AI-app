// Configurações > Fontes (ADR-047): de onde vêm as fontes das dúvidas e das notas criadas com IA,
// e quais sites a pessoa bloqueia ou em quais confia.
import React, { useEffect, useState } from 'react';
import { Icon } from '../../components/Icon';
import { useToast } from '../../contexts/ToastContext';
import { sourceRulesApi } from './sourceRulesApi';
import { SOURCE_MODES } from './sourceMix';

function RuleList({ title, hint, kind, rules, busy, onAdd, onRemove }) {
  const [value, setValue] = useState('');
  const items = rules.filter(r => r.kind === kind);
  const submit = async e => {
    e.preventDefault();
    if (!value.trim()) return;
    if (await onAdd(value.trim(), kind)) setValue('');
  };
  return (
    <div className="source-rules">
      <h4>{title}</h4>
      <p className="small muted">{hint}</p>
      <ul className="source-rules-list">
        {items.length === 0 && <li className="tiny muted">Nenhum site.</li>}
        {items.map(r => (
          <li key={r.domain}>
            <span>{r.domain}</span>
            {r.fixed ? (
              <span className="tiny muted" title="Bloqueio do Notee: enciclopédias que qualquer pessoa edita não entram como fonte.">
                fixo
              </span>
            ) : (
              <button type="button" className="icon-btn source-rules-remove" disabled={busy} aria-label={`Remover ${r.domain}`} title="Remover" onClick={() => onRemove(r.domain)}>
                <Icon name="close" className="sm" />
              </button>
            )}
          </li>
        ))}
      </ul>
      <form className="source-rules-add" onSubmit={submit}>
        <input value={value} onChange={e => setValue(e.target.value)} placeholder="ex.: scielo.br" maxLength={253} aria-label={`Adicionar a ${title}`} />
        <button type="submit" className="btn compact" disabled={busy || !value.trim()}>
          Adicionar
        </button>
      </form>
    </div>
  );
}

export function SourceSettings({ prefs, saving, onChangeMode }) {
  const [rules, setRules] = useState([]);
  const [busy, setBusy] = useState(false);
  const { addToast } = useToast();

  useEffect(() => {
    let alive = true;
    sourceRulesApi
      .list()
      .then(rows => alive && setRules(rows || []))
      .catch(err => alive && addToast(err.message || 'Não foi possível carregar os sites.', 'warning'));
    return () => {
      alive = false;
    };
  }, [addToast]);

  const run = async action => {
    setBusy(true);
    try {
      setRules((await action()) || []);
      return true;
    } catch (err) {
      addToast(err.message || 'Não foi possível salvar o site.', 'warning');
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card settings-section mt24">
      <h2 className="mb16">Fontes</h2>
      <p className="small muted mb16">
        Vale para as respostas das dúvidas e para as notas criadas com IA (cada nota pode escolher outra opção). Artigos vêm da base
        acadêmica aberta OpenAlex; a internet, da busca do DeepSeek (só quando ele é a IA ativa).
      </p>
      <div className="source-mode-options" role="radiogroup" aria-label="Origem das fontes">
        {SOURCE_MODES.map(mode => (
          <label key={mode.value} className={`source-mode-option ${prefs.sourceMode === mode.value ? 'is-selected' : ''}`}>
            <input
              type="radio"
              name="source-mode"
              value={mode.value}
              checked={prefs.sourceMode === mode.value}
              disabled={saving}
              onChange={() => onChangeMode(mode.value)}
            />
            <span>
              <strong>{mode.label}</strong>
              <span className="small muted">{mode.hint}</span>
            </span>
          </label>
        ))}
      </div>

      <div className="source-rules-grid">
        <RuleList
          title="Sites bloqueados"
          hint="Nunca usados como fonte (vale também para os subdomínios)."
          kind="blocked"
          rules={rules}
          busy={busy}
          onAdd={(domain, kind) => run(() => sourceRulesApi.save(domain, kind))}
          onRemove={domain => run(() => sourceRulesApi.remove(domain))}
        />
        <RuleList
          title="Sites confiáveis"
          hint="Entram primeiro e são aceitos mesmo com relevância um pouco menor; aparecem com o selo “confiável”."
          kind="trusted"
          rules={rules}
          busy={busy}
          onAdd={(domain, kind) => run(() => sourceRulesApi.save(domain, kind))}
          onRemove={domain => run(() => sourceRulesApi.remove(domain))}
        />
      </div>
    </div>
  );
}
