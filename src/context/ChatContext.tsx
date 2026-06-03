import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import type { ReactNode } from 'react'
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { jwtDecode } from 'jwt-decode'
import type { ChatMessage } from '../types'
import {
  addChatReaction,
  deleteChatMessage,
  getApiBase,
  getChatMessages,
  postChatMessage,
  removeChatReaction,
} from '../api/client'
import { useAuth } from './AuthContext'
import { useBettingGroup } from './BettingGroupContext'

interface ChatContextValue {
  messages: ChatMessage[]
  isLoading: boolean
  isConnected: boolean
  hasMore: boolean
  unreadCount: number
  newMessagesMarkerId: string | null
  currentUserId: string | null
  sendMessage: (content: string) => Promise<void>
  deleteMessage: (id: string) => Promise<void>
  toggleReaction: (messageId: string, emoji: string) => Promise<void>
  loadOlder: () => Promise<void>
  markAsRead: () => void
}

const ChatContext = createContext<ChatContextValue | null>(null)

const PAGE_SIZE = 50
const TOKEN_KEY = 'auth_token'

function lastReadKey(groupId: string): string {
  return `chat_last_read_${groupId}`
}

function safeGet(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function safeSet(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    // ignore
  }
}

interface JwtPayload {
  sub?: string
  nameid?: string
  // .NET puts NameIdentifier under this URI claim
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'?: string
}

function getCurrentUserIdFromToken(): string | null {
  const token = safeGet(TOKEN_KEY)
  if (!token) return null
  try {
    const payload = jwtDecode<JwtPayload>(token)
    return (
      payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ??
      payload.nameid ??
      payload.sub ??
      null
    )
  } catch {
    return null
  }
}

