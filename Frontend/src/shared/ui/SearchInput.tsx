import { Search } from 'lucide-react'
import type { InputHTMLAttributes } from 'react'

interface SearchInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  className?: string
}

export function SearchInput({ className = '', ...props }: SearchInputProps) {
  return (
    <div className={`relative ${className}`}>
      <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
      <input
        type="search"
        className="w-full rounded-md border border-line bg-paper py-2 pl-9 pr-3 text-sm text-ink outline-none focus:border-navy"
        {...props}
      />
    </div>
  )
}
