import type { Match, Team, Venue } from '../types'
import { formatMatchDate, formatMatchTime } from '../utils/dateUtils'
import { usePredictions } from '../context/PredictionsContext'

interface MatchCardProps {
  match: Match
  teams: Record<string, Team>
  venues: Venue[]
  locked: boolean
  onTipClick: (match: Match) => void
  onViewOthers: (match: Match) => void
}

const STAGE_LABELS: Record<string, string> = {
  'group': 'Gruppespill',
  'round-of-32': '32-delsfinale',
  'round-of-16': '8-delsfinale',
  'quarter-final': 'Kvartfinale',
  'semi-final': 'Semifinale',
  'third-place': 'Bronsefinale',
  'final': 'Finale',
}

export default function MatchCard({ match, teams, venues, locked, onTipClick, onViewOthers }: MatchCardProps) {
  const { predictions } = usePredictions()
  const prediction = predictions.get(match.id)
  const venue = venues.find(v => v.id === match.venueId)
  const isGroupStage = match.stage === 'group'

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
    homeDisplay = match.homePlaceholder ?? 'Ikke avgjort'
    awayDisplay = match.awayPlaceholder ?? 'Ikke avgjort'
  }

  return (
    <div data-testid="match-card" className="rounded-lg border border-gray-200 bg-white p-3 shadow-sm sm:p-4">
      <div className="flex items-center justify-between text-xs text-gray-500 sm:text-sm">
        <div>
          <span>{formatMatchDate(match.date)}</span>
          <span className="mx-1">·</span>
          <span>{formatMatchTime(match.date)}</span>
        </div>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600">
          {isGroupStage ? `Gruppe ${match.group}` : STAGE_LABELS[match.stage] ?? match.stage}
        </span>
      </div>

      <div className="mt-3 flex items-center justify-center gap-2 text-base font-semibold text-gray-900 sm:gap-3 sm:text-lg">
        <span className="min-w-0 truncate text-right">
          {homeFlag ? `${homeFlag} ` : ''}{homeDisplay}
        </span>
        <span className="shrink-0 text-sm font-normal text-gray-400">mot</span>
        <span className="min-w-0 truncate text-left">
          {awayFlag ? `${awayFlag} ` : ''}{awayDisplay}
        </span>
      </div>

      {venue ? (
        <p className="mt-2 text-center text-sm text-gray-500">
          {venue.name}, {venue.city}
        </p>
      ) : null}

      <div className="mt-3 border-t border-gray-100 pt-3">
        {locked ? (
          <div className="flex items-center justify-between">
            {prediction ? (
              <div className="flex items-center gap-2">
                <span className="text-xs font-medium text-green-600">Ditt bet:</span>
                <span className="rounded-md bg-green-50 px-2 py-1 text-sm font-bold text-green-700">
                  {prediction.homeScore} – {prediction.awayScore}
                </span>
              </div>
            ) : (
              <span className="text-xs text-gray-400">Ingen bet registrert</span>
            )}
            <span className="flex items-center gap-1 text-xs text-gray-400" title="Betting er stengt for denne runden">
              <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2" strokeWidth={2} />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" strokeWidth={2} strokeLinecap="round" />
              </svg>
              Låst
            </span>
          </div>
        ) : prediction ? (
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-xs font-medium text-green-600">Ditt bet:</span>
              <span className="rounded-md bg-green-50 px-2 py-1 text-sm font-bold text-green-700">
                {prediction.homeScore} – {prediction.awayScore}
              </span>
            </div>
            <button
              onClick={() => onTipClick(match)}
              className="rounded-md px-3 py-1 text-xs font-medium text-blue-600 hover:bg-blue-50"
            >
              Endre
            </button>
          </div>
        ) : (
          <button
            onClick={() => onTipClick(match)}
            className="w-full rounded-md bg-blue-50 px-3 py-2 text-sm font-medium text-blue-600 hover:bg-blue-100"
          >
            Bet resultat
          </button>
        )}
      </div>

      <button
        onClick={() => onViewOthers(match)}
        className="mt-2 w-full text-center text-xs font-medium text-gray-400 hover:text-gray-600"
      >
        Se andres bets
      </button>
    </div>
  )
}
