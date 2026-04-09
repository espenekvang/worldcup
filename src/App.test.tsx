import { render, screen } from '@testing-library/react'
import App from './App'
import { AuthProvider } from './context/AuthContext'
import { PredictionsProvider } from './context/PredictionsContext'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { MemoryRouter } from 'react-router-dom'

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <GoogleOAuthProvider clientId="test">
      <MemoryRouter>
        <AuthProvider>
          <PredictionsProvider>
            {children}
          </PredictionsProvider>
        </AuthProvider>
      </MemoryRouter>
    </GoogleOAuthProvider>
  )
}

describe('App', () => {
  it('renders the title', () => {
    render(<App />, { wrapper: Wrapper })

    expect(screen.getByText(/FIFA World Cup 2026/)).toBeInTheDocument()
  })
})
