import { useEffect, useMemo, useState } from 'react'
import { getLeaderboard, getGlobalLeaderboard, type LeaderboardEntry, type GlobalLeaderboardEntry } from '../api/client'
import { useBettingGroup } from '../context/BettingGroupContext'
import { firstName } from '../utils/nameUtils'

/**
 * Beregner premie per deltaker for en betalt liga.
 *
 * Regler:
 *  - Plassering bestemmes av totalPoints (likt poeng = delt plassering).
 *  - Pott fordeles 70 % / 20 % / 10 % til 1./2./3. plass.
 *  - Ved delt plassering deles pengene i den/de aktuelle "kategoriene" likt mellom delte spillere.
 *    Eks: 3 spillere deler 1. plass → de deler (70 + 20 + 10) % av potten likt.
 *    Eks: 2 spillere deler 2. plass → de deler (20 + 10) % av potten likt; 3. plass utdeles ikke separat.
 */
function calculatePrizes(
  entries: LeaderboardEntry[],
  prizePot: number,
): Map<string, number> {
  const result = new Map<string, number>()
  if (prizePot <= 0 || entries.length === 0) return result

  // Grupper deltakere etter poengsum, i samme rekkefølge som leaderboard (sortert synkende).
  const groupsByPoints: LeaderboardEntry[][] = []
  for (const entry of entries) {
    const last = groupsByPoints[groupsByPoints.length - 1]
    if (last && last[0].totalPoints === entry.totalPoints) {
      last.push(entry)
    } else {
      groupsByPoints.push([entry])
    }
  }

  const shares = [0.7, 0.2, 0.1]
  let slotIndex = 0 // hvilken premie-slot (0=1.plass, 1=2.plass, 2=3.plass) som er neste å dele ut

  for (const tieGroup of groupsByPoints) {
    if (slotIndex >= shares.length) break

    // Hvor mange slots dekker denne tie-gruppen?
    const slotsConsumed = Math.min(tieGroup.length, shares.length - slotIndex)
    let combinedShare = 0
    for (let i = 0; i < slotsConsumed; i++) {
      combinedShare += shares[slotIndex + i]
    }

    const amountPerPerson = (prizePot * combinedShare) / tieGroup.length
    for (const member of tieGroup) {
      result.set(member.name, amountPerPerson)
    }

    slotIndex += tieGroup.length
  }

  return result
}

function formatCurrency(amount: number): string {
  return `${Math.round(amount).toLocaleString('no-NO')} kr`
}

