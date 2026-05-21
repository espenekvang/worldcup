import { useEffect, useMemo } from 'react'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import rulesMarkdown from '../data/regler.md?raw'

interface RulesModalProps {
  onClose: () => void
}

function renderRules(): string {
  const raw = marked.parse(rulesMarkdown, { async: false }) as string
  return DOMPurify.sanitize(raw, {
    ALLOWED_TAGS: [
      'h1', 'h2', 'h3', 'h4', 'p', 'br', 'strong', 'b', 'em', 'i',
      'ul', 'ol', 'li', 'blockquote', 'code', 'pre', 'a', 'span', 'hr',
    ],
    ALLOWED_ATTR: ['href', 'title', 'target', 'rel'],
  })
}

export default function RulesModal({ onClose }: RulesModalProps) {
  const html = useMemo(() => renderRules(), [])

  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKey)
    return () => document.removeEventListener('keydown', handleKey)
  }, [onClose])

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4"
      style={{ backgroundColor: 'var(--color-surface-overlay)' }}
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="rules-modal-title"
    >
      <div
        className="max-h-[85vh] w-full max-w-md overflow-y-auto rounded-t-xl p-5 shadow-xl sm:rounded-xl sm:p-6"
        style={{ backgroundColor: 'var(--color-surface-card)' }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2
            id="rules-modal-title"
            className="text-lg font-semibold"
            style={{ color: 'var(--color-text-primary)' }}
          >
            Regler & poeng
          </h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 transition-colors"
            style={{ color: 'var(--color-text-muted)' }}
            aria-label="Lukk"
          >
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div
          className="rules-content"
          style={{ color: 'var(--color-text-secondary)' }}
          dangerouslySetInnerHTML={{ __html: html }}
        />

        <div className="mt-6">
          <button
            onClick={onClose}
            className="w-full rounded-lg border px-4 py-3 text-sm font-medium transition-colors sm:py-2.5"
            style={{
              borderColor: 'var(--color-border)',
              color: 'var(--color-text-secondary)',
              backgroundColor: 'var(--color-surface-card)',
            }}
          >
            Lukk
          </button>
        </div>
      </div>
    </div>
  )
}
