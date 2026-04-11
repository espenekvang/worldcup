import type { Match, Team, Venue } from '../types'
import { formatMatchTime } from '../utils/dateUtils'
import { usePredictions } from '../context/PredictionsContext'
import { useResults } from '../context/ResultsContext'

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
  const { results, points } = useResults()
  
  const prediction = predictions.get(match.id)
  const result = results.get(match.id)
  const pts = points.get(match.id)

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

  const stageLabel = isGroupStage ? `Gruppe ${match.group}` : STAGE_LABELS[match.stage] ?? match.stage

  return (
    <div
      data-testid="match-card"
      className="rounded-lg border px-3 py-2 shadow-sm transition-colors sm:px-4 sm:py-2.5"
      style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
    >
      <div className="flex items-center gap-3">
        <div className="flex w-20 shrink-0 flex-col items-center text-xs" style={{ color: 'var(--color-text-muted)' }}>
          <span className="font-medium">{formatMatchTime(match.date)}</span>
          <span
            className="mt-0.5 whitespace-nowrap rounded-full px-1.5 py-px text-[10px] font-medium"
            style={{ backgroundColor: 'var(--color-badge-bg)', color: 'var(--color-badge-text)' }}
          >
            {stageLabel}
          </span>
        </div>

        <div className="flex min-w-0 flex-1 items-center justify-center gap-1.5 text-sm font-semibold sm:gap-2 sm:text-base" style={{ color: 'var(--color-text-primary)' }}>
          <span className="min-w-0 truncate text-right">
            {homeFlag ? `${homeFlag} ` : ''}{homeDisplay}
          </span>
          {result ? (
            <span className="shrink-0 font-bold" style={{ color: 'var(--color-text-primary)' }}>
              {result.homeScore} – {result.awayScore}
            </span>
          ) : (
            <span className="shrink-0 text-xs font-normal" style={{ color: 'var(--color-text-muted)' }}>–</span>
          )}
          <span className="min-w-0 truncate text-left">
            {awayFlag ? `${awayFlag} ` : ''}{awayDisplay}
          </span>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          {result && pts !== undefined && (
            <span
              className="rounded-md px-1.5 py-0.5 text-xs font-bold"
              style={{
                backgroundColor: pts.points === 4 ? '#fef9c3' : pts.points >= 2 ? 'var(--color-success-light)' : pts.points === 1 ? '#fff7ed' : '#fee2e2',
                color: pts.points === 4 ? '#854d0e' : pts.points >= 2 ? 'var(--color-success-text)' : pts.points === 1 ? '#9a3412' : '#991b1b',
              }}
            >
              {pts.points}p
            </span>
          )}
          {locked ? (
            <>
              {prediction ? (
                <span
                  className="rounded-md px-1.5 py-0.5 text-xs font-bold"
                  style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
                >
                  {prediction.homeScore}–{prediction.awayScore}
                </span>
              ) : (
                <span className="text-xs" style={{ color: 'var(--color-text-muted)' }}>—</span>
              )}
              <svg className="h-3 w-3" style={{ color: 'var(--color-text-muted)' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2" strokeWidth={2} />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" strokeWidth={2} strokeLinecap="round" />
              </svg>
            </>
          ) : prediction ? (
            <>
              <span
                className="rounded-md px-1.5 py-0.5 text-xs font-bold"
                style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
              >
                {prediction.homeScore}–{prediction.awayScore}
              </span>
              <button
                onClick={() => onTipClick(match)}
                className="rounded-md px-2 py-0.5 text-xs font-medium"
                style={{ color: 'var(--color-primary)' }}
              >
                Endre
              </button>
            </>
          ) : (
            <button
              onClick={() => onTipClick(match)}
              className="rounded-md px-2.5 py-1 text-xs font-medium transition-colors"
              style={{ backgroundColor: 'var(--color-primary-light)', color: 'var(--color-primary)' }}
            >
              Bet
            </button>
          )}
          <button
            onClick={() => onViewOthers(match)}
            className="text-xs font-medium transition-colors"
            style={{ color: 'var(--color-text-muted)' }}
          >
            👥
          </button>
        </div>
      </div>

      {venue ? (
        <p className="mt-0.5 text-center text-[11px]" style={{ color: 'var(--color-text-muted)' }}>
          {venue.name}, {venue.city}
        </p>
      ) : null}
    </div>
  )
}
