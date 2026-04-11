import type { Match, Stage } from '../types'

export function formatMatchDate(isoDate: string): string {
  return new Intl.DateTimeFormat('nb-NO', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }).format(new Date(isoDate))
}

export function formatMatchTime(isoDate: string): string {
  return new Intl.DateTimeFormat('nb-NO', { hour: 'numeric', minute: '2-digit' }).format(new Date(isoDate))
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

export function areTeamsUndetermined(match: Match): boolean {
  return match.homeTeam === null || match.awayTeam === null
}
