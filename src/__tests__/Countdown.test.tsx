import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import Countdown from '../components/Countdown'
import type { Match, Team, Venue } from '../types'

const mockTeams: Record<string, Team> = {
  MEX: { code: 'MEX', name: 'Mexico', flag: '🇲🇽' },
  CZE: { code: 'CZE', name: 'Czech Republic', flag: '🇨🇿' },
}

const mockVenues: Venue[] = [
  { id: 'azteca', name: 'Estadio Azteca', city: 'Mexico City', country: 'Mexico', timezone: 'America/Mexico_City' },
]

const mockMatches: Match[] = [
  { id: 1, date: '2026-06-11T20:00:00Z', homeTeam: 'MEX', awayTeam: 'CZE', stage: 'group', group: 'A', venueId: 'azteca' },
  { id: 2, date: '2026-06-20T20:00:00Z', homeTeam: 'MEX', awayTeam: 'CZE', stage: 'group', group: 'B', venueId: 'azteca' },
]

describe('Countdown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders the pre-tournament countdown state', () => {
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))

    render(<Countdown matches={mockMatches} teams={mockTeams} venues={mockVenues} />)

    expect(screen.getByText(/Until World Cup 2026 begins/)).toBeInTheDocument()
    expect(screen.getByText('days')).toBeInTheDocument()
  })

  it('shows the next match context and venue after the opener', () => {
    vi.setSystemTime(new Date('2026-06-12T00:00:00Z'))

    render(<Countdown matches={mockMatches} teams={mockTeams} venues={mockVenues} />)

    expect(screen.getByText(/Until Mexico vs Czech Republic/)).toBeInTheDocument()
    expect(screen.getByText(/Estadio Azteca, Mexico City/)).toBeInTheDocument()
  })
})
