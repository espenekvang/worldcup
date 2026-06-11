import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render } from '@testing-library/react'
import type { ChatMessage } from '../types'

// ChatPanel er den samme komponenten på desktop (sidepanel) og mobil (/chat),
// så denne testen dekker scroll-til-uleste-logikken begge steder.

let mockChat: {
  messages: ChatMessage[]
  isLoading: boolean
  isConnected: boolean
  hasMore: boolean
  newMessagesMarkerId: string | null
  currentUserId: string | null
  sendMessage: ReturnType<typeof vi.fn>
  deleteMessage: ReturnType<typeof vi.fn>
  toggleReaction: ReturnType<typeof vi.fn>
  loadOlder: ReturnType<typeof vi.fn>
  markAsRead: ReturnType<typeof vi.fn>
}

vi.mock('../context/ChatContext', () => ({
  useChat: () => mockChat,
}))

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'me', name: 'Meg', email: 'meg@test.com', isAdmin: false, groupAdminGroupIds: [] },
  }),
}))

vi.mock('../context/BettingGroupContext', () => ({
  useBettingGroup: () => ({ activeGroup: { id: 'g1', name: 'Test-liga' } }),
}))

import ChatPanel from '../components/ChatPanel'

function msg(id: string, createdAt: string, userId = 'other'): ChatMessage {
  return {
    id,
    userId,
    userName: 'Bob',
    userPicture: null,
    content: `melding ${id}`,
    createdAt,
    isDeleted: false,
    isSystem: false,
    reactions: [],
  }
}

let scrollIntoViewSpy: ReturnType<typeof vi.fn>

beforeEach(() => {
  // jsdom implementerer ikke layout/scroll — mock dem.
  scrollIntoViewSpy = vi.fn()
  Element.prototype.scrollIntoView =
    scrollIntoViewSpy as unknown as Element['scrollIntoView']
  Object.defineProperty(HTMLElement.prototype, 'scrollHeight', {
    configurable: true,
    get: () => 1000,
  })

  mockChat = {
    messages: [],
    isLoading: false,
    isConnected: true,
    hasMore: false,
    newMessagesMarkerId: null,
    currentUserId: 'me',
    sendMessage: vi.fn(),
    deleteMessage: vi.fn(),
    toggleReaction: vi.fn(),
    loadOlder: vi.fn(),
    markAsRead: vi.fn(),
  }
})

describe('ChatPanel scroll-til-uleste', () => {
  it('scroller til "Nye meldinger"-skillet når det finnes uleste meldinger', () => {
    mockChat.messages = [
      msg('m1', '2026-06-10T10:00:00Z'),
      msg('m2', '2026-06-11T09:00:00Z'),
      msg('m3', '2026-06-11T10:00:00Z'),
    ]
    mockChat.newMessagesMarkerId = 'm2'

    render(<ChatPanel visible />)

    // Skillet skal ha blitt scrollet inn i visningen, justert til toppen,
    // slik at brukeren lander på første uleste melding (ikke bunnen).
    expect(scrollIntoViewSpy).toHaveBeenCalledWith({ behavior: 'instant', block: 'start' })
  })

  it('scroller IKKE til skillet når alt er lest (går til bunnen i stedet)', () => {
    mockChat.messages = [
      msg('m1', '2026-06-10T10:00:00Z'),
      msg('m2', '2026-06-11T10:00:00Z'),
    ]
    mockChat.newMessagesMarkerId = null

    render(<ChatPanel visible />)

    expect(scrollIntoViewSpy).not.toHaveBeenCalled()
  })

  it('scroller til bunnen (ikke tilbake til skillet) når en ny melding kommer inn', () => {
    mockChat.messages = [
      msg('m1', '2026-06-10T10:00:00Z'),
      msg('m2', '2026-06-11T09:00:00Z'),
    ]
    mockChat.newMessagesMarkerId = 'm2'

    const { rerender } = render(<ChatPanel visible />)
    expect(scrollIntoViewSpy).toHaveBeenCalledTimes(1) // initial: til skillet
    scrollIntoViewSpy.mockClear()

    // Ny melding kommer inn mens man er i chatten — skillet står fortsatt på m2.
    mockChat.messages = [...mockChat.messages, msg('m3', '2026-06-11T11:00:00Z')]
    rerender(<ChatPanel visible />)

    // Skal IKKE hoppe tilbake til "Nye meldinger"-skillet; den nye meldingen
    // vises nederst via scrollTop i stedet.
    expect(scrollIntoViewSpy).not.toHaveBeenCalled()
  })
})
