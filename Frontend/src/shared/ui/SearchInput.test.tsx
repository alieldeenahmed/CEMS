import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { SearchInput } from './SearchInput'

function ControlledSearchInput() {
  const [value, setValue] = useState('')
  return <SearchInput value={value} onChange={(e) => setValue(e.target.value)} placeholder="Search..." aria-label="Search" />
}

describe('SearchInput', () => {
  it('renders as a search input with the given placeholder', () => {
    render(<ControlledSearchInput />)
    const input = screen.getByPlaceholderText('Search...')
    expect(input).toHaveAttribute('type', 'search')
  })

  it('updates its value as the user types', async () => {
    const user = userEvent.setup()
    render(<ControlledSearchInput />)

    const input = screen.getByLabelText('Search')
    await user.type(input, 'tara')

    expect(input).toHaveValue('tara')
  })
})
