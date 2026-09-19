import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Wallet } from 'lucide-react'
import { describe, expect, it, vi } from 'vitest'
import { Button } from './Button'
import { Input, Select } from './Input'
import { Modal } from './Modal'
import { PageHeader } from './PageHeader'
import { StatCard } from './StatCard'

describe('Input and Select', () => {
  it('connects each label to its field, so clicking the label focuses it and it has an accessible name', async () => {
    const user = userEvent.setup()
    render(
      <>
        <Input label="Full name" />
        <Select label="Gender">
          <option>Male</option>
        </Select>
      </>,
    )

    const input = screen.getByLabelText('Full name')
    expect(screen.getByLabelText('Gender').tagName).toBe('SELECT')

    await user.click(screen.getByText('Full name'))
    expect(input).toHaveFocus()
  })

  it('gives every field its own id, so two identical labels never point at the same one', () => {
    render(
      <>
        <Input label="Name" />
        <Input label="Name" />
      </>,
    )

    const [first, second] = screen.getAllByLabelText('Name')
    expect(first.id).not.toBe(second.id)
  })

  it('respects an id the caller supplies', () => {
    render(<Input label="Email" id="email-field" />)

    expect(screen.getByLabelText('Email')).toHaveAttribute('id', 'email-field')
  })

  it('shows an error message and marks the field invalid', () => {
    render(<Input label="Phone" error="Phone is required" />)

    expect(screen.getByText('Phone is required')).toBeInTheDocument()
    expect(screen.getByLabelText('Phone')).toHaveAttribute('aria-invalid', 'true')
  })

  it('is not marked invalid without an error', () => {
    render(<Input label="Phone" />)

    expect(screen.getByLabelText('Phone')).not.toHaveAttribute('aria-invalid')
  })

  it('shows a select error too', () => {
    render(
      <Select label="Branch" error="Branch is required">
        <option />
      </Select>,
    )

    expect(screen.getByText('Branch is required')).toBeInTheDocument()
    expect(screen.getByLabelText('Branch')).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('Button', () => {
  it('is a plain button by default, so it never submits a form by accident', () => {
    render(<Button>Save</Button>)

    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('type', 'button')
  })

  it('can be a submit button, and cannot be clicked while disabled', async () => {
    const onClick = vi.fn()
    const user = userEvent.setup()
    render(
      <Button type="submit" disabled onClick={onClick}>
        Save
      </Button>,
    )

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('type', 'submit')
    expect(onClick).not.toHaveBeenCalled()
  })

  it.each(['primary', 'secondary', 'danger', 'ghost'] as const)('renders the %s variant', (variant) => {
    render(<Button variant={variant}>Go</Button>)

    expect(screen.getByRole('button', { name: 'Go' })).toBeInTheDocument()
  })
})

describe('Modal', () => {
  it('shows its title and content, and closes from the X button', async () => {
    const onClose = vi.fn()
    const user = userEvent.setup()
    render(
      <Modal title="Edit student" onClose={onClose}>
        <p>form goes here</p>
      </Modal>,
    )

    expect(screen.getByRole('heading', { name: 'Edit student' })).toBeInTheDocument()
    expect(screen.getByText('form goes here')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalledOnce()
  })
})

describe('PageHeader and StatCard', () => {
  it('renders the title, description and action', () => {
    render(<PageHeader title="Students" description="Manage records." action={<button>New</button>} />)

    expect(screen.getByRole('heading', { name: 'Students' })).toBeInTheDocument()
    expect(screen.getByText('Manage records.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New' })).toBeInTheDocument()
  })

  it('renders without a description or action', () => {
    render(<PageHeader title="Payroll" />)

    expect(screen.getByRole('heading', { name: 'Payroll' })).toBeInTheDocument()
  })

  it('shows a stat with its label and value', () => {
    render(<StatCard label="Revenue collected" value="$3,600.00" icon={Wallet} />)

    expect(screen.getByText('Revenue collected')).toBeInTheDocument()
    expect(screen.getByText('$3,600.00')).toBeInTheDocument()
  })
})
