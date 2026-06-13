import { useState, useRef, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { useChat } from '../context/ChatContext'
import { firstName } from '../utils/nameUtils'
import { useTheme } from '../hooks/useTheme'
import FeedbackModal from './FeedbackModal'
import LeagueSwitcher from './LeagueSwitcher'

export default function Header() {
  const { user, logout } = useAuth()
  const { unreadCount } = useChat()
  const { theme, toggle: toggleTheme } = useTheme()
  const navigate = useNavigate()
  const canAccessAdmin = user?.isAdmin || (user?.groupAdminGroupIds?.length ?? 0) > 0
  const [menuOpen, setMenuOpen] = useState(false)
  const [feedbackOpen, setFeedbackOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false)
      }
    }
    if (menuOpen) {
      document.addEventListener('mousedown', handleClickOutside)
      return () => document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [menuOpen])

  return (
    <header
      className="sticky top-0 z-40 py-2 text-white sm:py-4"
      style={{ background: 'linear-gradient(to right, var(--color-header-from), var(--color-header-to))' }}
    >
      <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between">
          <div className="min-w-0 flex-1">
            <h1 className="text-xl font-thin sm:text-3xl">
              VM-Betting 2026
            </h1>
            <LeagueSwitcher />
            <p className="text-[10px] font-thin opacity-40" style={{ color: 'var(--color-header-text-muted)' }}>
              v{__APP_VERSION__}
            </p>
          </div>

          {user ? (
            <div className="flex items-center">
              {canAccessAdmin && (
                <button
                  onClick={() => navigate('/', { state: { mobileView: 'admin' } })}
                  className="mr-3 flex h-8 w-8 items-center justify-center rounded-full transition-opacity hover:opacity-80 sm:h-9 sm:w-9"
                  style={{ backgroundColor: 'var(--color-header-btn)', color: '#fff' }}
                  aria-label="Admin"
                  title="Admin"
                >
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth={2}
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    className="h-4 w-4 sm:h-[18px] sm:w-[18px]"
                    aria-hidden="true"
                  >
                    <circle cx="12" cy="12" r="3" />
                    <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
                  </svg>
                </button>
              )}
              <button
                onClick={() => setFeedbackOpen(true)}
                className="flex h-8 w-8 items-center justify-center rounded-full text-sm font-bold transition-opacity hover:opacity-80 sm:h-9 sm:w-9"
                style={{ backgroundColor: 'var(--color-header-btn)', color: '#fff' }}
                aria-label="Gi tilbakemelding"
              >
                ?
              </button>
            <div className="relative ml-3 shrink-0" ref={menuRef}>
              <button
                onClick={() => setMenuOpen(prev => !prev)}
                className="relative flex items-center rounded-full transition-opacity hover:opacity-80"
                aria-label="Brukermeny"
                aria-expanded={menuOpen}
              >
                {user.picture ? (
                  <img
                    src={user.picture}
                    alt={firstName(user.name)}
                    className="h-8 w-8 rounded-full sm:h-9 sm:w-9"
                    referrerPolicy="no-referrer"
                  />
                ) : (
                  <div
                    className="flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium sm:h-9 sm:w-9"
                style={{ backgroundColor: 'rgba(255,255,255,0.25)', color: '#fff' }}
                  >
                    {firstName(user.name).charAt(0).toUpperCase()}
                  </div>
                )}
                {unreadCount > 0 && (
                  <span
                    className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-semibold ring-2"
                    style={{
                      backgroundColor: 'var(--color-danger)',
                      color: '#fff',
                      // ring matches the header gradient — use a neutral that works on both
                      // ring colour set inline so it picks up theme
                    }}
                    aria-label={`${unreadCount} uleste meldinger`}
                  >
                    {unreadCount > 99 ? '99+' : unreadCount}
                  </span>
                )}
              </button>

              {menuOpen && (
                <div
                  className="absolute right-0 z-50 mt-2 w-52 overflow-hidden rounded-lg border shadow-lg"
                  style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
                >
                  <div className="border-b px-4 py-3" style={{ borderColor: 'var(--color-border)' }}>
                    <p className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
                      {user.name}
                    </p>
                    <p className="truncate text-xs" style={{ color: 'var(--color-text-muted)' }}>
                      {user.email}
                    </p>
                  </div>

                  <div className="py-1">
                    <button
                      onClick={toggleTheme}
                      className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors hover:opacity-80"
                      style={{ color: 'var(--color-text-primary)' }}
                    >
                      <span className="w-5 text-center">{theme === 'dark' ? '☀️' : '🌙'}</span>
                      {theme === 'dark' ? 'Lyst tema' : 'Mørkt tema'}
                    </button>

                    <div className="my-1 border-t" style={{ borderColor: 'var(--color-border)' }} />

                    <button
                      onClick={() => { setMenuOpen(false); logout() }}
                      className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors hover:opacity-80"
                      style={{ color: 'var(--color-danger)' }}
                    >
                      <span className="w-5 text-center">🚪</span>
                      Logg ut
                    </button>
                  </div>
                </div>
              )}
            </div>
            </div>
          ) : null}
          {feedbackOpen && <FeedbackModal onClose={() => setFeedbackOpen(false)} />}
        </div>
      </div>
    </header>
  )
}
