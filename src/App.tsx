import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import type { Stage, Section, Match } from './types'
import { teams, venues } from './data'
import Header from './components/Header'
import Countdown from './components/Countdown'
import TabNav from './components/TabNav'
import MobileStageNav from './components/MobileStageNav'
import RoundPills from './components/RoundPills'
import BottomNav from './components/BottomNav'
import MatchList from './components/MatchList'
import Leaderboard from './components/Leaderboard'
import PredictionModal from './components/PredictionModal'
import OtherPredictionsModal from './components/OtherPredictionsModal'
import AdminPanel from './components/AdminPanel'
import ChatPanel from './components/ChatPanel'
import RulesModal from './components/RulesModal'
import { useAuth } from './context/AuthContext'
import { useMatches } from './context/MatchesContext'
import { useResults } from './context/ResultsContext'
import { isMatchLocked, GROUP_ROUNDS, getNextBettingDeadline, getSectionForStage, getDefaultStage } from './utils/dateUtils'

type MatchesSection = Exclude<Section, 'leaderboard' | 'admin'>

/**
 * Husker hvilken seksjon/runde brukeren sist så på, slik at vi kan gjenopprette
 * den når <App/> remountes innenfor samme økt – f.eks. når man har bettet på en
 * kamp via detaljsiden (/match/:id) og trykker «Tilbake». Uten dette ville
 * visningen alltid hoppe tilbake til auto-valgt standardrunde.
 *
 * Vi holder dette i en modul-variabel (in-memory), IKKE i sessionStorage.
 * sessionStorage overlever en full sideoppdatering, noe som gjorde at en
 * tidligere valgt runde «klistret seg fast» og overstyrte standardvalget ved
 * refresh (man ble f.eks. værende på runde 1 selv om runde 1 var ferdigspilt).
 * En modul-variabel nullstilles ved full reload, slik at en refresh alltid
 * faller tilbake til pågår-/neste-kamp-runden, men består gjennom SPA-
 * navigasjon (App unmountes/remountes uten at modulen lastes på nytt).
 */
let inMemoryViewState: PersistedViewState | null = null

interface PersistedViewState {
  section: Section
  stage: Stage
  lastGroupStage: Stage
  lastKnockoutStage: Stage
  lastMatchesSection: MatchesSection
}

function loadViewState(): PersistedViewState | null {
  return inMemoryViewState
}

function saveViewState(state: PersistedViewState): void {
  inMemoryViewState = state
}

/**
 * Velg fornuftig default-runde når brukeren bytter til en seksjon.
 * For gruppespill: første runde som ikke er låst, ellers siste runde.
 * For sluttspill: alltid 32-delsfinale.
 */
function defaultStageFor(section: MatchesSection, matches: Match[]): Stage {
  if (section === 'knockout') return 'round-of-32'
  const firstOpen = GROUP_ROUNDS.find(s => matches.filter(m => m.stage === s).some(m => !isMatchLocked(m)))
  return firstOpen ?? 'group-3'
}

/**
 * Neste runde brukeren skal bette på (første runde med kommende frist).
 * Returnerer null hvis alle runder er låst.
 */
function nextBettingStage(matches: Match[]): { section: MatchesSection; stage: Stage } | null {
  const next = getNextBettingDeadline(matches)
  if (!next) return null
  const section = getSectionForStage(next.stage)
  if (section === 'leaderboard' || section === 'admin') return null
  return { section, stage: next.stage }
}

/**
 * Runden appen skal åpne som standard: den med en pågående kamp, ellers den
 * med neste kommende kamp. Returnerer null hvis ingen kamper gjenstår eller
 * runden ikke hører hjemme i kamp-seksjonene (gruppe/sluttspill).
 */
function defaultMatchesStage(
  matches: Match[],
  hasResult: (matchId: number) => boolean,
): { section: MatchesSection; stage: Stage } | null {
  const stage = getDefaultStage(matches, hasResult)
  if (!stage) return null
  const section = getSectionForStage(stage)
  if (section === 'leaderboard' || section === 'admin') return null
  return { section, stage }
}

