import { GoogleLogin } from '@react-oauth/google'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { useState } from 'react'

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
    <div className="flex min-h-screen flex-col items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-lg sm:p-8">
        <div className="mb-6 text-center sm:mb-8">
          <h1 className="text-2xl font-bold text-gray-900 sm:text-3xl">⚽ VM-Tipping 2026 🏆</h1>
          <p className="mt-2 text-sm text-gray-500 sm:text-base">Logg inn for å tippe på VM-kampene</p>
        </div>

        <div className="flex flex-col items-center gap-4">
          {isLoading ? (
            <div className="flex items-center gap-2 text-gray-500">
              <div className="h-5 w-5 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
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
            <p className="mt-2 text-sm text-red-600">{error}</p>
          ) : null}
        </div>

        <p className="mt-8 text-center text-xs text-gray-400">
          USA • Mexico • Canada
        </p>
      </div>
    </div>
  )
}
