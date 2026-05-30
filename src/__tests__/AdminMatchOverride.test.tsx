import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import AdminPanel from '../components/AdminPanel'
import { MatchesProvider } from '../context/MatchesContext'
import type { Match } from '../types'

vi.mock('../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/client')>()
  return {
    ...actual,
    getInvitations: vi.fn().mockResolvedValue([]),
    createInvitation: vi.fn(),
    deleteInvitation: vi.fn(),
    updateMatchTeams: vi.fn().mockResolvedValue({}),
    getResults: vi.fn().mockResolvedValue([]),
    getUserPoints: vi.fn().mockResolvedValue([]),
    setMatchResult: vi.fn().mockResolvedValue({}),
    getAllGroups: vi.fn().mockResolvedValue([]),
    createGroup: vi.fn(),
    updateGroup: vi.fn(),
    deleteGroup: vi.fn(),
    getGroupMembers: vi.fn().mockResolvedValue([]),
    addGroupMember: vi.fn(),
    removeGroupMember: vi.fn(),
    getInviteLinks: vi.fn().mockResolvedValue([]),
    createInviteLink: vi.fn(),
    revokeInviteLink: vi.fn(),
  }
})

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: '1', name: 'Admin', email: 'admin@test.com', isAdmin: true, groupAdminGroupIds: [] },
    isLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

vi.mock('../context/ResultsContext', () => ({
  useResults: () => ({
    results: new Map(),
    points: new Map(),
    isLoading: false,
    refreshResults: vi.fn().mockResolvedValue(undefined),
  }),
}))

vi.mock('../context/FeatureFlagsContext', () => ({
  useFeatureFlag: () => false,
  useFeatureFlags: () => ({ flags: {}, isLoading: false, isEnabled: () => false }),
}))

vi.mock('../context/BettingGroupContext', () => ({
  useBettingGroup: () => ({
    activeGroup: null,
    refreshGroups: vi.fn(),
  }),
}))

import { updateMatchTeams } from '../api/client'

const knockoutMatch: Match = {
  id: 200,
  date: '2026-07-01T20:00:00Z',
  homeTeam: null,
  awayTeam: null,
  homePlaceholder: 'Vinner gruppe A',
  awayPlaceholder: '2. plass gruppe B',
  stage: 'round-of-16',
  venueId: 'metlife',
}

function renderWithProvider() {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
    ok: true,
    status: 200,
    json: () => Promise.resolve([knockoutMatch]),
    text: () => Promise.resolve(''),
  }))

  return render(
    <MatchesProvider>
      <AdminPanel />
    </MatchesProvider>
  )
}

describe('AdminPanel match override', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders the match override section heading', async () => {
    renderWithProvider()

    await waitFor(() => {
      expect(screen.getByText('Kamp-overstyring')).toBeInTheDocument()
    })
  })

  it('renders knockout match options in the match select', async () => {
    renderWithProvider()

    await waitFor(() => {
      const options = screen.getAllByText(/Vinner gruppe A vs 2. plass gruppe B/)
      expect(options.length).toBeGreaterThanOrEqual(1)
    })
  })

  it('shows team selects after a knockout match is selected', async () => {
    renderWithProvider()

    await waitFor(() => {
      const options = screen.getAllByText(/Vinner gruppe A vs 2. plass gruppe B/)
      expect(options.length).toBeGreaterThanOrEqual(1)
    })

    // 0 = invitation group, 1 = invite-link group, 2 = match override select
    const matchSelect = screen.getAllByRole('combobox')[2]
    fireEvent.change(matchSelect, { target: { value: String(knockoutMatch.id) } })

    const selects = screen.getAllByRole('combobox')
    expect(selects.length).toBeGreaterThanOrEqual(5)
  })

  it('calls updateMatchTeams with correct args on form submit', async () => {
    renderWithProvider()

    await waitFor(() => {
      const options = screen.getAllByText(/Vinner gruppe A vs 2. plass gruppe B/)
      expect(options.length).toBeGreaterThanOrEqual(1)
    })

    // 0 = invitation group, 1 = invite-link group, 2 = match override select
    const matchSelect = screen.getAllByRole('combobox')[2]
    fireEvent.change(matchSelect, { target: { value: String(knockoutMatch.id) } })

    await waitFor(() => {
      expect(screen.getAllByRole('combobox').length).toBeGreaterThanOrEqual(5)
    })

    const selects = screen.getAllByRole('combobox')
    // 0 = invitation group, 1 = invite-link group, 2 = match, 3 = home team, 4 = away team
    fireEvent.change(selects[3], { target: { value: 'BRA' } })
    fireEvent.change(selects[4], { target: { value: 'ARG' } })

    const saveButton = screen.getByRole('button', { name: /^lagre$/i })
    fireEvent.click(saveButton)

    await waitFor(() => {
      expect(updateMatchTeams).toHaveBeenCalledWith(200, 'BRA', 'ARG')
    })
  })
})
