import type { Match, Team, Venue, Stage } from '../types'
import { formatMatchDate } from '../utils/dateUtils'
import { isStageLocked } from '../utils/dateUtils'
import MatchCard from './MatchCard'

interface MatchListProps {
  matches: Match[]
  teams: Record<string, Team>
  venues: Venue[]
  activeStage: Stage
  onTipClick: (match: Match) => void
  onViewOthers: (match: Match) => void
}

export default function MatchList({ matches, teams, venues, activeStage, onTipClick, onViewOthers }: MatchListProps) {
  const filtered = matches.filter(m => {
    if (activeStage === 'final') return m.stage === 'final' || m.stage === 'third-place'
    return m.stage === activeStage
  })

  const sorted = [...filtered].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())

  const isLocked = (match: Match) => isStageLocked(match.stage, matches)

  const dayGroups = new Map<string, Match[]>()
  for (const match of sorted) {
    const dayKey = match.date.slice(0, 10)
    if (!dayGroups.has(dayKey)) dayGroups.set(dayKey, [])
    dayGroups.get(dayKey)!.push(match)
  }

  return (
    <div className="space-y-6 p-2 sm:p-4">
      <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>{sorted.length} kamper</p>
      {[...dayGroups.entries()].map(([dayKey, dayMatches]) => (
        <div key={dayKey}>
          <h3
            className="mb-3 text-sm font-semibold uppercase tracking-wide"
            style={{ color: 'var(--color-text-secondary)' }}
          >
            {formatMatchDate(dayMatches[0].date)}
          </h3>
          <div className="space-y-3">
            {dayMatches.map(match => (
              <MatchCard key={match.id} match={match} teams={teams} venues={venues} locked={isLocked(match)} onTipClick={onTipClick} onViewOthers={onViewOthers} />
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}
