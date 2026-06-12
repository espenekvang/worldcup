import type { Match, Stage, Section } from '../types'

export function formatMatchDate(isoDate: string): string {
  return new Intl.DateTimeFormat('nb-NO', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }).format(new Date(isoDate))
}

export function formatMatchTime(isoDate: string): string {
  return new Intl.DateTimeFormat('nb-NO', { hour: 'numeric', minute: '2-digit' }).format(new Date(isoDate))
}

/**
 * Nøkkel for å gruppere kamper etter kalenderdag i brukerens lokale tidssone.
 * Bruker lokale datokomponenter (ikke UTC-slice), slik at en kamp som starter
 * f.eks. 01:00 lokal tid havner på riktig dag selv om UTC-datoen er dagen før.
 */
export function getLocalDateKey(isoDate: string): string {
  const d = new Date(isoDate)
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/**
 * Sjekker om en kamp er på samme kalenderdag som `now` i brukerens lokale
 * tidssone. Brukes for å løfte frem at neste kamp faktisk er i dag.
 */
export function isToday(isoDate: string, now: Date = new Date()): boolean {
  return getLocalDateKey(isoDate) === getLocalDateKey(now.toISOString())
}

export function getTimeUntil(targetDate: string): { days: number; hours: number; minutes: number; seconds: number } {
  const diff = Math.max(0, new Date(targetDate).getTime() - Date.now())

  return {
    days: Math.floor(diff / (1000 * 60 * 60 * 24)),
    hours: Math.floor((diff / (1000 * 60 * 60)) % 24),
    minutes: Math.floor((diff / (1000 * 60)) % 60),
    seconds: Math.floor((diff / 1000) % 60),
  }
}

export function getNextMatch(matches: Match[]): Match | null {
  const now = Date.now()
  const upcoming = matches
    .filter(match => new Date(match.date).getTime() > now)
    .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())

  return upcoming[0] ?? null
}

export function isBeforeTournament(firstMatchDate: string): boolean {
  return Date.now() < new Date(firstMatchDate).getTime()
}

export function getEarliestKickoffByStage(matches: Match[]): Map<Stage, number> {
  const result = new Map<Stage, number>()
  for (const match of matches) {
    const t = new Date(match.date).getTime()
    const prev = result.get(match.stage)
    if (prev === undefined || t < prev) {
      result.set(match.stage, t)
    }
  }
  return result
}

export function isStageLocked(stage: Stage, matches: Match[], now: number = Date.now()): boolean {
  const earliest = getEarliestKickoffByStage(matches)
  const kickoff = earliest.get(stage)
  if (kickoff === undefined) return false
  return now >= kickoff
}

export function isMatchLocked(match: Match, now: number = Date.now()): boolean {
  return now >= new Date(match.date).getTime()
}

/**
 * Antatt maks varighet for en kamp: spilletid + pause + ev. ekstraomganger og
 * straffer, med litt margin. Brukes som øvre grense for "pågår"-vinduet slik at
 * en glemt resultatregistrering ikke etterlater kampen som "pågår" i dagevis.
 */
const MATCH_DURATION_MS = 3.5 * 60 * 60 * 1000

/**
 * En kamp regnes som "pågår" når avsparket har vært, men vi ennå ikke har et
 * resultat – innenfor et rimelig tidsvindu fra avspark.
 */
export function isMatchInProgress(match: Match, hasResult: boolean, now: number = Date.now()): boolean {
  if (hasResult || areTeamsUndetermined(match)) return false
  const kickoff = new Date(match.date).getTime()
  return now >= kickoff && now < kickoff + MATCH_DURATION_MS
}

/** Menneskelig navn for hver runde, brukt i UI. */
export const STAGE_LABELS: Record<Stage, string> = {
  'group-1': 'Runde 1',
  'group-2': 'Runde 2',
  'group-3': 'Runde 3',
  'round-of-32': '32-delsfinale',
  'round-of-16': '8-delsfinale',
  'quarter-final': 'Kvartfinale',
  'semi-final': 'Semifinale',
  'third-place': 'Bronsefinale',
  'final': 'Finale',
  'leaderboard': 'Toppliste',
}

/**
 * Returnerer neste runde som fortsatt kan bettes på og dens frist
 * (= tidspunktet for første kamp i runden). Returnerer null hvis alle
 * runder er låst / tournament er ferdig.
 */
export function getNextBettingDeadline(
  matches: Match[],
  now: number = Date.now(),
): { stage: Stage; deadline: string; isFirstMatch: boolean } | null {
  // Find the next match that hasn't started yet
  let bestMatch: Match | null = null
  let bestTime = Number.POSITIVE_INFINITY
  for (const match of matches) {
    const kickoff = new Date(match.date).getTime()
    if (kickoff > now && kickoff < bestTime) {
      bestTime = kickoff
      bestMatch = match
    }
  }
  if (!bestMatch) return null
  // Check if this is the very first match of the tournament
  const earliestKickoff = Math.min(...matches.map(m => new Date(m.date).getTime()))
  const isFirstMatch = bestTime === earliestKickoff
  return { stage: bestMatch.stage, deadline: bestMatch.date, isFirstMatch }
}

export function areTeamsUndetermined(match: Match): boolean {
  return !match.homeTeam || !match.awayTeam
}

/**
 * Group stage consists of three rounds (`group-1`, `group-2`, `group-3`),
 * each locked independently when its first kickoff starts.
 */
export function isGroupStage(stage: Stage): boolean {
  return stage === 'group-1' || stage === 'group-2' || stage === 'group-3'
}

/** Toppnivå-seksjon for en gitt stage. Brukes for å gruppere faner i UI. */
export function getSectionForStage(stage: Stage): Section {
  if (stage === 'leaderboard') return 'leaderboard'
  if (isGroupStage(stage)) return 'group'
  return 'knockout'
}

/** Stages som vises som "runder" innenfor en seksjon (ekskl. leaderboard og third-place). */
export const GROUP_ROUNDS: Stage[] = ['group-1', 'group-2', 'group-3']
export const KNOCKOUT_ROUNDS: Stage[] = ['round-of-32', 'round-of-16', 'quarter-final', 'semi-final', 'final']
