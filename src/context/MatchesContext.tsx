import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { Match } from '../types'
import { getMatches } from '../api/client'
import { matches as staticMatches } from '../data'

interface MatchesContextValue {
  matches: Match[]
  isLoading: boolean
}

const MatchesContext = createContext<MatchesContextValue | null>(null)

export function MatchesProvider({ children }: { children: ReactNode }) {
  const [matches, setMatches] = useState<Match[]>(staticMatches)
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    let cancelled = false

    setIsLoading(true)

    getMatches()
      .then((data) => {
        if (cancelled) return
        setMatches(data)
      })
      .catch(() => {
        if (cancelled) return
        setMatches(staticMatches)
      })
      .finally(() => {
        if (cancelled) return
        setIsLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  const value = useMemo(() => ({ matches, isLoading }), [matches, isLoading])

  return <MatchesContext value={value}>{children}</MatchesContext>
}

export function useMatches(): MatchesContextValue {
  const context = useContext(MatchesContext)
  if (!context) {
    throw new Error('useMatches must be used within a MatchesProvider')
  }
  return context
}
