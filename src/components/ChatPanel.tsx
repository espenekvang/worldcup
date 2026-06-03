import {
  Fragment,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { useChat } from '../context/ChatContext'
import { useAuth } from '../context/AuthContext'
import { useBettingGroup } from '../context/BettingGroupContext'
import type { ChatMessage } from '../types'
import { firstName } from '../utils/nameUtils'

interface ChatPanelProps {
  /** Whether this panel is currently visible (used for auto-marking as read). */
  visible?: boolean
  /** Optional className to apply to the outer container (for layout). */
  className?: string
}

const MAX_LENGTH = 500

// Configure marked once: GitHub-flavoured, breaks=true (single newline -> <br>).
marked.setOptions({ gfm: true, breaks: true })

function renderMarkdown(content: string): string {
  // marked.parse can return a Promise in async mode — we use sync.
  const raw = marked.parse(content, { async: false }) as string
  return DOMPurify.sanitize(raw, {
    ALLOWED_TAGS: [
      'a', 'b', 'strong', 'i', 'em', 'code', 'pre', 'p', 'br',
      'ul', 'ol', 'li', 'blockquote', 'del', 's', 'span',
    ],
    ALLOWED_ATTR: ['href', 'title', 'target', 'rel'],
    ALLOWED_URI_REGEXP: /^(?:(?:https?|mailto):|[^a-z]|[a-z+.-]+(?:[^a-z+.\-:]|$))/i,
  })
}

function formatTime(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleTimeString('nb-NO', { hour: '2-digit', minute: '2-digit' })
}

function formatDateLabel(iso: string): string {
  const d = new Date(iso)
  const today = new Date()
  const yesterday = new Date()
  yesterday.setDate(today.getDate() - 1)
  const sameDay = (a: Date, b: Date) =>
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  if (sameDay(d, today)) return 'I dag'
  if (sameDay(d, yesterday)) return 'I går'
  return d.toLocaleDateString('nb-NO', { weekday: 'long', day: 'numeric', month: 'short' })
}

function dayKey(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`
}

export default function ChatPanel({ visible = true, className }: ChatPanelProps) {
  const {
    messages,
    isLoading,
    isConnected,
    hasMore,
    newMessagesMarkerId,
    sendMessage,
    deleteMessage,
    toggleReaction,
    loadOlder,
    markAsRead,
    currentUserId,
  } = useChat()
  const { user } = useAuth()
  const { activeGroup } = useBettingGroup()

  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [isLoadingOlder, setIsLoadingOlder] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [reactionPickerFor, setReactionPickerFor] = useState<string | null>(null)

  const EMOJI_OPTIONS = ['👍', '❤️', '😂', '😮', '😢']

  const listRef = useRef<HTMLDivElement>(null)
  const lastMessageIdRef = useRef<string | null>(null)
  const previousScrollHeightRef = useRef<number | null>(null)
  const newMessagesDividerRef = useRef<HTMLDivElement>(null)
  const hasScrolledToUnreadRef = useRef(false)

  const isGroupAdmin = useMemo(() => {
    if (!user || !activeGroup) return false
    if (user.isAdmin) return true
    return user.groupAdminGroupIds?.includes(activeGroup.id) ?? false
  }, [user, activeGroup])

  // Auto-scroll: to "nye meldinger" divider on first load, otherwise to bottom
  useLayoutEffect(() => {
    const list = listRef.current
    if (!list) return

    if (previousScrollHeightRef.current !== null) {
      // We just prepended older messages — preserve scroll position
      const diff = list.scrollHeight - previousScrollHeightRef.current
      list.scrollTop = list.scrollTop + diff
      previousScrollHeightRef.current = null
      return
    }

    const newest = messages.length > 0 ? messages[messages.length - 1].id : null
    if (newest !== lastMessageIdRef.current) {
      lastMessageIdRef.current = newest
      if (!hasScrolledToUnreadRef.current && newMessagesDividerRef.current) {
        hasScrolledToUnreadRef.current = true
        newMessagesDividerRef.current.scrollIntoView({ behavior: 'instant', block: 'start' })
      } else {
        list.scrollTop = list.scrollHeight
      }
    }
  }, [messages])

  // Auto-mark as read when visible + messages change
  useEffect(() => {
    if (!visible) return
    if (typeof document !== 'undefined' && document.visibilityState !== 'visible') return
    markAsRead()
  }, [visible, messages, markAsRead])

  // Mark as read when tab becomes visible
  useEffect(() => {
    if (!visible) return
    function onVisible() {
      if (document.visibilityState === 'visible') markAsRead()
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [visible, markAsRead])

  const handleSend = useCallback(async () => {
    const trimmed = draft.trim()
    if (!trimmed || isSending) return
    if (trimmed.length > MAX_LENGTH) {
      setError(`Maks ${MAX_LENGTH} tegn.`)
      return
    }
    setIsSending(true)
    setError(null)
    try {
      await sendMessage(trimmed)
      setDraft('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Kunne ikke sende melding.')
    } finally {
      setIsSending(false)
    }
  }, [draft, isSending, sendMessage])

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void handleSend()
    }
  }

  const handleLoadOlder = async () => {
    if (isLoadingOlder || !listRef.current) return
    setIsLoadingOlder(true)
    previousScrollHeightRef.current = listRef.current.scrollHeight
    try {
      await loadOlder()
    } finally {
      setIsLoadingOlder(false)
    }
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Slette denne meldingen?')) return
    try {
      await deleteMessage(id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Kunne ikke slette melding.')
    }
  }

  const canDeleteMessage = (m: ChatMessage): boolean => {
    if (m.isDeleted) return false
    // Systemmeldinger (f.eks. Resultatservice) kan kun fjernes av admin via backend.
    if (m.isSystem) return false
    if (currentUserId && m.userId === currentUserId) return true
    return isGroupAdmin
  }

  // Group messages by day for date dividers
  const grouped = useMemo(() => {
    const result: Array<{ key: string; label: string; messages: ChatMessage[] }> = []
    let current: { key: string; label: string; messages: ChatMessage[] } | null = null
    for (const m of messages) {
      const key = dayKey(m.createdAt)
      if (!current || current.key !== key) {
        current = { key, label: formatDateLabel(m.createdAt), messages: [] }
        result.push(current)
      }
      current.messages.push(m)
    }
    return result
  }, [messages])

  const remaining = MAX_LENGTH - draft.length

  return (
    <div
      className={`flex h-full min-h-0 flex-col overflow-hidden rounded-lg border ${className ?? ''}`}
      style={{
        backgroundColor: 'var(--color-surface-card)',
        borderColor: 'var(--color-border)',
      }}
    >
      <div
        className="flex items-center justify-between border-b px-4 py-3"
        style={{ borderColor: 'var(--color-border)' }}
      >
        <div>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--color-text-primary)' }}>
            Liga-chat
          </h2>
          <p className="text-xs" style={{ color: 'var(--color-text-muted)' }}>
            {activeGroup?.name ?? ''}
          </p>
        </div>
        <span
          className="flex items-center gap-1.5 text-xs"
          style={{ color: 'var(--color-text-muted)' }}
          title={isConnected ? 'Tilkoblet' : 'Frakoblet'}
        >
          <span
            className="inline-block h-2 w-2 rounded-full"
            style={{ backgroundColor: isConnected ? '#22c55e' : '#9ca3af' }}
          />
          {isConnected ? 'Live' : 'Offline'}
        </span>
      </div>

      <div ref={listRef} className="themed-scrollbar flex-1 overflow-y-auto px-3 py-3">
        {hasMore && (
          <div className="mb-3 flex justify-center">
            <button
              onClick={handleLoadOlder}
              disabled={isLoadingOlder}
              className="rounded px-3 py-1 text-xs transition-colors hover:opacity-80 disabled:opacity-50"
              style={{
                backgroundColor: 'var(--color-surface)',
                color: 'var(--color-text-muted)',
              }}
            >
              {isLoadingOlder ? 'Laster…' : 'Vis eldre meldinger'}
            </button>
          </div>
        )}

        {isLoading && messages.length === 0 ? (
          <p className="py-8 text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>
            Laster meldinger…
          </p>
        ) : messages.length === 0 ? (
          <p className="py-8 text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>
            Ingen meldinger ennå. Vær først ute!
          </p>
        ) : (
          grouped.map((group) => (
            <div key={group.key} className="mb-4">
              <div className="mb-2 flex items-center gap-2">
                <div className="h-px flex-1" style={{ backgroundColor: 'var(--color-border)' }} />
                <span
                  className="text-xs font-medium"
                  style={{ color: 'var(--color-text-muted)' }}
                >
                  {group.label}
                </span>
                <div className="h-px flex-1" style={{ backgroundColor: 'var(--color-border)' }} />
              </div>
              {group.messages.map((m) => {
                const isOwn = currentUserId === m.userId
                return (
                  <Fragment key={m.id}>
                    {m.id === newMessagesMarkerId && (
                      <div
                        ref={newMessagesDividerRef}
                        className="my-3 flex items-center gap-2"
                        aria-label="Nye meldinger"
                      >
                        <div
                          className="h-px flex-1"
                          style={{ backgroundColor: 'var(--color-tab-active)', opacity: 0.4 }}
                        />
                        <span
                          className="text-xs font-medium"
                          style={{ color: 'var(--color-tab-active)' }}
                        >
                          Nye meldinger
                        </span>
                        <div
                          className="h-px flex-1"
                          style={{ backgroundColor: 'var(--color-tab-active)', opacity: 0.4 }}
                        />
                      </div>
                    )}
                  <div
                    className="group relative mb-2 flex items-start gap-2"
                    onMouseLeave={() => setReactionPickerFor(null)}
                  >
                    {m.isSystem ? (
                      <div
                        className="flex h-7 w-7 shrink-0 items-center justify-center"
                        aria-label={m.userName}
                        title={m.userName}
                      >
                        <svg
                          aria-hidden="true"
                          viewBox="0 0 24 24"
                          className="h-7 w-7"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="1.75"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                        >
                          {/* snorring */}
                          <circle cx="16" cy="5" r="1.5" />
                          <line x1="16" y1="6.5" x2="16" y2="8" />
                          {/* fløytekropp */}
                          <circle cx="16" cy="14" r="6" />
                          {/* slits på toppen */}
                          <line x1="14" y1="9.5" x2="18" y2="9.5" />
                          {/* munnstykke */}
                          <path d="M10 11 H3.5 a1.5 1.5 0 0 0 -1.5 1.5 v3 a1.5 1.5 0 0 0 1.5 1.5 H10" />
                        </svg>
                      </div>
                    ) : m.userPicture ? (
                      <img
                        src={m.userPicture}
                        alt={firstName(m.userName)}
                        referrerPolicy="no-referrer"
                        className="h-7 w-7 shrink-0 rounded-full"
                      />
                    ) : (
                      <div
                        className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-medium"
                        style={{
                          backgroundColor: 'var(--color-header-btn)',
                          color: 'var(--color-text-inverse)',
                        }}
                      >
                        {firstName(m.userName).charAt(0).toUpperCase()}
                      </div>
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="flex items-baseline gap-2">
                        <span
                          className="text-sm font-medium"
                          style={{
                            color: isOwn ? 'var(--color-tab-active)' : 'var(--color-text-primary)',
                          }}
                        >
                          {m.isSystem ? m.userName : firstName(m.userName)}
                        </span>
                        <span
                          className="text-[10px]"
                          style={{ color: 'var(--color-text-muted)' }}
                        >
                          {formatTime(m.createdAt)}
                        </span>
                        {!m.isDeleted && (
                          <button
                            onClick={() =>
                              setReactionPickerFor((prev) => (prev === m.id ? null : m.id))
                            }
                            className="text-base opacity-0 transition-opacity group-hover:opacity-100 [@media(hover:none)]:opacity-60"
                            title="Reager"
                          >
                            😊
                          </button>
                        )}
                        {canDeleteMessage(m) && (
                          <button
                            onClick={() => handleDelete(m.id)}
                            className="ml-auto text-[10px] opacity-0 transition-opacity hover:underline group-hover:opacity-100"
                            style={{ color: 'var(--color-danger)' }}
                          >
                            Slett
                          </button>
                        )}
                      </div>
                      {m.isDeleted ? (
                        <p
                          className="text-sm italic"
                          style={{ color: 'var(--color-text-muted)' }}
                        >
                          Melding slettet
                        </p>
                      ) : (
                        <div
                          className="chat-content text-sm"
                          style={{ color: 'var(--color-text-primary)', wordBreak: 'break-word' }}
                          dangerouslySetInnerHTML={{ __html: renderMarkdown(m.content) }}
                        />
                      )}
                      {reactionPickerFor === m.id && (
                        <div
                          className="mt-1 flex gap-1 rounded-full border px-2 py-1 shadow-md"
                          style={{
                            backgroundColor: 'var(--color-surface-card)',
                            borderColor: 'var(--color-border)',
                          }}
                        >
                          {EMOJI_OPTIONS.map((emoji) => (
                            <button
                              key={emoji}
                              onClick={() => {
                                void toggleReaction(m.id, emoji)
                                setReactionPickerFor(null)
                              }}
                              className="rounded-full px-1 py-0.5 text-base transition-transform hover:scale-125"
                              title={emoji}
                            >
                              {emoji}
                            </button>
                          ))}
                        </div>
                      )}
                      {m.reactions.length > 0 && (
                        <div className="mt-1 flex flex-wrap gap-1">
                          {m.reactions.map((r) => (
                            <button
                              key={r.emoji}
                              onClick={() => void toggleReaction(m.id, r.emoji)}
                              className="flex items-center gap-0.5 rounded-full border px-2 py-0.5 text-xs transition-colors"
                              style={{
                                backgroundColor: r.reactedByMe
                                  ? 'color-mix(in srgb, var(--color-tab-active) 15%, transparent)'
                                  : 'var(--color-surface)',
                                borderColor: r.reactedByMe
                                  ? 'var(--color-tab-active)'
                                  : 'var(--color-border)',
                                color: 'var(--color-text-primary)',
                              }}
                              title={r.reactedByMe ? 'Fjern reaksjon' : 'Legg til reaksjon'}
                            >
                              <span>{r.emoji}</span>
                              <span>{r.count}</span>
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                  </Fragment>
                )
              })}
            </div>
          ))
        )}
      </div>

      <div className="border-t p-3" style={{ borderColor: 'var(--color-border)' }}>
        {error && (
          <p
            className="mb-2 text-xs"
            style={{ color: 'var(--color-danger)' }}
            role="alert"
          >
            {error}
          </p>
        )}
        <div
          className="flex items-end gap-2 rounded-md border p-2"
          style={{
            backgroundColor: 'var(--color-surface)',
            borderColor: 'var(--color-border)',
          }}
        >
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Skriv en melding… (markdown støttes)"
            rows={1}
            maxLength={MAX_LENGTH + 100}
            className="min-h-6 max-h-32 flex-1 resize-none bg-transparent text-sm focus:outline-none"
            style={{ color: 'var(--color-text-primary)' }}
          />
          <button
            onClick={handleSend}
            disabled={isSending || draft.trim().length === 0 || draft.trim().length > MAX_LENGTH}
            className="rounded px-3 py-1.5 text-sm font-medium transition-opacity hover:opacity-80 disabled:opacity-50"
            style={{
              backgroundColor: 'var(--color-tab-active)',
              color: 'var(--color-text-inverse)',
            }}
          >
            Send
          </button>
        </div>
        <div className="mt-1 flex items-center justify-between text-[10px]" style={{ color: 'var(--color-text-muted)' }}>
          <span>Enter for å sende, Shift+Enter for ny linje</span>
          <span style={{ color: remaining < 0 ? 'var(--color-danger)' : 'var(--color-text-muted)' }}>
            {remaining}
          </span>
        </div>
      </div>
    </div>
  )
}
