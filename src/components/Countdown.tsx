import { useEffect, useState } from 'react'
import type { Match, Team, Venue } from '../types'
import { getNextMatch, getTimeUntil, isBeforeTournament } from '../utils/dateUtils'

interface CountdownProps {
  matches: Match[]
  teams: Record<string, Team>
  venues: Venue[]
}

const FIRST_MATCH_DATE = '2026-06-11T20:00:00Z'

export default function Countdown({ matches, teams, venues }: CountdownProps) {
  const [targetMatch, setTargetMatch] = useState<Match | null>(() => {
    if (isBeforeTournament(FIRST_MATCH_DATE)) {
      return null
    }

    return getNextMatch(matches)
  })

  const targetDate = targetMatch ? targetMatch.date : FIRST_MATCH_DATE
  const isTournamentOver = !isBeforeTournament(FIRST_MATCH_DATE) && !targetMatch
  const [timeLeft, setTimeLeft] = useState(() => getTimeUntil(targetDate))

  useEffect(() => {
    if (isTournamentOver) {
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
    contextText = 'World Cup 2026 has ended'
  } else if (!targetMatch) {
    contextText = 'Until World Cup 2026 begins'
  } else if (targetMatch.homeTeam && targetMatch.awayTeam) {
    const home = teams[targetMatch.homeTeam]
    const away = teams[targetMatch.awayTeam]
    contextText = `Until ${home?.name ?? targetMatch.homeTeam} vs ${away?.name ?? targetMatch.awayTeam}`
  } else {
    contextText = `Until ${targetMatch.homePlaceholder ?? 'TBD'} vs ${targetMatch.awayPlaceholder ?? 'TBD'}`
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
    <div className="py-8 text-center">
      <div className="flex items-center justify-center gap-4 text-4xl font-bold text-gray-900 sm:text-5xl">
        <div className="flex flex-col items-center">
          <span>{timeLeft.days}</span>
          <span className="text-xs font-normal uppercase tracking-wide text-gray-500">days</span>
        </div>
        <span className="text-gray-300">:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.hours).padStart(2, '0')}</span>
          <span className="text-xs font-normal uppercase tracking-wide text-gray-500">hours</span>
        </div>
        <span className="text-gray-300">:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.minutes).padStart(2, '0')}</span>
          <span className="text-xs font-normal uppercase tracking-wide text-gray-500">min</span>
        </div>
        <span className="text-gray-300">:</span>
        <div className="flex flex-col items-center">
          <span>{String(timeLeft.seconds).padStart(2, '0')}</span>
          <span className="text-xs font-normal uppercase tracking-wide text-gray-500">sec</span>
        </div>
      </div>
      <p className="mt-4 text-lg font-medium text-gray-700">{contextText}</p>
      {venue ? (
        <p className="mt-1 text-sm text-gray-500">
          at {venue.name}, {venue.city}
        </p>
      ) : null}
    </div>
  )
}
