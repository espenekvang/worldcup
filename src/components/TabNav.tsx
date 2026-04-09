import type { Stage } from '../types'

interface TabNavProps {
  activeTab: Stage
  onTabChange: (stage: Stage) => void
}

const TABS: { stage: Stage; label: string }[] = [
  { stage: 'group', label: 'Group Stage' },
  { stage: 'round-of-32', label: 'Round of 32' },
  { stage: 'round-of-16', label: 'Round of 16' },
  { stage: 'quarter-final', label: 'Quarter-finals' },
  { stage: 'semi-final', label: 'Semi-finals' },
  { stage: 'final', label: 'Final' },
]

export default function TabNav({ activeTab, onTabChange }: TabNavProps) {
  return (
    <nav className="flex gap-1 overflow-x-auto border-b border-gray-200 px-4">
      {TABS.map(tab => (
        <button
          key={tab.stage}
          onClick={() => onTabChange(tab.stage)}
          className={`whitespace-nowrap px-4 py-2 text-sm font-medium transition-colors ${
            activeTab === tab.stage
              ? 'border-b-2 border-blue-600 text-blue-600'
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          {tab.label}
        </button>
      ))}
    </nav>
  )
}
