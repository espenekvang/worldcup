import { useMemo, useState } from 'react'

/**
 * Deltakelseskurve: antall medlemmer som tippet på hver spilte kamp, i
 * kronologisk rekkefølge. Én serie, så ingen legend – tittelen navngir den.
 * Areal + 2px linje mot en referanselinje for totalt antall deltakere, slik at
 * frafallet gjennom turneringen leses mot «alle». Tema-tokens gir lyst/mørkt.
 */

export interface ParticipationChartPoint {
  count: number
  /** Lagnavn e.l. for tooltip, f.eks. «🇳🇴 Norge – Brasil 🇧🇷». */
  label: string
  /** Kort dato for x-aksen, f.eks. «14. jul». */
  dateLabel: string
}

interface Props {
  points: ParticipationChartPoint[]
  memberCount: number
}

const W = 680
const H = 260
const PAD_L = 34
const PAD_R = 16
const PAD_T = 20
const PAD_B = 30
const PLOT_W = W - PAD_L - PAD_R
const PLOT_H = H - PAD_T - PAD_B
const BASELINE_Y = PAD_T + PLOT_H

export default function ParticipationChart({ points, memberCount }: Props) {
  const [hover, setHover] = useState<number | null>(null)

  const n = points.length
  const maxCount = useMemo(() => points.reduce((m, p) => Math.max(m, p.count), 0), [points])
  const yMax = Math.max(memberCount, maxCount, 1)

  const xFor = (i: number) => (n <= 1 ? PAD_L + PLOT_W / 2 : PAD_L + (i / (n - 1)) * PLOT_W)
  const yFor = (v: number) => PAD_T + (1 - v / yMax) * PLOT_H

  const { linePath, areaPath } = useMemo(() => {
    if (n === 0) return { linePath: '', areaPath: '' }
    const coords = points.map((p, i) => `${xFor(i).toFixed(1)},${yFor(p.count).toFixed(1)}`)
    const line = `M${coords.join(' L')}`
    const area = `M${xFor(0).toFixed(1)},${BASELINE_Y} L${coords.join(' L')} L${xFor(n - 1).toFixed(1)},${BASELINE_Y} Z`
    return { linePath: line, areaPath: area }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [points, yMax])

  if (n === 0) return null

  const refY = yFor(memberCount)

  function handleMove(e: React.PointerEvent<HTMLDivElement>) {
    const rect = e.currentTarget.getBoundingClientRect()
    const ratio = (e.clientX - rect.left) / rect.width
    // Map fra container-bredde til plot-området (som har venstre/høyre marg).
    const plotRatio = (ratio * W - PAD_L) / PLOT_W
    const idx = Math.round(plotRatio * (n - 1))
    setHover(Math.max(0, Math.min(n - 1, idx)))
  }

  const active = hover != null ? points[hover] : null
  const hoverXPct = hover != null ? (xFor(hover) / W) * 100 : 0
  const first = points[0]
  const last = points[n - 1]

  return (
    <div
      className="relative w-full touch-none select-none"
      onPointerMove={handleMove}
      onPointerLeave={() => setHover(null)}
      role="img"
      aria-label={`Deltakelse per kamp: ${first.count} tippet den første kampen, ${last.count} den siste, av ${memberCount} deltakere.`}
    >
      <svg viewBox={`0 0 ${W} ${H}`} width="100%" preserveAspectRatio="xMidYMid meet" className="block">
        {/* Referanselinje: alle deltakere */}
        <line
          x1={PAD_L} y1={refY} x2={W - PAD_R} y2={refY}
          stroke="var(--color-text-muted)" strokeWidth={1} strokeDasharray="3 3" opacity={0.5}
        />
        <text x={W - PAD_R} y={refY - 5} textAnchor="end" fontSize={11} fill="var(--color-text-muted)">
          Alle {memberCount}
        </text>

        {/* Baseline + y-merker */}
        <line x1={PAD_L} y1={BASELINE_Y} x2={W - PAD_R} y2={BASELINE_Y} stroke="var(--color-border)" strokeWidth={1} />
        <text x={PAD_L - 6} y={BASELINE_Y} textAnchor="end" dominantBaseline="middle" fontSize={11} fill="var(--color-text-muted)">0</text>
        <text x={PAD_L - 6} y={yFor(yMax)} textAnchor="end" dominantBaseline="middle" fontSize={11} fill="var(--color-text-muted)">{yMax}</text>

        {/* Areal + linje */}
        <path d={areaPath} fill="var(--color-primary)" fillOpacity={0.14} />
        <path d={linePath} fill="none" stroke="var(--color-primary)" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />

        {/* Hover: crosshair + markør */}
        {active && (
          <>
            <line x1={xFor(hover!)} y1={PAD_T} x2={xFor(hover!)} y2={BASELINE_Y} stroke="var(--color-text-muted)" strokeWidth={1} opacity={0.4} />
            <circle cx={xFor(hover!)} cy={yFor(active.count)} r={4} fill="var(--color-primary)" stroke="var(--color-surface-card)" strokeWidth={2} />
          </>
        )}

        {/* X-akse: start- og sluttdato */}
        <text x={PAD_L} y={H - 8} textAnchor="start" fontSize={11} fill="var(--color-text-muted)">{first.dateLabel}</text>
        <text x={W - PAD_R} y={H - 8} textAnchor="end" fontSize={11} fill="var(--color-text-muted)">{last.dateLabel}</text>
      </svg>

      {/* Tooltip */}
      {active && (
        <div
          className="pointer-events-none absolute top-0 z-10 -translate-x-1/2 rounded-lg border px-2.5 py-1.5 text-xs shadow-sm"
          style={{
            left: `${Math.min(88, Math.max(12, hoverXPct))}%`,
            backgroundColor: 'var(--color-surface-elevated)',
            borderColor: 'var(--color-border)',
            color: 'var(--color-text-primary)',
          }}
        >
          <div className="font-medium">{active.dateLabel} · {active.label}</div>
          <div style={{ color: 'var(--color-text-muted)' }}>
            <span style={{ color: 'var(--color-primary)' }}>{active.count}</span> av {memberCount} tippet
          </div>
        </div>
      )}
    </div>
  )
}
