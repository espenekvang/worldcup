import { useLocation, useNavigate } from 'react-router-dom'
import { useChat } from '../context/ChatContext'

type MobileView = 'matches' | 'leaderboard'

interface BottomNavProps {
  /** Currently active mobile view, when on the home (`/`) route. */
  mobileView?: MobileView
  /** Called when user selects Kamper or The Boss from the home route. */
  onSelectView?: (view: MobileView) => void
}

/**
 * Fast bunnmeny synlig kun på mobil (<lg). Tre hovedvalg: Kamper, The Boss, Chat.
 * Chat-knappen viser ulest-badge.
 */
export default function BottomNav({ mobileView, onSelectView }: BottomNavProps) {
  const navigate = useNavigate()
  const location = useLocation()
  const { unreadCount } = useChat()

  const onChat = location.pathname === '/chat'
  const onHome = location.pathname === '/'
  const matchesActive = onHome && mobileView === 'matches'
  const bossActive = onHome && mobileView === 'leaderboard'

  function handleHome(view: MobileView) {
    if (onHome) {
      onSelectView?.(view)
    } else {
      navigate('/', { state: { mobileView: view } })
    }
  }

  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-40 flex border-t lg:hidden"
      style={{
        backgroundColor: 'var(--color-surface-card)',
        borderColor: 'var(--color-border)',
        paddingBottom: 'env(safe-area-inset-bottom)',
      }}
      aria-label="Hovedmeny"
    >
      <NavButton
        label="Kamper"
        icon="⚽"
        active={matchesActive}
        onClick={() => handleHome('matches')}
      />
      <NavButton
        label="The Boss"
        icon="🏆"
        active={bossActive}
        onClick={() => handleHome('leaderboard')}
      />
      <NavButton
        label="Chat"
        icon="💬"
        active={onChat}
        badge={unreadCount}
        onClick={() => navigate('/chat')}
      />
    </nav>
  )
}

interface NavButtonProps {
  label: string
  icon: string
  active: boolean
  badge?: number
  onClick: () => void
}

function NavButton({ label, icon, active, badge, onClick }: NavButtonProps) {
  return (
    <button
      onClick={onClick}
      className="relative flex flex-1 flex-col items-center justify-center gap-0.5 py-2 text-[11px] font-medium transition-colors"
      style={{
        color: active ? 'var(--color-tab-active)' : 'var(--color-tab-inactive)',
      }}
      aria-current={active ? 'page' : undefined}
    >
      <span className="relative text-xl leading-none">
        {icon}
        {badge && badge > 0 ? (
          <span
            className="absolute -right-2 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-semibold"
            style={{ backgroundColor: 'var(--color-danger)', color: '#fff' }}
            aria-label={`${badge} uleste meldinger`}
          >
            {badge > 99 ? '99+' : badge}
          </span>
        ) : null}
      </span>
      <span>{label}</span>
    </button>
  )
}
