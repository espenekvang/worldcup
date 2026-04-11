import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { PointsResponse, ResultResponse } from '../api/client'
import { getResults, getUserPoints } from '../api/client'
import { useAuth } from './AuthContext'

interface ResultsContextValue {
  results: Map<number, ResultResponse>
  points: Map<number, PointsResponse>
  isLoading: boolean
}

const ResultsContext = createContext<ResultsContextValue | null>(null)

export function ResultsProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const [results, setResults] = useState<Map<number, ResultResponse>>(new Map())
  const [points, setPoints] = useState<Map<number, PointsResponse>>(new Map())
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    let cancelled = false

    setIsLoading(true)

    Promise.all([
      getResults(),
      user ? getUserPoints() : Promise.resolve([] as PointsResponse[]),
    ])
      .then(([resultsData, pointsData]) => {
        if (cancelled) return
        setResults(new Map(resultsData.map((result) => [result.matchId, result])))
        setPoints(new Map(pointsData.map((point) => [point.matchId, point])))
      })
      .catch(() => {
        if (cancelled) return
        setResults(new Map())
        setPoints(new Map())
      })
      .finally(() => {
        if (cancelled) return
        setIsLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [user])

  const value = useMemo(
    () => ({ results, points, isLoading }),
    [results, points, isLoading],
  )

  return <ResultsContext value={value}>{children}</ResultsContext>
}

export function useResults(): ResultsContextValue {
  const context = useContext(ResultsContext)
  if (!context) {
    throw new Error('useResults must be used within a ResultsProvider')
  }
  return context
}
