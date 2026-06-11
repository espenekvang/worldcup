import { useState, useRef, useEffect } from 'react'
import { useAuth } from '../context/AuthContext'
import { useChat } from '../context/ChatContext'
import { firstName } from '../utils/nameUtils'
import { useTheme } from '../hooks/useTheme'
import FeedbackModal from './FeedbackModal'
import DisplayNameModal from './DisplayNameModal'
import LeagueSwitcher from './LeagueSwitcher'

export default function Header() {
  const { user, logout } = useAuth()
  const { unreadCount } = useChat()
  const { theme, toggle: toggleTheme } = useTheme()
  const [menuOpen, setMenuOpen] = useState(false)
  const [feedbackOpen, setFeedbackOpen] = useState(false)
  const [displayNameOpen, setDisplayNameOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  // Eget visningsnavn overstyrer Google-navnet overalt det vises.
  const shownName = user ? (user.displayName?.trim() || user.name) : ''

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
      className="py-2 text-white sm:py-4"
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
                    alt={firstName(shownName)}
                    className="h-8 w-8 rounded-full sm:h-9 sm:w-9"
                    referrerPolicy="no-referrer"
                  />
                ) : (
                  <div
                    className="flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium sm:h-9 sm:w-9"
                style={{ backgroundColor: 'rgba(255,255,255,0.25)', color: '#fff' }}
                  >
                    {firstName(shownName).charAt(0).toUpperCase()}
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
                      {shownName}
                    </p>
                    <p className="truncate text-xs" style={{ color: 'var(--color-text-muted)' }}>
                      {user.email}
                    </p>
                  </div>

                  <div className="py-1">
                    <button
                      onClick={() => { setMenuOpen(false); setDisplayNameOpen(true) }}
                      className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors hover:opacity-80"
                      style={{ color: 'var(--color-text-primary)' }}
                    >
                      <span className="w-5 text-center">✏️</span>
                      Kallenavn
                    </button>

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
          {displayNameOpen && <DisplayNameModal onClose={() => setDisplayNameOpen(false)} />}
        </div>
      </div>
    </header>
  )
}
