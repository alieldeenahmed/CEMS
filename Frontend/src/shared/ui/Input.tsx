import { forwardRef, useId, type InputHTMLAttributes, type SelectHTMLAttributes } from 'react'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
}

// The label is tied to its field with a generated id when the caller doesn't supply one, so clicking
// the label focuses the field and assistive technology announces it.
export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, error, id, className = '', ...props },
  ref,
) {
  const generatedId = useId()
  const inputId = id ?? generatedId

  return (
    <div>
      {label && (
        <label htmlFor={inputId} className="mb-1 block text-sm font-medium text-ink">
          {label}
        </label>
      )}
      <input
        ref={ref}
        id={inputId}
        aria-invalid={error ? true : undefined}
        className={`w-full rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink outline-none focus:border-navy ${className}`}
        {...props}
      />
      {error && <p className="mt-1 text-xs text-coral">{error}</p>}
    </div>
  )
})

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string
  error?: string
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  { label, error, id, className = '', children, ...props },
  ref,
) {
  const generatedId = useId()
  const selectId = id ?? generatedId

  return (
    <div>
      {label && (
        <label htmlFor={selectId} className="mb-1 block text-sm font-medium text-ink">
          {label}
        </label>
      )}
      <select
        ref={ref}
        id={selectId}
        aria-invalid={error ? true : undefined}
        className={`w-full rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink outline-none focus:border-navy ${className}`}
        {...props}
      >
        {children}
      </select>
      {error && <p className="mt-1 text-xs text-coral">{error}</p>}
    </div>
  )
})
