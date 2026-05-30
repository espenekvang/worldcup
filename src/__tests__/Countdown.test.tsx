import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import Countdown from '../components/Countdown'
import type { Match, Team, Venue } from '../types'

const mockTeams: Record<string, Team> = {
  MEX: { code: 'MEX', name: 'Mexico', flag: '🇲🇽' },
  CZE: { code: 'CZE', name: 'Tsjekkia', flag: '🇨🇿' },
}

const mockVenues: Venue[] = [
  { id: 'azteca', name: 'Estadio Azteca', city: 'Mexico City', country: 'Mexico', timezone: 'America/Mexico_City' },
]

const mockMatches: Match[] = [
  { id: 1, date: '2026-06-11T20:00:00Z', homeTeam: 'MEX', awayTeam: 'CZE', stage: 'group-1', group: 'A', venueId: 'azteca' },
  { id: 2, date: '2026-06-20T20:00:00Z', homeTeam: 'MEX', awayTeam: 'CZE', stage: 'group-2', group: 'B', venueId: 'azteca' },
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

    expect(screen.getByText(/Til første kamp starter i VM 2026/)).toBeInTheDocument()
    expect(screen.getByText('dager')).toBeInTheDocument()
  })

  it('shows the next betting deadline after the first round has started', () => {
    vi.setSystemTime(new Date('2026-06-12T00:00:00Z'))

    render(<Countdown matches={mockMatches} teams={mockTeams} venues={mockVenues} />)

    expect(screen.getByText(/Til du må ha lagt inn bet på neste kamp/)).toBeInTheDocument()
  })

  it('renders the rules link and invokes onShowRules when clicked', () => {
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))
    const onShowRules = vi.fn()

    render(
      <Countdown
        matches={mockMatches}
        teams={mockTeams}
        venues={mockVenues}
        onShowRules={onShowRules}
      />,
    )

    const link = screen.getByRole('button', { name: /Hvordan funker bettingen\?/ })
    fireEvent.click(link)
    expect(onShowRules).toHaveBeenCalledTimes(1)
  })

  it('does not render the rules link when onShowRules is not provided', () => {
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))

    render(<Countdown matches={mockMatches} teams={mockTeams} venues={mockVenues} />)

    expect(screen.queryByRole('button', { name: /Hvordan funker bettingen\?/ })).toBeNull()
  })
})
