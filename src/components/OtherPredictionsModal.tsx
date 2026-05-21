import type { Match, Team } from '../types'
import MatchPredictionsList from './MatchPredictionsList'

interface OtherPredictionsModalProps {
  match: Match
  teams: Record<string, Team>
  onClose: () => void
}

export default function OtherPredictionsModal({ match, teams, onClose }: OtherPredictionsModalProps) {
  const homeTeam = match.homeTeam ? teams[match.homeTeam] : null
  const awayTeam = match.awayTeam ? teams[match.awayTeam] : null
  const homeDisplay = homeTeam?.name ?? match.homePlaceholder ?? 'Ikke avgjort'
  const awayDisplay = awayTeam?.name ?? match.awayPlaceholder ?? 'Ikke avgjort'

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4"
      style={{ backgroundColor: 'var(--color-surface-overlay)' }}
      onClick={onClose}
    >
      <div
        className="max-h-[85vh] w-full max-w-md overflow-y-auto rounded-t-xl p-5 shadow-xl sm:rounded-xl sm:p-6"
        style={{ backgroundColor: 'var(--color-surface-card)' }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Andres bets</h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 transition-colors"
            style={{ color: 'var(--color-text-muted)' }}
          >
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <p className="mb-4 text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>
          {homeDisplay} mot {awayDisplay}
        </p>

        <MatchPredictionsList matchId={match.id} variant="modal" />

        <div className="mt-4">
          <button
            onClick={onClose}
            className="w-full rounded-lg border px-4 py-3 text-sm font-medium transition-colors sm:py-2.5"
            style={{
              borderColor: 'var(--color-border)',
              color: 'var(--color-text-secondary)',
              backgroundColor: 'var(--color-surface-card)',
            }}
          >
            Lukk
          </button>
        </div>
      </div>
    </div>
  )
}
