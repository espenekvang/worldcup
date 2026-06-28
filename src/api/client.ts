import type { Match, BettingGroup, BettingGroupMember, ChatMessage } from '../types'

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5211'

export function getApiBase(): string {
  return API_BASE
}

function getToken(): string | null {
  try {
    return localStorage.getItem('auth_token')
  } catch {
    return null
  }
}

function getActiveGroupId(): string | null {
  try {
    return localStorage.getItem('active_group_id')
  } catch {
    return null
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((options.headers as Record<string, string>) ?? {}),
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  const groupId = getActiveGroupId()
  if (groupId) {
    headers['X-Group-Id'] = groupId
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  })

  if (!response.ok) {
    if (response.status === 401) {
      // Token er utløpt/ugyldig – rydd lokal auth-state slik at UI ser brukeren
      // som utlogget og kan sende dem gjennom innloggingsflyten på nytt.
      try {
        localStorage.removeItem('auth_token')
        localStorage.removeItem('auth_user')
      } catch {
        // storage unavailable
      }
    }
    const errorText = await response.text().catch(() => 'Unknown error')
    throw new Error(`API error ${response.status}: ${errorText}`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export interface AuthResponse {
  token: string
  email: string
  name: string
  picture: string | null
  isAdmin: boolean
  groups: BettingGroup[]
  groupAdminGroupIds: string[]
}

export interface PredictionResponse {
  matchId: number
  homeScore: number
  awayScore: number
  updatedAt: string
}

export interface ResultResponse {
  matchId: number
  homeScore: number
  awayScore: number
  fetchedAt: string
}

export interface PointsResponse {
  matchId: number
  points: number
  outcomePoints: number
  homeGoalPoints: number
  awayGoalPoints: number
}

export interface PredictionDto {
  matchId: number
  homeScore: number
  awayScore: number
}

export function loginWithGoogle(idToken: string, inviteToken?: string): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/google', {
    method: 'POST',
    body: JSON.stringify({ idToken, inviteToken: inviteToken ?? null }),
  })
}

export function getPredictions(): Promise<PredictionResponse[]> {
  return request<PredictionResponse[]>('/api/predictions')
}

export function getResults(): Promise<ResultResponse[]> {
  return request<ResultResponse[]>('/api/results')
}

export function getUserPoints(): Promise<PointsResponse[]> {
  return request<PointsResponse[]>('/api/results/points')
}

export function putPrediction(matchId: number, prediction: PredictionDto): Promise<PredictionResponse> {
  return request<PredictionResponse>(`/api/predictions/${matchId}`, {
    method: 'PUT',
    body: JSON.stringify(prediction),
  })
}

export interface InvitationResponse {
  id: string
  email: string
  bettingGroupId: string
  groupName: string
  createdAt: string
}

export function getInvitations(groupId?: string): Promise<InvitationResponse[]> {
  const query = groupId ? `?groupId=${groupId}` : ''
  return request<InvitationResponse[]>(`/api/invitations${query}`)
}

export function createInvitation(email: string, bettingGroupId: string): Promise<InvitationResponse> {
  return request<InvitationResponse>('/api/invitations', {
    method: 'POST',
    body: JSON.stringify({ email, bettingGroupId }),
  })
}

export function deleteInvitation(id: string): Promise<void> {
  return request<void>(`/api/invitations/${id}`, {
    method: 'DELETE',
  })
}

export interface InviteLinkResponse {
  id: string
  bettingGroupId: string
  groupName: string
  token: string
  createdAt: string
  isRevoked: boolean
}

export interface InviteLinkInfoResponse {
  bettingGroupId: string
  groupName: string
}

export interface AcceptInviteLinkResponse {
  bettingGroupId: string
  groupName: string
  alreadyMember: boolean
}

export function getInviteLinks(groupId: string): Promise<InviteLinkResponse[]> {
  return request<InviteLinkResponse[]>(`/api/groups/${groupId}/invite-links`)
}

