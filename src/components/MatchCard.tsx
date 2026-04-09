import type { Match, Team, Venue } from '../types'
import { formatMatchDate, formatMatchTime } from '../utils/dateUtils'
import { usePredictions } from '../context/PredictionsContext'

interface MatchCardProps {
  match: Match
  teams: Record<string, Team>
  venues: Venue[]
  onTipClick: (match: Match) => void
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

export default function MatchCard({ match, teams, venues, onTipClick }: MatchCardProps) {
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
    <div data-testid="match-card" className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between text-sm text-gray-500">
        <div>
          <span>{formatMatchDate(match.date)}</span>
          <span className="mx-1">·</span>
          <span>{formatMatchTime(match.date)}</span>
        </div>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600">
          {isGroupStage ? `Gruppe ${match.group}` : STAGE_LABELS[match.stage] ?? match.stage}
        </span>
      </div>

      <div className="mt-3 flex items-center justify-center gap-3 text-lg font-semibold text-gray-900">
        <span className="text-right">
          {homeFlag ? `${homeFlag} ` : ''}{homeDisplay}
        </span>
        <span className="text-sm font-normal text-gray-400">mot</span>
        <span className="text-left">
          {awayFlag ? `${awayFlag} ` : ''}{awayDisplay}
        </span>
      </div>

      {venue ? (
        <p className="mt-2 text-center text-sm text-gray-500">
          {venue.name}, {venue.city}
        </p>
      ) : null}

      <div className="mt-3 border-t border-gray-100 pt-3">
        {prediction ? (
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-xs font-medium text-green-600">Ditt tipp:</span>
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
            Tipp resultat
          </button>
        )}
      </div>
    </div>
  )
}
