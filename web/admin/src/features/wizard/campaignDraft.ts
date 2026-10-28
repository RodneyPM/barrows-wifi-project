
import type { SaveCampaignRequest } from '../../lib/api'

export interface CampaignDraft {
  name: string
  objective: string
  startDate: string
  endDate: string
  budget: string
  outletIds: number[]
}

export const emptyDraft: CampaignDraft = {
  name: '',
  objective: '',
  startDate: '',
  endDate: '',
  budget: '',
  outletIds: [],
}

export type CampaignStep = 0 | 1 | 2 | 3

function parseBudget(value: string): number {
  const normalized = value.trim().replace(/[,\s]/g, '')

  if (!normalized) return Number.NaN

  return Number(normalized)
}

/**
 * Converts a YYYY-MM-DD calendar date to an ISO timestamp at
 * midnight SAST (UTC+02:00). When exclusiveEnd is true, uses
 * midnight on the following calendar day.
 *
 * Avoids relying on the browser's local timezone.
 */
export function sastDateToISOString(
  date: string,
  exclusiveEnd = false,
): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date)

  if (!match) {
    throw new RangeError('Invalid calendar date')
  }

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])

  // Validate the selected date before applying the exclusive-end adjustment.
  const dateCheck = new Date(Date.UTC(year, month - 1, day))

  if (
    dateCheck.getUTCFullYear() !== year ||
    dateCheck.getUTCMonth() !== month - 1 ||
    dateCheck.getUTCDate() !== day
  ) {
    throw new RangeError('Invalid calendar date')
  }

  // South Africa is UTC+02:00. Midnight SAST is 22:00 UTC
  // on the previous UTC date.
  // For an exclusive end, advance one calendar day first.
  const timestamp = Date.UTC(
    year,
    month - 1,
    day + (exclusiveEnd ? 1 : 0),
    -2,
  )

  return new Date(timestamp).toISOString()
}

export function validateCampaignStep(
  draft: CampaignDraft,
  step: CampaignStep,
): string[] {
  const errors: string[] = []

  if (step === 0 || step === 3) {
    if (!draft.name.trim()) {
      errors.push('Name is required')
    }
  }

  if (step === 1 || step === 3) {
    if (!draft.startDate) {
      errors.push('Start date is required')
    }

    if (!draft.endDate) {
      errors.push('End date is required')
    }

    if (
      draft.startDate &&
      draft.endDate &&
      draft.endDate < draft.startDate
    ) {
      errors.push('End date must be on or after start date')
    }

    const budget = parseBudget(draft.budget)

    if (!draft.budget.trim()) {
      errors.push('Budget is required')
    } else if (!Number.isFinite(budget) || budget <= 0) {
      errors.push('Budget must be a valid number greater than zero')
    }
  }

  if (step === 2 || step === 3) {
    if (draft.outletIds.length === 0) {
      errors.push('Select at least one outlet')
    }
  }

  return errors
}

export function validateCampaign(draft: CampaignDraft): string[] {
  return validateCampaignStep(draft, 3)
}

export function toRequest(draft: CampaignDraft): SaveCampaignRequest {
  return {
    name: draft.name.trim(),
    startsAt: sastDateToISOString(draft.startDate),
    endsAt: sastDateToISOString(draft.endDate, true),
    budgetZar: parseBudget(draft.budget),
    outletIds: draft.outletIds,
  }
}
