export function firstName(fullName: string): string {
  return fullName.split(' ')[0]
}

/**
 * Velger visningsnavn ut fra ligaens innstilling.
 * showFullName=true gir hele navnet, ellers kun fornavn.
 */
export function displayName(fullName: string, showFullName: boolean): string {
  return showFullName ? fullName : firstName(fullName)
}
