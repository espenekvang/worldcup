export type Stage =
  | "group-1"
  | "group-2"
  | "group-3"
  | "round-of-32"
  | "round-of-16"
  | "quarter-final"
  | "semi-final"
  | "third-place"
  | "final"
  | "leaderboard"

/** Top-level UI grouping: gruppespill, sluttspill eller The Boss. */
export type Section = "group" | "knockout" | "leaderboard"

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

export interface BettingGroup {
  id: string
  name: string
  memberCount: number
  createdAt: string
  /** True når dette er en betalt liga som krever avgift før betting. */
  isPaid: boolean
  /** Avgift per deltaker i NOK. 0 når liga ikke er betalt. */
  entryFee: number
  /** Total pott (sum av betalte avgifter) i NOK. */
  prizePot: number
  /** Antall medlemmer som har betalt avgift. */
  paidMemberCount: number
  /** True hvis innlogget bruker selv har betalt i denne ligaen. */
  currentUserHasPaid: boolean
}

export interface BettingGroupMember {
  userId: string
  name: string
  email: string
  picture: string | null
  isGroupAdmin: boolean
  joinedAt: string
  hasPaid: boolean
  paidAt: string | null
}

export interface ChatMessage {
  id: string
  userId: string
  userName: string
  userPicture: string | null
  content: string
  createdAt: string
  isDeleted: boolean
  isSystem: boolean
}
