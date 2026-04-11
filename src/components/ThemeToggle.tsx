import { useEffect, useState } from 'react'

type Theme = 'light' | 'dark'

function getStoredTheme(): Theme {
  try {
    const stored = localStorage.getItem('theme')
    if (stored === 'dark' || stored === 'light') return stored
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) return 'dark'
  } catch { /* noop */ }
  return 'light'
}

export default function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(getStoredTheme)

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark')
    try { localStorage.setItem('theme', theme) } catch { /* noop */ }
  }, [theme])

  function toggle() {
    setTheme(prev => (prev === 'dark' ? 'light' : 'dark'))
  }

  return (
    <button
      onClick={toggle}
      className="rounded-md px-2 py-1 text-xs font-medium transition-colors sm:px-3 sm:py-1.5"
      style={{ backgroundColor: 'var(--color-header-btn)', color: 'var(--color-text-inverse)' }}
      aria-label={theme === 'dark' ? 'Bytt til lyst tema' : 'Bytt til mørkt tema'}
    >
      {theme === 'dark' ? '☀️' : '🌙'}
    </button>
  )
}
