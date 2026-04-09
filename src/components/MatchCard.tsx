import type { Match, Team, Venue } from '../types'
import { formatMatchDate, formatMatchTime } from '../utils/dateUtils'

interface MatchCardProps {
  match: Match
  teams: Record<string, Team>
  venues: Venue[]
}

const STAGE_LABELS: Record<string, string> = {
  'group': 'Group Stage',
  'round-of-32': 'Round of 32',
  'round-of-16': 'Round of 16',
  'quarter-final': 'Quarter-final',
  'semi-final': 'Semi-final',
  'third-place': 'Third Place',
  'final': 'Final',
}

export default function MatchCard({ match, teams, venues }: MatchCardProps) {
  const venue = venues.find(v => v.id === match.venueId)
  const isGroupStage = match.stage === 'group'

  // Team names or placeholders
  let homeDisplay: string
  let awayDisplay: string
  let homeFlag = ''
  let awayFlag = ''

  if (match.homeTeam && match.awayTeam) {
    const home = teams[match.homeTeam]
    const away = teams[match.awayTeam]
    homeDisplay = home?.name ?? match.homeTeam
    awayDisplay = away?.name ?? match.awayTeam
    homeFlag = home?.flag ?? ''
    awayFlag = away?.flag ?? ''
  } else {
    homeDisplay = match.homePlaceholder ?? 'TBD'
    awayDisplay = match.awayPlaceholder ?? 'TBD'
  }

  return (
    <div data-testid="match-card" className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      {/* Header: date/time + stage/group badge */}
      <div className="flex items-center justify-between text-sm text-gray-500">
        <div>
          <span>{formatMatchDate(match.date)}</span>
          <span className="mx-1">·</span>
          <span>{formatMatchTime(match.date)}</span>
        </div>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600">
          {isGroupStage ? `Group ${match.group}` : STAGE_LABELS[match.stage] ?? match.stage}
        </span>
      </div>

      {/* Teams */}
      <div className="mt-3 flex items-center justify-center gap-3 text-lg font-semibold text-gray-900">
        <span className="text-right">
          {homeFlag ? `${homeFlag} ` : ''}{homeDisplay}
        </span>
        <span className="text-sm font-normal text-gray-400">vs</span>
        <span className="text-left">
          {awayFlag ? `${awayFlag} ` : ''}{awayDisplay}
        </span>
      </div>

      {/* Venue */}
      {venue ? (
        <p className="mt-2 text-center text-sm text-gray-500">
          {venue.name}, {venue.city}
        </p>
      ) : null}
    </div>
  )
}
