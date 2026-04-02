// Captive portal → API. The portal runs inside the phone's "Sign in to network" window
// before the user has internet access, on whatever browser the handset ships with.

const API_BASE = import.meta.env.VITE_API_BASE
const API_KEY = import.meta.env.VITE_INGEST_KEY

export type EventType = 'connect' | 'portal_view' | 'ad_complete' | 'survey_complete'

export interface PortalConfig {
  stpNumber: string
  outletName: string
  campaignId: number | null
  campaignName: string | null
  sponsor: string | null
}

// Stable per-handset id so we can count unique users.
export const deviceId: string = localStorage.getItem('bw_device') ?? crypto.randomUUID()
localStorage.setItem('bw_device', deviceId)

export async function getConfig(stp: string): Promise<PortalConfig> {
  const res = await fetch(`${API_BASE}/api/portal/${stp}`, { headers: { 'X-Api-Key': API_KEY } })
  if (!res.ok) throw new Error(`Portal config failed: ${res.status}`)
  return res.json()
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))

/** Fire-and-forget event with retries, because outlet backhaul is often slow. */
export async function sendEvent(stp: string, eventType: EventType, campaignId: number | null, msisdn?: string) {
  for (let attempt = 1; attempt <= 3; attempt++) {
    const payload = {
      eventId: crypto.randomUUID(),
      stpNumber: stp,
      campaignId,
      deviceHash: deviceId,
      eventType,
      occurredAt: new Date().toISOString(),
      msisdn: msisdn ?? null,
    }
    try {
      const controller = new AbortController()
      const timer = setTimeout(() => controller.abort(), 4000)
      const res = await fetch(`${API_BASE}/api/portal/events`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Api-Key': API_KEY },
        body: JSON.stringify(payload),
        signal: controller.signal,
      })
      clearTimeout(timer)
      if (res.ok) return
    } catch {
      // timed out or offline – try again
    }
    await sleep(1000 * attempt)
  }
}
