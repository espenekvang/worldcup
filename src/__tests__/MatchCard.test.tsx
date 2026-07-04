import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import MatchCard from '../components/MatchCard'
import type { Match, Team, Venue } from '../types'
import { AuthProvider } from '../context/AuthContext'
import { PredictionsProvider } from '../context/PredictionsContext'
import { ResultsProvider } from '../context/ResultsContext'
import { BettingGroupProvider } from '../context/BettingGroupContext'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { MemoryRouter } from 'react-router-dom'

const mockTeams: Record<string, Team> = {
  BRA: { code: 'BRA', name: 'Brasil', flag: '🇧🇷' },
  ARG: { code: 'ARG', name: 'Argentina', flag: '🇦🇷' },
}

const mockVenues: Venue[] = [
  { id: 'metlife', name: 'MetLife Stadium', city: 'East Rutherford', country: 'USA', timezone: 'America/New_York' },
]

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <GoogleOAuthProvider clientId="test">
      <MemoryRouter>
        <BettingGroupProvider>
          <AuthProvider>
            <ResultsProvider>
              <PredictionsProvider>
                {children}
              </PredictionsProvider>
            </ResultsProvider>
          </AuthProvider>
        </BettingGroupProvider>
      </MemoryRouter>
    </GoogleOAuthProvider>
  )
}

describe('MatchCard', () => {
  const onTipClick = vi.fn()
  const onViewOthers = vi.fn()

  it('renders team names and venue for a group stage match', () => {
    const match: Match = { id: 1, date: '2026-06-15T18:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group-1', group: 'C', venueId: 'metlife' }

    render(<MatchCard match={match} teams={mockTeams} venues={mockVenues} locked={false} onTipClick={onTipClick} onViewOthers={onViewOthers} />, { wrapper: Wrapper })

    expect(screen.getByText(/Brasil/)).toBeInTheDocument()
    expect(screen.getByText(/Argentina/)).toBeInTheDocument()
    expect(screen.getAllByText(/MetLife Stadium, East Rutherford/).length).toBeGreaterThan(0)
  })

  it('viser TV-kanal-logo for en kamp som er kanalsatt', () => {
    // Kamp 1 (Mexico–Sør-Afrika) sendes på TV2 ifølge tvChannels-dataen.
    const match: Match = { id: 1, date: '2026-06-15T18:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group-1', group: 'C', venueId: 'metlife' }

    render(<MatchCard match={match} teams={mockTeams} venues={mockVenues} locked={false} onTipClick={onTipClick} onViewOthers={onViewOthers} />, { wrapper: Wrapper })

    expect(screen.getByText('TV 2')).toBeInTheDocument()
  })

  it('viser ingen TV-kanal-logo for en kamp uten fastsatt kanal (kvartfinale)', () => {
    // NRK/TV2 har ikke fordelt kvartfinalene per kamp ennå, så ingen logo skal vises.
    const match: Match = { id: 97, date: '2026-07-09T20:00:00Z', homeTeam: null, awayTeam: null, homePlaceholder: 'Vinner kamp 89', awayPlaceholder: 'Vinner kamp 90', stage: 'quarter-final', venueId: 'metlife' }

    render(<MatchCard match={match} teams={mockTeams} venues={mockVenues} locked={false} onTipClick={onTipClick} onViewOthers={onViewOthers} />, { wrapper: Wrapper })

    expect(screen.queryByText('NRK')).not.toBeInTheDocument()
    expect(screen.queryByText('TV 2')).not.toBeInTheDocument()
  })

  it('renders knockout placeholders and stage label', () => {
    const match: Match = { id: 73, date: '2026-07-01T20:00:00Z', homeTeam: null, awayTeam: null, homePlaceholder: '1st Group A', awayPlaceholder: '2nd Group B', stage: 'round-of-32', venueId: 'metlife' }

    render(<MatchCard match={match} teams={mockTeams} venues={mockVenues} locked={false} onTipClick={onTipClick} onViewOthers={onViewOthers} />, { wrapper: Wrapper })

    expect(screen.getByText(/1st Group A/)).toBeInTheDocument()
    expect(screen.getByText(/2nd Group B/)).toBeInTheDocument()
    expect(screen.getByText(/32-delsfinale/)).toBeInTheDocument()
  })

  it('navigerer til matchdetalj-siden når man klikker på kortet', () => {
    const match: Match = { id: 42, date: '2026-06-15T18:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group-1', group: 'C', venueId: 'metlife' }

    function NavWrapper({ children }: { children: React.ReactNode }) {
      return (
        <GoogleOAuthProvider clientId="test">
          <MemoryRouter initialEntries={['/']}>
            <BettingGroupProvider>
              <AuthProvider>
                <ResultsProvider>
                  <PredictionsProvider>
                    <Routes>
                      <Route path="/" element={children} />
                      <Route path="/match/:matchId" element={<p>DETAIL PAGE</p>} />
                    </Routes>
                  </PredictionsProvider>
                </ResultsProvider>
              </AuthProvider>
            </BettingGroupProvider>
          </MemoryRouter>
        </GoogleOAuthProvider>
      )
    }

    render(
      <MatchCard match={match} teams={mockTeams} venues={mockVenues} locked={false} onTipClick={onTipClick} onViewOthers={onViewOthers} />,
      { wrapper: NavWrapper },
    )

    fireEvent.click(screen.getByTestId('match-card'))
    expect(screen.getByText('DETAIL PAGE')).toBeInTheDocument()
  })

  it('klikk på "Bet"-knappen åpner modalen uten å navigere bort', () => {
    const tipClick = vi.fn()
    const match: Match = { id: 5, date: '2026-06-15T18:00:00Z', homeTeam: 'BRA', awayTeam: 'ARG', stage: 'group-1', group: 'C', venueId: 'metlife' }

    function NavWrapper({ children }: { children: React.ReactNode }) {
      return (
        <GoogleOAuthProvider clientId="test">
          <MemoryRouter initialEntries={['/']}>
            <BettingGroupProvider>
              <AuthProvider>
                <ResultsProvider>
                  <PredictionsProvider>
                    <Routes>
                      <Route path="/" element={children} />
                      <Route path="/match/:matchId" element={<p>NAVIGATED</p>} />
                    </Routes>
                  </PredictionsProvider>
                </ResultsProvider>
              </AuthProvider>
            </BettingGroupProvider>
          </MemoryRouter>
        </GoogleOAuthProvider>
      )
    }

    render(
      <MatchCard match={match} teams={mockTeams} venues={mockVenues} locked={false} onTipClick={tipClick} onViewOthers={onViewOthers} />,
      { wrapper: NavWrapper },
    )

    // Det er to "Bet"-knapper (mobile + desktop) — klikk den første.
    fireEvent.click(screen.getAllByRole('button', { name: 'Bet' })[0])
    expect(tipClick).toHaveBeenCalledWith(match)
    expect(screen.queryByText('NAVIGATED')).not.toBeInTheDocument()
  })
})
