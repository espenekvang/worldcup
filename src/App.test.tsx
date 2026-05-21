import { render, screen } from '@testing-library/react'
import App from './App'
import { AuthProvider } from './context/AuthContext'
import { PredictionsProvider } from './context/PredictionsContext'
import { BettingGroupProvider } from './context/BettingGroupContext'
import { MatchesProvider } from './context/MatchesContext'
import { ResultsProvider } from './context/ResultsContext'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { MemoryRouter } from 'react-router-dom'

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <GoogleOAuthProvider clientId="test">
      <MemoryRouter>
        <BettingGroupProvider>
          <AuthProvider>
            <MatchesProvider>
              <PredictionsProvider>
                <ResultsProvider>
                  {children}
                </ResultsProvider>
              </PredictionsProvider>
            </MatchesProvider>
          </AuthProvider>
        </BettingGroupProvider>
      </MemoryRouter>
    </GoogleOAuthProvider>
  )
}

describe('App', () => {
  it('renders the title', () => {
    render(<App />, { wrapper: Wrapper })

    expect(screen.getByText(/VM-Betting 2026/)).toBeInTheDocument()
  })
})
