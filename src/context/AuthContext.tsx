import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { AuthResponse } from '../api/client'
import {
  loginWithGoogle as apiLoginWithGoogle,
  getMyGroups,
  updateDisplayName as apiUpdateDisplayName,
} from '../api/client'
import type { BettingGroup } from '../types'
import { useBettingGroup } from './BettingGroupContext'

interface AuthUser {
  email: string
  name: string
  /** Selvvalgt visningsnavn. Null = bruk `name`. */
  displayName: string | null
  picture: string | null
  isAdmin: boolean
  groups: BettingGroup[]
  groupAdminGroupIds: string[]
}

interface AuthContextValue {
  user: AuthUser | null
  isLoading: boolean
  loginWithGoogle: (idToken: string, inviteToken?: string) => Promise<void>
  /** Setter/nullstiller eget visningsnavn. Tom streng nullstiller det. */
  updateDisplayName: (displayName: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

const TOKEN_KEY = 'auth_token'
const USER_KEY = 'auth_user'

function safeGetItem(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function safeSetItem(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    // storage unavailable
  }
}

function safeRemoveItem(key: string): void {
  try {
    localStorage.removeItem(key)
  } catch {
    // storage unavailable
  }
}

function loadStoredUser(): AuthUser | null {
  try {
    const stored = safeGetItem(USER_KEY)
    if (!stored) return null
    return JSON.parse(stored) as AuthUser
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(loadStoredUser)
  const [isLoading, setIsLoading] = useState(false)
  const { setGroups } = useBettingGroup()

  // On mount: restore groups from stored user, deretter hent ferske grupper fra
  // serveren. Den cachede innloggingen kan ha utdaterte visningsfelter (f.eks.
  // showFullName endret av liga-admin etter innlogging), så vi synker på nytt
  // slik at hele appen – ikke bare The Boss-listen – ser gjeldende innstilling.
  useEffect(() => {
    const token = safeGetItem(TOKEN_KEY)
    if (!token) {
      setUser(null)
      safeRemoveItem(USER_KEY)
      setGroups([])
      return
    }

    const storedUser = loadStoredUser()
    if (storedUser?.groups) {
      setGroups(storedUser.groups)
    }

    let cancelled = false
    getMyGroups()
      .then(freshGroups => {
        if (cancelled) return
        setGroups(freshGroups)
        // Hold den cachede brukeren i synk så neste oppstart starter oppdatert.
        const cached = loadStoredUser()
        if (cached) {
          safeSetItem(USER_KEY, JSON.stringify({ ...cached, groups: freshGroups }))
        }
      })
      .catch(() => {
        // Behold cachede grupper hvis nettverket feiler.
      })

    return () => { cancelled = true }
  }, [setGroups])

  const loginWithGoogle = useCallback(async (idToken: string, inviteToken?: string) => {
    setIsLoading(true)
    try {
      const response: AuthResponse = await apiLoginWithGoogle(idToken, inviteToken)
      safeSetItem(TOKEN_KEY, response.token)

      const authUser: AuthUser = {
        email: response.email,
        name: response.name,
        displayName: response.displayName ?? null,
        picture: response.picture,
        isAdmin: response.isAdmin,
        groups: response.groups,
        groupAdminGroupIds: response.groupAdminGroupIds ?? [],
      }

      safeSetItem(USER_KEY, JSON.stringify(authUser))
      setUser(authUser)
      setGroups(response.groups)
    } finally {
      setIsLoading(false)
    }
  }, [setGroups])

  const updateDisplayName = useCallback(async (displayName: string) => {
    const response = await apiUpdateDisplayName(displayName)
    setUser(prev => {
      if (!prev) return prev
      const updated = { ...prev, displayName: response.displayName ?? null }
      safeSetItem(USER_KEY, JSON.stringify(updated))
      return updated
    })
  }, [])

  const logout = useCallback(() => {
    safeRemoveItem(TOKEN_KEY)
    safeRemoveItem(USER_KEY)
    safeRemoveItem('active_group_id')
    setUser(null)
    setGroups([])
  }, [setGroups])

  const value = useMemo(
    () => ({ user, isLoading, loginWithGoogle, updateDisplayName, logout }),
    [user, isLoading, loginWithGoogle, updateDisplayName, logout],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
