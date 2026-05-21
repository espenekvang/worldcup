// One-shot migration: assign group-stage matches to round 1/2/3 by group + date.
// Per FIFA group format: 4 teams x 6 matches per group, with each team playing
// exactly one match per round. We sort each group by date and assign the first
// 2 to round 1, next 2 to round 2, last 2 to round 3, then verify that every
// team appears exactly once per round (i.e. no overlap).
//
// Writes the updated stage values back to src/data/matches.json.

import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const __dirname = dirname(fileURLToPath(import.meta.url))
const matchesPath = resolve(__dirname, '../src/data/matches.json')

const raw = readFileSync(matchesPath, 'utf8')
const matches = JSON.parse(raw)

const groupMatches = matches.filter(m => typeof m.stage === 'string' && m.stage.startsWith('group'))
const knockout = matches.filter(m => !(typeof m.stage === 'string' && m.stage.startsWith('group')))

// Group by group letter
const byGroup = new Map()
for (const m of groupMatches) {
  if (!m.group) throw new Error(`Group match ${m.id} mangler group-felt`)
  if (!byGroup.has(m.group)) byGroup.set(m.group, [])
  byGroup.get(m.group).push(m)
}

const updated = []
const summary = []

for (const [group, list] of [...byGroup.entries()].sort(([a], [b]) => a.localeCompare(b))) {
  if (list.length !== 6) {
    throw new Error(`Gruppe ${group} har ${list.length} kamper, forventet 6`)
  }
  const sorted = [...list].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
  const rounds = [
    { stage: 'group-1', games: sorted.slice(0, 2) },
    { stage: 'group-2', games: sorted.slice(2, 4) },
    { stage: 'group-3', games: sorted.slice(4, 6) },
  ]

  for (const { stage, games } of rounds) {
    const teamsInRound = new Set()
    for (const g of games) {
      if (teamsInRound.has(g.homeTeam) || teamsInRound.has(g.awayTeam)) {
        throw new Error(`Gruppe ${group} ${stage}: lag spiller to ganger samme runde (kamp ${g.id})`)
      }
      teamsInRound.add(g.homeTeam)
      teamsInRound.add(g.awayTeam)
    }
    if (teamsInRound.size !== 4) {
      throw new Error(`Gruppe ${group} ${stage}: forventet 4 unike lag, fant ${teamsInRound.size}`)
    }
    for (const g of games) {
      updated.push({ ...g, stage })
      summary.push(`  ${stage}  ${g.date}  ${g.homeTeam}-${g.awayTeam}`)
    }
  }
  summary.push(`Gruppe ${group} OK`)
}

const out = [...updated, ...knockout].sort((a, b) => a.id - b.id)

if (out.length !== matches.length) {
  throw new Error(`Output har ${out.length} kamper, input hadde ${matches.length}`)
}

writeFileSync(matchesPath, JSON.stringify(out, null, 2) + '\n', 'utf8')

console.log(summary.join('\n'))
console.log(`\nOppdatert ${updated.length} gruppespill-kamper i ${matchesPath}`)
