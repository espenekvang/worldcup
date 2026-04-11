import type { Stage } from '../types'

interface TabNavProps {
  activeTab: Stage
  onTabChange: (stage: Stage) => void
}

const TABS: { stage: Stage; label: string }[] = [
  { stage: 'group', label: 'Gruppespill' },
  { stage: 'round-of-32', label: '32-delsfinale' },
  { stage: 'round-of-16', label: '8-delsfinale' },
  { stage: 'quarter-final', label: 'Kvartfinale' },
  { stage: 'semi-final', label: 'Semifinale' },
  { stage: 'final', label: 'Finale' },
  { stage: 'leaderboard', label: 'The Boss' },
]

export default function TabNav({ activeTab, onTabChange }: TabNavProps) {
  return (
    <nav
      className="-mx-4 flex gap-1 overflow-x-auto border-b px-4 scrollbar-none sm:mx-0"
      style={{ borderColor: 'var(--color-border)' }}
    >
      {TABS.map(tab => (
        <button
          key={tab.stage}
          onClick={() => onTabChange(tab.stage)}
          className="whitespace-nowrap px-3 py-3 text-sm font-medium transition-colors sm:px-4 sm:py-2"
          style={{
            color: activeTab === tab.stage ? 'var(--color-tab-active)' : 'var(--color-tab-inactive)',
            borderBottom: activeTab === tab.stage ? '2px solid var(--color-tab-active)' : '2px solid transparent',
          }}
        >
          {tab.label}
        </button>
      ))}
    </nav>
  )
}
