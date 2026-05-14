import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './lib/auth'
import { Layout } from './components/Layout'
import LoginPage from './pages/LoginPage'
import CampaignsPage from './pages/CampaignsPage'
import CampaignReportPage from './pages/CampaignReportPage'
import NewCampaignPage from './pages/NewCampaignPage'

export default function App() {
  const { user } = useAuth()

  if (!user) {
    return (
      <Routes>
        <Route path="*" element={<LoginPage />} />
      </Routes>
    )
  }

  return (
    <Layout>
      <Routes>
        <Route path="/" element={<Navigate to="/campaigns" replace />} />
        <Route path="/campaigns" element={<CampaignsPage />} />
        <Route path="/campaigns/new" element={<NewCampaignPage />} />
        <Route path="/campaigns/:id" element={<CampaignReportPage />} />
        <Route path="*" element={<Navigate to="/campaigns" replace />} />
      </Routes>
    </Layout>
  )
}
