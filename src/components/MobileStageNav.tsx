import type { Stage } from '../types'

type StageOnly = Exclude<Stage, 'leaderboard'>

interface MobileStageNavProps {
  activeStage: StageOnly
  onStageChange: (stage: StageOnly) => void
}

const STAGES: { stage: StageOnly; label: string; short: string }[] = [
  { stage: 'group', label: 'Gruppespill', short: 'Gruppe' },
  { stage: 'round-of-32', label: '32-delsfinale', short: '1/16' },
  { stage: 'round-of-16', label: '8-delsfinale', short: '1/8' },
  { stage: 'quarter-final', label: 'Kvartfinale', short: 'Kvart' },
  { stage: 'semi-final', label: 'Semifinale', short: 'Semi' },
  { stage: 'final', label: 'Finale', short: 'Finale' },
]

/**
 * Kompakt horisontal pill-scroll for stage-valg på mobil. Brukes kun innenfor "Kamper"-fanen.
 */
export default function MobileStageNav({ activeStage, onStageChange }: MobileStageNavProps) {
  return (
    <nav
      className="-mx-4 mb-3 flex gap-2 overflow-x-auto px-4 scrollbar-none lg:hidden"
      aria-label="Velg turneringsfase"
    >
      {STAGES.map(s => {
        const isActive = activeStage === s.stage
        return (
          <button
            key={s.stage}
            onClick={() => onStageChange(s.stage)}
            className="shrink-0 whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-medium transition-colors"
            style={{
              backgroundColor: isActive ? 'var(--color-tab-active)' : 'transparent',
              borderColor: isActive ? 'var(--color-tab-active)' : 'var(--color-border)',
              color: isActive ? 'var(--color-text-inverse)' : 'var(--color-text-primary)',
            }}
            aria-current={isActive ? 'page' : undefined}
          >
            <span className="sm:hidden">{s.short}</span>
            <span className="hidden sm:inline">{s.label}</span>
          </button>
        )
      })}
    </nav>
  )
}
