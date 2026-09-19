import { AxiosError } from 'axios'
import { describe, expect, it } from 'vitest'
import { getErrorMessage } from './errors'

function serverError(data: unknown, status = 400) {
  const response = { data, status, statusText: '', headers: {}, config: {} as never }
  return new AxiosError('failed', 'ERR_BAD_REQUEST', undefined, {}, response)
}

describe('getErrorMessage', () => {
  it('joins the messages of a business-rule error (errors is a list)', () => {
    expect(getErrorMessage(serverError({ title: 'Bad request', errors: ['Room is booked.', 'Teacher is busy.'] }))).toBe('Room is booked. Teacher is busy.')
  })

  it('lists every field message of a validation error (errors is keyed by field), not just "Validation failed"', () => {
    const error = serverError({
      title: 'Validation failed',
      errors: { Name: ["'Name' must not be empty."], Phone: ["'Phone' must not be empty.", 'Phone is too short.'] },
    })

    expect(getErrorMessage(error)).toBe("'Name' must not be empty. 'Phone' must not be empty. Phone is too short.")
  })

  it('falls back to the title when there is no error list', () => {
    expect(getErrorMessage(serverError({ title: 'Not allowed.' }, 403))).toBe('Not allowed.')
  })

  it('ignores an empty error list and uses the title', () => {
    expect(getErrorMessage(serverError({ title: 'Something specific', errors: [] }))).toBe('Something specific')
  })

  it('uses the supplied fallback for a non-server error', () => {
    expect(getErrorMessage(new Error('boom'), 'Could not save.')).toBe('Could not save.')
    expect(getErrorMessage('a string', undefined)).toBe('Something went wrong.')
  })

  it('uses the fallback when the server sent nothing usable', () => {
    expect(getErrorMessage(serverError({}, 500), 'Try again later.')).toBe('Try again later.')
  })
})
