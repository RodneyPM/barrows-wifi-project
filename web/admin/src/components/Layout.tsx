import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { Button } from './ui'

export function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()
  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <Link to="/campaigns" className="font-semibold text-brand-700">
            Barrows WiFi <span className="font-normal text-slate-500">Campaigns</span>
          </Link>
          <div className="flex items-center gap-3 text-sm">
            <span className="hidden text-slate-600 sm:inline">
              {user?.email} · {user?.organisationName}
            </span>
            <Button variant="ghost" onClick={logout}>
              Sign out
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
    </div>
  )
}
