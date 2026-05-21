import type { Match, Stage, Section } from '../types'

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
