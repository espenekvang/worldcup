import { describe, it, expect, vi } from 'vitest'
import { formatMatchDate, formatMatchTime, getTimeUntil, getNextMatch, isBeforeTournament, isMatchInProgress } from '../utils/dateUtils'
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

    const past: Match = { id: 1, date: '2020-01-01T00:00:00Z', homeTeam: 'USA', awayTeam: 'MEX', stage: 'group-1', group: 'A', venueId: 'metlife' }
    const future: Match = { id: 2, date: '2099-12-31T00:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group-1', group: 'B', venueId: 'azteca' }

    const result = getNextMatch([past, future])

    expect(result).not.toBeNull()
    expect(result!.id).toBe(2)
    expect(isBeforeTournament('2026-06-11T20:00:00Z')).toBe(true)
    expect(isBeforeTournament('2025-01-01T00:00:00Z')).toBe(false)

    vi.useRealTimers()
  })

  describe('isMatchInProgress', () => {
    const match: Match = { id: 1, date: '2026-06-11T20:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group-1', group: 'A', venueId: 'metlife' }

    it('is false before kickoff', () => {
      const now = new Date('2026-06-11T19:59:00Z').getTime()
      expect(isMatchInProgress(match, false, now)).toBe(false)
    })

    it('is true shortly after kickoff when no result is entered', () => {
      const now = new Date('2026-06-11T20:45:00Z').getTime()
      expect(isMatchInProgress(match, false, now)).toBe(true)
    })

    it('is false once a result is entered', () => {
      const now = new Date('2026-06-11T20:45:00Z').getTime()
      expect(isMatchInProgress(match, true, now)).toBe(false)
    })

    it('is false long after kickoff (outside the match window)', () => {
      const now = new Date('2026-06-12T02:00:00Z').getTime()
      expect(isMatchInProgress(match, false, now)).toBe(false)
    })

    it('is false when the teams are not yet determined', () => {
      const knockout: Match = { id: 73, date: '2026-07-01T20:00:00Z', homeTeam: null, awayTeam: null, stage: 'round-of-32', venueId: 'metlife' }
      const now = new Date('2026-07-01T20:45:00Z').getTime()
      expect(isMatchInProgress(knockout, false, now)).toBe(false)
    })
  })
})
