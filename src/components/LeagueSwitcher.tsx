import { useState, useRef, useEffect } from 'react'
import { useBettingGroup } from '../context/BettingGroupContext'

/**
 * Liga-velger i headeren (venstre side). Viser navnet på aktiv liga som ren
 * tekst når brukeren bare er medlem i én liga, og som en dropdown når man er
 * medlem i flere – slik at bytte skjer med ett klikk uten å forlate siden.
 * Samme komponent rendres på alle skjermbredder (ingen egen mobil-header).
 */
export default function LeagueSwitcher() {
  const { groups, activeGroup, setActiveGroup } = useBettingGroup()
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClickOutside)
      return () => document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [open])

  const nameClass = 'mt-0.5 text-xs font-thin sm:mt-1 sm:text-sm'

  // Bare én (eller ingen) liga → ren tekst, akkurat som før.
  if (groups.length <= 1) {
    return (
      <p className={nameClass} style={{ color: 'var(--color-header-text-muted)' }}>
        {activeGroup ? activeGroup.name : 'USA • Mexico • Canada'}
      </p>
    )
  }

  return (
    <div className="relative" ref={menuRef}>
      <button
        onClick={() => setOpen(prev => !prev)}
        className={`flex max-w-full items-center gap-1 ${nameClass} transition-opacity hover:opacity-80`}
        style={{ color: 'var(--color-header-text-muted)' }}
        aria-label="Bytt liga"
        aria-expanded={open}
      >
        <span className="min-w-0 truncate">{activeGroup?.name}</span>
        <span className="shrink-0 text-[10px]" aria-hidden="true">▾</span>
      </button>

      {open && (
        <div
          className="absolute left-0 z-50 mt-2 max-h-[70vh] w-56 overflow-y-auto overflow-x-hidden rounded-lg border shadow-lg"
          style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
        >
          <div className="py-1">
            {groups.map(group => {
              const isActive = group.id === activeGroup?.id
              return (
                <button
                  key={group.id}
                  onClick={() => {
                    setOpen(false)
                    if (!isActive) setActiveGroup(group)
                  }}
                  className="flex w-full items-center gap-3 px-4 py-2.5 text-left transition-colors hover:opacity-80"
                  style={{ color: 'var(--color-text-primary)' }}
                >
                  <span className="w-4 shrink-0 text-center text-sm">{isActive ? '✓' : ''}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{group.name}</span>
                    <span className="block text-xs" style={{ color: 'var(--color-text-muted)' }}>
                      {group.memberCount} {group.memberCount === 1 ? 'medlem' : 'medlemmer'}
                    </span>
                  </span>
                </button>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}
