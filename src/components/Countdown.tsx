import { useEffect, useMemo, useState } from 'react'
import type { Match, Team, Venue } from '../types'
import { getNextMatch, getTimeUntil, isBeforeTournament } from '../utils/dateUtils'

interface CountdownProps {
  matches: Match[]
  teams: Record<string, Team>
  venues: Venue[]
}

export default function Countdown({ matches, teams, venues }: CountdownProps) {
  const firstMatchDate = useMemo(() => matches.map(match => match.date).sort()[0] ?? null, [matches])

  const [targetMatch, setTargetMatch] = useState<Match | null>(() => {
    if (!firstMatchDate || isBeforeTournament(firstMatchDate)) {
      return null
    }

    return getNextMatch(matches)
  })

  const targetDate = targetMatch?.date ?? firstMatchDate
  const isTournamentOver = firstMatchDate !== null && !isBeforeTournament(firstMatchDate) && !targetMatch
  const [timeLeft, setTimeLeft] = useState(() => getTimeUntil(targetDate ?? new Date().toISOString()))

  useEffect(() => {
    if (isTournamentOver || !targetDate) {
      return undefined
    }

    const interval = setInterval(() => {
      const nextTime = getTimeUntil(targetDate)
      setTimeLeft(nextTime)

      if (nextTime.days === 0 && nextTime.hours === 0 && nextTime.minutes === 0 && nextTime.seconds === 0) {
        setTargetMatch(getNextMatch(matches))
      }
    }, 1000)

    return () => clearInterval(interval)
  }, [isTournamentOver, matches, targetDate])

  let contextText: string

  if (isTournamentOver) {
    contextText = 'VM 2026 er avsluttet'
  } else if (!targetMatch) {
    contextText = 'Til VM 2026 starter'
  } else if (targetMatch.homeTeam && targetMatch.awayTeam) {
    const home = teams[targetMatch.homeTeam]
    const away = teams[targetMatch.awayTeam]
    contextText = `Til ${home?.name ?? targetMatch.homeTeam} mot ${away?.name ?? targetMatch.awayTeam}`
  } else {
    contextText = `Til ${targetMatch.homePlaceholder ?? 'Ikke avgjort'} mot ${targetMatch.awayPlaceholder ?? 'Ikke avgjort'}`
  }

  const venue = targetMatch ? venues.find(venueItem => venueItem.id === targetMatch.venueId) : null

  if (isTournamentOver) {
    return (
      <div className="py-8 text-center">
        <p className="text-xl font-semibold text-gray-600">{contextText}</p>
      </div>
    )
  }

  return (
    <div className="py-6 text-center sm:py-8">
      <div className="flex items-center justify-center gap-2 text-3xl font-bold text-gray-900 sm:gap-4 sm:text-5xl">
        <div className="flex flex-col items-center">
          <span>{timeLeft.days}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide text-gray-500 sm:text-xs">dager</span>
        </div>
        <span className="text-gray-300">:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.hours).padStart(2, '0')}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide text-gray-500 sm:text-xs">timer</span>
        </div>
        <span className="text-gray-300">:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.minutes).padStart(2, '0')}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide text-gray-500 sm:text-xs">min</span>
        </div>
        <span className="text-gray-300">:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.seconds).padStart(2, '0')}</span>
          <span className="text-[10px] font-normal uppercase tracking-wide text-gray-500 sm:text-xs">sek</span>
        </div>
      </div>
      <p className="mt-3 text-base font-medium text-gray-700 sm:mt-4 sm:text-lg">{contextText}</p>
      {venue ? (
        <p className="mt-1 text-sm text-gray-500">
          {venue.name}, {venue.city}
        </p>
      ) : null}
    </div>
  )
}
