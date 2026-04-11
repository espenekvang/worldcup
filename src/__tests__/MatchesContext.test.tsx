import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MatchesProvider, useMatches } from '../context/MatchesContext'
import type { Match } from '../types'

function MatchesConsumer() {
  const { matches, isLoading } = useMatches()
  return (
    <div>
      <span data-testid="match-count">{matches.length}</span>
      <span data-testid="loading">{String(isLoading)}</span>
      {matches.map((m) => (
        <span key={m.id} data-testid={`match-${m.id}`}>{m.id}</span>
      ))}
    </div>
  )
}

const apiMatches: Match[] = [
  { id: 999, date: '2026-07-01T20:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'final', venueId: 'metlife' },
  { id: 998, date: '2026-06-30T18:00:00Z', homeTeam: 'FRA', awayTeam: 'ENG', stage: 'semi-final', venueId: 'metlife' },
]

describe('MatchesContext', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('fetches /api/matches on mount and exposes loaded matches', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(apiMatches),
        text: () => Promise.resolve(''),
      })
    )

    render(
      <MatchesProvider>
        <MatchesConsumer />
      </MatchesProvider>
    )

    await waitFor(() => {
      expect(screen.getByTestId('match-999')).toBeInTheDocument()
    })

    expect(screen.getByTestId('match-998')).toBeInTheDocument()
    expect(screen.getByTestId('match-count').textContent).toBe('2')
  })

  it('falls back to static data when API fetch fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockRejectedValue(new Error('Network error'))
    )

    render(
      <MatchesProvider>
        <MatchesConsumer />
      </MatchesProvider>
    )

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false')
    })

    const count = parseInt(screen.getByTestId('match-count').textContent ?? '0', 10)
    expect(count).toBeGreaterThan(0)
    expect(screen.queryByTestId('match-999')).not.toBeInTheDocument()
  })

  it('initialises with static data before the fetch resolves', () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockReturnValue(new Promise(() => undefined))
    )

    render(
      <MatchesProvider>
        <MatchesConsumer />
      </MatchesProvider>
    )

    const count = parseInt(screen.getByTestId('match-count').textContent ?? '0', 10)
    expect(count).toBeGreaterThan(0)
  })

  it('throws when useMatches is used outside a MatchesProvider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => {
      render(<MatchesConsumer />)
    }).toThrow('useMatches must be used within a MatchesProvider')

    spy.mockRestore()
  })
})
