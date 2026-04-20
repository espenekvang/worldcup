import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { BettingGroup } from '../types'

interface BettingGroupContextValue {
  groups: BettingGroup[]
  activeGroup: BettingGroup | null
  setActiveGroup: (group: BettingGroup) => void
  clearActiveGroup: () => void
  setGroups: (groups: BettingGroup[]) => void
  isLoading: boolean
}

const BettingGroupContext = createContext<BettingGroupContextValue | null>(null)

const GROUP_KEY = 'active_group_id'

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

export function BettingGroupProvider({ children }: { children: ReactNode }) {
  const [groups, setGroupsState] = useState<BettingGroup[]>([])
  const [activeGroup, setActiveGroupState] = useState<BettingGroup | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const setGroups = useCallback((newGroups: BettingGroup[]) => {
    setGroupsState(newGroups)

    if (newGroups.length === 0) {
      setActiveGroupState(null)
      safeRemoveItem(GROUP_KEY)
      setIsLoading(false)
      return
    }

    // Try to restore from localStorage
    const storedId = safeGetItem(GROUP_KEY)
    const stored = storedId ? newGroups.find(g => g.id === storedId) : null

    if (stored) {
      setActiveGroupState(stored)
    } else if (newGroups.length === 1) {
      // Auto-select when only 1 group
      setActiveGroupState(newGroups[0])
      safeSetItem(GROUP_KEY, newGroups[0].id)
    } else {
      // Multiple groups, none stored → user must choose
      setActiveGroupState(null)
      safeRemoveItem(GROUP_KEY)
    }

    setIsLoading(false)
  }, [])

  const setActiveGroup = useCallback((group: BettingGroup) => {
    setActiveGroupState(group)
    safeSetItem(GROUP_KEY, group.id)
  }, [])

  const clearActiveGroup = useCallback(() => {
    setActiveGroupState(null)
    safeRemoveItem(GROUP_KEY)
  }, [])

  // On mount, mark as not loading if no groups are set yet
  useEffect(() => {
    // Initial load state will be resolved when setGroups is called from AuthContext
    const timer = setTimeout(() => setIsLoading(false), 100)
    return () => clearTimeout(timer)
  }, [])

  const value = useMemo(
    () => ({ groups, activeGroup, setActiveGroup, clearActiveGroup, setGroups, isLoading }),
    [groups, activeGroup, setActiveGroup, clearActiveGroup, setGroups, isLoading],
  )

  return <BettingGroupContext value={value}>{children}</BettingGroupContext>
}

export function useBettingGroup(): BettingGroupContextValue {
  const context = useContext(BettingGroupContext)
  if (!context) {
    throw new Error('useBettingGroup must be used within a BettingGroupProvider')
  }
  return context
}
