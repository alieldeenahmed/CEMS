import { NavLink } from 'react-router-dom'
import { useAuth } from '@/features/auth/AuthContext'
import { NAV_GROUPS } from '../nav'

export function Sidebar() {
  const { user } = useAuth()

  return (
    <aside className="flex h-screen w-60 flex-col bg-navy text-parchment">
      <div className="flex items-center gap-2 px-5 py-5">
        <span className="font-serif text-xl font-semibold text-paper">CEMS</span>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 pb-4">
        {NAV_GROUPS.map((group) => {
          const items = group.items.filter((item) =>
            item.roles.some((role) => user?.roles.includes(role)),
          )
          if (items.length === 0) return null

          return (
            <div key={group.label} className="mb-5">
              <p className="mb-1.5 px-2 text-xs font-medium uppercase tracking-wide text-muted">
                {group.label}
              </p>
              <ul className="space-y-0.5">
                {items.map((item) => (
                  <li key={item.path}>
                    <NavLink
                      to={item.path}
                      end={item.path === '/'}
                      className={({ isActive }) =>
                        `flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm transition-colors ${
                          isActive
                            ? 'bg-navy-deep text-ochre'
                            : 'text-parchment/80 hover:bg-navy-deep hover:text-paper'
                        }`
                      }
                    >
                      <item.icon size={16} strokeWidth={2} />
                      {item.label}
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          )
        })}
      </nav>
    </aside>
  )
}
