import { useAuth } from '../context/AuthContext'
import { firstName } from '../utils/nameUtils'
import ThemeToggle from './ThemeToggle'

interface HeaderProps {
  onAdminClick?: () => void
}

export default function Header({ onAdminClick }: HeaderProps) {
  const { user, logout } = useAuth()

  return (
    <header
      className="py-4 text-white sm:py-6"
      style={{ background: 'linear-gradient(to right, var(--color-header-from), var(--color-header-to))' }}
    >
      <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between">
          <div className="min-w-0 flex-1 text-center">
            <h1 className="text-2xl font-bold sm:text-4xl">
              <span className="text-wc-gold">⚽</span> VM-Betting 2026 <span className="text-wc-gold">🏆</span>
            </h1>
            <p className="mt-1 text-sm sm:mt-2 sm:text-base" style={{ color: 'var(--color-header-text-muted)' }}>
              USA • Mexico • Canada
            </p>
          </div>

          <div className="ml-3 flex shrink-0 items-center gap-2 sm:gap-3">
            <ThemeToggle />
            {user ? (
              <>
                {user.picture ? (
                  <img
                    src={user.picture}
                    alt={firstName(user.name)}
                    className="h-7 w-7 rounded-full sm:h-8 sm:w-8"
                    referrerPolicy="no-referrer"
                  />
                ) : null}
                <span className="hidden text-sm sm:block" style={{ color: 'var(--color-header-text-muted)' }}>
                  {firstName(user.name)}
                </span>
                {onAdminClick ? (
                  <button
                    onClick={onAdminClick}
                    className="rounded-md bg-wc-gold px-2 py-1 text-xs font-medium text-wc-navy hover:bg-wc-gold-light sm:px-3 sm:py-1.5"
                  >
                    Admin
                  </button>
                ) : null}
                <button
                  onClick={logout}
                  className="rounded-md px-2 py-1 text-xs font-medium transition-colors hover:opacity-80 sm:px-3 sm:py-1.5"
                  style={{ backgroundColor: 'var(--color-header-btn)', color: 'var(--color-header-text-muted)' }}
                >
                  Logg ut
                </button>
              </>
            ) : null}
          </div>
        </div>
      </div>
    </header>
  )
}
