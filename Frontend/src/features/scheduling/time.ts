// CEMS does not model branch time zones (see README) - a session's wall-clock time is
// treated as UTC directly, with no browser-local conversion in either direction.

export function toUtcIso(localDateTimeValue: string): string {
  return localDateTimeValue.length === 16 ? `${localDateTimeValue}:00.000Z` : localDateTimeValue
}

export function formatUtcForDisplay(utcIso: string): string {
  return utcIso.slice(0, 16).replace('T', ' ')
}
