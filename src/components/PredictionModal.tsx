import { useState } from 'react'
import type { Match, Team } from '../types'
import { usePredictions } from '../context/PredictionsContext'
import { isStageLocked } from '../utils/dateUtils'
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
  const homeDisplay = homeTeam?.name ?? match.homePlaceholder ?? 'TBD'
  const awayDisplay = awayTeam?.name ?? match.awayPlaceholder ?? 'TBD'
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

    if (isStageLocked(match.stage, allMatches)) {
      setError('Tipping er stengt for denne runden')
      return
    }

    setIsSaving(true)
    setError(null)

    try {
      await savePrediction(match.id, home, away)
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Kunne ikke lagre tippet')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-6 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-900">Tipp resultat</h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
          >
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="flex items-center justify-center gap-4">
            <div className="flex flex-1 flex-col items-center gap-2">
              <span className="text-sm font-medium text-gray-700">
                {homeFlag ? `${homeFlag} ` : ''}{homeDisplay}
              </span>
              <input
                type="number"
                min="0"
                max="99"
                value={homeScore}
                onChange={(e) => setHomeScore(e.target.value)}
                className="w-20 rounded-lg border border-gray-300 p-3 text-center text-2xl font-bold focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
                placeholder="0"
                autoFocus
              />
            </div>

            <span className="mt-6 text-lg font-medium text-gray-400">–</span>

            <div className="flex flex-1 flex-col items-center gap-2">
              <span className="text-sm font-medium text-gray-700">
                {awayFlag ? `${awayFlag} ` : ''}{awayDisplay}
              </span>
              <input
                type="number"
                min="0"
                max="99"
                value={awayScore}
                onChange={(e) => setAwayScore(e.target.value)}
                className="w-20 rounded-lg border border-gray-300 p-3 text-center text-2xl font-bold focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
                placeholder="0"
              />
            </div>
          </div>

          {error ? (
            <p className="mt-4 text-center text-sm text-red-600">{error}</p>
          ) : null}

          <div className="mt-6 flex gap-3">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-lg border border-gray-300 px-4 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Avbryt
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="flex-1 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {isSaving ? 'Lagrer...' : existing ? 'Oppdater tipp' : 'Lagre tipp'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
