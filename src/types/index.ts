export type Stage =
  | "group"
  | "round-of-32"
  | "round-of-16"
  | "quarter-final"
  | "semi-final"
  | "third-place"
  | "final"

export interface Team {
  code: string
  name: string
  flag: string // emoji flag
}

export interface Venue {
  id: string
  name: string
  city: string
  country: string
  timezone: string // IANA identifier e.g. "America/New_York"
}

export interface Match {
  id: number
  date: string // ISO 8601 UTC e.g. "2026-06-11T20:00:00Z"
  homeTeam: string | null // team code for group, null for knockout
  awayTeam: string | null // team code for group, null for knockout
  homePlaceholder?: string // f.eks. "Vinner gruppe A" for sluttspill
  awayPlaceholder?: string // f.eks. "2. plass gruppe B" for sluttspill
  group?: string // "A" through "L" for group stage only
  stage: Stage
  venueId: string // references Venue.id
}

export interface MatchData {
  teams: Record<string, Team>
  venues: Venue[]
  matches: Match[]
}
