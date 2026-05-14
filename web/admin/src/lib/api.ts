// Thin fetch wrapper for the Barrows WiFi API.

export const API_BASE = 'http://localhost:5080'

const TOKEN_KEY = 'bw_admin_token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

export class ApiError extends Error {
  status: number
  body: unknown
  constructor(status: number, body: unknown) {
    super(`API request failed with status ${status}`)
    this.status = status
    this.body = body
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers })
  const text = await res.text()
  const body = text ? JSON.parse(text) : null
  if (!res.ok) throw new ApiError(res.status, body)
  return body as T
}

// ---- Types shared with the API

export interface User {
  id: number
  email: string
  role: 'PlatformAdmin' | 'ClientAdmin' | 'ClientViewer'
  organisationId: number
  organisationName: string
}

export interface CampaignListItem {
  id: number
  name: string
  organisation: string
  status: string
  startsAt: string
  endsAt: string
  budgetZar: number
  outletCount: number
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface Outlet {
  id: number
  stpNumber: string
  name: string
  province: string
  apStatus: string
  isActive: boolean
  latitude: number | null
  longitude: number | null
}

export interface CampaignReport {
  campaignId: number
  campaignName: string
  organisation: string
  startsAt: string
  endsAt: string
  outlets: number
  impressions: number
  uniqueUsers: number
  surveyCompletes: number
}

export interface SaveCampaignRequest {
  name: string
  startsAt: string
  endsAt: string
  budgetZar: number
  outletIds: number[]
  status?: string
  organisationId?: number
}
