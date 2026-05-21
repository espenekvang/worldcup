import type { Section } from '../types'

interface TabNavProps {
  activeSection: Section
  onSectionChange: (section: Section) => void
}

const TABS: { section: Section; label: string }[] = [
  { section: 'group', label: 'Gruppespill' },
  { section: 'knockout', label: 'Sluttspill' },
  { section: 'leaderboard', label: 'The Boss' },
]

/**
 * Desktop-tab-nav (lg+). Tre toppnivå-faner. Runder vises som pills under
 * via RoundPills når en seksjon med flere runder er valgt.
 */
export default function TabNav({ activeSection, onSectionChange }: TabNavProps) {
  return (
    <nav
      className="hidden gap-1 border-b lg:flex"
      style={{ borderColor: 'var(--color-border)' }}
    >
      {TABS.map(tab => (
        <button
          key={tab.section}
          onClick={() => onSectionChange(tab.section)}
          className="whitespace-nowrap px-4 py-2 text-sm font-medium transition-colors"
          style={{
            color: activeSection === tab.section ? 'var(--color-tab-active)' : 'var(--color-tab-inactive)',
            borderBottom: activeSection === tab.section ? '2px solid var(--color-tab-active)' : '2px solid transparent',
          }}
        >
          {tab.label}
        </button>
      ))}
    </nav>
  )
}
