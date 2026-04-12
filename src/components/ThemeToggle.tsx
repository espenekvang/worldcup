import { useTheme } from '../hooks/useTheme'

export default function ThemeToggle() {
  const { theme, toggle } = useTheme()

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
