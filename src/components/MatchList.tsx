import type { Match, Team, Venue, Stage } from '../types'
import { formatMatchDate, getLocalDateKey, isMatchLocked, areTeamsUndetermined } from '../utils/dateUtils'
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

  const isLocked = (match: Match) => isMatchLocked(match) || areTeamsUndetermined(match)

  // Find the globally next upcoming unlocked match (across all stages)
  const now = new Date()
  const globalNextMatch = [...matches]
    .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
    .find(m => new Date(m.date) > now && !isLocked(m))

  // Only show "Neste kamp" if it belongs to the currently active stage
  const nextMatch = sorted.find(m => m.id === globalNextMatch?.id) || null

  // Remove the pinned match from the regular list
  const regularMatches = nextMatch ? sorted.filter(m => m.id !== nextMatch.id) : sorted

  const dayGroups = new Map<string, Match[]>()
  for (const match of regularMatches) {
    const dayKey = getLocalDateKey(match.date)
    if (!dayGroups.has(dayKey)) dayGroups.set(dayKey, [])
    dayGroups.get(dayKey)!.push(match)
  }

  return (
    <div className="space-y-6 p-2 sm:p-4">
      <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>{sorted.length} kamper</p>

      {nextMatch && (
        <div>
          <h3
            className="mb-3 text-sm font-semibold uppercase tracking-wide"
            style={{ color: 'var(--color-primary)' }}
          >
            ⚽ Neste kamp &middot; {formatMatchDate(nextMatch.date)}
          </h3>
          <MatchCard
            match={nextMatch}
            teams={teams}
            venues={venues}
            locked={isLocked(nextMatch)}
            isNext
            onTipClick={onTipClick}
            onViewOthers={onViewOthers}
          />
        </div>
      )}

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
