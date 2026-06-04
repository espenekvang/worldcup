import type { Team } from '../../types'
import { useEffect, useState } from 'react'
import { getTeamStats, getHeadToHead, type TeamStatsResponse, type HeadToHeadResponse, type PlayerEntry } from '../../api/client'

/**
 * Henter team-stats for inntil to lag og H2H mellom dem i ett kall.
 * Returnerer `loading=true` til alt er ferdig. Manglende data settes
 * til `null` slik at komponenter kan vise "ikke tilgjengelig"-tilstand.
 */
export function useMatchStats(homeCode: string | null, awayCode: string | null) {
  const [homeStats, setHomeStats] = useState<TeamStatsResponse | null>(null)
  const [awayStats, setAwayStats] = useState<TeamStatsResponse | null>(null)
  const [headToHead, setHeadToHead] = useState<HeadToHeadResponse | null>(null)
  const [loading, setLoading] = useState<boolean>(Boolean(homeCode || awayCode))

  useEffect(() => {
    let cancelled = false

    if (!homeCode && !awayCode) {
      setHomeStats(null); setAwayStats(null); setHeadToHead(null)
      setLoading(false)
      return
    }

    setLoading(true)
    Promise.all([
      homeCode ? getTeamStats(homeCode).catch(() => null) : Promise.resolve(null),
      awayCode ? getTeamStats(awayCode).catch(() => null) : Promise.resolve(null),
      homeCode && awayCode ? getHeadToHead(homeCode, awayCode).catch(() => null) : Promise.resolve(null),
    ]).then(([h, a, hh]) => {
      if (cancelled) return
      setHomeStats(h)
      setAwayStats(a)
      setHeadToHead(hh)
      setLoading(false)
    })

    return () => { cancelled = true }
  }, [homeCode, awayCode])

  return { homeStats, awayStats, headToHead, loading }
}

/**
 * Mapping fra API-ets engelske form-bokstaver (W/D/L) til norske
 * (S/U/T = Seier/Uavgjort/Tap). API-et beholder W/D/L som standard
 * datafortmat, mens UI viser norske bokstaver.
 */
const FORM_LABELS: Record<string, { letter: string; label: string; color: string }> = {
  W: { letter: 'S', label: 'Seier', color: 'var(--color-success-text, #16a34a)' },
  D: { letter: 'U', label: 'Uavgjort', color: '#94a3b8' },
  L: { letter: 'T', label: 'Tap', color: '#dc2626' },
}

interface TeamFormStripProps {
  stats: TeamStatsResponse | null
  team: Team | null
  fallbackName: string
  teams: Record<string, Team>
}

/**
 * Viser form-streng ("WWDWL") og de siste 5 kampene som farge-koda
 * sirkler. Brukes side-om-side for hjemme- og bortelaget. `teams`-mappet
 * brukes til å vise flagg + fullt navn på motstandere i stedet for
 * FIFA-koden alene.
 */
