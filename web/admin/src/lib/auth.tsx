import { createContext, useContext, useState, type ReactNode } from 'react'
import { api, setToken, type User } from './api'

interface AuthState {
  user: User | null
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)
const USER_KEY = 'bw_admin_user'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as User) : null
  })

  async function login(email: string, password: string) {
    const res = await api<{ accessToken: string; user: User }>('/api/auth/token', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    setToken(res.accessToken)
    localStorage.setItem(USER_KEY, JSON.stringify(res.user))
    setUser(res.user)
  }

  function logout() {
    setToken(null)
    localStorage.removeItem(USER_KEY)
    setUser(null)
  }

  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
