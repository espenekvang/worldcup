import { render, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import InvitePage from '../pages/InvitePage'

const navigateMock = vi.fn()
const logoutMock = vi.fn()
const setGroupsMock = vi.fn()
const setActiveGroupMock = vi.fn()
const getInviteLinkInfoMock = vi.fn()
const acceptInviteLinkMock = vi.fn()

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>()
  return {
    ...actual,
    useNavigate: () => navigateMock,
    useParams: () => ({ token: 'invite-token' }),
  }
})

vi.mock('../api/client', () => ({
  getInviteLinkInfo: (...args: unknown[]) => getInviteLinkInfoMock(...args),
  acceptInviteLink: (...args: unknown[]) => acceptInviteLinkMock(...args),
}))

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'user-1', name: 'Test User', email: 'test@example.com', isAdmin: false },
    logout: logoutMock,
  }),
}))

vi.mock('../context/BettingGroupContext', () => ({
  useBettingGroup: () => ({
    groups: [],
    setGroups: setGroupsMock,
    setActiveGroup: setActiveGroupMock,
  }),
}))

vi.mock('../components/ThemeToggle', () => ({
  default: () => null,
}))

describe('InvitePage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()

    getInviteLinkInfoMock.mockResolvedValue({
      bettingGroupId: 'group-1',
      groupName: 'Testliga',
    })
  })

  it('logs out and sends the user to login when invite acceptance hits 401', async () => {
    acceptInviteLinkMock.mockRejectedValue(new Error('API error 401: Unauthorized'))

    render(<InvitePage />)

    await waitFor(() => {
      expect(logoutMock).toHaveBeenCalledTimes(1)
      expect(navigateMock).toHaveBeenCalledWith('/login', { replace: true })
    })

    expect(localStorage.getItem('pending_invite_token')).toBe('invite-token')
  })
})
