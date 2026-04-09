import type { MatchData, Team, Venue, Match, Stage } from '../types'
import venuesData from './venues.json'
import teamsData from './teams.json'
import matchesData from './matches.json'

export const venues: Venue[] = venuesData as Venue[]
export const teams: Record<string, Team> = teamsData as Record<string, Team>
export const matches: Match[] = matchesData as Match[]

export const matchData: MatchData = {
  teams,
  venues,
  matches,
}

export type { Team, Venue, Match, Stage, MatchData }
