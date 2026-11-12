// Renderização somente leitura de um bloco de gráfico (ADR-036): SVG próprio, interativo (mouse e teclado),
// compacto por padrão; "Ver dados" expande tabela e fontes. Tudo é texto renderizado pelo React:
// nenhum HTML/script do autor e nenhuma busca externa.
import React, { useId, useState } from 'react';
import { formatNumber, formatValue, isHttpUrl, niceScale } from './chartModel';

const W = 720;
const M = { top: 18, right: 18, bottom: 46, left: 62 };

// Séries diferenciadas por cor E por traço/marcador/padrão (não dependem só da cor).
const DASHES = ['', '7 4', '2 4', '10 4 2 4', '4 2', '12 3', '1 3', '6 2 2 2'];
const MARKERS = ['circle', 'square', 'triangle', 'diamond', 'circle', 'square', 'triangle', 'diamond'];
const PATTERNS = [null, 'diag', 'dots', 'horiz', 'cross', 'diag', 'dots', 'horiz'];

const colorOf = i => `var(--chart-${(i % 8) + 1})`;
const isValue = v => typeof v === 'number' && Number.isFinite(v);

function Marker({ shape, x, y, color, size = 4.5, className }) {
  const s = size;
  const common = { fill: color, className };
  if (shape === 'square') return <rect x={x - s} y={y - s} width={s * 2} height={s * 2} rx={1} {...common} />;
  if (shape === 'triangle') return <path d={`M${x} ${y - s - 1}L${x + s + 1} ${y + s}L${x - s - 1} ${y + s}Z`} {...common} />;
  if (shape === 'diamond') return <path d={`M${x} ${y - s - 1}L${x + s + 1} ${y}L${x} ${y + s + 1}L${x - s - 1} ${y}Z`} {...common} />;
  return <circle cx={x} cy={y} r={s} {...common} />;
}

function LegendSwatch({ index, chartType, patternId }) {
  const color = colorOf(index);
  return (
    <svg width="26" height="12" aria-hidden="true" className="chart-swatch">
      {chartType === 'bar' ? (
        <>
          <rect x="1" y="0" width="24" height="12" rx="3" fill={color} />
          {PATTERNS[index] && <rect x="1" y="0" width="24" height="12" rx="3" fill={`url(#${patternId}-${PATTERNS[index]})`} />}
        </>
      ) : (
        <>
          <line x1="1" y1="6" x2="25" y2="6" stroke={color} strokeWidth="2.5" strokeDasharray={DASHES[index % 8]} />
          <Marker shape={MARKERS[index % 8]} x={13} y={6} color={color} size={3.5} />
        </>
      )}
    </svg>
  );
}

