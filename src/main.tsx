import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { AuthProvider, useAuth } from './context/AuthContext'
import { BettingGroupProvider, useBettingGroup } from './context/BettingGroupContext'
import { FeatureFlagsProvider } from './context/FeatureFlagsContext'
import { PredictionsProvider } from './context/PredictionsContext'
import { MatchesProvider } from './context/MatchesContext'
import { ResultsProvider } from './context/ResultsContext'
import { ChatProvider } from './context/ChatContext'
import './index.css'
import App from './App'
import LoginPage from './pages/LoginPage'
import WaitingPage from './pages/WaitingPage'
import GroupSelectorPage from './pages/GroupSelectorPage'
import ChatPage from './pages/ChatPage'
import InvitePage from './pages/InvitePage'
import MatchDetailsPage from './pages/MatchDetailsPage'

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID ?? ''

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  const { groups, activeGroup } = useBettingGroup()

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (groups.length === 0) {
    return <WaitingPage />
  }

  if (!activeGroup) {
    return <GroupSelectorPage />
  }

  return <ChatProvider><FeatureFlagsProvider>{children}</FeatureFlagsProvider></ChatProvider>
}

/**
 * Felles dataprovider-stabel for alle sider som trenger kamper, tips og resultater
 * (kamplisten på "/" og detaljsiden på "/match/:id"). Tidligere lå disse rundt
 * <App/>, men når detaljsiden også trenger dem er det enklere å løfte dem hit.
 */
function MatchDataProviders({ children }: { children: React.ReactNode }) {
  return (
    <MatchesProvider>
      <PredictionsProvider>
        <ResultsProvider>{children}</ResultsProvider>
      </PredictionsProvider>
    </MatchesProvider>
  )
}

function AppRoutes() {
  const { user } = useAuth()

  return (
    <Routes>
      <Route
        path="/login"
        element={user ? <Navigate to="/" replace /> : <LoginPage />}
      />
      <Route path="/invite/:token" element={<InvitePage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <MatchDataProviders>
              <App />
            </MatchDataProviders>
          </ProtectedRoute>
        }
      />
      <Route
        path="/match/:matchId"
        element={
          <ProtectedRoute>
            <MatchDataProviders>
              <MatchDetailsPage />
            </MatchDataProviders>
          </ProtectedRoute>
        }
      />
      <Route
        path="/chat"
        element={
          <ProtectedRoute>
            <ChatPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <BrowserRouter>
        <BettingGroupProvider>
          <AuthProvider>
            <AppRoutes />
          </AuthProvider>
        </BettingGroupProvider>
      </BrowserRouter>
    </GoogleOAuthProvider>
  </StrictMode>,
)