export function createInviteLink(groupId: string): Promise<InviteLinkResponse> {
  return request<InviteLinkResponse>(`/api/groups/${groupId}/invite-links`, {
    method: 'POST',
  })
}

export function revokeInviteLink(id: string): Promise<void> {
  return request<void>(`/api/invite-links/${id}`, {
    method: 'DELETE',
  })
}

export function getInviteLinkInfo(token: string): Promise<InviteLinkInfoResponse> {
  return request<InviteLinkInfoResponse>(`/api/invite-links/${encodeURIComponent(token)}`)
}

export function acceptInviteLink(token: string): Promise<AcceptInviteLinkResponse> {
  return request<AcceptInviteLinkResponse>(`/api/invite-links/${encodeURIComponent(token)}/accept`, {
    method: 'POST',
  })
}

export interface MatchPredictionResponse {
  name: string | null
  picture: string | null
  homeScore: number | null
  awayScore: number | null
  points: number | null
}

export interface LeaderboardEntry {
  name: string
  picture: string | null
  totalPoints: number
  matchCount: number
  /** Plassering før siste registrerte kamp. Null hvis ingen tidligere kamp finnes. */
  previousRank: number | null
  hasPaid: boolean
}

export function getMatchPredictions(matchId: number): Promise<MatchPredictionResponse[]> {
  return request<MatchPredictionResponse[]>(`/api/predictions/match/${matchId}`)
}

export function getMatchOdds(matchId: number): Promise<MatchPredictionResponse[]> {
  return request<MatchPredictionResponse[]>(`/api/predictions/match/${matchId}/odds`)
}

export interface GlobalLeaderboardEntry {
  name: string | null
  picture: string | null
  totalPoints: number
  matchCount: number
  isInCurrentGroup: boolean
  groupName: string | null
}

export function getLeaderboard(): Promise<LeaderboardEntry[]> {
  return request<LeaderboardEntry[]>('/api/results/leaderboard')
}

export function getGlobalLeaderboard(): Promise<GlobalLeaderboardEntry[]> {
  return request<GlobalLeaderboardEntry[]>('/api/results/leaderboard/global')
}

export function getMatches(): Promise<Match[]> {
  return request<Match[]>('/api/matches')
}

export interface WeatherForecast {
  date: string
  weatherCode: number
  tempMaxC: number | null
  tempMinC: number | null
  precipitationMm: number | null
  precipitationProbabilityPct: number | null
}

/**
 * Henter daglig værvarsel for et stadion på en gitt dato (yyyy-MM-dd lokal stadion-tid).
 * APIet returnerer 204 No Content når det ikke finnes prognose (> ~16 dager frem),
 * og klienten oversetter det til `null`.
 */
export async function getWeatherForVenue(venueId: string, date: string): Promise<WeatherForecast | null> {
  const result = await request<WeatherForecast | undefined>(`/api/weather/${encodeURIComponent(venueId)}/${date}`)
  return result ?? null
}

export function updateMatchTeams(matchId: number, homeTeam?: string, awayTeam?: string): Promise<unknown> {
  return request<unknown>(`/api/admin/matches/${matchId}`, {
    method: 'PUT',
    body: JSON.stringify({ homeTeam: homeTeam ?? null, awayTeam: awayTeam ?? null }),
  })
}

export function setMatchResult(matchId: number, homeScore: number, awayScore: number): Promise<ResultResponse> {
  return request<ResultResponse>(`/api/admin/results/${matchId}`, {
    method: 'PUT',
    body: JSON.stringify({ homeScore, awayScore }),
  })
}

// Betting Group API functions
export function getMyGroups(): Promise<BettingGroup[]> {
  return request<BettingGroup[]>('/api/groups')
}

export function getAllGroups(): Promise<BettingGroup[]> {
  return request<BettingGroup[]>('/api/admin/groups')
}

export function createGroup(
  name: string,
  joinGroup: boolean = true,
  isPaid: boolean = false,
  entryFee: number = 0,
): Promise<BettingGroup> {
  return request<BettingGroup>('/api/groups', {
    method: 'POST',
    body: JSON.stringify({ name, joinGroup, isPaid, entryFee }),
  })
}

