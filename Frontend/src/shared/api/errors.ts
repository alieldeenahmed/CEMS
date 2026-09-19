import { isAxiosError } from 'axios'

export function getErrorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (isAxiosError(error)) {
    const errors = error.response?.data?.errors

    // A business-rule error carries a list of messages...
    if (Array.isArray(errors) && errors.length > 0) {
      return errors.join(' ')
    }

    // ...while a validation error carries them keyed by field ({ Name: ['...'], Phone: ['...'] }).
    if (errors && typeof errors === 'object') {
      const messages = Object.values(errors)
        .flat()
        .filter((message): message is string => typeof message === 'string')
      if (messages.length > 0) {
        return messages.join(' ')
      }
    }

    if (typeof error.response?.data?.title === 'string') {
      return error.response.data.title
    }
  }
  return fallback
}
