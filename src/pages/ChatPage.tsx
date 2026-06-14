import Header from '../components/Header'
import ChatPanel from '../components/ChatPanel'
import BottomNav from '../components/BottomNav'

export default function ChatPage() {
  return (
    <div
      className="flex h-dvh flex-col overflow-hidden"
      style={{ backgroundColor: 'var(--color-surface)' }}
    >
      <Header />
      <main className="mx-auto flex w-full min-h-0 max-w-6xl flex-1 flex-col p-4 pb-20 sm:p-6 lg:p-8 lg:pb-8">
        <div className="min-h-0 flex-1">
          <ChatPanel visible className="h-full" />
        </div>
      </main>
      <BottomNav />
    </div>
  )
}
