import type { Match, BettingGroup, BettingGroupMember } from '../types'

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5211'

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

export function loginWithGoogle(idToken: string): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/google', {
    method: 'POST',
    body: JSON.stringify({ idToken }),
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

export interface MatchPredictionResponse {
  name: string
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
}

export function getMatchPredictions(matchId: number): Promise<MatchPredictionResponse[]> {
  return request<MatchPredictionResponse[]>(`/api/predictions/match/${matchId}`)
}

export function getLeaderboard(): Promise<LeaderboardEntry[]> {
  return request<LeaderboardEntry[]>('/api/results/leaderboard')
}

export function getMatches(): Promise<Match[]> {
  return request<Match[]>('/api/matches')
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

export function createGroup(name: string, joinGroup: boolean = true): Promise<BettingGroup> {
  return request<BettingGroup>('/api/groups', {
    method: 'POST',
    body: JSON.stringify({ name, joinGroup }),
  })
}

export function updateGroup(id: string, name: string): Promise<BettingGroup> {
  return request<BettingGroup>(`/api/groups/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ name }),
  })
}

export function deleteGroup(id: string): Promise<void> {
  return request<void>(`/api/groups/${id}`, {
    method: 'DELETE',
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
