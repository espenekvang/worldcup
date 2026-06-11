import type { Match, Team, Venue, Stage } from '../types'
import { formatMatchDate, getLocalDateKey, isMatchLocked, areTeamsUndetermined, isMatchInProgress } from '../utils/dateUtils'
import { useResults } from '../context/ResultsContext'
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
  const { results } = useResults()

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

  // Matches in the active stage that have kicked off but have no result yet
  const inProgressMatches = sorted.filter(m => isMatchInProgress(m, results.has(m.id)))
  const inProgressIds = new Set(inProgressMatches.map(m => m.id))

  // Remove the pinned and in-progress matches from the regular list
  const regularMatches = sorted.filter(m => m.id !== nextMatch?.id && !inProgressIds.has(m.id))

  const dayGroups = new Map<string, Match[]>()
  for (const match of regularMatches) {
    const dayKey = getLocalDateKey(match.date)
    if (!dayGroups.has(dayKey)) dayGroups.set(dayKey, [])
    dayGroups.get(dayKey)!.push(match)
  }

  return (
    <div className="space-y-6 p-2 sm:p-4">
      <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>{sorted.length} kamper</p>

      {inProgressMatches.length > 0 && (
        <div>
          <h3
            className="mb-3 inline-flex items-center gap-2 text-sm font-semibold uppercase tracking-wide"
            style={{ color: 'var(--color-danger)' }}
          >
            <span className="h-2 w-2 animate-pulse rounded-full" style={{ backgroundColor: 'var(--color-danger)' }} />
            Pågår nå
          </h3>
          <div className="space-y-3">
            {inProgressMatches.map(match => (
              <MatchCard
                key={match.id}
                match={match}
                teams={teams}
                venues={venues}
                locked={isLocked(match)}
                isLive
                onTipClick={onTipClick}
                onViewOthers={onViewOthers}
              />
            ))}
          </div>
        </div>
      )}

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
