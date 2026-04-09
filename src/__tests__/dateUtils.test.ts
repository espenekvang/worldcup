import { describe, it, expect, vi } from 'vitest'
import { formatMatchDate, formatMatchTime, getTimeUntil, getNextMatch, isBeforeTournament } from '../utils/dateUtils'
import type { Match } from '../types'

describe('dateUtils', () => {
  it('formats match date and time', () => {
    const date = '2026-06-11T20:00:00Z'

    const formattedDate = formatMatchDate(date)
    const formattedTime = formatMatchTime(date)

    expect(formattedDate).toContain('2026')
    expect(formattedDate).toMatch(/[A-Za-z]/)
    expect(formattedTime).toMatch(/\d/)
  })

  it('returns non-negative remaining time for a future date', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))

    const result = getTimeUntil('2026-01-11T00:00:00Z')

    expect(result.days).toBeGreaterThanOrEqual(9)
    expect(result.hours).toBeGreaterThanOrEqual(0)
    expect(result.minutes).toBeGreaterThanOrEqual(0)
    expect(result.seconds).toBeGreaterThanOrEqual(0)

    vi.useRealTimers()
  })

  it('finds the next upcoming match and checks tournament timing', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))

    const past: Match = { id: 1, date: '2020-01-01T00:00:00Z', homeTeam: 'USA', awayTeam: 'MEX', stage: 'group', group: 'A', venueId: 'metlife' }
    const future: Match = { id: 2, date: '2099-12-31T00:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group', group: 'B', venueId: 'azteca' }

    const result = getNextMatch([past, future])

    expect(result).not.toBeNull()
    expect(result!.id).toBe(2)
    expect(isBeforeTournament('2026-06-11T20:00:00Z')).toBe(true)
    expect(isBeforeTournament('2025-01-01T00:00:00Z')).toBe(false)

    vi.useRealTimers()
  })
})
