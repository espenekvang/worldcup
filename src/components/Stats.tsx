import { useEffect, useMemo, useState } from 'react'
import { getLeagueStats, type LeagueStats, type AwardEntry } from '../api/client'
import { useBettingGroup } from '../context/BettingGroupContext'
import { useMatches } from '../context/MatchesContext'
import { useResults } from '../context/ResultsContext'
import { teams } from '../data'
import { displayName } from '../utils/nameUtils'

/**
 * «VM-oppsummering» per liga: personlige priser, kampfakta, aggregerte tall og
 * drama fra poengtavla. Alt regnes ut på backend over ligaens medlemmer; her
 * eier vi presentasjonen (norsk tekst, emoji og formatering).
 */

interface AwardMeta {
  emoji: string
  title: string
  description: string
  format: (value: number) => string
}

// Rekkefølge og presentasjon for de personlige prisene. Nøklene matcher backend.
const AWARD_META: Record<string, AwardMeta> = {
  nostradamus: { emoji: '🔮', title: 'Nostradamus', description: 'Flest blinkskudd (eksakt resultat)', format: v => `${v} blinkskudd` },
  utfallsekspert: { emoji: '🎯', title: 'Utfallseksperten', description: 'Flest riktige utfall', format: v => `${v} riktige utfall` },
  best_snitt: { emoji: '⭐', title: 'Snittkongen', description: 'Best poengsnitt per kamp', format: v => `${v.toFixed(2)} poeng/kamp` },
  presisjon: { emoji: '📐', title: 'Presisjonstipperen', description: 'Størst andel eksakt-målbonus', format: v => `${Math.round(v * 100)} % fra eksakt-mål` },
  optimist: { emoji: '🎉', title: 'Optimisten', description: 'Tipper flest mål', format: v => `${v.toFixed(1)} mål/kamp` },
  gjerrigknark: { emoji: '🔒', title: 'Gjerrigknarken', description: 'Tipper færrest mål', format: v => `${v.toFixed(1)} mål/kamp` },
  kaptein_uavgjort: { emoji: '🤝', title: 'Kaptein Uavgjort', description: 'Flest tippede uavgjort', format: v => `${v} uavgjort` },
  lengste_rekke: { emoji: '🔥', title: 'Formkurven', description: 'Lengst rekke med poeng', format: v => `${v} kamper på rad` },
  lengste_torke: { emoji: '🌵', title: 'Tørkeperioden', description: 'Lengst rekke uten poeng', format: v => `${v} kamper på rad` },
  brannfakkel: { emoji: '💣', title: 'Årets brannfakkel', description: 'Verste enkeltbom på utfall', format: v => `bom på ${v} mål` },
}

const AWARD_ORDER = [
  'nostradamus', 'utfallsekspert', 'best_snitt', 'presisjon', 'optimist',
  'gjerrigknark', 'kaptein_uavgjort', 'lengste_rekke', 'lengste_torke', 'brannfakkel',
]

