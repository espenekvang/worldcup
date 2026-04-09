import { useAuth } from '../context/AuthContext'

interface HeaderProps {
  onAdminClick?: () => void
}

export default function Header({ onAdminClick }: HeaderProps) {
  const { user, logout } = useAuth()

  return (
    <header className="bg-gradient-to-r from-blue-900 to-blue-700 py-6 text-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 sm:px-6 lg:px-8">
        <div className="flex-1 text-center">
          <h1 className="text-3xl font-bold sm:text-4xl">⚽ FIFA World Cup 2026 🏆</h1>
          <p className="mt-2 text-blue-200">USA • Mexico • Canada</p>
        </div>

        {user ? (
          <div className="flex items-center gap-3">
            {user.picture ? (
              <img
                src={user.picture}
                alt={user.name}
                className="h-8 w-8 rounded-full"
                referrerPolicy="no-referrer"
              />
            ) : null}
            <span className="hidden text-sm text-blue-100 sm:block">{user.name}</span>
            {onAdminClick ? (
              <button
                onClick={onAdminClick}
                className="rounded-md bg-amber-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-amber-500"
              >
                Admin
              </button>
            ) : null}
            <button
              onClick={logout}
              className="rounded-md bg-blue-800 px-3 py-1.5 text-xs font-medium text-blue-100 hover:bg-blue-600"
            >
              Logg ut
            </button>
          </div>
        ) : null}
      </div>
    </header>
  )
}
