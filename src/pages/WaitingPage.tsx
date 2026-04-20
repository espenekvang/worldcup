import { useAuth } from '../context/AuthContext'
import { useTheme } from '../hooks/useTheme'

export default function WaitingPage() {
  const { logout } = useAuth()
  const { theme, toggle: toggleTheme } = useTheme()

  return (
    <div
      className="flex min-h-screen items-center justify-center px-4"
      style={{ backgroundColor: 'var(--color-surface-main)' }}
    >
      <div
        className="w-full max-w-md rounded-xl border p-8 text-center"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <div className="text-4xl">⏳</div>
        <h1 className="mt-4 text-xl font-semibold" style={{ color: 'var(--color-text-primary)' }}>
          Venter på tilgang
        </h1>
        <p className="mt-2 text-sm" style={{ color: 'var(--color-text-muted)' }}>
          Du er ikke lagt til i noen liga ennå. Kontakt administratoren for å bli invitert.
        </p>
        <div className="mt-6 flex justify-center gap-3">
          <button
            onClick={toggleTheme}
            className="rounded-lg border px-4 py-2 text-sm transition-colors"
            style={{ borderColor: 'var(--color-border)', color: 'var(--color-text-primary)' }}
          >
            {theme === 'dark' ? '☀️ Lyst tema' : '🌙 Mørkt tema'}
          </button>
          <button
            onClick={logout}
            className="rounded-lg px-4 py-2 text-sm font-medium text-white transition-colors"
            style={{ backgroundColor: 'var(--color-danger)' }}
          >
            Logg ut
          </button>
        </div>
      </div>
    </div>
  )
}
