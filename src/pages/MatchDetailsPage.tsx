import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { teams, venues } from '../data'
import Header from '../components/Header'
import BottomNav from '../components/BottomNav'
import WeatherBadge from '../components/WeatherBadge'
import MatchPredictionsList from '../components/MatchPredictionsList'
import PredictionModal from '../components/PredictionModal'
import {
  useMatchStats,
  TeamFormStrip,
  TeamStatsCompare,
  HeadToHeadSection,
  FormLegend,
} from '../components/match-details/StatsSections'
import { useMatches } from '../context/MatchesContext'
import { usePredictions } from '../context/PredictionsContext'
import { useResults } from '../context/ResultsContext'
import { useWeather } from '../hooks/useWeather'
import { useAuth } from '../context/AuthContext'
import { formatMatchDate, formatMatchTime, areTeamsUndetermined, isStageLocked, isGroupStage } from '../utils/dateUtils'

const STAGE_LABELS: Record<string, string> = {
  'group-1': 'Gruppespill',
  'group-2': 'Gruppespill',
  'group-3': 'Gruppespill',
  'round-of-32': '32-delsfinale',
  'round-of-16': '8-delsfinale',
  'quarter-final': 'Kvartfinale',
  'semi-final': 'Semifinale',
  'third-place': 'Bronsefinale',
  'final': 'Finale',
}

/**
 * Detaljside for én kamp (URL /match/:matchId). Samler all info som kan
 * hjelpe en tipper: kickoff, venue, vær, ditt tips, andres tips, lagform,
 * head-to-head og sammenlignende lag-statistikk.
 *
 * Når et lag ikke er bestemt ennå (typisk sluttspillkamper før gruppene
 * er ferdige) skjuler vi form/H2H/sammenligning og forklarer hvorfor.
 */