export function TeamFormStrip({ stats, team, fallbackName, teams }: TeamFormStripProps) {
  const name = team?.name ?? fallbackName
  const flag = team?.flag ?? ''

  if (!stats) {
    return (
      <div className="rounded-lg border p-3" style={{ borderColor: 'var(--color-border)', backgroundColor: 'var(--color-surface-card)' }}>
        <p className="text-sm font-semibold" style={{ color: 'var(--color-text-primary)' }}>
          {flag} {name}
        </p>
        <p className="mt-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>
          Statistikk ikke tilgjengelig.
        </p>
      </div>
    )
  }

  const formChars = (stats.recentForm ?? '').split('')

  return (
    <div className="rounded-lg border p-3" style={{ borderColor: 'var(--color-border)', backgroundColor: 'var(--color-surface-card)' }}>
      <div className="mb-2 flex items-center justify-between">
        <p className="text-sm font-semibold" style={{ color: 'var(--color-text-primary)' }}>
          {flag} {name}
        </p>
        {stats.fifaRank ? (
          <span className="rounded-full px-2 py-0.5 text-[10px] font-medium"
            style={{ backgroundColor: 'var(--color-badge-bg)', color: 'var(--color-badge-text)' }}>
            FIFA #{stats.fifaRank}
          </span>
        ) : null}
      </div>

      <div className="mb-2 flex items-center gap-1.5">
        <span className="text-xs font-medium" style={{ color: 'var(--color-text-muted)' }}>Form:</span>
        {formChars.length === 0 ? (
          <span className="text-xs" style={{ color: 'var(--color-text-muted)' }}>—</span>
        ) : formChars.map((c, i) => {
          const fl = FORM_LABELS[c] ?? FORM_LABELS.L
          return (
            <span
              key={i}
              title={fl.label}
              aria-label={fl.label}
              className="inline-flex h-5 w-5 items-center justify-center rounded-full text-[10px] font-bold text-white"
              style={{ backgroundColor: fl.color }}
            >
              {fl.letter}
            </span>
          )
        })}
      </div>

      <ul className="space-y-1 text-xs" style={{ color: 'var(--color-text-secondary)' }}>
        {stats.recentMatches.slice(0, 5).map((m, i) => {
          const opp = teams[m.opponent]
          const oppLabel = opp ? `${opp.flag} ${opp.name}` : m.opponent
          const venueLabel =
            m.venue === 'home' ? 'hjemme mot' :
            m.venue === 'away' ? 'borte mot' :
            'nøytral bane mot'
          return (
            <li key={i} className="flex items-center justify-between gap-2">
              <span style={{ color: 'var(--color-text-muted)' }}>{m.date.slice(5)}</span>
              <span className="flex-1 truncate" title={`${venueLabel} ${opp?.name ?? m.opponent}`}>
                <span className="text-[10px]" style={{ color: 'var(--color-text-muted)' }}>
                  {m.venue === 'home' ? 'H ' : m.venue === 'away' ? 'B ' : 'N '}
                </span>
                {oppLabel}
              </span>
              <span className="font-semibold tabular-nums" style={{ color: 'var(--color-text-primary)' }}>
                {m.goalsFor}–{m.goalsAgainst}
              </span>
              <span className="w-16 truncate text-right text-[10px]" title={m.competition} style={{ color: 'var(--color-text-muted)' }}>
                {m.competition}
              </span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

/**
 * Liten forklaring av form-bokstavene (S/U/T) og venue-prefikser (H/B/N).
 * Plasseres over form-stripene så brukeren skjønner hva ikonene betyr.
 */
export function FormLegend() {
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px]" style={{ color: 'var(--color-text-muted)' }}>
      <span className="inline-flex items-center gap-1">
        <span className="inline-flex h-4 w-4 items-center justify-center rounded-full text-[9px] font-bold text-white" style={{ backgroundColor: FORM_LABELS.W.color }}>{FORM_LABELS.W.letter}</span>
        {FORM_LABELS.W.label}
      </span>
      <span className="inline-flex items-center gap-1">
        <span className="inline-flex h-4 w-4 items-center justify-center rounded-full text-[9px] font-bold text-white" style={{ backgroundColor: FORM_LABELS.D.color }}>{FORM_LABELS.D.letter}</span>
        {FORM_LABELS.D.label}
      </span>
      <span className="inline-flex items-center gap-1">
        <span className="inline-flex h-4 w-4 items-center justify-center rounded-full text-[9px] font-bold text-white" style={{ backgroundColor: FORM_LABELS.L.color }}>{FORM_LABELS.L.letter}</span>
        {FORM_LABELS.L.label}
      </span>
      <span className="ml-2 hidden sm:inline">·</span>
      <span><strong>H</strong> hjemme · <strong>B</strong> borte · <strong>N</strong> nøytral bane</span>
    </div>
  )
}

// Posisjonsrekkefølge + norske gruppe-etiketter for tropp-visningen.
const POSITION_ORDER = ['GK', 'DF', 'MF', 'FW'] as const
const POSITION_LABELS: Record<string, string> = {
  GK: 'Keepere',
  DF: 'Forsvar',
  MF: 'Midtbane',
  FW: 'Angrep',
  '?': 'Øvrige',
}

function positionGroup(pos: string | null): string {
  const p = (pos ?? '').toUpperCase()
  return POSITION_LABELS[p] ? p : '?'
}

interface SquadCardProps {
  squad: PlayerEntry[] | undefined
  team: Team | null
  fallbackName: string
}

/**
 * Viser ett lags VM-tropp gruppert på posisjon (keeper/forsvar/midtbane/
 * angrep). Hver spiller vises med draktnummer, navn, alder og klubblag.
 * Tom/uoppgitt tropp gir en "ikke publisert ennå"-melding.
 */
export function SquadCard({ squad, team, fallbackName }: SquadCardProps) {
  const name = team?.name ?? fallbackName
  const flag = team?.flag ?? ''
  const players = squad ?? []

  // Sorter innen hver gruppe på draktnummer (manglende nummer sist).
  const groupKeys: string[] = [...POSITION_ORDER, '?']
  const groups = groupKeys.map(group => ({
    group,
    label: POSITION_LABELS[group],
    players: players
      .filter(p => positionGroup(p.position) === group)
      .sort((a, b) => (a.shirtNumber ?? 99) - (b.shirtNumber ?? 99)),
  })).filter(g => g.players.length > 0)

  return (
    <div className="rounded-lg border p-3" style={{ borderColor: 'var(--color-border)', backgroundColor: 'var(--color-surface-card)' }}>
      <div className="mb-2 flex items-center justify-between">
        <p className="text-sm font-semibold" style={{ color: 'var(--color-text-primary)' }}>
          {flag} {name}
        </p>
        {players.length > 0 ? (
          <span className="text-[10px]" style={{ color: 'var(--color-text-muted)' }}>
            {players.length} spillere
          </span>
        ) : null}
      </div>

      {groups.length === 0 ? (
        <p className="text-xs" style={{ color: 'var(--color-text-muted)' }}>
          Troppen er ikke publisert ennå.
        </p>
      ) : (
        <div className="space-y-2">
          {groups.map(g => (
            <div key={g.group}>
              <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
                {g.label}
              </p>
              <ul className="space-y-0.5 text-xs">
                {g.players.map((p, i) => (
                  <li key={i} className="flex items-baseline justify-between gap-2">
                    <span className="flex min-w-0 items-baseline gap-1.5">
                      <span className="w-5 shrink-0 text-right tabular-nums text-[10px]" style={{ color: 'var(--color-text-muted)' }}>
                        {p.shirtNumber ?? ''}
                      </span>
                      <span className="truncate font-medium" style={{ color: 'var(--color-text-primary)' }}>
                        {p.name}
                      </span>
                      {p.age != null ? (
                        <span className="shrink-0 text-[10px]" style={{ color: 'var(--color-text-muted)' }}>
                          {p.age} år
                        </span>
                      ) : null}
                    </span>
                    <span className="shrink-0 truncate text-right text-[10px]" style={{ color: 'var(--color-text-secondary)' }} title={p.club ?? undefined}>
                      {p.club ?? '—'}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

interface TeamStatsCompareProps {
  homeStats: TeamStatsResponse | null
  awayStats: TeamStatsResponse | null
  homeName: string
  awayName: string
}

/**
 * Sammenligningstabell side-om-side: FIFA-rank, manager, nøkkelspiller,
 * formasjon, snitt mål for/mot, forrige VM, fravær.
 */
export function TeamStatsCompare({ homeStats, awayStats, homeName, awayName }: TeamStatsCompareProps) {
  const rows: { label: string; home: React.ReactNode; away: React.ReactNode }[] = [
    { label: 'FIFA-rank', home: fmtRank(homeStats?.fifaRank), away: fmtRank(awayStats?.fifaRank) },
    { label: 'Manager', home: homeStats?.manager ?? '—', away: awayStats?.manager ?? '—' },
    { label: 'Nøkkelspiller', home: homeStats?.starPlayer ?? '—', away: awayStats?.starPlayer ?? '—' },
    { label: 'Formasjon', home: homeStats?.preferredFormation ?? '—', away: awayStats?.preferredFormation ?? '—' },
    { label: 'Mål scoret (snitt)', home: fmtAvg(homeStats?.goalsScoredAvg), away: fmtAvg(awayStats?.goalsScoredAvg) },
    { label: 'Mål sluppet (snitt)', home: fmtAvg(homeStats?.goalsConcededAvg), away: fmtAvg(awayStats?.goalsConcededAvg) },
    { label: 'Forrige VM', home: homeStats?.lastWorldCupResult ?? '—', away: awayStats?.lastWorldCupResult ?? '—' },
    {
      label: 'Skader/karantene',
      home: homeStats?.keyAbsences.length ? homeStats.keyAbsences.join(', ') : 'Ingen kjente',
      away: awayStats?.keyAbsences.length ? awayStats.keyAbsences.join(', ') : 'Ingen kjente',
    },
  ]

  return (
    <div className="overflow-hidden rounded-lg border" style={{ borderColor: 'var(--color-border)' }}>
      <table className="w-full text-xs">
        <thead>
          <tr style={{ backgroundColor: 'var(--color-surface-elevated)', color: 'var(--color-text-muted)' }}>
            <th className="px-2 py-1.5 text-right font-medium">{homeName}</th>
            <th className="px-2 py-1.5 text-center font-medium"></th>
            <th className="px-2 py-1.5 text-left font-medium">{awayName}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i} className="border-t" style={{ borderColor: 'var(--color-border-light)' }}>
              <td className="px-2 py-1.5 text-right tabular-nums" style={{ color: 'var(--color-text-primary)' }}>{r.home}</td>
              <td className="px-2 py-1.5 text-center text-[10px] uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>{r.label}</td>
              <td className="px-2 py-1.5 text-left tabular-nums" style={{ color: 'var(--color-text-primary)' }}>{r.away}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function fmtRank(r: number | null | undefined): string {
  return r ? `#${r}` : '—'
}
function fmtAvg(v: number | null | undefined): string {
  return v === null || v === undefined ? '—' : v.toFixed(2)
}

interface HeadToHeadSectionProps {
  data: HeadToHeadResponse | null
  homeCode: string
  awayCode: string
  teams: Record<string, Team>
}

/**
 * Viser samlet H2H-statistikk og de siste møtene. teamA i responsen er
 * "hjemmelaget" (klientens første argument) — vi bruker denne mappingen
 * for å vise W/D/L fra hjemmelagets perspektiv.
 */
export function HeadToHeadSection({ data, homeCode, awayCode, teams }: HeadToHeadSectionProps) {
  const homeName = teams[homeCode]?.name ?? homeCode
  const awayName = teams[awayCode]?.name ?? awayCode

  if (!data || data.totalMatches === 0) {
    return (
      <div className="rounded-lg border p-4 text-center text-sm" style={{ borderColor: 'var(--color-border)', color: 'var(--color-text-muted)' }}>
        Ingen tidligere oppgjør registrert mellom {homeName} og {awayName}.
      </div>
    )
  }

  // Siden teamA i responsen alltid matcher det vi spurte om, kan vi
  // bruke homeCode-perspektivet direkte.
  const total = data.totalMatches
  const pct = (n: number) => Math.round((n / Math.max(total, 1)) * 100)

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-3 gap-2 text-center">
        <Stat label={`${homeName} vant`} value={data.teamAWins} pct={pct(data.teamAWins)} color="var(--color-success-text, #16a34a)" />
        <Stat label="Uavgjort" value={data.draws} pct={pct(data.draws)} color="#94a3b8" />
        <Stat label={`${awayName} vant`} value={data.teamBWins} pct={pct(data.teamBWins)} color="#dc2626" />
      </div>

      <p className="text-center text-xs" style={{ color: 'var(--color-text-muted)' }}>
        {total} kamper totalt · Mål: {data.teamAGoals}–{data.teamBGoals}
      </p>

      <div className="rounded-lg border" style={{ borderColor: 'var(--color-border)' }}>
        <p className="px-3 pt-2 text-[10px] uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
          Siste møter
        </p>
        <ul className="divide-y px-3 pb-2" style={{ borderColor: 'var(--color-border-light)' }}>
          {data.recentMatches.slice(0, 5).map((m, i) => {
            const home = teams[m.homeTeam]
            const away = teams[m.awayTeam]
            return (
              <li key={i} className="flex items-center justify-between gap-2 py-1.5 text-xs">
                <span style={{ color: 'var(--color-text-muted)' }}>{m.date}</span>
                <span className="flex-1 truncate text-right" style={{ color: 'var(--color-text-secondary)' }}>
                  {home?.flag ?? ''} {home?.name ?? m.homeTeam}
                </span>
                <span className="font-bold tabular-nums" style={{ color: 'var(--color-text-primary)' }}>
                  {m.homeScore}–{m.awayScore}
                </span>
                <span className="flex-1 truncate" style={{ color: 'var(--color-text-secondary)' }}>
                  {away?.flag ?? ''} {away?.name ?? m.awayTeam}
                </span>
                <span className="w-20 truncate text-[10px]" title={m.competition} style={{ color: 'var(--color-text-muted)' }}>
                  {m.competition}
                </span>
              </li>
            )
          })}
        </ul>
      </div>
    </div>
  )
}

function Stat({ label, value, pct, color }: { label: string; value: number; pct: number; color: string }) {
  return (
    <div className="rounded-lg border p-2" style={{ borderColor: 'var(--color-border)' }}>
      <p className="text-lg font-bold tabular-nums" style={{ color }}>{value}</p>
      <p className="text-[10px]" style={{ color: 'var(--color-text-muted)' }}>{label}</p>
      <p className="text-[10px] tabular-nums" style={{ color: 'var(--color-text-muted)' }}>{pct}%</p>
    </div>
  )
}
