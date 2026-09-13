import { isAxiosError } from 'axios'

export function getErrorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (isAxiosError(error)) {
    const errors = error.response?.data?.errors
    if (Array.isArray(errors) && errors.length > 0) {
      return errors.join(' ')
    }
    if (typeof error.response?.data?.title === 'string') {
      return error.response.data.title
    }
  }
  return fallback
}
