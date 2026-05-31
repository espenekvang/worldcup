import type { MatchPredictionResponse } from '../api/client'

export interface MatchOdds {
  home: number | null
  draw: number | null
  away: number | null
  totalPredictions: number
}

const MIN_PREDICTIONS = 3

/**
 * Beregner parimutuel-odds (H/U/B) basert på alle predictions for en kamp.
 * Returnerer null dersom færre enn 3 brukere har tippet.
 */
export function calculateOdds(predictions: MatchPredictionResponse[]): MatchOdds | null {
  const valid = predictions.filter((p) => p.homeScore !== null && p.awayScore !== null)
  const total = valid.length

  if (total < MIN_PREDICTIONS) return null

  let home = 0
  let draw = 0
  let away = 0

  for (const p of valid) {
    if (p.homeScore! > p.awayScore!) home++
    else if (p.homeScore === p.awayScore) draw++
    else away++
  }

  return {
    home: home > 0 ? +(total / home).toFixed(2) : null,
    draw: draw > 0 ? +(total / draw).toFixed(2) : null,
    away: away > 0 ? +(total / away).toFixed(2) : null,
    totalPredictions: total,
  }
}
