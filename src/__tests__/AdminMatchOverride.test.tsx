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
  }
})

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
      expect(screen.getByText(/Vinner gruppe A vs 2. plass gruppe B/)).toBeInTheDocument()
    })
  })

  it('shows team selects after a knockout match is selected', async () => {
    renderWithProvider()

    await waitFor(() => {
      expect(screen.getByText(/Vinner gruppe A vs 2. plass gruppe B/)).toBeInTheDocument()
    })

    const [matchSelect] = screen.getAllByRole('combobox')
    fireEvent.change(matchSelect, { target: { value: String(knockoutMatch.id) } })

    const selects = screen.getAllByRole('combobox')
    expect(selects.length).toBeGreaterThanOrEqual(3)
  })

  it('calls updateMatchTeams with correct args on form submit', async () => {
    renderWithProvider()

    await waitFor(() => {
      expect(screen.getByText(/Vinner gruppe A vs 2. plass gruppe B/)).toBeInTheDocument()
    })

    const [matchSelect] = screen.getAllByRole('combobox')
    fireEvent.change(matchSelect, { target: { value: String(knockoutMatch.id) } })

    const [, homeSelect, awaySelect] = screen.getAllByRole('combobox')

    fireEvent.change(homeSelect, { target: { value: 'BRA' } })
    fireEvent.change(awaySelect, { target: { value: 'ARG' } })

    const saveButton = screen.getByRole('button', { name: /lagre/i })
    fireEvent.click(saveButton)

    await waitFor(() => {
      expect(updateMatchTeams).toHaveBeenCalledWith(200, 'BRA', 'ARG')
    })
  })
})
