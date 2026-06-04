import type { MatchPredictionResponse } from '../api/client'

export interface MatchOdds {
  home: number
  draw: number
  away: number
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

  // Parimutuel-odds: total / antall som har valgt utfallet. Hvis ingen har valgt
  // et utfall ville den som bettet det vunnet hele potten alene – altså høyest
  // mulig odds (total + 1, inkludert sitt eget bet).
  const oddsFor = (count: number) => (count > 0 ? +(total / count).toFixed(2) : total + 1)

  return {
    home: oddsFor(home),
    draw: oddsFor(draw),
    away: oddsFor(away),
    totalPredictions: total,
  }
}
