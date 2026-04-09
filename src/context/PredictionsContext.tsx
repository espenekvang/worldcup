import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { createContext, useContext } from 'react'
import type { PredictionResponse } from '../api/client'
import { getPredictions, putPrediction } from '../api/client'
import { useAuth } from './AuthContext'

interface PredictionsContextValue {
  predictions: Map<number, PredictionResponse>
  isLoading: boolean
  savePrediction: (matchId: number, homeScore: number, awayScore: number) => Promise<void>
}

const PredictionsContext = createContext<PredictionsContextValue | null>(null)

export function PredictionsProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const [predictions, setPredictions] = useState<Map<number, PredictionResponse>>(new Map())
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    if (!user) {
      setPredictions(new Map())
      return
    }

    setIsLoading(true)
    getPredictions()
      .then((data) => {
        const map = new Map<number, PredictionResponse>()
        for (const p of data) {
          map.set(p.matchId, p)
        }
        setPredictions(map)
      })
      .catch(() => {
        setPredictions(new Map())
      })
      .finally(() => setIsLoading(false))
  }, [user])

  const savePrediction = useCallback(async (matchId: number, homeScore: number, awayScore: number) => {
    const result = await putPrediction(matchId, { matchId, homeScore, awayScore })
    setPredictions((prev) => {
      const next = new Map(prev)
      next.set(matchId, result)
      return next
    })
  }, [])

  const value = useMemo(
    () => ({ predictions, isLoading, savePrediction }),
    [predictions, isLoading, savePrediction],
  )

  return <PredictionsContext value={value}>{children}</PredictionsContext>
}

export function usePredictions(): PredictionsContextValue {
  const context = useContext(PredictionsContext)
  if (!context) {
    throw new Error('usePredictions must be used within a PredictionsProvider')
  }
  return context
}
