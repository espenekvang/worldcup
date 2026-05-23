import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { acceptInviteLink, getInviteLinkInfo } from '../api/client'
import { useAuth } from '../context/AuthContext'
import { useBettingGroup } from '../context/BettingGroupContext'
import ThemeToggle from '../components/ThemeToggle'

const PENDING_INVITE_KEY = 'pending_invite_token'

type Status = 'loading' | 'invalid' | 'needs-login' | 'accepting' | 'success' | 'error'

export default function InvitePage() {
  const { token } = useParams<{ token: string }>()
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const { groups, setGroups, setActiveGroup } = useBettingGroup()

  const [status, setStatus] = useState<Status>('loading')
  const [groupName, setGroupName] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!token) {
      setStatus('invalid')
      return
    }

    let cancelled = false

    async function run() {
      // Lagre alltid token-en først, så vi kan re-prøve å akseptere
      // invitasjonen etter en eventuell ny innlogging (f.eks. utløpt JWT).
      try { localStorage.setItem(PENDING_INVITE_KEY, token!) } catch { /* ignore */ }

      try {
        const info = await getInviteLinkInfo(token!)
        if (cancelled) return
        setGroupName(info.groupName)

        if (!user) {
          setStatus('needs-login')
          return
        }

        setStatus('accepting')
        const result = await acceptInviteLink(token!)
        if (cancelled) return

        // Merge into local groups state if missing
        const newGroup = {
          id: result.bettingGroupId,
          name: result.groupName,
          memberCount: 0,
          createdAt: new Date().toISOString(),
          isPaid: false,
          entryFee: 0,
          prizePot: 0,
          paidMemberCount: 0,
          currentUserHasPaid: false,
        }
        const merged = groups.some((g) => g.id === newGroup.id)
          ? groups
          : [...groups, newGroup]
        setGroups(merged)
        const target = merged.find((g) => g.id === newGroup.id) ?? newGroup
        setActiveGroup(target)

        try { localStorage.removeItem(PENDING_INVITE_KEY) } catch { /* ignore */ }
        setStatus('success')

        // Brief delay so user sees confirmation, then redirect
        window.setTimeout(() => {
          if (!cancelled) navigate('/', { replace: true })
        }, 1200)
      } catch (err) {
        if (cancelled) return
        const msg = err instanceof Error ? err.message : 'Noe gikk galt'
        // Utløpt/ugyldig JWT: send brukeren til login. API-klienten har allerede
        // ryddet auth_token/auth_user, og pending_invite_token gjør at login-
        // flyten auto-joiner gruppen via /api/auth/google.
        if (msg.includes('API error 401')) {
          logout()
          navigate('/login', { replace: true })
          return
        }
        setErrorMessage(msg)
        setStatus(user ? 'error' : 'invalid')
      }
    }

    run()
    return () => { cancelled = true }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token, user, logout, navigate, groups, setGroups, setActiveGroup])

  return (
    <div className="flex min-h-screen flex-col items-center justify-center px-4" style={{ backgroundColor: 'var(--color-surface)' }}>
      <div className="absolute right-4 top-4">
        <ThemeToggle />
      </div>
      <div
        className="w-full max-w-md rounded-xl p-6 shadow-lg sm:p-8"
        style={{ backgroundColor: 'var(--color-surface-card)' }}
      >
        <h1 className="mb-4 text-center text-2xl font-bold" style={{ color: 'var(--color-text-primary)' }}>
          Invitasjon til liga
        </h1>

        {status === 'loading' && (
          <p className="text-center" style={{ color: 'var(--color-text-muted)' }}>
            Sjekker invitasjon…
          </p>
        )}

        {status === 'invalid' && (
          <div className="text-center">
            <p style={{ color: 'var(--color-danger)' }}>
              Denne invitasjonslenken er ugyldig eller utløpt.
            </p>
            <button
              type="button"
              onClick={() => navigate('/login', { replace: true })}
              className="mt-4 rounded-md px-4 py-2 text-white"
              style={{ backgroundColor: 'var(--color-primary)' }}
            >
              Gå til innlogging
            </button>
          </div>
        )}

        {status === 'needs-login' && (
          <div className="text-center">
            <p className="mb-4" style={{ color: 'var(--color-text-primary)' }}>
              Du er invitert til ligaen <strong>{groupName}</strong>.
            </p>
            <p className="mb-4 text-sm" style={{ color: 'var(--color-text-muted)' }}>
              Logg inn med Google for å bli med.
            </p>
            <button
              type="button"
              onClick={() => navigate('/login', { replace: true })}
              className="rounded-md px-4 py-2 text-white"
              style={{ backgroundColor: 'var(--color-primary)' }}
            >
              Gå til innlogging
            </button>
          </div>
        )}

        {status === 'accepting' && (
          <p className="text-center" style={{ color: 'var(--color-text-muted)' }}>
            Legger deg til i <strong>{groupName}</strong>…
          </p>
        )}

        {status === 'success' && (
          <p className="text-center" style={{ color: 'var(--color-text-primary)' }}>
            Du er nå medlem av <strong>{groupName}</strong>! Sender deg videre…
          </p>
        )}

        {status === 'error' && (
          <div className="text-center">
            <p style={{ color: 'var(--color-danger)' }}>
              Kunne ikke godta invitasjonen: {errorMessage}
            </p>
            <button
              type="button"
              onClick={() => navigate('/', { replace: true })}
              className="mt-4 rounded-md px-4 py-2 text-white"
              style={{ backgroundColor: 'var(--color-primary)' }}
            >
              Til forsiden
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