function Defs({ id, seriesCount }) {
  return (
    <defs>
      <pattern id={`${id}-diag`} width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
        <line x1="0" y1="0" x2="0" y2="6" stroke="#fff" strokeWidth="2" opacity="0.55" />
      </pattern>
      <pattern id={`${id}-dots`} width="6" height="6" patternUnits="userSpaceOnUse">
        <circle cx="3" cy="3" r="1.3" fill="#fff" opacity="0.7" />
      </pattern>
      <pattern id={`${id}-horiz`} width="6" height="6" patternUnits="userSpaceOnUse">
        <line x1="0" y1="3" x2="6" y2="3" stroke="#fff" strokeWidth="1.6" opacity="0.55" />
      </pattern>
      <pattern id={`${id}-cross`} width="7" height="7" patternUnits="userSpaceOnUse">
        <path d="M0 0L7 7M7 0L0 7" stroke="#fff" strokeWidth="1.3" opacity="0.55" />
      </pattern>
      {Array.from({ length: seriesCount }, (_, i) => (
        <linearGradient key={i} id={`${id}-area-${i}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={colorOf(i)} stopOpacity="0.18" />
          <stop offset="100%" stopColor={colorOf(i)} stopOpacity="0" />
        </linearGradient>
      ))}
    </defs>
  );
}

export function ChartView({ chart, defaultExpanded = false, embedded = false }) {
  const uid = useId().replace(/:/g, '');
  const [expanded, setExpanded] = useState(defaultExpanded);
  // Destaque atual: índice do rótulo e, opcionalmente, da série sob o mouse.
  const [active, setActive] = useState(null);

  const labels = chart.labels || [];
  const series = chart.series || [];
  const unit = chart.unit || '';
  const values = series.flatMap(s => (s.values || []).filter(isValue));
  const hasData = values.length > 0 && labels.length > 0;
  const dataMin = hasData ? Math.min(...values) : 0;
  const dataMax = hasData ? Math.max(...values) : 0;
  const scale = niceScale(dataMin, dataMax, expanded ? 5 : 4);
  const missingCount = series.reduce((n, s) => n + (s.values || []).filter(v => !isValue(v)).length, 0);

  const H = expanded ? 300 : 230;
  const plotW = W - M.left - M.right;
  const plotH = H - M.top - M.bottom;
  const y = v => M.top + plotH - ((v - scale.min) / (scale.max - scale.min)) * plotH;
  const band = plotW / Math.max(labels.length, 1);
  const xCenter = i => M.left + band * (i + 0.5);
  const labelStep = Math.max(1, Math.ceil(labels.length / (expanded ? 12 : 8)));
  const baseline = y(Math.max(scale.min, Math.min(0, scale.max)));
  const isBar = chart.chartType === 'bar';

  const titleText = chart.title || 'Gráfico sem título';
  const summary = hasData
    ? `Gráfico de ${isBar ? 'barras' : 'linha'} com ${series.length} ${series.length === 1 ? 'série' : 'séries'} e ${labels.length} ${
        labels.length === 1 ? 'categoria' : 'categorias'
      }, de ${labels[0]} a ${labels[labels.length - 1]}. Valores entre ${formatValue(dataMin, unit)} e ${formatValue(dataMax, unit)}.${
        missingCount ? ` ${missingCount} ${missingCount === 1 ? 'valor ausente' : 'valores ausentes'}.` : ''
      }`
    : 'Gráfico sem valores informados.';
  const sources = (chart.sources || []).filter(s => s && s.label);

  const activeIndex = active?.index ?? null;
  const activeSeries = active?.series ?? null;
  const dimSeries = si => activeSeries !== null && activeSeries !== si;

  const announce =
    activeIndex === null
      ? ''
      : `${labels[activeIndex]}: ${series.map(s => `${s.name} ${formatValue(s.values?.[activeIndex], unit)}`).join('; ')}`;

  const handleKey = e => {
    if (!labels.length) return;
    const current = activeIndex ?? -1;
    let next = null;
    if (e.key === 'ArrowRight') next = Math.min(labels.length - 1, current + 1);
    else if (e.key === 'ArrowLeft') next = Math.max(0, current <= 0 ? 0 : current - 1);
    else if (e.key === 'Home') next = 0;
    else if (e.key === 'End') next = labels.length - 1;
    else if (e.key === 'Escape') {
      setActive(null);
      return;
    }
    if (next !== null) {
      e.preventDefault();
      setActive({ index: next, series: null });
    }
  };

  // Posição do tooltip em % do SVG (responsivo).
  let tip = null;
  if (activeIndex !== null && hasData) {
    const rows = series
      .map((s, si) => ({ si, name: s.name, value: s.values?.[activeIndex] }))
      .filter(r => activeSeries === null || r.si === activeSeries);
    // Topo do elemento destacado: para barras negativas, a linha de base (o tooltip não cobre a barra).
    const top = Math.min(...rows.map(r => (isValue(r.value) ? Math.min(y(r.value), isBar ? baseline : y(r.value)) : baseline)));
    // Com muitas séries o tooltip fica ao lado da coluna (não sai do card); com poucas, acima do valor.
    const side = rows.length > 2 ? (activeIndex < labels.length / 2 ? 'right' : 'left') : null;
    tip = {
      rows,
      side,
      left: ((side === 'right' ? M.left + band * (activeIndex + 1) : side === 'left' ? M.left + band * activeIndex : xCenter(activeIndex)) / W) * 100,
      top: ((side ? M.top : Math.max(top, M.top)) / H) * 100,
    };
  }

  return (
    <figure className={`chart-figure ${expanded ? 'is-expanded' : 'is-compact'} ${embedded ? 'embedded' : ''}`}>
      <figcaption className="chart-caption">
        <div className="chart-heading">
          <strong id={`${uid}-title`} className="chart-title">
            {titleText}
          </strong>
          <span className="chart-meta">
            {isBar ? 'Barras' : 'Linha'}
            {unit && ` · ${unit}`}
            {hasData && ` · ${labels[0]}–${labels[labels.length - 1]}`}
          </span>
        </div>
        {chart.description && <span className={`chart-description ${expanded ? '' : 'clamped'}`}>{chart.description}</span>}
      </figcaption>

      {series.length > 1 && (
        <ul className="chart-legend" aria-label="Séries">
          {series.map((s, i) => (
            <li
              key={i}
              className={dimSeries(i) ? 'is-dim' : undefined}
              onMouseEnter={() => setActive(a => ({ index: a?.index ?? null, series: i }))}
              onMouseLeave={() => setActive(null)}
            >
              <LegendSwatch index={i} chartType={chart.chartType} patternId={uid} />
              {s.name}
            </li>
          ))}
        </ul>
      )}

      <div
        className="chart-plot"
        tabIndex={hasData ? 0 : undefined}
        role="group"
        aria-label={`${titleText}. Use as setas para percorrer os valores.`}
        onKeyDown={handleKey}
        onBlur={() => setActive(null)}
        onMouseLeave={() => setActive(null)}
      >
        <svg className="chart-svg" viewBox={`0 0 ${W} ${H}`} role="img" aria-labelledby={`${uid}-title ${uid}-desc`}>
          <desc id={`${uid}-desc`}>{summary}</desc>
          <Defs id={uid} seriesCount={series.length} />

          {scale.ticks.map(t => (
            <g key={t}>
              <line x1={M.left} x2={W - M.right} y1={y(t)} y2={y(t)} className={t === 0 ? 'chart-zero' : 'chart-grid'} />
              <text x={M.left - 10} y={y(t)} className="chart-tick" textAnchor="end" dominantBaseline="middle">
                {formatNumber(t)}
              </text>
            </g>
          ))}

          {activeIndex !== null && (
            <rect
              x={M.left + band * activeIndex}
              y={M.top}
              width={band}
              height={plotH}
              className="chart-active-band"
              pointerEvents="none"
            />
          )}

          {labels.map((label, i) =>
            i % labelStep === 0 || i === activeIndex ? (
              <text
                key={i}
                x={xCenter(i)}
                y={M.top + plotH + 20}
                className={`chart-tick ${i === activeIndex ? 'is-active' : ''}`}
                textAnchor="middle"
              >
                {String(label).length > 14 ? `${String(label).slice(0, 13)}…` : label}
              </text>
            ) : null,
          )}

          {chart.xAxisLabel && expanded && (
            <text x={M.left + plotW / 2} y={H - 6} className="chart-axis-label" textAnchor="middle">
              {chart.xAxisLabel}
            </text>
          )}
          {(chart.yAxisLabel || unit) && (
            <text x={16} y={M.top + plotH / 2} className="chart-axis-label" textAnchor="middle" transform={`rotate(-90 16 ${M.top + plotH / 2})`}>
              {[chart.yAxisLabel, unit && `(${unit})`].filter(Boolean).join(' ')}
            </text>
          )}

          {isBar
            ? series.map((s, si) => {
                const groupW = band * (series.length > 1 ? 0.8 : 0.62);
                const barW = groupW / Math.max(series.length, 1);
                return (
                  <g key={si}>
                    {labels.map((label, li) => {
                      const v = s.values?.[li];
                      const x = M.left + band * li + (band - groupW) / 2 + barW * si;
                      const w = Math.max(barW - 3, 1.5);
                      const isActive = activeIndex === li && (activeSeries === null || activeSeries === si);
                      const dim = activeIndex !== null && !isActive;
                      if (!isValue(v)) {
                        return (
                          <rect
                            key={li}
                            x={x + 1.5}
                            y={baseline - 12}
                            width={w}
                            height={12}
                            rx={3}
                            className={`chart-missing ${dim ? 'is-dim' : ''}`}
                            onMouseEnter={() => setActive({ index: li, series: si })}
                          />
                        );
                      }
                      const top = Math.min(y(v), baseline);
                      const height = Math.max(Math.abs(y(v) - baseline), 1.5);
                      return (
                        <g
                          key={li}
                          className={`chart-bar ${isActive ? 'is-active' : ''} ${dim ? 'is-dim' : ''}`}
                          onMouseEnter={() => setActive({ index: li, series: si })}
                        >
                          <rect x={x + 1.5} y={top} width={w} height={height} rx={Math.min(4, w / 3)} fill={colorOf(si)} />
                          {PATTERNS[si] && (
                            <rect
                              x={x + 1.5}
                              y={top}
                              width={w}
                              height={height}
                              rx={Math.min(4, w / 3)}
                              fill={`url(#${uid}-${PATTERNS[si]})`}
                              pointerEvents="none"
                            />
                          )}
                          {isActive && labels.length <= 24 && (
                            <text x={x + 1.5 + w / 2} y={v >= 0 ? top - 6 : top + height + 14} className="chart-value" textAnchor="middle">
                              {formatNumber(v)}
                            </text>
                          )}
                        </g>
                      );
                    })}
                  </g>
                );
              })
            : series.map((s, si) => {
                // Valor ausente quebra a linha (nunca é desenhado como zero).
                const segments = [];
                let current = [];
                labels.forEach((_, li) => {
                  const v = s.values?.[li];
                  if (!isValue(v)) {
                    if (current.length) segments.push(current);
                    current = [];
                  } else current.push([xCenter(li), y(v)]);
                });
                if (current.length) segments.push(current);
                const dim = dimSeries(si);
                return (
                  <g key={si} className={`chart-line ${dim ? 'is-dim' : ''}`}>
                    {series.length === 1 &&
                      segments
                        .filter(seg => seg.length > 1)
                        .map((seg, k) => (
                          <path
                            key={`a${k}`}
                            d={`M${seg[0][0]},${baseline} ${seg.map(p => `L${p[0]},${p[1]}`).join(' ')} L${seg[seg.length - 1][0]},${baseline}Z`}
                            fill={`url(#${uid}-area-${si})`}
                            pointerEvents="none"
                          />
                        ))}
                    {segments
                      .filter(seg => seg.length > 1)
                      .map((seg, k) => (
                        <polyline
                          key={k}
                          points={seg.map(p => p.join(',')).join(' ')}
                          fill="none"
                          stroke={colorOf(si)}
                          strokeWidth={activeSeries === si ? 3.5 : 2.5}
                          strokeDasharray={DASHES[si % 8]}
                          strokeLinejoin="round"
                          strokeLinecap="round"
                          onMouseEnter={() => setActive(a => ({ index: a?.index ?? null, series: si }))}
                        />
                      ))}
                    {labels.map((_, li) => {
                      const v = s.values?.[li];
                      if (!isValue(v)) return null;
                      const isActive = activeIndex === li;
                      return (
                        <Marker
                          key={li}
                          shape={MARKERS[si % 8]}
                          x={xCenter(li)}
                          y={y(v)}
                          color={colorOf(si)}
                          size={isActive ? 6.5 : 4}
                          className={isActive ? 'chart-point is-active' : 'chart-point'}
                        />
                      );
                    })}
                  </g>
                );
              })}

          {/* Colunas de captura do mouse para linha (e barras ausentes/estreitas). */}
          {hasData &&
            labels.map((_, li) => (
              <rect
                key={`hit${li}`}
                x={M.left + band * li}
                y={M.top}
                width={band}
                height={plotH}
                fill="transparent"
                pointerEvents={isBar ? 'none' : 'all'}
                onMouseEnter={() => setActive(a => ({ index: li, series: a?.series ?? null }))}
              />
            ))}

          {!hasData && (
            <text x={M.left + plotW / 2} y={M.top + plotH / 2} className="chart-empty" textAnchor="middle">
              Sem valores informados
            </text>
          )}
        </svg>

        {tip && (
          <div
            className={`chart-tooltip${tip.side ? ` side-${tip.side}` : ''}`}
            style={{ left: `${tip.left}%`, top: `${tip.top}%` }}
            aria-hidden="true"
          >
            <strong>{labels[activeIndex]}</strong>
            {tip.rows.map(r => (
              <span key={r.si} className="chart-tooltip-row">
                <i style={{ background: colorOf(r.si) }} />
                {series.length > 1 || r.name !== titleText ? `${r.name}: ` : ''}
                <b>{formatValue(r.value, unit)}</b>
              </span>
            ))}
          </div>
        )}
        <span className="visually-hidden" aria-live="polite">
          {announce}
        </span>
      </div>

      <div className="chart-footer">
        <span className="chart-source-line">
          {sources.length ? (
            <>
              Fonte declarada: {sources.map(s => s.label).join('; ')}
            </>
          ) : (
            'Sem fonte informada'
          )}
          {missingCount > 0 && !expanded && ' · contém períodos sem dado'}
        </span>
        <button
          type="button"
          className="chart-toggle"
          aria-expanded={expanded}
          onClick={() => setExpanded(v => !v)}
        >
          {expanded ? 'Ocultar dados ▴' : 'Ver dados ▾'}
        </button>
      </div>

      {expanded && (
        <div className="chart-details">
          {missingCount > 0 && (
            <p className="chart-note">
              {isBar
                ? 'Retângulos tracejados indicam períodos sem dado (diferente de zero).'
                : 'Interrupções na linha indicam períodos sem dado (diferente de zero).'}
            </p>
          )}
          <div className="chart-table-wrap" tabIndex={0} role="region" aria-label={`Tabela de dados: ${titleText}`}>
            <table className="chart-table">
              <caption>Dados do gráfico{unit ? ` (${unit})` : ''}</caption>
              <thead>
                <tr>
                  <th scope="col">{chart.xAxisLabel || 'Categoria'}</th>
                  {series.map((s, i) => (
                    <th key={i} scope="col">
                      {s.name}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {labels.map((label, li) => (
                  <tr
                    key={li}
                    className={li === activeIndex ? 'is-active' : undefined}
                    onMouseEnter={() => setActive({ index: li, series: null })}
                    onMouseLeave={() => setActive(null)}
                  >
                    <th scope="row">{label}</th>
                    {series.map((s, si) => {
                      const v = s.values?.[li];
                      const missing = !isValue(v);
                      return (
                        <td key={si} className={missing ? 'missing' : undefined}>
                          {missing ? 'sem dado' : formatNumber(v)}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="chart-sources">
            <span className="chart-sources-title">Fontes declaradas (não verificadas pelo Notee)</span>
            {sources.length === 0 ? (
              <span className="muted"> — nenhuma fonte informada.</span>
            ) : (
              <ul>
                {sources.map((s, i) => (
                  <li key={i}>
                    {s.url && isHttpUrl(s.url) ? (
                      <a href={s.url} target="_blank" rel="noopener noreferrer nofollow">
                        {s.label}
                      </a>
                    ) : (
                      s.label
                    )}
                    {s.url && isHttpUrl(s.url) && <span className="chart-source-host"> ({new URL(s.url).hostname})</span>}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </figure>
  );
}
