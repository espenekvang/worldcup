import { describe, it, expect } from 'vitest'
import { tvChannels, getTvChannel, matches } from '../data'

describe('tvChannels-data', () => {
  it('inneholder kun gyldige kanaler (NRK/TV2)', () => {
    for (const channel of Object.values(tvChannels)) {
      expect(['NRK', 'TV2']).toContain(channel)
    }
  })

  it('refererer kun til eksisterende kamp-id-er', () => {
    const matchIds = new Set(matches.map(m => m.id))
    for (const id of Object.keys(tvChannels)) {
      expect(matchIds.has(Number(id))).toBe(true)
    }
  })

  it('dekker alle gruppespill-kampene', () => {
    const groupStages = new Set(['group-1', 'group-2', 'group-3'])
    for (const match of matches) {
      if (groupStages.has(match.stage)) {
        expect(getTvChannel(match.id)).toBeDefined()
      }
    }
  })

  it('slår opp kjente kamper riktig', () => {
    // Kamp 1: Mexico–Sør-Afrika på TV2, kamp 2: Sør-Korea–Tsjekkia på NRK
    expect(getTvChannel(1)).toBe('TV2')
    expect(getTvChannel(2)).toBe('NRK')
  })

  it('dekker round-of-32-kampene', () => {
    for (const match of matches) {
      if (match.stage === 'round-of-32') {
        expect(getTvChannel(match.id)).toBeDefined()
      }
    }
  })

  it('dekker round-of-16-kampene', () => {
    for (const match of matches) {
      if (match.stage === 'round-of-16') {
        expect(getTvChannel(match.id)).toBeDefined()
      }
    }
  })

  it('kanalsetter finalen (NRK)', () => {
    expect(getTvChannel(104)).toBe('NRK')
  })

  it('slår opp åttedelsfinale-kamper riktig', () => {
    // Kamp 89: Paraguay–Frankrike på TV2, kamp 90: Canada–Marokko på NRK
    expect(getTvChannel(89)).toBe('TV2')
    expect(getTvChannel(90)).toBe('NRK')
  })

  it('returnerer undefined for runder uten fastsatt kanal (kvartfinaler)', () => {
    // NRK/TV2 har ikke fordelt kvartfinalene per kamp ennå.
    expect(getTvChannel(97)).toBeUndefined()
  })
})
