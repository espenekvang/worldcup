import { useState } from 'react'
import type { Stage, Match } from './types'
import { matches, teams, venues } from './data'
import Header from './components/Header'
import Countdown from './components/Countdown'
import TabNav from './components/TabNav'
import MatchList from './components/MatchList'
import PredictionModal from './components/PredictionModal'
import OtherPredictionsModal from './components/OtherPredictionsModal'
import AdminPanel from './components/AdminPanel'
import { useAuth } from './context/AuthContext'

export default function App() {
  const { user } = useAuth()
  const [activeTab, setActiveTab] = useState<Stage>('group')
  const [bettingMatch, setBettingMatch] = useState<Match | null>(null)
  const [viewingOthersMatch, setViewingOthersMatch] = useState<Match | null>(null)
  const [showAdmin, setShowAdmin] = useState(false)

  return (
    <div className="min-h-screen bg-slate-50">
      <Header onAdminClick={user?.isAdmin ? () => setShowAdmin((v) => !v) : undefined} />
      <main className="mx-auto max-w-6xl p-4 sm:p-6 lg:p-8">
        {showAdmin && user?.isAdmin ? (
          <div className="mb-6">
            <AdminPanel />
          </div>
        ) : null}
        <Countdown matches={matches} teams={teams} venues={venues} />
        <TabNav activeTab={activeTab} onTabChange={setActiveTab} />
        <MatchList
          matches={matches}
          teams={teams}
          venues={venues}
          activeStage={activeTab}
          onTipClick={setBettingMatch}
          onViewOthers={setViewingOthersMatch}
        />
      </main>

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