export default function MatchDetailsPage() {
  const { matchId } = useParams<{ matchId: string }>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const { matches } = useMatches()
  const { predictions } = usePredictions()
  const { results, points } = useResults()
  const [editing, setEditing] = useState(false)

  const id = Number(matchId)
  const match = matches.find(m => m.id === id) ?? null
  const canAccessAdmin = user?.isAdmin || (user?.groupAdminGroupIds?.length ?? 0) > 0

  const venue = match ? venues.find(v => v.id === match.venueId) : undefined
  const { forecast, loading: weatherLoading } = useWeather(venue?.id, match?.date ?? '', venue?.timezone)

  const undetermined = match ? areTeamsUndetermined(match) : true
  const homeCode = match?.homeTeam ?? null
  const awayCode = match?.awayTeam ?? null
  const { homeStats, awayStats, headToHead, loading: statsLoading } = useMatchStats(homeCode, awayCode)

  if (!match) {
    return (
      <div className="flex min-h-screen flex-col" style={{ backgroundColor: 'var(--color-surface)' }}>
        <Header onAdminClick={canAccessAdmin ? () => navigate('/?admin=1') : undefined} />
        <main className="mx-auto w-full max-w-4xl flex-1 p-6 text-center">
          <p style={{ color: 'var(--color-text-muted)' }}>Fant ikke kampen.</p>
          <button
            onClick={() => navigate('/')}
            className="mt-4 rounded-lg px-4 py-2 text-sm font-medium text-white"
            style={{ backgroundColor: 'var(--color-primary)' }}
          >
            Tilbake
          </button>
        </main>
        <BottomNav />
      </div>
    )
  }

  const homeTeam = match.homeTeam ? teams[match.homeTeam] : null
  const awayTeam = match.awayTeam ? teams[match.awayTeam] : null
  const homeDisplay = homeTeam?.name ?? match.homePlaceholder ?? 'Ikke avgjort'
  const awayDisplay = awayTeam?.name ?? match.awayPlaceholder ?? 'Ikke avgjort'
  const stageLabel = isGroupStage(match.stage) ? `Gruppe ${match.group}` : STAGE_LABELS[match.stage] ?? match.stage

  const prediction = predictions.get(match.id)
  const result = results.get(match.id)
  const pts = points.get(match.id)
  const locked = isStageLocked(match.stage, matches)
  const canBet = !undetermined && !locked

  return (
    <div className="flex min-h-screen flex-col" style={{ backgroundColor: 'var(--color-surface)' }}>
      <Header onAdminClick={canAccessAdmin ? () => navigate('/?admin=1') : undefined} />

      <main className="mx-auto w-full max-w-4xl flex-1 p-4 pb-20 sm:p-6 lg:p-8 lg:pb-8">
        <button
          onClick={() => navigate(-1)}
          className="mb-4 inline-flex items-center gap-1 text-sm font-medium"
          style={{ color: 'var(--color-primary)' }}
        >
          ← Tilbake
        </button>

        {/* Hovedkort: kamp-overskrift, kickoff, venue, vær */}
        <section
          className="rounded-xl border p-4 shadow-sm sm:p-6"
          style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
        >
          <div className="mb-3 flex flex-wrap items-center gap-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>
            <span
              className="rounded-full px-2 py-0.5 font-medium"
              style={{ backgroundColor: 'var(--color-badge-bg)', color: 'var(--color-badge-text)' }}
            >
              {stageLabel}
            </span>
            <span>{formatMatchDate(match.date)}</span>
            <span>kl. {formatMatchTime(match.date)}</span>
          </div>

          <div className="mt-2 grid grid-cols-[1fr_auto_1fr] items-center gap-3 sm:gap-6">
            <div className="text-center sm:text-right">
              <p className="text-2xl sm:text-3xl">{homeTeam?.flag ?? ''}</p>
              <p className="mt-1 text-sm font-semibold sm:text-base" style={{ color: 'var(--color-text-primary)' }}>
                {homeDisplay}
              </p>
            </div>
            <div className="text-center">
              {result ? (
                <p className="text-2xl font-bold sm:text-3xl" style={{ color: 'var(--color-text-primary)' }}>
                  {result.homeScore} – {result.awayScore}
                </p>
              ) : (
                <p className="text-xl font-medium" style={{ color: 'var(--color-text-muted)' }}>vs</p>
              )}
            </div>
            <div className="text-center sm:text-left">
              <p className="text-2xl sm:text-3xl">{awayTeam?.flag ?? ''}</p>
              <p className="mt-1 text-sm font-semibold sm:text-base" style={{ color: 'var(--color-text-primary)' }}>
                {awayDisplay}
              </p>
            </div>
          </div>

          {venue ? (
            <p className="mt-4 flex flex-wrap items-center justify-center gap-2 text-sm" style={{ color: 'var(--color-text-muted)' }}>
              <span>📍 {venue.name}, {venue.city}, {venue.country}</span>
              <WeatherBadge forecast={forecast} loading={weatherLoading} />
            </p>
          ) : null}
        </section>

        {/* Ditt tips */}
        <section
          className="mt-4 rounded-xl border p-4 shadow-sm"
          style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
        >
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
            Ditt bet
          </h2>
          <div className="flex items-center justify-between gap-3">
            {prediction ? (
              <span
                className="rounded-md px-3 py-1.5 text-lg font-bold tabular-nums"
                style={{ backgroundColor: 'var(--color-success-light)', color: 'var(--color-success-text)' }}
              >
                {prediction.homeScore} – {prediction.awayScore}
              </span>
            ) : (
              <span className="text-sm" style={{ color: 'var(--color-text-muted)' }}>
                {undetermined ? 'Venter på at lagene avgjøres' : locked ? 'Tipping stengt' : 'Du har ikke bettet ennå'}
              </span>
            )}
            {pts !== undefined && (
              <span
                className="rounded-md px-2 py-1 text-xs font-bold"
                style={{
                  backgroundColor: pts.points === 4 ? '#fef9c3' : pts.points >= 2 ? 'var(--color-success-light)' : pts.points === 1 ? '#fff7ed' : '#fee2e2',
                  color: pts.points === 4 ? '#854d0e' : pts.points >= 2 ? 'var(--color-success-text)' : pts.points === 1 ? '#9a3412' : '#991b1b',
                }}
              >
                {pts.points}p
              </span>
            )}
            {canBet && (
              <button
                onClick={() => setEditing(true)}
                className="rounded-lg px-3 py-1.5 text-sm font-medium text-white"
                style={{ backgroundColor: 'var(--color-primary)' }}
              >
                {prediction ? 'Endre' : 'Bet nå'}
              </button>
            )}
          </div>
        </section>

        {/* Lag-statistikk / form / H2H */}
        {undetermined ? (
          <section
            className="mt-4 rounded-xl border p-4 text-center text-sm"
            style={{ borderColor: 'var(--color-border)', color: 'var(--color-text-muted)', backgroundColor: 'var(--color-surface-card)' }}
          >
            Lagene bestemmes etter gruppespillet. Form, head-to-head og sammenlignende statistikk vises så snart begge lag er klare.
          </section>
        ) : (
          <>
            <section className="mt-4">
              <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
                Form siste 5 kamper
              </h2>
              <div className="mb-2">
                <FormLegend />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <TeamFormStrip stats={homeStats} team={homeTeam} fallbackName={homeDisplay} teams={teams} />
                <TeamFormStrip stats={awayStats} team={awayTeam} fallbackName={awayDisplay} teams={teams} />
              </div>
            </section>

            <section className="mt-4">
              <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
                Sammenligning
              </h2>
              {statsLoading ? (
                <p className="text-center text-xs" style={{ color: 'var(--color-text-muted)' }}>Laster statistikk…</p>
              ) : (
                <TeamStatsCompare
                  homeStats={homeStats}
                  awayStats={awayStats}
                  homeName={homeDisplay}
                  awayName={awayDisplay}
                />
              )}
            </section>

            {homeCode && awayCode && (
              <section className="mt-4">
                <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
                  Innbyrdes oppgjør
                </h2>
                <HeadToHeadSection
                  data={headToHead}
                  homeCode={homeCode}
                  awayCode={awayCode}
                  teams={teams}
                />
              </section>
            )}
          </>
        )}

        {/* Andres tips */}
        <section
          className="mt-4 rounded-xl border p-4 shadow-sm"
          style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
        >
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide" style={{ color: 'var(--color-text-muted)' }}>
            Andres bets i ligaen
          </h2>
          <MatchPredictionsList matchId={match.id} variant="page" />
        </section>
      </main>

      <BottomNav />

      {editing && (
        <PredictionModal
          match={match}
          teams={teams}
          onClose={() => setEditing(false)}
        />
      )}
    </div>
  )
}
