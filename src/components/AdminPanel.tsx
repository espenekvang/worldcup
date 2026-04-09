import { useCallback, useEffect, useState } from 'react'
import type { InvitationResponse } from '../api/client'
import { getInvitations, createInvitation, deleteInvitation } from '../api/client'

export default function AdminPanel() {
  const [invitations, setInvitations] = useState<InvitationResponse[]>([])
  const [email, setEmail] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadInvitations = useCallback(async () => {
    try {
      const data = await getInvitations()
      setInvitations(data)
    } catch {
      setError('Kunne ikke laste invitasjoner')
    }
  }, [])

  useEffect(() => {
    loadInvitations()
  }, [loadInvitations])

  async function handleInvite(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = email.trim()
    if (!trimmed) return

    setIsLoading(true)
    setError(null)

    try {
      await createInvitation(trimmed)
      setEmail('')
      await loadInvitations()
    } catch (err) {
      if (err instanceof Error && err.message.includes('409')) {
        setError('Denne e-postadressen er allerede invitert.')
      } else {
        setError(err instanceof Error ? err.message : 'Kunne ikke sende invitasjon')
      }
    } finally {
      setIsLoading(false)
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteInvitation(id)
      setInvitations((prev) => prev.filter((i) => i.id !== id))
    } catch {
      setError('Kunne ikke fjerne invitasjon')
    }
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6">
      <h2 className="text-lg font-semibold text-gray-900">Administrer invitasjoner</h2>
      <p className="mt-1 text-sm text-gray-500">Kun inviterte brukere kan logge inn og tippe.</p>

      <form onSubmit={handleInvite} className="mt-4 flex gap-2">
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="navn@eksempel.no"
          className="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20"
          required
        />
        <button
          type="submit"
          disabled={isLoading}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {isLoading ? 'Sender...' : 'Inviter'}
        </button>
      </form>

      {error ? (
        <p className="mt-3 text-sm text-red-600">{error}</p>
      ) : null}

      {invitations.length > 0 ? (
        <ul className="mt-4 divide-y divide-gray-100">
          {invitations.map((invitation) => (
            <li key={invitation.id} className="flex items-center justify-between py-3">
              <span className="text-sm text-gray-700">{invitation.email}</span>
              <button
                onClick={() => handleDelete(invitation.id)}
                className="rounded-md px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50"
              >
                Fjern
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-4 text-sm text-gray-400">Ingen invitasjoner ennå.</p>
      )}
    </div>
  )
}