export default function Stats() {
  const { activeGroup } = useBettingGroup()
  const { matches } = useMatches()
  const { results } = useResults()
  const [stats, setStats] = useState<LeagueStats | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const showFullName = activeGroup?.showFullName ?? false
  const groupId = activeGroup?.id

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    getLeagueStats()
      .then(data => { if (!cancelled) setStats(data) })
      .catch(() => { if (!cancelled) setError('Klarte ikke å laste statistikken.') })
      .finally(() => { if (!cancelled) setIsLoading(false) })
    return () => { cancelled = true }
  }, [groupId])

  /** Kort tekstetikett for en kamp, f.eks. «🇳🇴 Norge – Brasil 🇧🇷» med resultat. */
  const matchLabel = useMemo(() => (matchId: number): { title: string; score: string | null } => {
    const match = matches.find(m => m.id === matchId)
    if (!match) return { title: `Kamp ${matchId}`, score: null }
    const home = match.homeTeam ? teams[match.homeTeam] : undefined
    const away = match.awayTeam ? teams[match.awayTeam] : undefined
    const homeName = home ? `${home.flag} ${home.name}` : match.homePlaceholder ?? '?'
    const awayName = away ? `${away.name} ${away.flag}` : match.awayPlaceholder ?? '?'
    const result = results.get(matchId)
    const score = result ? `${result.homeScore}–${result.awayScore}` : null
    return { title: `${homeName} – ${awayName}`, score }
  }, [matches, results])

  if (isLoading) {
    return <p className="py-8 text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>Laster statistikk …</p>
  }

  if (error) {
    return <p className="py-8 text-center text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
  }

  if (!stats || stats.scoredMatchCount === 0) {
    return (
      <p className="py-8 text-center text-sm" style={{ color: 'var(--color-text-muted)' }}>
        Statistikk dukker opp så snart de første kampresultatene er registrert.
      </p>
    )
  }

  const facts = stats.matchFacts
  const agg = stats.aggregate
  const drama = stats.drama

  return (
    <div className="space-y-8 pb-4">
      <header>
        <h2 className="text-xl font-bold" style={{ color: 'var(--color-text-primary)' }}>VM-oppsummering</h2>
        <p className="text-sm" style={{ color: 'var(--color-text-muted)' }}>
          {stats.memberCount} deltakere · {stats.scoredMatchCount} spilte kamper
        </p>
      </header>

      {/* Personlige priser */}
      <section>
        <SectionTitle>🏆 Priser</SectionTitle>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {AWARD_ORDER.map(key => {
            const award = stats.personalAwards.find(a => a.key === key)
            if (!award) return null
            return <AwardCard key={key} award={award} showFullName={showFullName} matchLabel={matchLabel} />
          })}
        </div>
      </section>

      {/* Kampfakta */}
      <section>
        <SectionTitle>📅 Kampfakta</SectionTitle>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {facts.hardestMatch && (
            <FactCard emoji="😱" title="Kampen som lurte alle" value={`${facts.hardestMatch.value.toFixed(2)} poeng i snitt`} match={matchLabel(facts.hardestMatch.matchId)} />
          )}
          {facts.easiestMatch && (
            <FactCard emoji="😎" title="Gavekampen" value={`${facts.easiestMatch.value.toFixed(2)} poeng i snitt`} match={matchLabel(facts.easiestMatch.matchId)} />
          )}
          {facts.biggestShock && (
            <FactCard emoji="⚡" title="Sjokkresultatet" value={`Bare ${Math.round(facts.biggestShock.value * 100)} % traff utfallet`} match={matchLabel(facts.biggestShock.matchId)} />
          )}
          {facts.popularScoreline && (
            <FactCard emoji="📊" title="Folkets resultat" value={`${facts.popularScoreline.homeScore}–${facts.popularScoreline.awayScore} · tippet ${facts.popularScoreline.count} ganger`} />
          )}
        </div>
      </section>

      {/* Aggregerte tall */}
      <section>
        <SectionTitle>📈 Store linjer</SectionTitle>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <FactCard
            emoji="⚽"
            title="Målteften"
            value={`Dere tippet ${agg.avgPredictedGoalsPerMatch.toFixed(1)} mål/kamp – fasit ble ${agg.avgActualGoalsPerMatch.toFixed(1)}`}
          />
          <FactCard
            emoji="🎓"
            title="Gruppespill vs. sluttspill"
            value={
              agg.knockout.predictionCount > 0
                ? `${agg.groupStage.avgPoints.toFixed(2)} poeng/kamp i gruppespill, ${agg.knockout.avgPoints.toFixed(2)} i sluttspill`
                : `${agg.groupStage.avgPoints.toFixed(2)} poeng/kamp i gruppespill (${Math.round(agg.groupStage.outcomeHitRate * 100)} % traff utfallet)`
            }
          />
        </div>
      </section>

      {/* Drama */}
      <section>
        <SectionTitle>🎢 Dramaet</SectionTitle>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <FactCard emoji="🔄" title="Lederskifter" value={drama.leadChanges === 0 ? 'Ledelsen ble aldri utfordret' : `Ledelsen skiftet ${drama.leadChanges} ganger`} />
          {drama.biggestClimb && (
            <FactCard emoji="🚀" title="Comebacket" value={`${displayName(drama.biggestClimb.name ?? '', showFullName)} klatret ${drama.biggestClimb.positions} plasser`} />
          )}
          {drama.biggestFall && (
            <FactCard emoji="🪂" title="Kollapsen" value={`${displayName(drama.biggestFall.name ?? '', showFullName)} falt ${drama.biggestFall.positions} plasser`} />
          )}
        </div>
      </section>
    </div>
  )
}

