import { useEffect, useState } from 'react'
import { getMatchPredictions, type MatchPredictionResponse } from '../api/client'
import { useBettingGroup } from '../context/BettingGroupContext'
import { displayName } from '../utils/nameUtils'

interface MatchPredictionsListProps {
  matchId: number
  /**
   * Visningsvariant.
   * - "modal": eldre kompakt visning brukt i OtherPredictionsModal.
   * - "page": uten yttermargin, brukt på matchdetalj-siden.
   */
  variant?: 'modal' | 'page'
}

/**
 * Henter og viser alle gruppemedlemmenes tips for én kamp.
 * Hentet ut av OtherPredictionsModal slik at både modalen og
 * detaljsiden kan gjenbruke samme rendering.
 */
export default function MatchPredictionsList({ matchId, variant = 'modal' }: MatchPredictionsListProps) {
  const { activeGroup } = useBettingGroup()
  const [predictions, setPredictions] = useState<MatchPredictionResponse[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Følger ligaens navnevisning, samme innstilling som "The Boss"-listen.
  const showFullName = activeGroup?.showFullName ?? false

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)

    getMatchPredictions(matchId)
      .then(data => { if (!cancelled) setPredictions(data) })
      .catch(err => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Kunne ikke hente bets')
      })
      .finally(() => { if (!cancelled) setLoading(false) })

    return () => { cancelled = true }
  }, [matchId])

  if (loading) {
    return <p className="text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>Laster...</p>
  }
  if (error) {
    return <p className="text-center text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
  }
  if (predictions.length === 0) {
    return (
      <p className="text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>
        Ingen har bettet på denne kampen ennå
      </p>
    )
  }

  return (
    <ul className={variant === 'page' ? 'divide-y' : 'divide-y'} style={{ borderColor: 'var(--color-border-light)' }}>
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
                {displayName(p.name ?? '', showFullName, p.displayName).charAt(0)}
              </div>
            )}
            <span className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>{displayName(p.name ?? '', showFullName, p.displayName)}</span>
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
  )
}
