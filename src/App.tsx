import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import type { Stage, Match } from './types'
import { teams, venues } from './data'
import Header from './components/Header'
import Countdown from './components/Countdown'
import TabNav from './components/TabNav'
import MobileStageNav from './components/MobileStageNav'
import BottomNav from './components/BottomNav'
import MatchList from './components/MatchList'
import Leaderboard from './components/Leaderboard'
import PredictionModal from './components/PredictionModal'
import OtherPredictionsModal from './components/OtherPredictionsModal'
import AdminPanel from './components/AdminPanel'
import ChatPanel from './components/ChatPanel'
import { useAuth } from './context/AuthContext'
import { ResultsProvider } from './context/ResultsContext'
import { PredictionsProvider } from './context/PredictionsContext'
import { MatchesProvider, useMatches } from './context/MatchesContext'

type StageOnly = Exclude<Stage, 'leaderboard'>

function AppContent() {
  const { user } = useAuth()
  const { matches } = useMatches()
  const location = useLocation()
  const navigate = useNavigate()
  const [activeTab, setActiveTab] = useState<Stage>('group')
  const [lastStage, setLastStage] = useState<StageOnly>('group')
  const [bettingMatch, setBettingMatch] = useState<Match | null>(null)
  const [viewingOthersMatch, setViewingOthersMatch] = useState<Match | null>(null)
  const [showAdmin, setShowAdmin] = useState(false)

  const canAccessAdmin = user?.isAdmin || (user?.groupAdminGroupIds?.length ?? 0) > 0

  // Husk siste valgte stage så vi kan returnere dit fra The Boss/Chat.
  useEffect(() => {
    if (activeTab !== 'leaderboard') {
      setLastStage(activeTab as StageOnly)
    }
  }, [activeTab])

  // Reager på navigasjons-state (f.eks. fra BottomNav på ChatPage).
  useEffect(() => {
    const view = (location.state as { mobileView?: 'matches' | 'leaderboard' } | null)?.mobileView
    if (view === 'leaderboard') {
      setActiveTab('leaderboard')
      navigate(location.pathname, { replace: true, state: null })
    } else if (view === 'matches') {
      setActiveTab(lastStage)
      navigate(location.pathname, { replace: true, state: null })
    }
    // Vi vil kun reagere når location.state endres.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.state])

  function handleMobileViewChange(view: 'matches' | 'leaderboard') {
    if (view === 'leaderboard') {
      setActiveTab('leaderboard')
    } else {
      setActiveTab(lastStage)
    }
  }

  const mobileView: 'matches' | 'leaderboard' =
    activeTab === 'leaderboard' ? 'leaderboard' : 'matches'

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
            <Countdown matches={matches} teams={teams} venues={venues} />
            <TabNav activeTab={activeTab} onTabChange={setActiveTab} />
            {activeTab !== 'leaderboard' ? (
              <MobileStageNav
                activeStage={activeTab as StageOnly}
                onStageChange={(s) => setActiveTab(s)}
              />
            ) : null}
            {activeTab === 'leaderboard' ? (
              <Leaderboard />
            ) : (
              <MatchList
                matches={matches}
                teams={teams}
                venues={venues}
                activeStage={activeTab}
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
    </div>
  )
}

export default function App() {
  return (
    <MatchesProvider>
      <PredictionsProvider>
        <ResultsProvider>
          <AppContent />
        </ResultsProvider>
      </PredictionsProvider>
    </MatchesProvider>
  )
}
