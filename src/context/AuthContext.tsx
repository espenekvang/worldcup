import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { AuthResponse } from '../api/client'
import { loginWithGoogle as apiLoginWithGoogle } from '../api/client'
import type { BettingGroup } from '../types'
import { useBettingGroup } from './BettingGroupContext'

interface AuthUser {
  email: string
  name: string
  picture: string | null
  isAdmin: boolean
  groups: BettingGroup[]
  groupAdminGroupIds: string[]
}

interface AuthContextValue {
  user: AuthUser | null
  isLoading: boolean
  loginWithGoogle: (idToken: string) => Promise<void>
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
  const { setGroups, clearActiveGroup } = useBettingGroup()

  // On mount: restore groups from stored user
  useEffect(() => {
    const token = safeGetItem(TOKEN_KEY)
    if (!token) {
      setUser(null)
      safeRemoveItem(USER_KEY)
      setGroups([])
    } else {
      const storedUser = loadStoredUser()
      if (storedUser?.groups) {
        setGroups(storedUser.groups)
      }
    }
  }, [setGroups])

  const loginWithGoogle = useCallback(async (idToken: string) => {
    setIsLoading(true)
    try {
      const response: AuthResponse = await apiLoginWithGoogle(idToken)
      safeSetItem(TOKEN_KEY, response.token)

      const authUser: AuthUser = {
        email: response.email,
        name: response.name,
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

  const logout = useCallback(() => {
    safeRemoveItem(TOKEN_KEY)
    safeRemoveItem(USER_KEY)
    safeRemoveItem('active_group_id')
    setUser(null)
    setGroups([])
    clearActiveGroup()
  }, [setGroups, clearActiveGroup])

  const value = useMemo(
    () => ({ user, isLoading, loginWithGoogle, logout }),
    [user, isLoading, loginWithGoogle, logout],
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
