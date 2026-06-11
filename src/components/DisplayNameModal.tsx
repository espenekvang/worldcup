import { useState } from 'react'
import { useAuth } from '../context/AuthContext'

interface DisplayNameModalProps {
  onClose: () => void
}

const MAX_LENGTH = 40

export default function DisplayNameModal({ onClose }: DisplayNameModalProps) {
  const { user, updateDisplayName } = useAuth()
  const [value, setValue] = useState(user?.displayName ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') onClose()
  }

  const handleSave = async () => {
    setSaving(true)
    setError(null)
    try {
      await updateDisplayName(value.trim())
      onClose()
    } catch {
      setError('Kunne ikke lagre visningsnavn. Prøv igjen senere.')
    } finally {
      setSaving(false)
    }
  }

  const trimmed = value.trim()
  const current = user?.displayName ?? ''
  const hasChange = trimmed !== current.trim()

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
      onKeyDown={handleKeyDown}
    >
      <div
        className="mx-4 w-full max-w-md rounded-lg border p-6 shadow-xl"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
        onClick={e => e.stopPropagation()}
      >
        <h2 className="mb-1 text-lg font-medium" style={{ color: 'var(--color-text-primary)' }}>
          Visningsnavn
        </h2>
        <p className="mb-4 text-sm" style={{ color: 'var(--color-text-muted)' }}>
          Velg navnet som skal vises for deg på poengtavlen, i tipsoversikter og i chatten.
          La feltet stå tomt for å bruke navnet fra Google-kontoen din.
        </p>

        <input
          type="text"
          className="w-full rounded border p-2 text-sm"
          style={{
            backgroundColor: 'var(--color-surface-base)',
            borderColor: 'var(--color-border)',
            color: 'var(--color-text-primary)',
          }}
          value={value}
          maxLength={MAX_LENGTH}
          onChange={e => setValue(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter' && hasChange && !saving) handleSave() }}
          placeholder={user?.name ?? 'Ditt visningsnavn'}
          autoFocus
        />
        <p className="mt-1 text-right text-xs" style={{ color: 'var(--color-text-muted)' }}>
          {value.length}/{MAX_LENGTH}
        </p>

        {error && (
          <p className="mt-2 text-sm" style={{ color: 'var(--color-danger)' }}>
            {error}
          </p>
        )}

        <div className="mt-3 flex gap-2">
          <button
            onClick={onClose}
            className="flex-1 rounded border px-4 py-2 text-sm font-medium transition-opacity hover:opacity-80"
            style={{ borderColor: 'var(--color-border)', color: 'var(--color-text-primary)' }}
          >
            Avbryt
          </button>
          <button
            onClick={handleSave}
            disabled={saving || !hasChange}
            className="flex-1 rounded px-4 py-2 text-sm font-medium text-white transition-opacity disabled:opacity-50"
            style={{ backgroundColor: 'var(--color-primary)' }}
          >
            {saving ? 'Lagrer...' : 'Lagre'}
          </button>
        </div>
      </div>
    </div>
  )
}
