import type { Section } from '../types'

type MatchesSection = Exclude<Section, 'leaderboard' | 'admin'>

interface MobileSectionNavProps {
  activeSection: MatchesSection
  onSectionChange: (section: MatchesSection) => void
}

const SECTIONS: { section: MatchesSection; label: string }[] = [
  { section: 'group', label: 'Gruppespill' },
  { section: 'knockout', label: 'Sluttspill' },
]

/**
 * Kompakt 2-pill seksjons-toggle på mobil. Brukes kun innenfor "Kamper"-vinduet
 * for å bytte mellom gruppespill og sluttspill. Rundevalg vises i RoundPills
 * under denne.
 */
export default function MobileStageNav({ activeSection, onSectionChange }: MobileSectionNavProps) {
  return (
    <nav
      className="-mx-4 mb-3 flex gap-2 overflow-x-auto px-4 scrollbar-none lg:hidden"
      aria-label="Velg turneringsdel"
    >
      {SECTIONS.map(s => {
        const isActive = activeSection === s.section
        return (
          <button
            key={s.section}
            onClick={() => onSectionChange(s.section)}
            className="shrink-0 whitespace-nowrap rounded-full border px-4 py-1.5 text-sm font-medium transition-colors"
            style={{
              backgroundColor: isActive ? 'var(--color-tab-active)' : 'transparent',
              borderColor: isActive ? 'var(--color-tab-active)' : 'var(--color-border)',
              color: isActive ? 'var(--color-text-inverse)' : 'var(--color-text-primary)',
            }}
            aria-current={isActive ? 'page' : undefined}
          >
            {s.label}
          </button>
        )
      })}
    </nav>
  )
}
