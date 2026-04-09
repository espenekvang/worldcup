import { useState } from 'react'
import type { Stage } from './types'
import { matches, teams, venues } from './data'
import Header from './components/Header'
import Countdown from './components/Countdown'
import TabNav from './components/TabNav'
import MatchList from './components/MatchList'

function App() {
  const [activeTab, setActiveTab] = useState<Stage>('group')

  return (
    <div className="min-h-screen bg-slate-50">
      <Header />
      <main className="mx-auto max-w-6xl p-4 sm:p-6 lg:p-8">
        <Countdown matches={matches} teams={teams} venues={venues} />
        <TabNav activeTab={activeTab} onTabChange={setActiveTab} />
        <MatchList matches={matches} teams={teams} venues={venues} activeStage={activeTab} />
      </main>
    </div>
  )
}

export default App
