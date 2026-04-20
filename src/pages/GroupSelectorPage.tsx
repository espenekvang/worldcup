import { useBettingGroup } from '../context/BettingGroupContext'
import { useAuth } from '../context/AuthContext'

export default function GroupSelectorPage() {
  const { groups, setActiveGroup } = useBettingGroup()
  const { logout } = useAuth()

  return (
    <div
      className="flex min-h-screen items-center justify-center px-4"
      style={{ backgroundColor: 'var(--color-surface-main)' }}
    >
      <div className="w-full max-w-md">
        <h1 className="mb-6 text-center text-xl font-semibold" style={{ color: 'var(--color-text-primary)' }}>
          Velg betting-gruppe
        </h1>
        <div className="space-y-3">
          {groups.map(group => (
            <button
              key={group.id}
              onClick={() => setActiveGroup(group)}
              className="flex w-full items-center justify-between rounded-xl border p-4 text-left transition-colors hover:opacity-80"
              style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
            >
              <div>
                <p className="font-medium" style={{ color: 'var(--color-text-primary)' }}>
                  {group.name}
                </p>
                <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>
                  {group.memberCount} {group.memberCount === 1 ? 'medlem' : 'medlemmer'}
                </p>
              </div>
              <span style={{ color: 'var(--color-text-muted)' }}>→</span>
            </button>
          ))}
        </div>
        <div className="mt-6 text-center">
          <button
            onClick={logout}
            className="text-sm transition-colors hover:opacity-80"
            style={{ color: 'var(--color-danger)' }}
          >
            Logg ut
          </button>
        </div>
      </div>
    </div>
  )
}
