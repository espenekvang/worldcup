import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { getFeatureFlags, type FeatureFlags } from '../api/client'
import { useBettingGroup } from './BettingGroupContext'
import { useAuth } from './AuthContext'

interface FeatureFlagsContextValue {
  flags: FeatureFlags
  isLoading: boolean
  /** Check whether a named flag is enabled. Unknown flags default to false. */
  isEnabled: (name: string) => boolean
}

const FeatureFlagsContext = createContext<FeatureFlagsContextValue | null>(null)

/**
 * Loads feature flags from the backend whenever the active betting group changes.
 *
 * Backend evaluates flags via Microsoft.FeatureManagement using the BettingGroupFilter,
 * so a flag can be on for some leagues and off for others.
 */
export function FeatureFlagsProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const { activeGroup } = useBettingGroup()
  const [flags, setFlags] = useState<FeatureFlags>({})
  const [isLoading, setIsLoading] = useState(false)
  const requestIdRef = useRef(0)

  useEffect(() => {
    if (!user || !activeGroup) {
      setFlags({})
      setIsLoading(false)
      return
    }

    const requestId = ++requestIdRef.current
    setIsLoading(true)

    getFeatureFlags(activeGroup.id)
      .then(result => {
        if (requestId === requestIdRef.current) {
          setFlags(result ?? {})
        }
      })
      .catch(() => {
        if (requestId === requestIdRef.current) {
          setFlags({})
        }
      })
      .finally(() => {
        if (requestId === requestIdRef.current) {
          setIsLoading(false)
        }
      })
  }, [user, activeGroup])

  const value = useMemo<FeatureFlagsContextValue>(() => {
    const lookup = new Map<string, boolean>()
    for (const [k, v] of Object.entries(flags)) {
      lookup.set(k.toLowerCase(), v)
    }
    return {
      flags,
      isLoading,
      isEnabled: (name: string) => lookup.get(name.toLowerCase()) === true,
    }
  }, [flags, isLoading])

  return <FeatureFlagsContext value={value}>{children}</FeatureFlagsContext>
}

export function useFeatureFlags(): FeatureFlagsContextValue {
  const ctx = useContext(FeatureFlagsContext)
  if (!ctx) {
    throw new Error('useFeatureFlags must be used within a FeatureFlagsProvider')
  }
  return ctx
}

/** Convenience hook: returns true when the named flag is enabled for the active group. */
export function useFeatureFlag(name: string): boolean {
  return useFeatureFlags().isEnabled(name)
}
