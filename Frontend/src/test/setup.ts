import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { mockState } from './mockState'

afterEach(() => {
  cleanup()
  localStorage.clear()

  const unmocked = [...mockState.unmocked]
  mockState.unmocked.length = 0
  if (unmocked.length > 0) {
    throw new Error(`Requests were made that no mock handled: ${unmocked.join(', ')}`)
  }
})