export default function Leaderboard() {
  const { activeGroup } = useBettingGroup()
  const [entries, setEntries] = useState<LeaderboardEntry[]>([])
  const [globalEntries, setGlobalEntries] = useState<GlobalLeaderboardEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!activeGroup) return

    let cancelled = false
    setLoading(true)

    Promise.all([getLeaderboard(), getGlobalLeaderboard()])
      .then(([leaderboardData, globalData]) => {
        if (!cancelled) {
          setEntries(leaderboardData)
          setGlobalEntries(globalData)
        }
      })
      .catch(err => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Kunne ikke hente poengoversikt')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => { cancelled = true }
  }, [activeGroup])

  const prizes = useMemo(() => {
    if (!activeGroup?.isPaid) return new Map<string, number>()
    return calculatePrizes(entries, activeGroup.prizePot)
  }, [entries, activeGroup])

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
      {activeGroup ? (
        <p className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>{activeGroup.name}</p>
      ) : null}
      <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>{entries.length} deltakere</p>
      {activeGroup?.isPaid && (
        <div
          className="rounded-lg border px-3 py-2 text-xs"
          style={{
            borderColor: 'var(--color-border-light)',
            backgroundColor: 'var(--color-surface-elevated)',
            color: 'var(--color-text-secondary)',
          }}
        >
          <div className="font-medium" style={{ color: 'var(--color-text-primary)' }}>
            Pott: {formatCurrency(activeGroup.prizePot)}
          </div>
          <div className="mt-0.5" style={{ color: 'var(--color-text-muted)' }}>
            Innsats {activeGroup.entryFee} kr · {activeGroup.paidMemberCount}/{activeGroup.memberCount} har betalt · 1.&nbsp;plass 70 % · 2.&nbsp;plass 20 % · 3.&nbsp;plass 10 %
            {!activeGroup.currentUserHasPaid && (
              <span style={{ color: 'var(--color-danger)' }}>
                {' '}· Du har ikke betalt – betting er stengt.
              </span>
            )}
          </div>
        </div>
      )}
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
                  {firstName(entry.name).charAt(0)}
                </div>
              )}
              <div>
                <span className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
                  {firstName(entry.name)}
                </span>
                <span className="ml-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>
                  {entry.matchCount} {entry.matchCount === 1 ? 'kamp' : 'kamper'}
                </span>
                {prizes.get(entry.name) ? (
                  <span
                    className="ml-2 rounded-full px-1.5 py-0.5 text-[10px] font-medium"
                    style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
                  >
                    {formatCurrency(prizes.get(entry.name) ?? 0)}
                  </span>
                ) : null}
              </div>
            </div>
            <span
              className="flex items-center gap-1 rounded-md px-2.5 py-1 text-sm font-bold"
              style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
            >
              {(() => {
                const currentRank = i + 1
                const prev = entry.previousRank
                if (prev == null) return null
                if (prev === currentRank) {
                  return (
                    <span
                      aria-label="Uendret plassering"
                      title="Uendret plassering"
                      style={{
                        color: 'var(--color-text-muted)',
                        fontSize: '0.85em',
                        lineHeight: 1,
                      }}
                    >
                      ▬
                    </span>
                  )
                }
                const movedUp = currentRank < prev
                return (
                  <span
                    aria-label={movedUp ? `Opp ${prev - currentRank} plass(er)` : `Ned ${currentRank - prev} plass(er)`}
                    title={movedUp ? `Opp ${prev - currentRank} plass(er)` : `Ned ${currentRank - prev} plass(er)`}
                    style={{
                      color: movedUp ? 'var(--color-success-text)' : 'var(--color-danger)',
                      fontSize: '0.85em',
                      lineHeight: 1,
                    }}
                  >
                    {movedUp ? '▲' : '▼'}
                  </span>
                )
              })()}
              {entry.totalPoints}p
            </span>
          </div>
        ))}
      </div>

      {/* Global leaderboard – alle ligaer */}
      {globalEntries.length > 0 && (
        <>
          <p className="text-sm font-medium mt-4" style={{ color: 'var(--color-text-primary)' }}>
            The Boss over alle ligaer
          </p>
          <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>{globalEntries.length} deltakere</p>
          <div
            className="overflow-hidden rounded-xl"
            style={{ backgroundColor: 'var(--color-surface-card)', border: '1px solid var(--color-border-light)' }}
          >
            {globalEntries.map((entry, i) => (
              <div
                key={`global-${i}`}
                className="flex items-center justify-between px-4 py-3"
                style={{
                  borderBottom: i < globalEntries.length - 1 ? '1px solid var(--color-border-light)' : undefined,
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
                  {entry.isInCurrentGroup ? (
                    entry.picture ? (
                      <img src={entry.picture} alt="" className="h-8 w-8 rounded-full" referrerPolicy="no-referrer" />
                    ) : (
                      <div
                        className="flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium"
                        style={{ backgroundColor: 'var(--color-surface-elevated)', color: 'var(--color-text-muted)' }}
                      >
                        {entry.name ? firstName(entry.name).charAt(0) : '?'}
                      </div>
                    )
                  ) : (
                    <div
                      className="flex h-8 w-8 items-center justify-center rounded-full"
                      style={{ backgroundColor: 'var(--color-surface-elevated)', color: 'var(--color-text-muted)' }}
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="h-4 w-4">
                        <path d="M12 12c2.7 0 4.8-2.1 4.8-4.8S14.7 2.4 12 2.4 7.2 4.5 7.2 7.2 9.3 12 12 12zm0 2.4c-3.2 0-9.6 1.6-9.6 4.8v2.4h19.2v-2.4c0-3.2-6.4-4.8-9.6-4.8z" />
                      </svg>
                    </div>
                  )}
                  <div>
                    <span className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
                      {entry.isInCurrentGroup
                        ? firstName(entry.name ?? '')
                        : `Spiller fra ${entry.groupName ?? 'ukjent liga'}`}
                    </span>
                    <span className="ml-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>
                      {entry.matchCount} {entry.matchCount === 1 ? 'kamp' : 'kamper'}
                    </span>
                  </div>
                </div>
                <span
                  className="flex items-center gap-1 rounded-md px-2.5 py-1 text-sm font-bold"
                  style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
                >
                  {entry.totalPoints}p
                </span>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  )
}
