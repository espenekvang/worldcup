import { render, screen } from '@testing-library/react'
import App from './App'
import { AuthProvider } from './context/AuthContext'
import { PredictionsProvider } from './context/PredictionsContext'
import { BettingGroupProvider } from './context/BettingGroupContext'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { MemoryRouter } from 'react-router-dom'

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <GoogleOAuthProvider clientId="test">
      <MemoryRouter>
        <BettingGroupProvider>
          <AuthProvider>
            <PredictionsProvider>
              {children}
            </PredictionsProvider>
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
