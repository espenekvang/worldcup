import { useCallback, useEffect, useState } from 'react'
import type { InvitationResponse } from '../api/client'
import { getInvitations, createInvitation, deleteInvitation, updateMatchTeams, setMatchResult } from '../api/client'
import { useMatches } from '../context/MatchesContext'
import { useResults } from '../context/ResultsContext'
import { teams } from '../data'

const stageNames: Record<string, string> = {
  'group': 'Gruppe',
  'round-of-32': 'R32',
  'round-of-16': 'R16',
  'quarter-final': 'QF',
  'semi-final': 'SF',
  'third-place': '3.plass',
  'final': 'Finale',
}

export default function AdminPanel() {
  const [invitations, setInvitations] = useState<InvitationResponse[]>([])
  const [email, setEmail] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Match override state
  const { matches } = useMatches()
  const { results, refreshResults } = useResults()
  const [selectedMatchId, setSelectedMatchId] = useState<number | ''>('')
  const [overrideHome, setOverrideHome] = useState<string>('')
  const [overrideAway, setOverrideAway] = useState<string>('')
  const [overrideLoading, setOverrideLoading] = useState(false)
  const [overrideError, setOverrideError] = useState<string | null>(null)
  const [overrideSuccess, setOverrideSuccess] = useState(false)

  // Result override state
  const [resultMatchId, setResultMatchId] = useState<number | ''>('')
  const [resultHome, setResultHome] = useState('')
  const [resultAway, setResultAway] = useState('')
  const [resultLoading, setResultLoading] = useState(false)
  const [resultError, setResultError] = useState<string | null>(null)
  const [resultSuccess, setResultSuccess] = useState(false)

  const knockoutMatches = matches.filter((m) => m.stage !== 'group')
  const sortedTeams = Object.values(teams).sort((a, b) => a.name.localeCompare(b.name))

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

  async function handleOverrideSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedMatchId) return

    setOverrideLoading(true)
    setOverrideError(null)
    setOverrideSuccess(false)

    try {
      await updateMatchTeams(
        Number(selectedMatchId),
        overrideHome || undefined,
        overrideAway || undefined
      )
      setOverrideSuccess(true)
      setSelectedMatchId('')
      setOverrideHome('')
      setOverrideAway('')
    } catch (err) {
      setOverrideError(err instanceof Error ? err.message : 'Kunne ikke oppdatere kamp')
    } finally {
      setOverrideLoading(false)
    }
  }

  async function handleResultSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!resultMatchId) return

    const homeScore = parseInt(resultHome, 10)
    const awayScore = parseInt(resultAway, 10)

    if (isNaN(homeScore) || isNaN(awayScore) || homeScore < 0 || awayScore < 0) {
      setResultError('Ugyldig resultat')
      return
    }

    setResultLoading(true)
    setResultError(null)
    setResultSuccess(false)

    try {
      await setMatchResult(Number(resultMatchId), homeScore, awayScore)
      await refreshResults()
      setResultSuccess(true)
      setResultMatchId('')
      setResultHome('')
      setResultAway('')
    } catch (err) {
      setResultError(err instanceof Error ? err.message : 'Kunne ikke sette resultat')
    } finally {
      setResultLoading(false)
    }
  }

  return (
    <>
      <div
        className="rounded-xl border p-4 sm:p-6"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Administrer invitasjoner</h2>
      <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>Kun inviterte brukere kan logge inn og bette.</p>

      <form onSubmit={handleInvite} className="mt-4 flex flex-col gap-2 sm:flex-row">
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="navn@eksempel.no"
          className="flex-1 rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
          style={{
            backgroundColor: 'var(--color-surface-card)',
            borderColor: 'var(--color-input-border)',
            color: 'var(--color-text-primary)',
          }}
          required
        />
        <button
          type="submit"
          disabled={isLoading}
          className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
          style={{ backgroundColor: 'var(--color-primary)' }}
        >
          {isLoading ? 'Sender...' : 'Inviter'}
        </button>
      </form>

      {error ? (
        <p className="mt-3 text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
      ) : null}

      {invitations.length > 0 ? (
        <ul className="mt-4 divide-y" style={{ borderColor: 'var(--color-border-light)' }}>
          {invitations.map((invitation) => (
            <li key={invitation.id} className="flex items-center justify-between py-3">
              <span className="text-sm" style={{ color: 'var(--color-text-secondary)' }}>{invitation.email}</span>
              <button
                onClick={() => handleDelete(invitation.id)}
                className="rounded-md px-2 py-1 text-xs font-medium transition-colors"
                style={{ color: 'var(--color-danger)' }}
              >
                Fjern
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-4 text-sm" style={{ color: 'var(--color-text-muted)' }}>Ingen invitasjoner ennå.</p>
      )}
    </div>

    <div
      className="rounded-xl border p-4 sm:p-6 mt-6"
      style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
    >
      <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Kamp-overstyring</h2>
      <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>Sett lag manuelt for sluttspillkamper.</p>

      <form onSubmit={handleOverrideSubmit} className="mt-4 flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Kamp</label>
          <select
            value={selectedMatchId}
            onChange={(e) => {
              setSelectedMatchId(e.target.value === '' ? '' : Number(e.target.value))
              setOverrideSuccess(false)
            }}
            className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
            style={{
              backgroundColor: 'var(--color-surface-card)',
              borderColor: 'var(--color-input-border)',
              color: 'var(--color-text-primary)',
            }}
          >
            <option value="">-- Velg kamp --</option>
            {knockoutMatches.map((m) => {
              const homeLabel = m.homePlaceholder || m.homeTeam || '?'
              const awayLabel = m.awayPlaceholder || m.awayTeam || '?'
              const stageLabel = stageNames[m.stage] || m.stage
              return (
                <option key={m.id} value={m.id}>
                  {stageLabel}: {homeLabel} vs {awayLabel} (kamp {m.id})
                </option>
              )
            })}
          </select>
        </div>

        {selectedMatchId && (
          <>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-1">
                <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Hjemmelag</label>
                <select
                  value={overrideHome}
                  onChange={(e) => setOverrideHome(e.target.value)}
                  className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                  style={{
                    backgroundColor: 'var(--color-surface-card)',
                    borderColor: 'var(--color-input-border)',
                    color: 'var(--color-text-primary)',
                  }}
                >
                  <option value="">-- Velg lag --</option>
                  {sortedTeams.map((t) => (
                    <option key={`home-${t.code}`} value={t.code}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Bortelag</label>
                <select
                  value={overrideAway}
                  onChange={(e) => setOverrideAway(e.target.value)}
                  className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                  style={{
                    backgroundColor: 'var(--color-surface-card)',
                    borderColor: 'var(--color-input-border)',
                    color: 'var(--color-text-primary)',
                  }}
                >
                  <option value="">-- Velg lag --</option>
                  {sortedTeams.map((t) => (
                    <option key={`away-${t.code}`} value={t.code}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="flex items-center gap-4">
              <button
                type="submit"
                disabled={overrideLoading}
                className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
                style={{ backgroundColor: 'var(--color-primary)' }}
              >
                {overrideLoading ? 'Lagrer...' : 'Lagre'}
              </button>

              {overrideSuccess && (
                <p className="text-sm font-medium" style={{ color: 'var(--color-success)' }}>
                  Oppdatert!
                </p>
              )}
            </div>
          </>
        )}

        {overrideError && (
          <p className="mt-2 text-sm" style={{ color: 'var(--color-danger)' }}>
            Feil: {overrideError}
          </p>
        )}
      </form>
    </div>

    <div
      className="rounded-xl border p-4 sm:p-6 mt-6"
      style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
    >
      <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Sett resultat</h2>
      <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>Sett eller overstyr kampresultat manuelt. Poeng beregnes automatisk.</p>

      <form onSubmit={handleResultSubmit} className="mt-4 flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Kamp</label>
          <select
            value={resultMatchId}
            onChange={(e) => {
              const id = e.target.value === '' ? '' as const : Number(e.target.value)
              setResultMatchId(id)
              setResultSuccess(false)
              if (id !== '') {
                const existing = results.get(id)
                if (existing) {
                  setResultHome(String(existing.homeScore))
                  setResultAway(String(existing.awayScore))
                } else {
                  setResultHome('')
                  setResultAway('')
                }
              }
            }}
            className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
            style={{
              backgroundColor: 'var(--color-surface-card)',
              borderColor: 'var(--color-input-border)',
              color: 'var(--color-text-primary)',
            }}
          >
            <option value="">-- Velg kamp --</option>
            {matches.map((m) => {
              const homeLabel = m.homeTeam ? (teams[m.homeTeam]?.name ?? m.homeTeam) : (m.homePlaceholder || '?')
              const awayLabel = m.awayTeam ? (teams[m.awayTeam]?.name ?? m.awayTeam) : (m.awayPlaceholder || '?')
              const stageLabel = stageNames[m.stage] || m.stage
              const existingResult = results.get(m.id)
              const resultLabel = existingResult ? ` (${existingResult.homeScore}-${existingResult.awayScore})` : ''
              return (
                <option key={m.id} value={m.id}>
                  {stageLabel}: {homeLabel} vs {awayLabel}{resultLabel} (kamp {m.id})
                </option>
              )
            })}
          </select>
        </div>

        {resultMatchId !== '' && (
          <>
            <div className="grid grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Hjemmemål</label>
                <input
                  type="number"
                  min={0}
                  value={resultHome}
                  onChange={(e) => setResultHome(e.target.value)}
                  placeholder="0"
                  className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                  style={{
                    backgroundColor: 'var(--color-surface-card)',
                    borderColor: 'var(--color-input-border)',
                    color: 'var(--color-text-primary)',
                  }}
                  required
                />
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Bortemål</label>
                <input
                  type="number"
                  min={0}
                  value={resultAway}
                  onChange={(e) => setResultAway(e.target.value)}
                  placeholder="0"
                  className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                  style={{
                    backgroundColor: 'var(--color-surface-card)',
                    borderColor: 'var(--color-input-border)',
                    color: 'var(--color-text-primary)',
                  }}
                  required
                />
              </div>
            </div>

            <div className="flex items-center gap-4">
              <button
                type="submit"
                disabled={resultLoading}
                className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
                style={{ backgroundColor: 'var(--color-primary)' }}
              >
                {resultLoading ? 'Lagrer...' : 'Lagre resultat'}
              </button>

              {resultSuccess && (
                <p className="text-sm font-medium" style={{ color: 'var(--color-success)' }}>
                  Resultat lagret!
                </p>
              )}
            </div>
          </>
        )}

        {resultError && (
          <p className="mt-2 text-sm" style={{ color: 'var(--color-danger)' }}>
            Feil: {resultError}
          </p>
        )}
      </form>
    </div>
    </>
  )
}
