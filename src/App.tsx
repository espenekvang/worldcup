import { useEffect, useState } from 'react'
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
import { isMatchLocked, GROUP_ROUNDS, getNextBettingDeadline, getSectionForStage } from './utils/dateUtils'

type MatchesSection = Exclude<Section, 'leaderboard'>

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
  if (section === 'leaderboard') return null
  return { section, stage: next.stage }
}

function AppContent() {
  const { user } = useAuth()
  const { matches } = useMatches()
  const location = useLocation()
  const navigate = useNavigate()
  const initialBetting = nextBettingStage(matches)
  const [activeSection, setActiveSection] = useState<Section>(initialBetting?.section ?? 'group')
  const [activeStage, setActiveStage] = useState<Stage>(
    initialBetting?.stage ?? defaultStageFor('group', matches),
  )
  // Husk siste valgte stage per seksjon, slik at bytting frem/tilbake bevarer kontekst.
  const [lastGroupStage, setLastGroupStage] = useState<Stage>(
    initialBetting?.section === 'group' ? initialBetting.stage : 'group-1',
  )
  const [lastKnockoutStage, setLastKnockoutStage] = useState<Stage>(
    initialBetting?.section === 'knockout' ? initialBetting.stage : 'round-of-32',
  )
  const [lastMatchesSection, setLastMatchesSection] = useState<MatchesSection>(
    initialBetting?.section ?? 'group',
  )
  const [bettingMatch, setBettingMatch] = useState<Match | null>(null)
  const [viewingOthersMatch, setViewingOthersMatch] = useState<Match | null>(null)
  const [showAdmin, setShowAdmin] = useState(false)
  const [showRules, setShowRules] = useState(false)

  const canAccessAdmin = user?.isAdmin || (user?.groupAdminGroupIds?.length ?? 0) > 0

  // Når matches lastes inn (asynkront), hopp til neste runde brukeren kan bette på.
  // Skjer kun ved første lasting, slik at vi ikke overstyrer brukerens egne valg senere.
  const [hasAutoSelected, setHasAutoSelected] = useState(initialBetting !== null)
  useEffect(() => {
    if (hasAutoSelected || matches.length === 0) return
    const next = nextBettingStage(matches)
    if (!next) return
    setActiveSection(next.section)
    setActiveStage(next.stage)
    if (next.section === 'group') setLastGroupStage(next.stage)
    if (next.section === 'knockout') setLastKnockoutStage(next.stage)
    setLastMatchesSection(next.section)
    setHasAutoSelected(true)
  }, [matches, hasAutoSelected])

  // Husk siste seksjon (utenom leaderboard) og siste stage per seksjon.
  useEffect(() => {
    if (activeSection !== 'leaderboard') {
      setLastMatchesSection(activeSection)
    }
    if (activeSection === 'group') setLastGroupStage(activeStage)
    if (activeSection === 'knockout') setLastKnockoutStage(activeStage)
  }, [activeSection, activeStage])

  // Reager på navigasjons-state (f.eks. fra BottomNav på ChatPage).
  useEffect(() => {
    const view = (location.state as { mobileView?: 'matches' | 'leaderboard' } | null)?.mobileView
    if (view === 'leaderboard') {
      setActiveSection('leaderboard')
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
      <Header onAdminClick={canAccessAdmin ? () => setShowAdmin((v) => !v) : undefined} />
      <main className="pb-20 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:pb-0">
        <div className="mx-auto w-full max-w-6xl p-4 sm:p-6 lg:flex lg:min-h-0 lg:flex-1 lg:gap-6 lg:p-8">
          <div className="min-w-0 lg:flex-1 lg:overflow-y-auto lg:pr-1 lg:themed-scrollbar">
            {showAdmin && canAccessAdmin ? (
              <div className="mb-6">
                <AdminPanel />
              </div>
            ) : null}
            <Countdown matches={matches} teams={teams} venues={venues} onShowRules={() => setShowRules(true)} />
            <TabNav activeSection={activeSection} onSectionChange={handleSectionChange} />
            {activeSection !== 'leaderboard' ? (
              <>
                <MobileStageNav
                  activeSection={activeSection}
                  onSectionChange={handleSectionChange}
                />
                <RoundPills
                  section={activeSection}
                  activeStage={activeStage}
                  onStageChange={setActiveStage}
                  matches={matches}
                />
              </>
            ) : null}
            {activeSection === 'leaderboard' ? (
              <Leaderboard />
            ) : (
              <MatchList
                matches={matches}
                teams={teams}
                venues={venues}
                activeStage={activeStage}
                onTipClick={setBettingMatch}
                onViewOthers={setViewingOthersMatch}
              />
            )}
          </div>
          <aside className="hidden lg:block lg:w-[360px] lg:shrink-0">
            <ChatPanel visible className="h-full" />
          </aside>
        </div>
      </main>

      <BottomNav mobileView={mobileView} onSelectView={handleMobileViewChange} />

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
