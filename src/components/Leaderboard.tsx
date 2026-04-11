import { useEffect, useState } from 'react'
import { getLeaderboard, type LeaderboardEntry } from '../api/client'

export default function Leaderboard() {
  const [entries, setEntries] = useState<LeaderboardEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    getLeaderboard()
      .then(data => {
        if (!cancelled) setEntries(data)
      })
      .catch(err => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Kunne ikke hente poengoversikt')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => { cancelled = true }
  }, [])

  if (loading) {
    return (
      <div className="p-2 sm:p-4">
        <p className="text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>Laster...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="p-2 sm:p-4">
        <p className="text-center text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
      </div>
    )
  }

  if (entries.length === 0) {
    return (
      <div className="p-2 sm:p-4">
        <p className="text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>
          Ingen deltakere ennå
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-3 p-2 sm:p-4">
      <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>{entries.length} deltakere</p>
      <div
        className="overflow-hidden rounded-xl"
        style={{ backgroundColor: 'var(--color-surface-card)', border: '1px solid var(--color-border-light)' }}
      >
        {entries.map((entry, i) => (
          <div
            key={entry.name}
            className="flex items-center justify-between px-4 py-3"
            style={{
              borderBottom: i < entries.length - 1 ? '1px solid var(--color-border-light)' : undefined,
            }}
          >
            <div className="flex items-center gap-3">
              <span
                className="flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold"
                style={{
                  backgroundColor: i === 0 ? 'var(--color-success-light)' : 'var(--color-surface-elevated)',
                  color: i === 0 ? 'var(--color-success-text)' : 'var(--color-text-muted)',
                }}
              >
                {i + 1}
              </span>
              {entry.picture ? (
                <img src={entry.picture} alt="" className="h-8 w-8 rounded-full" referrerPolicy="no-referrer" />
              ) : (
                <div
                  className="flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium"
                  style={{ backgroundColor: 'var(--color-surface-elevated)', color: 'var(--color-text-muted)' }}
                >
                  {entry.name.charAt(0)}
                </div>
              )}
              <div>
                <span className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
                  {entry.name}
                </span>
                <span className="ml-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>
                  {entry.matchCount} {entry.matchCount === 1 ? 'kamp' : 'kamper'}
                </span>
              </div>
            </div>
            <span
              className="rounded-md px-2.5 py-1 text-sm font-bold"
              style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
            >
              {entry.totalPoints}p
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}
