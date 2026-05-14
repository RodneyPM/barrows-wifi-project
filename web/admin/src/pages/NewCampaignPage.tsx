import { CampaignWizard } from '../features/wizard/CampaignWizard'

export default function NewCampaignPage() {
  return (
    <div>
      <h1 className="mb-4 text-xl font-semibold">New campaign</h1>
      <CampaignWizard />
    </div>
  )
}
