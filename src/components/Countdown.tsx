import { useEffect, useState } from 'react'
import type { Match, Team, Venue } from '../types'
import { STAGE_LABELS, getNextBettingDeadline, getTimeUntil } from '../utils/dateUtils'

interface CountdownProps {
  matches: Match[]
  teams: Record<string, Team>
  venues: Venue[]
  onShowRules?: () => void
}

export default function Countdown({ matches, onShowRules }: CountdownProps) {
  const [nextDeadline, setNextDeadline] = useState(() => getNextBettingDeadline(matches))

  const targetDate = nextDeadline?.deadline ?? null
  const isTournamentOver = nextDeadline === null
  const [timeLeft, setTimeLeft] = useState(() => getTimeUntil(targetDate ?? new Date().toISOString()))

  useEffect(() => {
    if (isTournamentOver || !targetDate) {
      return undefined
    }

    const interval = setInterval(() => {
      const nextTime = getTimeUntil(targetDate)
      setTimeLeft(nextTime)

      if (nextTime.days === 0 && nextTime.hours === 0 && nextTime.minutes === 0 && nextTime.seconds === 0) {
        setNextDeadline(getNextBettingDeadline(matches))
      }
    }, 1000)

    return () => clearInterval(interval)
  }, [isTournamentOver, matches, targetDate])

  if (isTournamentOver) {
    return (
      <div className="py-8 text-center">
        <p className="text-xl font-semibold" style={{ color: 'var(--color-text-secondary)' }}>VM 2026 er avsluttet</p>
        {onShowRules ? (
          <div className="mt-3">
            <RulesLink onClick={onShowRules} />
          </div>
        ) : null}
      </div>
    )
  }

  const roundLabel = STAGE_LABELS[nextDeadline.stage]
  const contextText = `Til du må legge inn bets for ${roundLabel}`

  return (
    <div className="py-6 text-center sm:py-8">
      <div className="flex items-center justify-center gap-2 text-3xl font-bold sm:gap-4 sm:text-5xl" style={{ color: 'var(--color-text-primary)' }}>
        <div className="flex flex-col items-center">
          <span>{timeLeft.days}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide sm:text-xs" style={{ color: 'var(--color-text-muted)' }}>dager</span>
        </div>
        <span style={{ color: 'var(--color-border)' }}>:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.hours).padStart(2, '0')}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide sm:text-xs" style={{ color: 'var(--color-text-muted)' }}>timer</span>
        </div>
        <span style={{ color: 'var(--color-border)' }}>:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.minutes).padStart(2, '0')}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide sm:text-xs" style={{ color: 'var(--color-text-muted)' }}>min</span>
        </div>
        <span style={{ color: 'var(--color-border)' }}>:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.seconds).padStart(2, '0')}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide sm:text-xs" style={{ color: 'var(--color-text-muted)' }}>sek</span>
        </div>
      </div>
      <p className="mt-3 text-base font-medium sm:mt-4 sm:text-lg" style={{ color: 'var(--color-text-secondary)' }}>{contextText}</p>
      {onShowRules ? (
        <div className="mt-3">
          <RulesLink onClick={onShowRules} />
        </div>
      ) : null}
    </div>
  )
}

function RulesLink({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="inline-flex items-center gap-1.5 text-xs underline-offset-4 transition-colors hover:underline sm:text-sm"
      style={{ color: 'var(--color-text-muted)' }}
    >
      <span aria-hidden>ⓘ</span>
      Hvordan funker bettingen?
    </button>
  )
}
