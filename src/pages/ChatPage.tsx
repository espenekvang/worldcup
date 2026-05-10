import { useNavigate } from 'react-router-dom'
import Header from '../components/Header'
import ChatPanel from '../components/ChatPanel'
import BottomNav from '../components/BottomNav'
import { useAuth } from '../context/AuthContext'

export default function ChatPage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const canAccessAdmin = user?.isAdmin || (user?.groupAdminGroupIds?.length ?? 0) > 0

  return (
    <div
      className="flex min-h-screen flex-col"
      style={{ backgroundColor: 'var(--color-surface)' }}
    >
      <Header onAdminClick={canAccessAdmin ? () => navigate('/?admin=1') : undefined} />
      <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col p-4 pb-20 sm:p-6 lg:p-8 lg:pb-8">
        <div className="flex-1 min-h-[60vh]">
          <ChatPanel visible className="h-full" />
        </div>
      </main>
      <BottomNav />
    </div>
  )
}
