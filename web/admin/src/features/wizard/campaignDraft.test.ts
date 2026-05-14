import { describe, expect, it } from 'vitest'
import { emptyDraft, toRequest, validateCampaign } from './campaignDraft'

describe('validateCampaign', () => {
  it('requires a name', () => {
    expect(validateCampaign(emptyDraft)).toContain('Name is required')
  })
})

describe('toRequest', () => {
  it('passes selected outlets through', () => {
    const req = toRequest({
      ...emptyDraft,
      name: 'Test',
      startDate: '2026-11-01',
      endDate: '2026-11-30',
      budget: '50000',
      outletIds: [3, 7],
    })
    expect(req.outletIds).toEqual([3, 7])
    expect(req.name).toBe('Test')
  })
})