export function updateGroup(
  id: string,
  name: string,
  options?: { isPaid?: boolean; entryFee?: number },
): Promise<BettingGroup> {
  const body: Record<string, unknown> = { name }
  if (options?.isPaid !== undefined) body.isPaid = options.isPaid
  if (options?.entryFee !== undefined) body.entryFee = options.entryFee
  return request<BettingGroup>(`/api/groups/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  })
}

export function deleteGroup(id: string): Promise<void> {
  return request<void>(`/api/groups/${id}`, {
    method: 'DELETE',
  })
}

/** Setter om "The Boss"-listen i ligaen skal vise fornavn (false) eller fullt navn (true). */
export function setBossNameDisplay(id: string, showFullName: boolean): Promise<BettingGroup> {
  return request<BettingGroup>(`/api/groups/${id}/boss-name-display`, {
    method: 'PUT',
    body: JSON.stringify({ showFullName }),
  })
}

export function getGroupMembers(groupId: string): Promise<BettingGroupMember[]> {
  return request<BettingGroupMember[]>(`/api/groups/${groupId}/members`)
}

export function addGroupMember(groupId: string, email: string): Promise<BettingGroupMember> {
  return request<BettingGroupMember>(`/api/groups/${groupId}/members`, {
    method: 'POST',
    body: JSON.stringify({ email }),
  })
}

export function removeGroupMember(groupId: string, userId: string): Promise<void> {
  return request<void>(`/api/groups/${groupId}/members/${userId}`, {
    method: 'DELETE',
  })
}

export function toggleGroupAdmin(groupId: string, userId: string, isGroupAdmin: boolean): Promise<void> {
  return request<void>(`/api/groups/${groupId}/members/${userId}/admin`, {
    method: 'PUT',
    body: JSON.stringify({ isGroupAdmin }),
  })
}

export function setMemberPaid(groupId: string, userId: string, hasPaid: boolean): Promise<void> {
  return request<void>(`/api/groups/${groupId}/members/${userId}/payment`, {
    method: 'PUT',
    body: JSON.stringify({ hasPaid }),
  })
}

// Chat API functions
export function getChatMessages(before?: string, limit?: number): Promise<ChatMessage[]> {
  const params = new URLSearchParams()
  if (before) params.set('before', before)
  if (limit) params.set('limit', String(limit))
  const qs = params.toString()
  return request<ChatMessage[]>(`/api/chat${qs ? `?${qs}` : ''}`)
}

export function postChatMessage(content: string): Promise<ChatMessage> {
  return request<ChatMessage>('/api/chat', {
    method: 'POST',
    body: JSON.stringify({ content }),
  })
}

export function deleteChatMessage(id: string): Promise<void> {
  return request<void>(`/api/chat/${id}`, { method: 'DELETE' })
}

export function broadcastMessage(content: string): Promise<{ groupCount: number }> {
  return request<{ groupCount: number }>('/api/admin/broadcast', {
    method: 'POST',
    body: JSON.stringify({ content }),
  })
}

export function addChatReaction(messageId: string, emoji: string): Promise<void> {
  return request<void>(`/api/chat/${messageId}/reactions/${encodeURIComponent(emoji)}`, {
    method: 'POST',
  })
}

export function removeChatReaction(messageId: string, emoji: string): Promise<void> {
  return request<void>(`/api/chat/${messageId}/reactions/${encodeURIComponent(emoji)}`, {
    method: 'DELETE',
  })
}

export function getChatUnreadCount(since?: string): Promise<{ unreadCount: number }> {
  const params = new URLSearchParams()
  if (since) params.set('since', since)
  const qs = params.toString()
  return request<{ unreadCount: number }>(`/api/chat/unread-count${qs ? `?${qs}` : ''}`)
}

export type FeatureFlags = Record<string, boolean>

export function getFeatureFlags(groupId?: string): Promise<FeatureFlags> {
  const query = groupId ? `?groupId=${encodeURIComponent(groupId)}` : ''
  return request<FeatureFlags>(`/api/feature-flags${query}`)
}

// ─── Team stats & head-to-head (matchdetalj-siden) ──────────────────────

export interface RecentMatchEntry {
  date: string
  opponent: string
  /** "home" | "away" | "neutral" */
  venue: string
  goalsFor: number
  goalsAgainst: number
  /** "W" | "D" | "L" */
  result: string
  competition: string
}

export interface TeamStatsResponse {
  teamCode: string
  fifaRank: number | null
  manager: string | null
  starPlayer: string | null
  preferredFormation: string | null
  goalsScoredAvg: number | null
  goalsConcededAvg: number | null
  /** Form-streng, f.eks. "WWDWL" (eldste først). */
  recentForm: string | null
  recentMatches: RecentMatchEntry[]
  keyAbsences: string[]
  lastWorldCupResult: string | null
}

export interface HeadToHeadMatch {
  date: string
  homeTeam: string
  awayTeam: string
  homeScore: number
  awayScore: number
  competition: string
  venue: string | null
}

export interface HeadToHeadResponse {
  teamA: string
  teamB: string
  totalMatches: number
  teamAWins: number
  draws: number
  teamBWins: number
  teamAGoals: number
  teamBGoals: number
  recentMatches: HeadToHeadMatch[]
}

/**
 * Henter lag-statistikk for én lagkode. Returnerer `null` når serveren svarer
 * 404 — typisk hvis lagkoden ikke er bestemt ennå (knockout) eller mangler i seed-data.
 */
export async function getTeamStats(teamCode: string): Promise<TeamStatsResponse | null> {
  try {
    return await request<TeamStatsResponse>(`/api/team-stats/${encodeURIComponent(teamCode)}`)
  } catch (err) {
    if (err instanceof Error && err.message.includes('404')) return null
    throw err
  }
}

/**
 * Henter innbyrdes oppgjør for to lag. `teamA` blir alltid `teamA` i responsen —
 * service'en speilvender tellerne. Returnerer `null` ved 404.
 */
export async function getHeadToHead(teamA: string, teamB: string): Promise<HeadToHeadResponse | null> {
  try {
    return await request<HeadToHeadResponse>(
      `/api/team-stats/h2h/${encodeURIComponent(teamA)}/${encodeURIComponent(teamB)}`,
    )
  } catch (err) {
    if (err instanceof Error && err.message.includes('404')) return null
    throw err
  }
}

export interface DiagnosticsApiBudget {
  usedInLast24h: number
  dailyBudget: number
  remaining: number
}

export interface DiagnosticsMissingResult {
  matchId: number
  stage: string
  kickoffAt: string
  homeTeam: string | null
  awayTeam: string | null
  teamsUnknown: boolean
  expectedReadyAt: string
  pendingAttempts: number
  exhausted: boolean
  nextAttemptAt: string | null
}

export interface DiagnosticsUnresolvedFixture {
  matchId: number
  stage: string
  kickoffAt: string
  homePlaceholder: string | null
  awayPlaceholder: string | null
  status: 'resolvable' | 'waiting_for_feeders'
  waitingForMatchIds: number[]
}

export interface DiagnosticsResponse {
  asOf: string
  apiCallBudget: DiagnosticsApiBudget
  missingResults: DiagnosticsMissingResult[]
  unresolvedFixtures: DiagnosticsUnresolvedFixture[]
}

export function getDiagnostics(): Promise<DiagnosticsResponse> {
  return request<DiagnosticsResponse>('/api/admin/diagnostics/missing-results')
}

export interface ForceFetchResult {
  newResults: number
  teamFillsFromCompleted: number
  fixtureTeamFills: number
  completedDtosReceived: number
  remainingUndetermined: number
}

export function forceFetch(): Promise<ForceFetchResult> {
  return request<ForceFetchResult>('/api/admin/diagnostics/force-fetch', { method: 'POST' })
}
