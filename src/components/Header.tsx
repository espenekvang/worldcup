import { useAuth } from '../context/AuthContext'

interface HeaderProps {
  onAdminClick?: () => void
}

export default function Header({ onAdminClick }: HeaderProps) {
  const { user, logout } = useAuth()

  return (
    <header className="bg-gradient-to-r from-blue-900 to-blue-700 py-4 text-white sm:py-6">
      <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between">
          <div className="min-w-0 flex-1 text-center">
            <h1 className="text-2xl font-bold sm:text-4xl">⚽ FIFA VM 2026 🏆</h1>
            <p className="mt-1 text-sm text-blue-200 sm:mt-2 sm:text-base">USA • Mexico • Canada</p>
          </div>

          {user ? (
            <div className="ml-3 flex shrink-0 items-center gap-2 sm:gap-3">
              {user.picture ? (
                <img
                  src={user.picture}
                  alt={user.name}
                  className="h-7 w-7 rounded-full sm:h-8 sm:w-8"
                  referrerPolicy="no-referrer"
                />
              ) : null}
              <span className="hidden text-sm text-blue-100 sm:block">{user.name}</span>
              {onAdminClick ? (
                <button
                  onClick={onAdminClick}
                  className="rounded-md bg-amber-600 px-2 py-1 text-xs font-medium text-white hover:bg-amber-500 sm:px-3 sm:py-1.5"
                >
                  Admin
                </button>
              ) : null}
              <button
                onClick={logout}
                className="rounded-md bg-blue-800 px-2 py-1 text-xs font-medium text-blue-100 hover:bg-blue-600 sm:px-3 sm:py-1.5"
              >
                Logg ut
              </button>
            </div>
          ) : null}
        </div>
      </div>
    </header>
  )
}
