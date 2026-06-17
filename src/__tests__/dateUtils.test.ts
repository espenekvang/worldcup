import { describe, it, expect, vi } from 'vitest'
import { formatMatchDate, formatMatchTime, getTimeUntil, getNextMatch, isBeforeTournament, isMatchInProgress, getDefaultStage } from '../utils/dateUtils'
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

  describe('getDefaultStage', () => {
    const round1a: Match = { id: 1, date: '2026-06-11T16:00:00Z', homeTeam: 'A', awayTeam: 'B', stage: 'group-1', group: 'A', venueId: 'metlife' }
    const round1b: Match = { id: 2, date: '2026-06-11T20:00:00Z', homeTeam: 'C', awayTeam: 'D', stage: 'group-1', group: 'B', venueId: 'azteca' }
    const round2a: Match = { id: 3, date: '2026-06-14T16:00:00Z', homeTeam: 'A', awayTeam: 'C', stage: 'group-2', group: 'A', venueId: 'metlife' }
    const matches = [round1a, round1b, round2a]
    const noResults = () => false

    it('opens the round with the next upcoming match', () => {
      const now = new Date('2026-06-10T00:00:00Z').getTime()
      expect(getDefaultStage(matches, noResults, now)).toBe('group-1')
    })

    it('stays on the round while it still has upcoming matches', () => {
      // round1a has kicked off and finished, round1b is still upcoming
      const now = new Date('2026-06-11T19:00:00Z').getTime()
      const finished = (id: number) => id === round1a.id
      expect(getDefaultStage(matches, finished, now)).toBe('group-1')
    })

    it('stays on the round that has a match in progress (pågår)', () => {
      // round1b kicked off 30 min ago with no result; next upcoming is round 2
      const now = new Date('2026-06-11T20:30:00Z').getTime()
      expect(getDefaultStage(matches, noResults, now)).toBe('group-1')
    })

    it('moves to the next round once the current round is completed', () => {
      // all of round 1 is finished; next match is in round 2
      const now = new Date('2026-06-12T00:00:00Z').getTime()
      const finished = (id: number) => id === round1a.id || id === round1b.id
      expect(getDefaultStage(matches, finished, now)).toBe('group-2')
    })

    it('returns null when no matches remain', () => {
      const now = new Date('2026-07-01T00:00:00Z').getTime()
      const finished = () => true
      expect(getDefaultStage(matches, finished, now)).toBeNull()
    })
  })
})
