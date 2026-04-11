import { useState } from 'react'
import type { Match, Team } from '../types'
import { usePredictions } from '../context/PredictionsContext'
import { isStageLocked, areTeamsUndetermined } from '../utils/dateUtils'
import { matches as allMatches } from '../data'

interface PredictionModalProps {
  match: Match
  teams: Record<string, Team>
  onClose: () => void
}

export default function PredictionModal({ match, teams, onClose }: PredictionModalProps) {
  const { predictions, savePrediction } = usePredictions()
  const existing = predictions.get(match.id)

  const [homeScore, setHomeScore] = useState<string>(existing ? String(existing.homeScore) : '')
  const [awayScore, setAwayScore] = useState<string>(existing ? String(existing.awayScore) : '')
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const homeTeam = match.homeTeam ? teams[match.homeTeam] : null
  const awayTeam = match.awayTeam ? teams[match.awayTeam] : null
  const homeDisplay = homeTeam?.name ?? match.homePlaceholder ?? 'Ikke avgjort'
  const awayDisplay = awayTeam?.name ?? match.awayPlaceholder ?? 'Ikke avgjort'
  const homeFlag = homeTeam?.flag ?? ''
  const awayFlag = awayTeam?.flag ?? ''

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()

    const home = parseInt(homeScore, 10)
    const away = parseInt(awayScore, 10)

    if (isNaN(home) || isNaN(away) || home < 0 || away < 0) {
      setError('Skriv inn gyldige resultater (0 eller høyere)')
      return
    }

    if (areTeamsUndetermined(match)) {
      setError('Lagene er ikke avgjort ennå – betting er stengt')
      return
    }

    if (isStageLocked(match.stage, allMatches)) {
      setError('Betting er stengt for denne runden')
      return
    }

    setIsSaving(true)
    setError(null)

    try {
      await savePrediction(match.id, home, away)
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Kunne ikke lagre bettet')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4"
      style={{ backgroundColor: 'var(--color-surface-overlay)' }}
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-t-xl p-5 shadow-xl sm:rounded-xl sm:p-6"
        style={{ backgroundColor: 'var(--color-surface-card)' }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-6 flex items-center justify-between">
          <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Bet resultat</h2>
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

        <form onSubmit={handleSubmit}>
          <div className="flex items-center justify-center gap-4">
            <div className="flex flex-1 flex-col items-center gap-2">
              <span className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>
                {homeFlag ? `${homeFlag} ` : ''}{homeDisplay}
              </span>
              <input
                type="number"
                inputMode="numeric"
                min="0"
                max="99"
                step="1"
                value={homeScore}
                onChange={(e) => setHomeScore(e.target.value)}
                onFocus={(e) => { if (e.target.value === '') setHomeScore('0') }}
                className="w-20 rounded-lg border p-3 text-center text-2xl font-bold focus:outline-none focus:ring-2"
                style={{
                  backgroundColor: 'var(--color-surface-card)',
                  borderColor: 'var(--color-input-border)',
                  color: 'var(--color-text-primary)',
                }}
                placeholder="0"
                autoFocus
              />
            </div>

            <span className="mt-6 text-lg font-medium" style={{ color: 'var(--color-text-muted)' }}>–</span>

            <div className="flex flex-1 flex-col items-center gap-2">
              <span className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>
                {awayFlag ? `${awayFlag} ` : ''}{awayDisplay}
              </span>
              <input
                type="number"
                inputMode="numeric"
                min="0"
                max="99"
                step="1"
                value={awayScore}
                onChange={(e) => setAwayScore(e.target.value)}
                onFocus={(e) => { if (e.target.value === '') setAwayScore('0') }}
                className="w-20 rounded-lg border p-3 text-center text-2xl font-bold focus:outline-none focus:ring-2"
                style={{
                  backgroundColor: 'var(--color-surface-card)',
                  borderColor: 'var(--color-input-border)',
                  color: 'var(--color-text-primary)',
                }}
                placeholder="0"
              />
            </div>
          </div>

          {error ? (
            <p className="mt-4 text-center text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
          ) : null}

          <div className="mt-6 flex gap-3">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-lg border px-4 py-3 text-sm font-medium transition-colors sm:py-2.5"
              style={{
                borderColor: 'var(--color-border)',
                color: 'var(--color-text-secondary)',
                backgroundColor: 'var(--color-surface-card)',
              }}
            >
              Avbryt
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="flex-1 rounded-lg px-4 py-3 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2.5"
              style={{ backgroundColor: 'var(--color-primary)' }}
            >
              {isSaving ? 'Lagrer...' : existing ? 'Oppdater bet' : 'Lagre bet'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
