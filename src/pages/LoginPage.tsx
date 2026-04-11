import { GoogleLogin } from '@react-oauth/google'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { useState } from 'react'
import ThemeToggle from '../components/ThemeToggle'

export default function LoginPage() {
  const { loginWithGoogle, isLoading } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  async function handleGoogleSuccess(credentialResponse: { credential?: string }) {
    if (!credentialResponse.credential) {
      setError('Ingen credential mottatt fra Google')
      return
    }

    try {
      setError(null)
      await loginWithGoogle(credentialResponse.credential)
      navigate('/', { replace: true })
    } catch (err) {
      if (err instanceof Error && err.message.includes('403')) {
        setError('Du er ikke invitert. Be administrator om en invitasjon.')
      } else {
        setError(err instanceof Error ? err.message : 'Innlogging feilet')
      }
    }
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center px-4" style={{ backgroundColor: 'var(--color-surface)' }}>
      <div className="absolute right-4 top-4">
        <ThemeToggle />
      </div>
      <div
        className="w-full max-w-md rounded-xl p-6 shadow-lg sm:p-8"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <div className="mb-6 text-center sm:mb-8">
          <h1 className="text-2xl font-bold sm:text-3xl" style={{ color: 'var(--color-text-primary)' }}>
            <span className="text-wc-gold">⚽</span> VM-Betting 2026 <span className="text-wc-gold">🏆</span>
          </h1>
          <p className="mt-2 text-sm sm:text-base" style={{ color: 'var(--color-text-muted)' }}>
            Logg inn for å bette på VM-kampene
          </p>
        </div>

        <div className="flex flex-col items-center gap-4">
          {isLoading ? (
            <div className="flex items-center gap-2" style={{ color: 'var(--color-text-muted)' }}>
              <div
                className="h-5 w-5 animate-spin rounded-full border-2 border-t-transparent"
                style={{ borderColor: 'var(--color-primary)', borderTopColor: 'transparent' }}
              />
              Logger inn...
            </div>
          ) : (
            <GoogleLogin
              onSuccess={handleGoogleSuccess}
              onError={() => setError('Google-innlogging feilet')}
              theme="outline"
              size="large"
            />
          )}

          {error ? (
            <p className="mt-2 text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
          ) : null}
        </div>

        <p className="mt-8 text-center text-xs" style={{ color: 'var(--color-text-muted)' }}>
          USA • Mexico • Canada
        </p>
      </div>
    </div>
  )
}
