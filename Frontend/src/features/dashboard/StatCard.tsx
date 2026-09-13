import type { LucideIcon } from 'lucide-react'

interface StatCardProps {
  label: string
  value: string
  icon: LucideIcon
}

export function StatCard({ label, value, icon: Icon }: StatCardProps) {
  return (
    <div className="rounded-lg border border-line bg-paper p-5">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted">{label}</p>
        <Icon size={18} strokeWidth={2} className="text-ochre" />
      </div>
      <p className="mt-2 font-serif text-2xl font-semibold text-navy">{value}</p>
    </div>
  )
}
