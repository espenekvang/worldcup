import { useState, useRef, useEffect } from 'react'
import { useAuth } from '../context/AuthContext'
import { useBettingGroup } from '../context/BettingGroupContext'
import { firstName } from '../utils/nameUtils'
import { useTheme } from '../hooks/useTheme'

interface HeaderProps {
  onAdminClick?: () => void
}

export default function Header({ onAdminClick }: HeaderProps) {
  const { user, logout } = useAuth()
  const { groups, activeGroup, clearActiveGroup } = useBettingGroup()
  const { theme, toggle: toggleTheme } = useTheme()
  const [menuOpen, setMenuOpen] = useState(false)
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
      className="py-2 text-white sm:py-4"
      style={{ background: 'linear-gradient(to right, var(--color-header-from), var(--color-header-to))' }}
    >
      <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between">
          <div className="min-w-0 flex-1">
            <h1 className="text-xl font-thin sm:text-3xl">
              VM-Betting 2026
            </h1>
            <p className="mt-0.5 text-xs font-thin sm:mt-1 sm:text-sm" style={{ color: 'var(--color-header-text-muted)' }}>
              {activeGroup ? activeGroup.name : 'USA • Mexico • Canada'}
            </p>
            <p className="text-[10px] font-thin opacity-40" style={{ color: 'var(--color-header-text-muted)' }}>
              v{__APP_VERSION__}
            </p>
          </div>

          {user ? (
            <div className="relative ml-3 shrink-0" ref={menuRef}>
              <button
                onClick={() => setMenuOpen(prev => !prev)}
                className="flex items-center rounded-full transition-opacity hover:opacity-80"
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
                    style={{ backgroundColor: 'var(--color-header-btn)', color: 'var(--color-text-inverse)' }}
                  >
                    {firstName(user.name).charAt(0).toUpperCase()}
                  </div>
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

                    {onAdminClick ? (
                      <button
                        onClick={() => { setMenuOpen(false); onAdminClick() }}
                        className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors hover:opacity-80"
                        style={{ color: 'var(--color-text-primary)' }}
                      >
                        <span className="w-5 text-center">⚙️</span>
                        Admin
                      </button>
                    ) : null}

                    {groups.length > 1 ? (
                      <button
                        onClick={() => { setMenuOpen(false); clearActiveGroup() }}
                        className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors hover:opacity-80"
                        style={{ color: 'var(--color-text-primary)' }}
                      >
                        <span className="w-5 text-center">🔄</span>
                        Bytt gruppe
                      </button>
                    ) : null}

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
          ) : null}
        </div>
      </div>
    </header>
  )
}
