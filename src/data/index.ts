import type { MatchData, Team, Venue, Match, Stage } from '../types'
import venuesData from './venues.json'
import teamsData from './teams.json'
import matchesData from './matches.json'

const STAGES = new Set<string>(['group', 'round-of-32', 'round-of-16', 'quarter-final', 'semi-final', 'third-place', 'final'])

function isStage(value: string): value is Stage {
  return STAGES.has(value)
}

function isVenue(value: unknown): value is Venue {
  if (typeof value !== 'object' || value === null) {
    return false
  }

  return (
    'id' in value && typeof value.id === 'string' &&
    'name' in value && typeof value.name === 'string' &&
    'city' in value && typeof value.city === 'string' &&
    'country' in value && typeof value.country === 'string' &&
    'timezone' in value && typeof value.timezone === 'string'
  )
}

function isTeam(value: unknown): value is Team {
  if (typeof value !== 'object' || value === null) {
    return false
  }

  return (
    'code' in value && typeof value.code === 'string' &&
    'name' in value && typeof value.name === 'string' &&
    'flag' in value && typeof value.flag === 'string'
  )
}

function isMatch(value: unknown): value is Match {
  if (typeof value !== 'object' || value === null) {
    return false
  }

  const hasValidTeam = (team: unknown) => typeof team === 'string' || team === null
  const hasValidOptionalText = (text: unknown) => typeof text === 'string' || typeof text === 'undefined'

  return (
    'id' in value && typeof value.id === 'number' &&
    'date' in value && typeof value.date === 'string' &&
    'homeTeam' in value && hasValidTeam(value.homeTeam) &&
    'awayTeam' in value && hasValidTeam(value.awayTeam) &&
    'stage' in value && typeof value.stage === 'string' && isStage(value.stage) &&
    'venueId' in value && typeof value.venueId === 'string' &&
    (!('group' in value) || typeof value.group === 'string') &&
    (!('homePlaceholder' in value) || hasValidOptionalText(value.homePlaceholder)) &&
    (!('awayPlaceholder' in value) || hasValidOptionalText(value.awayPlaceholder))
  )
}

function parseVenues(data: unknown): Venue[] {
  if (!Array.isArray(data) || !data.every(isVenue)) {
    throw new Error('Invalid venues data')
  }

  return data
}

function parseTeams(data: unknown): Record<string, Team> {
  if (typeof data !== 'object' || data === null) {
    throw new Error('Invalid teams data')
  }

  const entries = Object.entries(data)

  const parsedTeams: Record<string, Team> = {}

  for (const [code, team] of entries) {
    if (!isTeam(team)) {
      throw new Error('Invalid teams data')
    }

    parsedTeams[code] = team
  }

  return parsedTeams
}

function parseMatches(data: unknown): Match[] {
  if (!Array.isArray(data) || !data.every(isMatch)) {
    throw new Error('Invalid matches data')
  }

  return data
}

export const venues = parseVenues(venuesData)
export const teams = parseTeams(teamsData)
export const matches = parseMatches(matchesData)

export const matchData = {
  teams,
  venues,
  matches,
} satisfies MatchData

export type { Team, Venue, Match, Stage, MatchData }
