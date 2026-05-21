import type { Match, Stage } from '../types'
import { GROUP_ROUNDS, KNOCKOUT_ROUNDS, isStageLocked } from '../utils/dateUtils'

interface RoundPillsProps {
  section: 'group' | 'knockout'
  activeStage: Stage
  onStageChange: (stage: Stage) => void
  matches: Match[]
}

const LABELS: Record<string, { full: string; short: string }> = {
  'group-1': { full: 'Runde 1', short: 'R1' },
  'group-2': { full: 'Runde 2', short: 'R2' },
  'group-3': { full: 'Runde 3', short: 'R3' },
  'round-of-32': { full: '32-delsfinale', short: '1/16' },
  'round-of-16': { full: '8-delsfinale', short: '1/8' },
  'quarter-final': { full: 'Kvartfinale', short: 'Kvart' },
  'semi-final': { full: 'Semifinale', short: 'Semi' },
  'final': { full: 'Finale', short: 'Finale' },
}

/**
 * Pill-rad for å velge runde innenfor valgt seksjon. Viser også en liten
 * lås-indikator når en runde er stengt for tipping.
 */
export default function RoundPills({ section, activeStage, onStageChange, matches }: RoundPillsProps) {
  const stages = section === 'group' ? GROUP_ROUNDS : KNOCKOUT_ROUNDS

  return (
    <nav
      className="-mx-4 mb-3 mt-2 flex gap-2 overflow-x-auto px-4 scrollbar-none lg:mx-0 lg:px-0"
      aria-label="Velg runde"
    >
      {stages.map(stage => {
        const isActive = activeStage === stage
        const locked = isStageLocked(stage, matches)
        const label = LABELS[stage]
        if (!label) return null
        return (
          <button
            key={stage}
            onClick={() => onStageChange(stage)}
            className="shrink-0 inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-3 py-1 text-xs font-medium transition-colors"
            style={{
              backgroundColor: isActive ? 'var(--color-primary-light)' : 'transparent',
              borderColor: isActive ? 'var(--color-primary)' : 'var(--color-border)',
              color: isActive ? 'var(--color-primary)' : 'var(--color-text-primary)',
            }}
            aria-current={isActive ? 'page' : undefined}
          >
            <span className="sm:hidden">{label.short}</span>
            <span className="hidden sm:inline">{label.full}</span>
            {locked && (
              <svg
                className="h-3 w-3"
                style={{ color: 'var(--color-text-muted)' }}
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
                aria-label="Stengt"
              >
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2" strokeWidth={2} />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" strokeWidth={2} strokeLinecap="round" />
              </svg>
            )}
          </button>
        )
      })}
    </nav>
  )
}
