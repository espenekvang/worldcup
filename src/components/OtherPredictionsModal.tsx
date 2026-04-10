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
  const homeDisplay = homeTeam?.name ?? match.homePlaceholder ?? 'TBD'
  const awayDisplay = awayTeam?.name ?? match.awayPlaceholder ?? 'TBD'

  useEffect(() => {
    let cancelled = false

    getMatchPredictions(match.id)
      .then(data => {
        if (!cancelled) setPredictions(data)
      })
      .catch(err => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Kunne ikke hente tipp')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => { cancelled = true }
  }, [match.id])

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 p-0 sm:items-center sm:p-4" onClick={onClose}>
      <div
        className="max-h-[85vh] w-full max-w-md overflow-y-auto rounded-t-xl bg-white p-5 shadow-xl sm:rounded-xl sm:p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-900">Andres tipp</h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
          >
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <p className="mb-4 text-center text-sm text-gray-500">
          {homeDisplay} mot {awayDisplay}
        </p>

        {loading ? (
          <p className="text-center text-sm text-gray-400">Laster...</p>
        ) : error ? (
          <p className="text-center text-sm text-red-600">{error}</p>
        ) : predictions.length === 0 ? (
          <p className="text-center text-sm text-gray-400">Ingen har tippet på denne kampen ennå</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {predictions.map((p, i) => (
              <li key={i} className="flex items-center justify-between py-3">
                <div className="flex items-center gap-3">
                  {p.picture ? (
                    <img src={p.picture} alt="" className="h-8 w-8 rounded-full" referrerPolicy="no-referrer" />
                  ) : (
                    <div className="flex h-8 w-8 items-center justify-center rounded-full bg-gray-200 text-sm font-medium text-gray-500">
                      {p.name.charAt(0)}
                    </div>
                  )}
                  <span className="text-sm font-medium text-gray-800">{p.name}</span>
                </div>
                {p.homeScore !== null && p.awayScore !== null ? (
                  <span className="rounded-md bg-green-50 px-2 py-1 text-sm font-bold text-green-700">
                    {p.homeScore} – {p.awayScore}
                  </span>
                ) : (
                  <span className="rounded-md bg-gray-100 px-2 py-1 text-xs font-medium text-gray-500">
                    Har tippet
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}

        <div className="mt-4">
          <button
            onClick={onClose}
            className="w-full rounded-lg border border-gray-300 px-4 py-3 text-sm font-medium text-gray-700 hover:bg-gray-50 sm:py-2.5"
          >
            Lukk
          </button>
        </div>
      </div>
    </div>
  )
}
