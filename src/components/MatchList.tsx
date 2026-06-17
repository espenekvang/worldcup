import { useEffect, useState } from 'react'
import type { Match, Team, Venue, Stage } from '../types'
import { formatMatchDate, getLocalDateKey, isMatchLocked, areTeamsUndetermined, isMatchInProgress, isToday } from '../utils/dateUtils'
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

type MatchView = 'remaining' | 'finished'

export default function MatchList({ matches, teams, venues, activeStage, onTipClick, onViewOthers }: MatchListProps) {
  const { results } = useResults()
  const [view, setView] = useState<MatchView>('remaining')

  // Reset til "gjenstående" når brukeren bytter runde, slik at man ikke havner
  // i en tom "ferdigspilte"-visning for en runde uten spilte kamper.
  useEffect(() => {
    setView('remaining')
  }, [activeStage])

  const filtered = matches.filter(m => {
    if (activeStage === 'final') return m.stage === 'final' || m.stage === 'third-place'
    return m.stage === activeStage
  })

  const sorted = [...filtered].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())

  const isLocked = (match: Match) => isMatchLocked(match) || areTeamsUndetermined(match)

  // En kamp regnes som ferdigspilt når den har et registrert resultat.
  const isFinished = (match: Match) => results.has(match.id)
  // Vis nyeste ferdigspilte kamp øverst.
  const finishedMatches = sorted.filter(isFinished).reverse()
  const remainingMatches = sorted.filter(m => !isFinished(m))

  // Find the globally next upcoming unlocked match (across all stages)
  const now = new Date()
  const globalNextMatch = [...matches]
    .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
    .find(m => new Date(m.date) > now && !isLocked(m))

  // Only show "Neste kamp" if it belongs to the currently active stage
  const nextMatch = remainingMatches.find(m => m.id === globalNextMatch?.id) || null

  // Matches in the active stage that have kicked off but have no result yet
  const inProgressMatches = remainingMatches.filter(m => isMatchInProgress(m, results.has(m.id)))
  const inProgressIds = new Set(inProgressMatches.map(m => m.id))

  // Remove the pinned and in-progress matches from the regular list
  const regularMatches = remainingMatches.filter(m => m.id !== nextMatch?.id && !inProgressIds.has(m.id))

  const groupByDay = (list: Match[]) => {
    const dayGroups = new Map<string, Match[]>()
    for (const match of list) {
      const dayKey = getLocalDateKey(match.date)
      if (!dayGroups.has(dayKey)) dayGroups.set(dayKey, [])
      dayGroups.get(dayKey)!.push(match)
    }
    return dayGroups
  }

  const regularDayGroups = groupByDay(regularMatches)
  const finishedDayGroups = groupByDay(finishedMatches)

  // Dager som allerede er representert av de festede "Neste kamp"- og "Pågår nå"-
  // seksjonene over. For disse dagene dropper vi den overflødige dag/dato-
  // overskriften i den vanlige listen.
  const highlightedDayKeys = new Set<string>()
  if (nextMatch) highlightedDayKeys.add(getLocalDateKey(nextMatch.date))
  for (const match of inProgressMatches) highlightedDayKeys.add(getLocalDateKey(match.date))

  return (
    <div className="space-y-6 p-2 sm:p-4">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
        <button
          type="button"
          onClick={() => setView('remaining')}
          className="transition-colors"
          style={{
            color: view === 'remaining' ? 'var(--color-primary)' : 'var(--color-text-muted)',
            fontWeight: view === 'remaining' ? 600 : 400,
          }}
          aria-pressed={view === 'remaining'}
        >
          {remainingMatches.length} gjenstående kamper
        </button>
        <button
          type="button"
          onClick={() => setView('finished')}
          className="transition-colors"
          style={{
            color: view === 'finished' ? 'var(--color-primary)' : 'var(--color-text-muted)',
            fontWeight: view === 'finished' ? 600 : 400,
          }}
          aria-pressed={view === 'finished'}
        >
          {finishedMatches.length} ferdigspilte kamper
        </button>
      </div>

      {view === 'finished' ? (
        finishedMatches.length === 0 ? (
          <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>
            Ingen ferdigspilte kamper i denne runden ennå.
          </p>
        ) : (
          [...finishedDayGroups.entries()].map(([dayKey, dayMatches]) => (
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
          ))
        )
      ) : (
        <>
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
                ⚽ {isToday(nextMatch.date) ? 'Neste kamp i dag' : <>Neste kamp &middot; {formatMatchDate(nextMatch.date)}</>}
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

          {regularMatches.length === 0 && !nextMatch && inProgressMatches.length === 0 && (
            <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>
              Ingen gjenstående kamper i denne runden.
            </p>
          )}

          {[...regularDayGroups.entries()].map(([dayKey, dayMatches]) => (
            <div key={dayKey}>
              {!highlightedDayKeys.has(dayKey) && (
                <h3
                  className="mb-3 text-sm font-semibold uppercase tracking-wide"
                  style={{ color: 'var(--color-text-secondary)' }}
                >
                  {formatMatchDate(dayMatches[0].date)}
                </h3>
              )}
              <div className="space-y-3">
                {dayMatches.map(match => (
                  <MatchCard key={match.id} match={match} teams={teams} venues={venues} locked={isLocked(match)} onTipClick={onTipClick} onViewOthers={onViewOthers} />
                ))}
              </div>
            </div>
          ))}
        </>
      )}
    </div>
  )
}
