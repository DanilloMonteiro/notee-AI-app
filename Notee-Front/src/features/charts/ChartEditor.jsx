// Editor de um bloco de gráfico: metadados, tabela de dados (rótulos × séries) e fontes declaradas,
// com pré-visualização gerada da mesma estrutura que será salva na nota.
import React, { useMemo, useState } from 'react';
import { Icon } from '../../components/Icon';
import { ChartView } from './ChartView';
import { CHART_LIMITS, CHART_TYPES, cleanChart, parseNumberInput, validateChart } from './chartModel';

const toDraft = chart => ({
  ...chart,
  labels: [...(chart.labels || [])],
  // Sem separador de milhar na edição: "1.234" seria ambíguo ao reler.
  series: (chart.series || []).map(s => ({
    name: s.name,
    cells: (s.values || []).map(v => (v === null || v === undefined ? '' : String(v).replace('.', ','))),
  })),
  sources: (chart.sources || []).map(s => ({ label: s.label || '', url: s.url || '' })),
});

/** Converte o rascunho (células como texto) no bloco salvo; marca células inválidas. */
function fromDraft(draft) {
  const invalidCells = [];
  const series = draft.series.map((s, si) => ({
    name: s.name,
    values: s.cells.map((cell, li) => {
      const parsed = parseNumberInput(cell);
      if (!parsed.valid) invalidCells.push(`${si}:${li}`);
      return parsed.value;
    }),
  }));
  return { chart: cleanChart({ ...draft, series }), invalidCells };
}

