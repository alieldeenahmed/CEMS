import { X } from 'lucide-react'
import type { ReactNode } from 'react'

interface ModalProps {
  title: string
  onClose: () => void
  children: ReactNode
  /** Fixed height for the scrollable body (e.g. "h-[500px]") so the modal doesn't resize as its
   * content changes - only pass this when the modal's content can grow/shrink between states. */
  bodyClassName?: string
}

export function Modal({ title, onClose, children, bodyClassName }: ModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/40 px-4">
      <div className="flex max-h-[85vh] w-full max-w-lg flex-col rounded-lg bg-paper shadow-lg">
        <div className="flex items-center justify-between border-b border-line px-6 py-5">
          <h2 className="font-serif text-lg font-semibold text-navy">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="text-muted transition-colors hover:text-ink"
          >
            <X size={18} />
          </button>
        </div>
        <div
          className={`overflow-y-auto p-6 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden ${bodyClassName ?? ''}`}
        >
          {children}
        </div>
      </div>
    </div>
  )
}
