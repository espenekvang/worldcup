import { useEffect, useState } from 'react'
import type { Match, Team } from '../types'
import { getMatchPredictions, type MatchPredictionResponse } from '../api/client'

interface OtherPredictionsModalProps {
  match: Match
  teams: Record<string, Team>
  onClose: () => void
}

export default function OtherPredictionsModal({ match, teams, onClose }: OtherPredictionsModalProps) {
  const [predictions, setPredictions] = useState<MatchPredictionResponse[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const homeTeam = match.homeTeam ? teams[match.homeTeam] : null
  const awayTeam = match.awayTeam ? teams[match.awayTeam] : null
  const homeDisplay = homeTeam?.name ?? match.homePlaceholder ?? 'Ikke avgjort'
  const awayDisplay = awayTeam?.name ?? match.awayPlaceholder ?? 'Ikke avgjort'

  useEffect(() => {
    let cancelled = false

    getMatchPredictions(match.id)
      .then(data => {
        if (!cancelled) setPredictions(data)
      })
      .catch(err => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Kunne ikke hente bets')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => { cancelled = true }
  }, [match.id])

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

        {loading ? (
          <p className="text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>Laster...</p>
        ) : error ? (
          <p className="text-center text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
        ) : predictions.length === 0 ? (
          <p className="text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>Ingen har bettet på denne kampen ennå</p>
        ) : (
          <ul className="divide-y" style={{ borderColor: 'var(--color-border-light)' }}>
            {predictions.map((p, i) => (
              <li key={i} className="flex items-center justify-between py-3">
                <div className="flex items-center gap-3">
                  {p.picture ? (
                    <img src={p.picture} alt="" className="h-8 w-8 rounded-full" referrerPolicy="no-referrer" />
                  ) : (
                    <div
                      className="flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium"
                      style={{ backgroundColor: 'var(--color-surface-elevated)', color: 'var(--color-text-muted)' }}
                    >
                      {p.name.charAt(0)}
                    </div>
                  )}
                  <span className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>{p.name}</span>
                </div>
                {p.homeScore !== null && p.awayScore !== null ? (
                  <div className="flex items-center gap-2">
                    <span
                      className="rounded-md px-2 py-1 text-sm font-bold"
                      style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
                    >
                      {p.homeScore} – {p.awayScore}
                    </span>
                    {p.points !== null && (
                      <span
                        className="rounded-md px-2 py-1 text-xs font-bold"
                        style={{ backgroundColor: 'var(--color-badge-bg)', color: 'var(--color-badge-text)' }}
                      >
                        {p.points}p
                      </span>
                    )}
                  </div>
                ) : (
                  <span
                    className="rounded-md px-2 py-1 text-xs font-medium"
                    style={{ backgroundColor: 'var(--color-badge-bg)', color: 'var(--color-badge-text)' }}
                  >
                    Har bettet
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}

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
