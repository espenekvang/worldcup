import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import MatchCard from '../components/MatchCard'
import type { Match, Team, Venue } from '../types'

const mockTeams: Record<string, Team> = {
  BRA: { code: 'BRA', name: 'Brazil', flag: '🇧🇷' },
  ARG: { code: 'ARG', name: 'Argentina', flag: '🇦🇷' },
}

const mockVenues: Venue[] = [
  { id: 'metlife', name: 'MetLife Stadium', city: 'East Rutherford', country: 'USA', timezone: 'America/New_York' },
]

describe('MatchCard', () => {
  it('renders team names and venue for a group stage match', () => {
    const match: Match = { id: 1, date: '2026-06-15T18:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group', group: 'C', venueId: 'metlife' }

    render(<MatchCard match={match} teams={mockTeams} venues={mockVenues} />)

    expect(screen.getByText(/Brazil/)).toBeInTheDocument()
    expect(screen.getByText(/Argentina/)).toBeInTheDocument()
    expect(screen.getByText(/MetLife Stadium, East Rutherford/)).toBeInTheDocument()
  })

  it('renders knockout placeholders and stage label', () => {
    const match: Match = { id: 73, date: '2026-07-01T20:00:00Z', homeTeam: null, awayTeam: null, homePlaceholder: '1st Group A', awayPlaceholder: '2nd Group B', stage: 'round-of-32', venueId: 'metlife' }

    render(<MatchCard match={match} teams={mockTeams} venues={mockVenues} />)

    expect(screen.getByText(/1st Group A/)).toBeInTheDocument()
    expect(screen.getByText(/2nd Group B/)).toBeInTheDocument()
    expect(screen.getByText(/Round of 32/)).toBeInTheDocument()
  })
})
