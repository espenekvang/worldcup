export function firstName(fullName: string): string {
  return fullName.split(' ')[0]
}

/**
 * Velger visningsnavn ut fra brukerens eget visningsnavn og ligaens innstilling.
 *
 * Har brukeren satt et eget visningsnavn (`custom`) vises det alltid, uavhengig
 * av `showFullName`. Ellers styrer `showFullName` om hele navnet (true) eller kun
 * fornavnet (false) vises.
 */
export function displayName(
  fullName: string,
  showFullName: boolean,
  custom?: string | null,
): string {
  const trimmed = custom?.trim()
  if (trimmed) return trimmed
  return showFullName ? fullName : firstName(fullName)
}
