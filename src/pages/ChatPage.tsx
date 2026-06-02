import Header from '../components/Header'
import ChatPanel from '../components/ChatPanel'
import BottomNav from '../components/BottomNav'

export default function ChatPage() {
  return (
    <div
      className="flex min-h-screen flex-col"
      style={{ backgroundColor: 'var(--color-surface)' }}
    >
      <Header />
      <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col p-4 pb-20 sm:p-6 lg:p-8 lg:pb-8">
        <div className="flex-1 min-h-[60vh]">
          <ChatPanel visible className="h-full" />
        </div>
      </main>
      <BottomNav />
    </div>
  )
}