export function ChartEditor({ chart, onDone, onCancel }) {
  const [draft, setDraft] = useState(() => toDraft(chart));
  const [showErrors, setShowErrors] = useState(false);
  const { chart: preview, invalidCells } = useMemo(() => fromDraft(draft), [draft]);
  const errors = useMemo(() => {
    const list = validateChart(preview);
    if (invalidCells.length) list.unshift('Há valores que não são números. Use ponto ou vírgula decimal; deixe vazio para "sem dado".');
    return list;
  }, [preview, invalidCells]);

  const set = (field, value) => setDraft(d => ({ ...d, [field]: value }));
  const setLabel = (li, value) => setDraft(d => ({ ...d, labels: d.labels.map((l, i) => (i === li ? value : l)) }));
  const setCell = (si, li, value) =>
    setDraft(d => ({
      ...d,
      series: d.series.map((s, i) => (i === si ? { ...s, cells: s.cells.map((c, j) => (j === li ? value : c)) } : s)),
    }));
  const setSeriesName = (si, value) =>
    setDraft(d => ({ ...d, series: d.series.map((s, i) => (i === si ? { ...s, name: value } : s)) }));

  const addRow = () =>
    setDraft(d => ({ ...d, labels: [...d.labels, ''], series: d.series.map(s => ({ ...s, cells: [...s.cells, ''] })) }));
  const removeRow = li =>
    setDraft(d => ({
      ...d,
      labels: d.labels.filter((_, i) => i !== li),
      series: d.series.map(s => ({ ...s, cells: s.cells.filter((_, i) => i !== li) })),
    }));
  const addSeries = () =>
    setDraft(d => ({ ...d, series: [...d.series, { name: `Série ${d.series.length + 1}`, cells: d.labels.map(() => '') }] }));
  const removeSeries = si => setDraft(d => ({ ...d, series: d.series.filter((_, i) => i !== si) }));

  const setSource = (i, field, value) =>
    setDraft(d => ({ ...d, sources: d.sources.map((s, j) => (j === i ? { ...s, [field]: value } : s)) }));
  const addSource = () => setDraft(d => ({ ...d, sources: [...d.sources, { label: '', url: '' }] }));
  const removeSource = i => setDraft(d => ({ ...d, sources: d.sources.filter((_, j) => j !== i) }));

  const handleDone = () => {
    if (errors.length) {
      setShowErrors(true);
      return;
    }
    onDone(preview);
  };

  const L = CHART_LIMITS;

  return (
    <section className="chart-editor" aria-label="Editar gráfico">
      <div className="row between mb16">
        <h4 style={{ margin: 0 }}>
          <Icon name="grid" className="sm" /> Dados do gráfico
        </h4>
        <span className="small muted">Valores vazios ficam como “sem dado” (não viram zero).</span>
      </div>

      <div className="chart-editor-grid">
        <div className="field">
          <label htmlFor={`${chart.id}-type`}>Tipo</label>
          <select id={`${chart.id}-type`} value={draft.chartType} onChange={e => set('chartType', e.target.value)}>
            {CHART_TYPES.map(t => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field chart-editor-wide">
          <label htmlFor={`${chart.id}-title`}>Título</label>
          <input
            id={`${chart.id}-title`}
            type="text"
            maxLength={L.title}
            value={draft.title}
            onChange={e => set('title', e.target.value)}
            placeholder="Ex.: Inflação anual (IPCA)"
          />
        </div>
        <div className="field">
          <label htmlFor={`${chart.id}-x`}>Eixo X (categorias/períodos)</label>
          <input id={`${chart.id}-x`} type="text" maxLength={L.short} value={draft.xAxisLabel} onChange={e => set('xAxisLabel', e.target.value)} placeholder="Ex.: Ano" />
        </div>
        <div className="field">
          <label htmlFor={`${chart.id}-y`}>Eixo Y</label>
          <input id={`${chart.id}-y`} type="text" maxLength={L.short} value={draft.yAxisLabel} onChange={e => set('yAxisLabel', e.target.value)} placeholder="Ex.: Variação" />
        </div>
        <div className="field">
          <label htmlFor={`${chart.id}-unit`}>Unidade</label>
          <input id={`${chart.id}-unit`} type="text" maxLength={L.short} value={draft.unit} onChange={e => set('unit', e.target.value)} placeholder="Ex.: %" />
        </div>
        <div className="field chart-editor-full">
          <label htmlFor={`${chart.id}-desc`}>Descrição (o que o gráfico mostra)</label>
          <textarea
            id={`${chart.id}-desc`}
            maxLength={L.description}
            value={draft.description}
            onChange={e => set('description', e.target.value)}
            placeholder="Descreva os dados e o período. Evite afirmar causas que os dados não mostram."
            style={{ minHeight: 60 }}
          />
        </div>
      </div>

      <div className="chart-table-wrap" role="region" aria-label="Tabela editável de dados" tabIndex={0}>
        <table className="chart-table chart-table-edit">
          <thead>
            <tr>
              <th scope="col">Rótulo</th>
              {draft.series.map((s, si) => (
                <th key={si} scope="col">
                  <div className="row gap8">
                    <input
                      type="text"
                      aria-label={`Nome da série ${si + 1}`}
                      maxLength={L.short}
                      value={s.name}
                      onChange={e => setSeriesName(si, e.target.value)}
                    />
                    <button
                      type="button"
                      className="icon-btn"
                      title="Remover série"
                      aria-label={`Remover série ${s.name || si + 1}`}
                      onClick={() => removeSeries(si)}
                      disabled={draft.series.length <= 1}
                    >
                      <Icon name="close" className="sm" />
                    </button>
                  </div>
                </th>
              ))}
              <th aria-label="Ações" />
            </tr>
          </thead>
          <tbody>
            {draft.labels.map((label, li) => (
              <tr key={li}>
                <th scope="row">
                  <input
                    type="text"
                    aria-label={`Rótulo da linha ${li + 1}`}
                    maxLength={L.short}
                    value={label}
                    onChange={e => setLabel(li, e.target.value)}
                    placeholder="Ex.: 2020"
                  />
                </th>
                {draft.series.map((s, si) => (
                  <td key={si}>
                    <input
                      type="text"
                      inputMode="decimal"
                      aria-label={`${s.name || `Série ${si + 1}`}, ${label || `linha ${li + 1}`}`}
                      aria-invalid={invalidCells.includes(`${si}:${li}`) || undefined}
                      className={invalidCells.includes(`${si}:${li}`) ? 'invalid' : undefined}
                      value={s.cells[li]}
                      onChange={e => setCell(si, li, e.target.value)}
                      placeholder="sem dado"
                    />
                  </td>
                ))}
                <td>
                  <button
                    type="button"
                    className="icon-btn"
                    title="Remover linha"
                    aria-label={`Remover linha ${label || li + 1}`}
                    onClick={() => removeRow(li)}
                    disabled={draft.labels.length <= 1}
                  >
                    <Icon name="trash" className="sm" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="row gap8 mt16 wrap">
        <button type="button" className="btn compact" onClick={addRow} disabled={draft.labels.length >= L.maxLabels}>
          <Icon name="plus" className="sm" /> Linha
        </button>
        <button type="button" className="btn compact" onClick={addSeries} disabled={draft.series.length >= L.maxSeries}>
          <Icon name="plus" className="sm" /> Série
        </button>
        <span className="small muted">
          {draft.labels.length}/{L.maxLabels} linhas · {draft.series.length}/{L.maxSeries} séries
        </span>
      </div>

      <h4 className="mt24">Fontes declaradas</h4>
      <p className="small muted">Informe de onde vieram os dados. O Notee não verifica o conteúdo dos links.</p>
      {draft.sources.map((s, i) => (
        <div key={i} className="row gap8 mb16 chart-source-row">
          <input
            type="text"
            aria-label={`Rótulo da fonte ${i + 1}`}
            maxLength={L.sourceLabel}
            value={s.label}
            onChange={e => setSource(i, 'label', e.target.value)}
            placeholder="Ex.: IBGE — IPCA"
          />
          <input
            type="text"
            inputMode="url"
            aria-label={`URL da fonte ${i + 1}`}
            maxLength={L.url}
            value={s.url}
            onChange={e => setSource(i, 'url', e.target.value)}
            placeholder="https://… (opcional)"
          />
          <button type="button" className="icon-btn" aria-label={`Remover fonte ${i + 1}`} onClick={() => removeSource(i)}>
            <Icon name="close" className="sm" />
          </button>
        </div>
      ))}
      <button type="button" className="btn compact" onClick={addSource} disabled={draft.sources.length >= L.maxSources}>
        <Icon name="plus" className="sm" /> Fonte
      </button>

      <h4 className="mt24">Pré-visualização</h4>
      <ChartView chart={preview} defaultExpanded embedded />

      {showErrors && errors.length > 0 && (
        <div className="callout warning mt16" role="alert">
          <Icon name="warning" />
          <ul style={{ margin: 0, paddingLeft: 18 }}>
            {errors.map((e, i) => (
              <li key={i}>{e}</li>
            ))}
          </ul>
        </div>
      )}

      <div className="row gap8 mt16" style={{ justifyContent: 'flex-end' }}>
        <button type="button" className="btn" onClick={onCancel}>
          Cancelar
        </button>
        <button type="button" className="btn primary" onClick={handleDone}>
          <Icon name="check" className="sm" /> Concluir gráfico
        </button>
      </div>
    </section>
  );
}