export function ChatProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const { activeGroup } = useBettingGroup()

  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isConnected, setIsConnected] = useState(false)
  const [hasMore, setHasMore] = useState(false)
  const [lastReadAt, setLastReadAt] = useState<string | null>(null)
  const [newMessagesMarkerId, setNewMessagesMarkerId] = useState<string | null>(null)

  const connectionRef = useRef<HubConnection | null>(null)
  const currentGroupIdRef = useRef<string | null>(null)
  const currentUserId = useMemo(getCurrentUserIdFromToken, [user])

  // Load last-read timestamp when active group changes
  useEffect(() => {
    if (!activeGroup) {
      setLastReadAt(null)
      return
    }
    setLastReadAt(safeGet(lastReadKey(activeGroup.id)))
  }, [activeGroup])

  // Initial load + SignalR connection per active group
  useEffect(() => {
    if (!user || !activeGroup) {
      setMessages([])
      setHasMore(false)
      return
    }

    const groupId = activeGroup.id
    currentGroupIdRef.current = groupId
    let cancelled = false

    setNewMessagesMarkerId(null)
    setIsLoading(true)
    getChatMessages(undefined, PAGE_SIZE)
      .then((data) => {
        if (cancelled || currentGroupIdRef.current !== groupId) return
        setMessages(data)
        setHasMore(data.length === PAGE_SIZE)

        const lastRead = safeGet(lastReadKey(groupId))
        if (lastRead) {
          const userId = getCurrentUserIdFromToken()
          const firstUnread = data.find(
            (m) => !m.isDeleted && m.userId !== userId && m.createdAt > lastRead,
          )
          setNewMessagesMarkerId(firstUnread?.id ?? null)
        }
      })
      .catch(() => {
        if (cancelled) return
        setMessages([])
        setHasMore(false)
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })

    // Build hub connection
    const token = safeGet(TOKEN_KEY)
    if (!token) return

    const connection = new HubConnectionBuilder()
      .withUrl(`${getApiBase()}/hubs/chat`, {
        accessTokenFactory: () => safeGet(TOKEN_KEY) ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('MessagePosted', (msg: ChatMessage) => {
      if (currentGroupIdRef.current !== groupId) return
      setMessages((prev) => {
        // Skip duplicates (e.g. our own message that we already appended optimistically)
        if (prev.some((m) => m.id === msg.id)) return prev
        return [...prev, { ...msg, reactions: msg.reactions ?? [] }]
      })
    })

    connection.on('MessageDeleted', (evt: { id: string; bettingGroupId: string }) => {
      if (currentGroupIdRef.current !== groupId) return
      setMessages((prev) =>
        prev.map((m) => (m.id === evt.id ? { ...m, content: '', isDeleted: true } : m)),
      )
    })

    connection.on(
      'ReactionUpdated',
      (evt: { messageId: string; bettingGroupId: string; emoji: string; count: number }) => {
        if (currentGroupIdRef.current !== groupId) return
        setMessages((prev) =>
          prev.map((m) => {
            if (m.id !== evt.messageId) return m
            const currentReactions = m.reactions ?? []
            const existing = currentReactions.find((r) => r.emoji === evt.emoji)
            let reactions
            if (evt.count === 0) {
              reactions = currentReactions.filter((r) => r.emoji !== evt.emoji)
            } else if (existing) {
              reactions = currentReactions.map((r) =>
                r.emoji === evt.emoji ? { ...r, count: evt.count } : r,
              )
            } else {
              reactions = [...currentReactions, { emoji: evt.emoji, count: evt.count, reactedByMe: false }]
            }
            return { ...m, reactions }
          }),
        )
      },
    )

    connection.onreconnected(() => {
      setIsConnected(true)
      connection.invoke('JoinGroup', groupId).catch(() => undefined)
    })
    connection.onclose(() => setIsConnected(false))

    connection
      .start()
      .then(() => {
        if (cancelled) {
          connection.stop().catch(() => undefined)
          return
        }
        setIsConnected(true)
        return connection.invoke('JoinGroup', groupId)
      })
      .catch(() => {
        setIsConnected(false)
      })

    connectionRef.current = connection

    return () => {
      cancelled = true
      currentGroupIdRef.current = null
      const conn = connectionRef.current
      connectionRef.current = null
      if (conn) {
        if (conn.state === HubConnectionState.Connected) {
          conn.invoke('LeaveGroup', groupId).catch(() => undefined)
        }
        conn.stop().catch(() => undefined)
      }
      setIsConnected(false)
    }
  }, [user, activeGroup])

  const sendMessage = useCallback(async (content: string) => {
    const trimmed = content.trim()
    if (!trimmed) return
    const result = await postChatMessage(trimmed)
    setMessages((prev) => {
      if (prev.some((m) => m.id === result.id)) return prev
      return [...prev, result]
    })
  }, [])

  const deleteMessage = useCallback(async (id: string) => {
    await deleteChatMessage(id)
    setMessages((prev) =>
      prev.map((m) => (m.id === id ? { ...m, content: '', isDeleted: true } : m)),
    )
  }, [])

  const toggleReaction = useCallback(
    async (messageId: string, emoji: string) => {
      const message = messages.find((m) => m.id === messageId)
      if (!message) return
      const existing = message.reactions.find((r) => r.emoji === emoji)
      const isOwn = existing?.reactedByMe ?? false

      // Optimistic update
      setMessages((prev) =>
        prev.map((m) => {
          if (m.id !== messageId) return m
          let reactions
          if (isOwn) {
            const newCount = (existing?.count ?? 1) - 1
            reactions = newCount === 0
              ? m.reactions.filter((r) => r.emoji !== emoji)
              : m.reactions.map((r) =>
                  r.emoji === emoji ? { ...r, count: newCount, reactedByMe: false } : r,
                )
          } else if (existing) {
            reactions = m.reactions.map((r) =>
              r.emoji === emoji ? { ...r, count: r.count + 1, reactedByMe: true } : r,
            )
          } else {
            reactions = [...m.reactions, { emoji, count: 1, reactedByMe: true }]
          }
          return { ...m, reactions }
        }),
      )

      try {
        if (isOwn) {
          await removeChatReaction(messageId, emoji)
        } else {
          await addChatReaction(messageId, emoji)
        }
      } catch {
        // Roll back optimistic update on failure
        setMessages((prev) =>
          prev.map((m) => {
            if (m.id !== messageId) return m
            return { ...m, reactions: message.reactions }
          }),
        )
      }
    },
    [messages],
  )

  const loadOlder = useCallback(async () => {
    if (!hasMore || messages.length === 0) return
    const oldest = messages[0]
    const older = await getChatMessages(oldest.createdAt, PAGE_SIZE)
    setMessages((prev) => {
      const existingIds = new Set(prev.map((m) => m.id))
      const merged = [...older.filter((m) => !existingIds.has(m.id)), ...prev]
      return merged
    })
    setHasMore(older.length === PAGE_SIZE)
  }, [hasMore, messages])

  const markAsRead = useCallback(() => {
    if (!activeGroup) return
    if (messages.length === 0) {
      // Still record a marker so unread is 0
      const now = new Date().toISOString()
      safeSet(lastReadKey(activeGroup.id), now)
      setLastReadAt(now)
      return
    }
    const newest = messages[messages.length - 1].createdAt
    safeSet(lastReadKey(activeGroup.id), newest)
    setLastReadAt(newest)
  }, [activeGroup, messages])

  const unreadCount = useMemo(() => {
    if (!currentUserId) return 0
    return messages.reduce((acc, m) => {
      if (m.isDeleted) return acc
      if (m.userId === currentUserId) return acc
      if (lastReadAt && m.createdAt <= lastReadAt) return acc
      return acc + 1
    }, 0)
  }, [messages, lastReadAt, currentUserId])

  const value = useMemo<ChatContextValue>(
    () => ({
      messages,
      isLoading,
      isConnected,
      hasMore,
      unreadCount,
      newMessagesMarkerId,
      currentUserId,
      sendMessage,
      deleteMessage,
      toggleReaction,
      loadOlder,
      markAsRead,
    }),
    [
      messages,
      isLoading,
      isConnected,
      hasMore,
      unreadCount,
      newMessagesMarkerId,
      currentUserId,
      sendMessage,
      deleteMessage,
      toggleReaction,
      loadOlder,
      markAsRead,
    ],
  )

  return <ChatContext value={value}>{children}</ChatContext>
}

const NOOP_CHAT: ChatContextValue = {
  messages: [],
  isLoading: false,
  isConnected: false,
  hasMore: false,
  unreadCount: 0,
  newMessagesMarkerId: null,
  currentUserId: null,
  sendMessage: async () => undefined,
  deleteMessage: async () => undefined,
  toggleReaction: async () => undefined,
  loadOlder: async () => undefined,
  markAsRead: () => undefined,
}

export function useChat(): ChatContextValue {
  const context = useContext(ChatContext)
  return context ?? NOOP_CHAT
}
