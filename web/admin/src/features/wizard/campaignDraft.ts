import type { SaveCampaignRequest } from '../../lib/api'

export interface CampaignDraft {
  name: string
  objective: string
  startDate: string // yyyy-mm-dd from <input type="date">
  endDate: string // yyyy-mm-dd, last day the campaign runs
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

export function validateCampaign(draft: CampaignDraft): string[] {
  const errors: string[] = []
  if (!draft.name.trim()) errors.push('Name is required')
  if (!draft.startDate) errors.push('Start date is required')
  if (!draft.endDate) errors.push('End date is required')
  if (!draft.budget) errors.push('Budget is required')
  if (draft.outletIds.length === 0) errors.push('Select at least one outlet')
  return errors
}

export function toRequest(draft: CampaignDraft): SaveCampaignRequest {
  return {
    name: draft.name.trim(),
    startsAt: new Date(draft.startDate).toISOString(),
    endsAt: new Date(draft.endDate).toISOString(),
    budgetZar: parseInt(draft.budget),
    outletIds: draft.outletIds,
  }
}
