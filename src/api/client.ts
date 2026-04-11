const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5211'

function getToken(): string | null {
  try {
    return localStorage.getItem('auth_token')
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
  createdAt: string
}

export function getInvitations(): Promise<InvitationResponse[]> {
  return request<InvitationResponse[]>('/api/invitations')
}

export function createInvitation(email: string): Promise<InvitationResponse> {
  return request<InvitationResponse>('/api/invitations', {
    method: 'POST',
    body: JSON.stringify({ email }),
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

export function getMatchPredictions(matchId: number): Promise<MatchPredictionResponse[]> {
  return request<MatchPredictionResponse[]>(`/api/predictions/match/${matchId}`)
}