function AppContent() {
  const { user } = useAuth()
  const { matches } = useMatches()
  const { results, isLoading: resultsLoading } = useResults()
  const location = useLocation()
  const navigate = useNavigate()
  // Gjenopprett tidligere visning hvis den finnes (f.eks. etter retur fra
  // kampdetaljer). Ellers faller vi tilbake til auto-valgt neste betting-runde.
  const persistedView = loadViewState()
  const initialBetting = nextBettingStage(matches)
  const [activeSection, setActiveSection] = useState<Section>(
    persistedView?.section ?? initialBetting?.section ?? 'group',
  )
  const [activeStage, setActiveStage] = useState<Stage>(
    persistedView?.stage ?? initialBetting?.stage ?? defaultStageFor('group', matches),
  )
  // Husk siste valgte stage per seksjon, slik at bytting frem/tilbake bevarer kontekst.
  const [lastGroupStage, setLastGroupStage] = useState<Stage>(
    persistedView?.lastGroupStage ?? (initialBetting?.section === 'group' ? initialBetting.stage : 'group-1'),
  )
  const [lastKnockoutStage, setLastKnockoutStage] = useState<Stage>(
    persistedView?.lastKnockoutStage ?? (initialBetting?.section === 'knockout' ? initialBetting.stage : 'round-of-32'),
  )
  const [lastMatchesSection, setLastMatchesSection] = useState<MatchesSection>(
    persistedView?.lastMatchesSection ?? initialBetting?.section ?? 'group',
  )
  const [bettingMatch, setBettingMatch] = useState<Match | null>(null)
  const [viewingOthersMatch, setViewingOthersMatch] = useState<Match | null>(null)
  const [showRules, setShowRules] = useState(false)
  const [isDesktop, setIsDesktop] = useState(() => window.matchMedia('(min-width: 1024px)').matches)
  useEffect(() => {
    const mq = window.matchMedia('(min-width: 1024px)')
    const handler = (e: MediaQueryListEvent) => setIsDesktop(e.matches)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  const canAccessAdmin = user?.isAdmin || (user?.groupAdminGroupIds?.length ?? 0) > 0

  // Velg standardrunde: den med en pågående kamp («pågår»), ellers den med
  // neste kommende kamp. Vi venter til resultatene er ferdig lastet, ellers
  // ville en ferdigspilt (men ennå ikke registrert) kamp se ut som «pågår».
  // Skjer kun én gang, slik at vi ikke overstyrer brukerens egne valg senere.
  // Har vi en lagret visning (retur fra kampdetaljer) hopper vi over auto-valg.
  const [hasAutoSelected, setHasAutoSelected] = useState(persistedView !== null)
  const resultsFetchStarted = useRef(false)
  useEffect(() => {
    if (resultsLoading) resultsFetchStarted.current = true
  }, [resultsLoading])
  useEffect(() => {
    if (hasAutoSelected || matches.length === 0) return
    // Vent på at resultat-henting har startet og fullført, slik at «pågår»-
    // deteksjonen baserer seg på faktiske resultater og ikke en tom cache.
    if (!resultsFetchStarted.current || resultsLoading) return
    const next = defaultMatchesStage(matches, id => results.has(id))
    if (next) {
      setActiveSection(next.section)
      setActiveStage(next.stage)
      if (next.section === 'group') setLastGroupStage(next.stage)
      if (next.section === 'knockout') setLastKnockoutStage(next.stage)
      setLastMatchesSection(next.section)
    }
    setHasAutoSelected(true)
  }, [matches, results, resultsLoading, hasAutoSelected])

  // Husk siste seksjon (utenom leaderboard/admin) og siste stage per seksjon.
  useEffect(() => {
    if (activeSection !== 'leaderboard' && activeSection !== 'admin') {
      setLastMatchesSection(activeSection)
    }
    if (activeSection === 'group') setLastGroupStage(activeStage)
    if (activeSection === 'knockout') setLastKnockoutStage(activeStage)
  }, [activeSection, activeStage])

  // Lagre gjeldende visning slik at den overlever en remount av <App/>
  // (typisk navigasjon til kampdetaljer og tilbake).
  useEffect(() => {
    saveViewState({
      section: activeSection,
      stage: activeStage,
      lastGroupStage,
      lastKnockoutStage,
      lastMatchesSection,
    })
  }, [activeSection, activeStage, lastGroupStage, lastKnockoutStage, lastMatchesSection])

  // Reager på navigasjons-state (f.eks. fra BottomNav på ChatPage).
  useEffect(() => {
    const view = (location.state as { mobileView?: 'matches' | 'leaderboard' | 'admin' } | null)?.mobileView
    if (view === 'leaderboard') {
      setActiveSection('leaderboard')
      navigate(location.pathname, { replace: true, state: null })
    } else if (view === 'admin') {
      setActiveSection('admin')
      navigate(location.pathname, { replace: true, state: null })
    } else if (view === 'matches') {
      setActiveSection(lastMatchesSection)
      navigate(location.pathname, { replace: true, state: null })
    }
    // Vi vil kun reagere når location.state endres.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.state])

  function handleSectionChange(section: Section) {
    setActiveSection(section)
    if (section === 'group') {
      setActiveStage(lastGroupStage)
    } else if (section === 'knockout') {
      setActiveStage(lastKnockoutStage)
    }
  }

  function handleMobileViewChange(view: 'matches' | 'leaderboard') {
    if (view === 'leaderboard') {
      setActiveSection('leaderboard')
    } else {
      handleSectionChange(lastMatchesSection)
    }
  }

  const mobileView: 'matches' | 'leaderboard' =
    activeSection === 'leaderboard' ? 'leaderboard' : 'matches'

  return (
    <div
      className="min-h-screen lg:flex lg:h-screen lg:min-h-0 lg:flex-col"
      style={{ backgroundColor: 'var(--color-surface)' }}
    >
      <Header />
      <main className="pb-20 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:pb-0">
        <div className="mx-auto w-full max-w-6xl p-4 sm:p-6 lg:flex lg:min-h-0 lg:flex-1 lg:gap-6 lg:p-8">
          <div className="min-w-0 lg:flex-1 lg:overflow-y-auto lg:pr-1 lg:themed-scrollbar">
            <Countdown matches={matches} teams={teams} venues={venues} onShowRules={() => setShowRules(true)} />
            <TabNav activeSection={activeSection} onSectionChange={handleSectionChange} />
            {activeSection === 'admin' && canAccessAdmin ? (
              <AdminPanel />
            ) : activeSection === 'leaderboard' ? (
              <Leaderboard />
            ) : (
              <>
                <MobileStageNav
                  activeSection={activeSection as 'group' | 'knockout'}
                  onSectionChange={handleSectionChange}
                />
                <RoundPills
                  section={activeSection as 'group' | 'knockout'}
                  activeStage={activeStage}
                  onStageChange={setActiveStage}
                  matches={matches}
                />
                <MatchList
                  matches={matches}
                  teams={teams}
                  venues={venues}
                  activeStage={activeStage}
                  onTipClick={setBettingMatch}
                  onViewOthers={setViewingOthersMatch}
                />
              </>
            )}
          </div>
          <aside className="hidden lg:block lg:w-[360px] lg:shrink-0">
            <ChatPanel visible={isDesktop} className="h-full" />
          </aside>
        </div>
      </main>

      <BottomNav
        mobileView={mobileView}
        onSelectView={handleMobileViewChange}
      />

      {bettingMatch ? (
        <PredictionModal
          match={bettingMatch}
          teams={teams}
          onClose={() => setBettingMatch(null)}
        />
      ) : null}

      {viewingOthersMatch ? (
        <OtherPredictionsModal
          match={viewingOthersMatch}
          teams={teams}
          onClose={() => setViewingOthersMatch(null)}
        />
      ) : null}

      {showRules ? <RulesModal onClose={() => setShowRules(false)} /> : null}
    </div>
  )
}

export default function App() {
  return <AppContent />
}