function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
      {children}
    </h3>
  )
}

interface CardShellProps {
  children: React.ReactNode
}

function CardShell({ children }: CardShellProps) {
  return (
    <div
      className="rounded-xl border p-4"
      style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
    >
      {children}
    </div>
  )
}

function AwardCard({
  award,
  showFullName,
  matchLabel,
}: {
  award: AwardEntry
  showFullName: boolean
  matchLabel: (matchId: number) => { title: string; score: string | null }
}) {
  const meta = AWARD_META[award.key]
  if (!meta) return null

  const hasWinner = !!award.winnerName
  const match = award.matchId != null ? matchLabel(award.matchId) : null

  return (
    <CardShell>
      <div className="flex items-start gap-3">
        <span className="text-2xl leading-none" aria-hidden>{meta.emoji}</span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold" style={{ color: 'var(--color-text-primary)' }}>{meta.title}</p>
          <p className="text-xs" style={{ color: 'var(--color-text-muted)' }}>{meta.description}</p>
          {hasWinner ? (
            <div className="mt-2 flex items-center gap-2">
              <Avatar name={award.winnerName!} picture={award.winnerPicture} />
              <div className="min-w-0">
                <p className="truncate text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
                  {displayName(award.winnerName!, showFullName)}
                </p>
                <p className="truncate text-xs" style={{ color: 'var(--color-tab-active)' }}>
                  {meta.format(award.value)}
                  {match && <span style={{ color: 'var(--color-text-muted)' }}> · {match.title}{match.score ? ` (${match.score})` : ''}</span>}
                </p>
              </div>
            </div>
          ) : (
            <p className="mt-2 text-xs italic" style={{ color: 'var(--color-text-muted)' }}>Ingen kandidat ennå</p>
          )}
        </div>
      </div>
    </CardShell>
  )
}

function FactCard({
  emoji,
  title,
  value,
  match,
}: {
  emoji: string
  title: string
  value: string
  match?: { title: string; score: string | null }
}) {
  return (
    <CardShell>
      <div className="flex items-start gap-3">
        <span className="text-2xl leading-none" aria-hidden>{emoji}</span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold" style={{ color: 'var(--color-text-primary)' }}>{title}</p>
          <p className="text-sm" style={{ color: 'var(--color-text-primary)' }}>{value}</p>
          {match && (
            <p className="mt-1 truncate text-xs" style={{ color: 'var(--color-text-muted)' }}>
              {match.title}{match.score ? ` · ${match.score}` : ''}
            </p>
          )}
        </div>
      </div>
    </CardShell>
  )
}

function Avatar({ name, picture }: { name: string; picture: string | null }) {
  if (picture) {
    return <img src={picture} alt="" className="h-8 w-8 shrink-0 rounded-full object-cover" />
  }
  return (
    <span
      className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-semibold"
      style={{ backgroundColor: 'var(--color-surface-elevated)', color: 'var(--color-text-primary)' }}
      aria-hidden
    >
      {name.charAt(0).toUpperCase()}
    </span>
  )
}
