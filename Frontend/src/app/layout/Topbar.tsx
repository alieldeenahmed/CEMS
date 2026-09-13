import { LogOut } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/AuthContext'

export function Topbar() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <header className="flex h-16 items-center justify-between border-b border-line bg-paper px-6">
      <div>
        <p className="font-serif text-lg font-semibold text-ink">Welcome, {user?.fullName}</p>
        <p className="text-xs text-muted">{user?.roles.join(', ')}</p>
      </div>

      <button
        type="button"
        onClick={handleLogout}
        className="flex items-center gap-1.5 rounded-md border border-line px-3 py-1.5 text-sm text-ink transition-colors hover:bg-parchment"
      >
        <LogOut size={15} strokeWidth={2} />
        Sign out
      </button>
    </header>
  )
}
