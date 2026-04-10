import type { Match, Team, Venue, Stage } from '../types'
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

  if (activeStage === 'group') {
    const groups = new Map<string, Match[]>()
    for (const match of sorted) {
      const group = match.group ?? '?'
      if (!groups.has(group)) groups.set(group, [])
      groups.get(group)!.push(match)
    }

    const sortedGroups = [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))

    return (
      <div className="space-y-6 p-4">
        <p className="text-sm text-gray-500">{sorted.length} kamper</p>
        {sortedGroups.map(([group, groupMatches]) => (
          <div key={group}>
            <h3 className="mb-3 text-lg font-semibold text-gray-800">Gruppe {group}</h3>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {groupMatches.map(match => (
                <MatchCard key={match.id} match={match} teams={teams} venues={venues} locked={isLocked(match)} onTipClick={onTipClick} onViewOthers={onViewOthers} />
              ))}
            </div>
          </div>
        ))}
      </div>
    )
  }

  return (
    <div className="space-y-3 p-4">
      <p className="text-sm text-gray-500">{sorted.length} kamper</p>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {sorted.map(match => (
          <MatchCard key={match.id} match={match} teams={teams} venues={venues} locked={isLocked(match)} onTipClick={onTipClick} onViewOthers={onViewOthers} />
        ))}
      </div>
    </div>
  )
}
