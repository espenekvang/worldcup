import { useCallback, useEffect, useState } from 'react'
import type { InvitationResponse } from '../api/client'
import {
  getInvitations, createInvitation, deleteInvitation,
  updateMatchTeams, setMatchResult,
  getAllGroups, getMyGroups, createGroup, updateGroup, deleteGroup,
  getGroupMembers, addGroupMember, removeGroupMember, toggleGroupAdmin,
} from '../api/client'
import type { BettingGroup, BettingGroupMember } from '../types'
import { useMatches } from '../context/MatchesContext'
import { useResults } from '../context/ResultsContext'
import { useAuth } from '../context/AuthContext'
import { teams } from '../data'

const stageNames: Record<string, string> = {
  'group': 'Gruppe',
  'round-of-32': 'R32',
  'round-of-16': 'R16',
  'quarter-final': 'QF',
  'semi-final': 'SF',
  'third-place': '3.plass',
  'final': 'Finale',
}

export default function AdminPanel() {
  const { user } = useAuth()
  const isGlobalAdmin = user?.isAdmin ?? false
  const groupAdminGroupIds = user?.groupAdminGroupIds ?? []

  // Group management state
  const [groupList, setGroupList] = useState<BettingGroup[]>([])
  const [newGroupName, setNewGroupName] = useState('')
  const [joinGroup, setJoinGroup] = useState(true)
  const [groupLoading, setGroupLoading] = useState(false)
  const [groupError, setGroupError] = useState<string | null>(null)
  const [expandedGroupId, setExpandedGroupId] = useState<string | null>(null)
  const [groupMembers, setGroupMembers] = useState<BettingGroupMember[]>([])
  const [membersLoading, setMembersLoading] = useState(false)
  const [newMemberEmail, setNewMemberEmail] = useState('')
  const [memberError, setMemberError] = useState<string | null>(null)
  const [editingGroupId, setEditingGroupId] = useState<string | null>(null)
  const [editingGroupName, setEditingGroupName] = useState('')

  // Invitation state
  const [invitations, setInvitations] = useState<InvitationResponse[]>([])
  const [email, setEmail] = useState('')
  const [inviteGroupId, setInviteGroupId] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Match override state
  const { matches } = useMatches()
  const { results, refreshResults } = useResults()
  const [selectedMatchId, setSelectedMatchId] = useState<number | ''>('')
  const [overrideHome, setOverrideHome] = useState<string>('')
  const [overrideAway, setOverrideAway] = useState<string>('')
  const [overrideLoading, setOverrideLoading] = useState(false)
  const [overrideError, setOverrideError] = useState<string | null>(null)
  const [overrideSuccess, setOverrideSuccess] = useState(false)

  // Result override state
  const [resultMatchId, setResultMatchId] = useState<number | ''>('')
  const [resultHome, setResultHome] = useState('')
  const [resultAway, setResultAway] = useState('')
  const [resultLoading, setResultLoading] = useState(false)
  const [resultError, setResultError] = useState<string | null>(null)
  const [resultSuccess, setResultSuccess] = useState(false)

  const knockoutMatches = matches.filter((m) => m.stage !== 'group')
  const sortedTeams = Object.values(teams).sort((a, b) => a.name.localeCompare(b.name))

  // Load groups
  const loadGroups = useCallback(async () => {
    try {
      let data: BettingGroup[]
      if (isGlobalAdmin) {
        data = await getAllGroups()
      } else {
        // Group admins only see groups they admin
        const myGroups = await getMyGroups()
        data = myGroups.filter((g) => groupAdminGroupIds.includes(g.id))
      }
      setGroupList(data)
      if (data.length > 0 && !inviteGroupId) {
        setInviteGroupId(data[0].id)
      }
    } catch {
      setGroupError('Kunne ikke laste ligaer')
    }
  }, [inviteGroupId, isGlobalAdmin, groupAdminGroupIds])

  useEffect(() => {
    loadGroups()
  }, [loadGroups])

  // Load members when expanding a group
  async function loadMembers(groupId: string) {
    setMembersLoading(true)
    setMemberError(null)
    try {
      const data = await getGroupMembers(groupId)
      setGroupMembers(data)
    } catch {
      setMemberError('Kunne ikke laste medlemmer')
    } finally {
      setMembersLoading(false)
    }
  }

  function handleExpandGroup(groupId: string) {
    if (expandedGroupId === groupId) {
      setExpandedGroupId(null)
      setGroupMembers([])
    } else {
      setExpandedGroupId(groupId)
      loadMembers(groupId)
    }
  }

  async function handleCreateGroup(e: React.FormEvent) {
    e.preventDefault()
    if (!newGroupName.trim()) return

    setGroupLoading(true)
    setGroupError(null)
    try {
      await createGroup(newGroupName.trim(), joinGroup)
      setNewGroupName('')
      setJoinGroup(true)
      await loadGroups()
    } catch (err) {
      setGroupError(err instanceof Error ? err.message : 'Kunne ikke opprette liga')
    } finally {
      setGroupLoading(false)
    }
  }

  async function handleDeleteGroup(id: string) {
    if (!confirm('Er du sikker på at du vil slette denne ligaen? Alle prediksjoner i ligaen slettes.')) return

    try {
      await deleteGroup(id)
      if (expandedGroupId === id) {
        setExpandedGroupId(null)
        setGroupMembers([])
      }
      await loadGroups()
    } catch (err) {
      setGroupError(err instanceof Error ? err.message : 'Kunne ikke slette liga')
    }
  }

  async function handleRenameGroup(id: string) {
    if (!editingGroupName.trim()) return
    try {
      await updateGroup(id, editingGroupName.trim())
      setEditingGroupId(null)
      setEditingGroupName('')
      await loadGroups()
    } catch (err) {
      setGroupError(err instanceof Error ? err.message : 'Kunne ikke endre navn')
    }
  }

  async function handleAddMember(e: React.FormEvent) {
    e.preventDefault()
    if (!expandedGroupId || !newMemberEmail.trim()) return

    setMemberError(null)
    try {
      await addGroupMember(expandedGroupId, newMemberEmail.trim())
      setNewMemberEmail('')
      await loadMembers(expandedGroupId)
      await loadGroups()
    } catch (err) {
      if (err instanceof Error && err.message.includes('409')) {
        setMemberError('Brukeren er allerede medlem av denne ligaen.')
      } else if (err instanceof Error && err.message.includes('404')) {
        setMemberError('Bruker ikke funnet. Inviter dem først.')
      } else {
        setMemberError(err instanceof Error ? err.message : 'Kunne ikke legge til medlem')
      }
    }
  }

  async function handleRemoveMember(userId: string) {
    if (!expandedGroupId) return
    try {
      await removeGroupMember(expandedGroupId, userId)
      await loadMembers(expandedGroupId)
      await loadGroups()
    } catch (err) {
      setMemberError(err instanceof Error ? err.message : 'Kunne ikke fjerne medlem')
    }
  }

  async function handleToggleGroupAdmin(userId: string, currentStatus: boolean) {
    if (!expandedGroupId) return
    try {
      await toggleGroupAdmin(expandedGroupId, userId, !currentStatus)
      await loadMembers(expandedGroupId)
    } catch (err) {
      setMemberError(err instanceof Error ? err.message : 'Kunne ikke endre admin-status')
    }
  }

  // Invitations
  const loadInvitations = useCallback(async () => {
    try {
      const data = await getInvitations()
      setInvitations(data)
    } catch {
      setError('Kunne ikke laste invitasjoner')
    }
  }, [])

  useEffect(() => {
    loadInvitations()
  }, [loadInvitations])

  async function handleInvite(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = email.trim()
    if (!trimmed || !inviteGroupId) return

    setIsLoading(true)
    setError(null)

    try {
      await createInvitation(trimmed, inviteGroupId)
      setEmail('')
      await loadInvitations()
    } catch (err) {
      if (err instanceof Error && err.message.includes('409')) {
        setError('Denne e-postadressen er allerede invitert til denne ligaen.')
      } else {
        setError(err instanceof Error ? err.message : 'Kunne ikke sende invitasjon')
      }
    } finally {
      setIsLoading(false)
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteInvitation(id)
      setInvitations((prev) => prev.filter((i) => i.id !== id))
    } catch {
      setError('Kunne ikke fjerne invitasjon')
    }
  }

  async function handleOverrideSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedMatchId) return

    setOverrideLoading(true)
    setOverrideError(null)
    setOverrideSuccess(false)

    try {
      await updateMatchTeams(
        Number(selectedMatchId),
        overrideHome || undefined,
        overrideAway || undefined
      )
      setOverrideSuccess(true)
      setSelectedMatchId('')
      setOverrideHome('')
      setOverrideAway('')
    } catch (err) {
      setOverrideError(err instanceof Error ? err.message : 'Kunne ikke oppdatere kamp')
    } finally {
      setOverrideLoading(false)
    }
  }

  async function handleResultSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!resultMatchId) return

    const homeScore = parseInt(resultHome, 10)
    const awayScore = parseInt(resultAway, 10)

    if (isNaN(homeScore) || isNaN(awayScore) || homeScore < 0 || awayScore < 0) {
      setResultError('Ugyldig resultat')
      return
    }

    setResultLoading(true)
    setResultError(null)
    setResultSuccess(false)

    try {
      await setMatchResult(Number(resultMatchId), homeScore, awayScore)
      await refreshResults()
      setResultSuccess(true)
      setResultMatchId('')
      setResultHome('')
      setResultAway('')
    } catch (err) {
      setResultError(err instanceof Error ? err.message : 'Kunne ikke sette resultat')
    } finally {
      setResultLoading(false)
    }
  }

  return (
    <>
      {/* Group Management */}
      <div
        className="rounded-xl border p-4 sm:p-6"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Administrer ligaer</h2>
        <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>
          {isGlobalAdmin ? 'Opprett ligaer og administrer medlemmer.' : 'Administrer medlemmer i dine ligaer.'}
        </p>

        {isGlobalAdmin && (
          <form onSubmit={handleCreateGroup} className="mt-4 flex flex-col gap-2 sm:flex-row sm:items-center">
          <input
            type="text"
            value={newGroupName}
            onChange={(e) => setNewGroupName(e.target.value)}
            placeholder="Navn på ny liga"
            className="flex-1 rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
            style={{
              backgroundColor: 'var(--color-surface-card)',
              borderColor: 'var(--color-input-border)',
              color: 'var(--color-text-primary)',
            }}
            required
          />
          <label className="flex items-center gap-1.5 text-sm whitespace-nowrap" style={{ color: 'var(--color-text-secondary)' }}>
            <input
              type="checkbox"
              checked={joinGroup}
              onChange={(e) => setJoinGroup(e.target.checked)}
              className="rounded"
            />
            Bli med selv
          </label>
          <button
            type="submit"
            disabled={groupLoading}
            className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
            style={{ backgroundColor: 'var(--color-primary)' }}
          >
            {groupLoading ? 'Oppretter...' : 'Opprett liga'}
          </button>
        </form>
        )}

        {groupError ? (
          <p className="mt-3 text-sm" style={{ color: 'var(--color-danger)' }}>{groupError}</p>
        ) : null}

        {groupList.length > 0 ? (
          <div className="mt-4 space-y-2">
            {groupList.map((group) => (
              <div key={group.id}>
                <div
                  className="flex items-center justify-between rounded-lg border px-4 py-3"
                  style={{ borderColor: 'var(--color-border-light)' }}
                >
                  <button
                    type="button"
                    onClick={() => handleExpandGroup(group.id)}
                    className="flex-1 text-left"
                  >
                    {editingGroupId === group.id ? (
                      <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                        <input
                          type="text"
                          value={editingGroupName}
                          onChange={(e) => setEditingGroupName(e.target.value)}
                          onKeyDown={(e) => { if (e.key === 'Enter') handleRenameGroup(group.id); if (e.key === 'Escape') setEditingGroupId(null) }}
                          className="rounded border px-2 py-1 text-sm"
                          style={{
                            backgroundColor: 'var(--color-surface-card)',
                            borderColor: 'var(--color-input-border)',
                            color: 'var(--color-text-primary)',
                          }}
                          autoFocus
                        />
                        <button
                          onClick={() => handleRenameGroup(group.id)}
                          className="text-xs font-medium"
                          style={{ color: 'var(--color-primary)' }}
                        >
                          Lagre
                        </button>
                        <button
                          onClick={() => setEditingGroupId(null)}
                          className="text-xs"
                          style={{ color: 'var(--color-text-muted)' }}
                        >
                          Avbryt
                        </button>
                      </div>
                    ) : (
                      <>
                        <span className="text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
                          {group.name}
                        </span>
                        <span className="ml-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>
                          {group.memberCount} {group.memberCount === 1 ? 'medlem' : 'medlemmer'}
                        </span>
                      </>
                    )}
                  </button>
                  <div className="flex items-center gap-2">
                    {isGlobalAdmin && (
                      <>
                        <button
                          onClick={(e) => { e.stopPropagation(); setEditingGroupId(group.id); setEditingGroupName(group.name) }}
                          className="text-xs"
                          style={{ color: 'var(--color-text-muted)' }}
                        >
                          Endre
                        </button>
                        <button
                          onClick={(e) => { e.stopPropagation(); handleDeleteGroup(group.id) }}
                          className="text-xs"
                          style={{ color: 'var(--color-danger)' }}
                        >
                          Slett
                        </button>
                      </>
                    )}
                    <span className="text-xs" style={{ color: 'var(--color-text-muted)' }}>
                      {expandedGroupId === group.id ? '▲' : '▼'}
                    </span>
                  </div>
                </div>

                {expandedGroupId === group.id && (
                  <div
                    className="ml-4 mt-1 rounded-lg border p-3"
                    style={{ borderColor: 'var(--color-border-light)', backgroundColor: 'var(--color-surface-elevated)' }}
                  >
                    <form onSubmit={handleAddMember} className="flex flex-col gap-2 sm:flex-row">
                      <input
                        type="email"
                        value={newMemberEmail}
                        onChange={(e) => setNewMemberEmail(e.target.value)}
                        placeholder="Legg til medlem (e-post)"
                        className="flex-1 rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2"
                        style={{
                          backgroundColor: 'var(--color-surface-card)',
                          borderColor: 'var(--color-input-border)',
                          color: 'var(--color-text-primary)',
                        }}
                        required
                      />
                      <button
                        type="submit"
                        className="rounded-lg px-3 py-2 text-sm font-medium text-white"
                        style={{ backgroundColor: 'var(--color-primary)' }}
                      >
                        Legg til
          </button>
        </form>

                    {memberError ? (
                      <p className="mt-2 text-xs" style={{ color: 'var(--color-danger)' }}>{memberError}</p>
                    ) : null}

                    {membersLoading ? (
                      <p className="mt-2 text-xs" style={{ color: 'var(--color-text-muted)' }}>Laster...</p>
                    ) : (
                      <ul className="mt-2 space-y-1">
                        {groupMembers.map((m) => (
                          <li key={m.userId} className="flex items-center justify-between py-1">
                            <div className="flex items-center gap-2">
                              {m.picture ? (
                                <img src={m.picture} alt="" className="h-6 w-6 rounded-full" referrerPolicy="no-referrer" />
                              ) : (
                                <div
                                  className="flex h-6 w-6 items-center justify-center rounded-full text-xs"
                                  style={{ backgroundColor: 'var(--color-surface-card)', color: 'var(--color-text-muted)' }}
                                >
                                  {m.name.charAt(0)}
                                </div>
                              )}
                              <span className="text-sm" style={{ color: 'var(--color-text-primary)' }}>{m.name}</span>
                              <span className="text-xs" style={{ color: 'var(--color-text-muted)' }}>{m.email}</span>
                              {m.isGroupAdmin && (
                                <span className="rounded-full px-1.5 py-0.5 text-[10px] font-medium" style={{ backgroundColor: 'var(--color-primary)', color: 'white' }}>
                                  Liga-admin
                                </span>
                              )}
                            </div>
                            <div className="flex items-center gap-2">
                              {isGlobalAdmin && (
                                <button
                                  onClick={() => handleToggleGroupAdmin(m.userId, m.isGroupAdmin)}
                                  className="text-xs"
                                  style={{ color: 'var(--color-primary)' }}
                                >
                                  {m.isGroupAdmin ? 'Fjern admin' : 'Gjør admin'}
                                </button>
                              )}
                              <button
                                onClick={() => handleRemoveMember(m.userId)}
                                className="text-xs"
                                style={{ color: 'var(--color-danger)' }}
                              >
                                Fjern
                              </button>
                            </div>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                )}
              </div>
            ))}
          </div>
        ) : (
          <p className="mt-4 text-sm" style={{ color: 'var(--color-text-muted)' }}>Ingen ligaer opprettet ennå.</p>
        )}
      </div>

      {/* Invitations (scoped by group) */}
      <div
        className="rounded-xl border p-4 sm:p-6 mt-6"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Administrer invitasjoner</h2>
        <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>Kun inviterte brukere kan logge inn og bette.</p>

        <form onSubmit={handleInvite} className="mt-4 flex flex-col gap-2 sm:flex-row">
          <select
            value={inviteGroupId}
            onChange={(e) => setInviteGroupId(e.target.value)}
            className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
            style={{
              backgroundColor: 'var(--color-surface-card)',
              borderColor: 'var(--color-input-border)',
              color: 'var(--color-text-primary)',
            }}
            required
          >
            <option value="">-- Velg liga --</option>
            {groupList.map((g) => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="navn@eksempel.no"
            className="flex-1 rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
            style={{
              backgroundColor: 'var(--color-surface-card)',
              borderColor: 'var(--color-input-border)',
              color: 'var(--color-text-primary)',
            }}
            required
          />
          <button
            type="submit"
            disabled={isLoading}
            className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
            style={{ backgroundColor: 'var(--color-primary)' }}
          >
            {isLoading ? 'Sender...' : 'Inviter'}
          </button>
        </form>

        {error ? (
          <p className="mt-3 text-sm" style={{ color: 'var(--color-danger)' }}>{error}</p>
        ) : null}

        {invitations.length > 0 ? (
          <div className="mt-4 space-y-2">
            {Object.entries(
              invitations.reduce<Record<string, InvitationResponse[]>>((acc, inv) => {
                const key = inv.groupName || 'Ukjent liga'
                if (!acc[key]) acc[key] = []
                acc[key].push(inv)
                return acc
              }, {})
            )
              .sort(([a], [b]) => a.localeCompare(b))
              .map(([groupName, groupInvitations]) => (
                <details key={groupName}>
                  <summary
                    className="cursor-pointer rounded-lg border px-4 py-2 text-sm font-medium"
                    style={{ borderColor: 'var(--color-border-light)', color: 'var(--color-text-primary)' }}
                  >
                    {groupName}
                    <span className="ml-2 text-xs font-normal" style={{ color: 'var(--color-text-muted)' }}>
                      ({groupInvitations.length} {groupInvitations.length === 1 ? 'invitasjon' : 'invitasjoner'})
                    </span>
                  </summary>
                  <ul className="ml-4 mt-1 divide-y" style={{ borderColor: 'var(--color-border-light)' }}>
                    {groupInvitations.map((invitation) => (
                      <li key={invitation.id} className="flex items-center justify-between py-2">
                        <span className="text-sm" style={{ color: 'var(--color-text-secondary)' }}>{invitation.email}</span>
                        <button
                          onClick={() => handleDelete(invitation.id)}
                          className="rounded-md px-2 py-1 text-xs font-medium transition-colors"
                          style={{ color: 'var(--color-danger)' }}
                        >
                          Fjern
                        </button>
                      </li>
                    ))}
                  </ul>
                </details>
              ))}
          </div>
        ) : (
          <p className="mt-4 text-sm" style={{ color: 'var(--color-text-muted)' }}>Ingen invitasjoner ennå.</p>
        )}
      </div>

      {isGlobalAdmin && (
      <>
      {/* Match Override */}
      <div
        className="rounded-xl border p-4 sm:p-6 mt-6"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Kamp-overstyring</h2>
        <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>Sett lag manuelt for sluttspillkamper.</p>

        <form onSubmit={handleOverrideSubmit} className="mt-4 flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Kamp</label>
            <select
              value={selectedMatchId}
              onChange={(e) => {
                setSelectedMatchId(e.target.value === '' ? '' : Number(e.target.value))
                setOverrideSuccess(false)
              }}
              className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
              style={{
                backgroundColor: 'var(--color-surface-card)',
                borderColor: 'var(--color-input-border)',
                color: 'var(--color-text-primary)',
              }}
            >
              <option value="">-- Velg kamp --</option>
              {knockoutMatches.map((m) => {
                const homeLabel = m.homePlaceholder || m.homeTeam || '?'
                const awayLabel = m.awayPlaceholder || m.awayTeam || '?'
                const stageLabel = stageNames[m.stage] || m.stage
                return (
                  <option key={m.id} value={m.id}>
                    {stageLabel}: {homeLabel} vs {awayLabel} (kamp {m.id})
                  </option>
                )
              })}
            </select>
          </div>

          {selectedMatchId && (
            <>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="flex flex-col gap-1">
                  <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Hjemmelag</label>
                  <select
                    value={overrideHome}
                    onChange={(e) => setOverrideHome(e.target.value)}
                    className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                    style={{
                      backgroundColor: 'var(--color-surface-card)',
                      borderColor: 'var(--color-input-border)',
                      color: 'var(--color-text-primary)',
                    }}
                  >
                    <option value="">-- Velg lag --</option>
                    {sortedTeams.map((t) => (
                      <option key={`home-${t.code}`} value={t.code}>
                        {t.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Bortelag</label>
                  <select
                    value={overrideAway}
                    onChange={(e) => setOverrideAway(e.target.value)}
                    className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                    style={{
                      backgroundColor: 'var(--color-surface-card)',
                      borderColor: 'var(--color-input-border)',
                      color: 'var(--color-text-primary)',
                    }}
                  >
                    <option value="">-- Velg lag --</option>
                    {sortedTeams.map((t) => (
                      <option key={`away-${t.code}`} value={t.code}>
                        {t.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="flex items-center gap-4">
                <button
                  type="submit"
                  disabled={overrideLoading}
                  className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
                  style={{ backgroundColor: 'var(--color-primary)' }}
                >
                  {overrideLoading ? 'Lagrer...' : 'Lagre'}
                </button>

                {overrideSuccess && (
                  <p className="text-sm font-medium" style={{ color: 'var(--color-success)' }}>
                    Oppdatert!
                  </p>
                )}
              </div>
            </>
          )}

          {overrideError && (
            <p className="mt-2 text-sm" style={{ color: 'var(--color-danger)' }}>
              Feil: {overrideError}
            </p>
          )}
        </form>
      </div>

      {/* Result Override */}
      <div
        className="rounded-xl border p-4 sm:p-6 mt-6"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
      >
        <h2 className="text-lg font-semibold" style={{ color: 'var(--color-text-primary)' }}>Sett resultat</h2>
        <p className="mt-1 text-sm" style={{ color: 'var(--color-text-muted)' }}>Sett eller overstyr kampresultat manuelt. Poeng beregnes automatisk.</p>

        <form onSubmit={handleResultSubmit} className="mt-4 flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Kamp</label>
            <select
              value={resultMatchId}
              onChange={(e) => {
                const id = e.target.value === '' ? '' as const : Number(e.target.value)
                setResultMatchId(id)
                setResultSuccess(false)
                if (id !== '') {
                  const existing = results.get(id)
                  if (existing) {
                    setResultHome(String(existing.homeScore))
                    setResultAway(String(existing.awayScore))
                  } else {
                    setResultHome('')
                    setResultAway('')
                  }
                }
              }}
              className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
              style={{
                backgroundColor: 'var(--color-surface-card)',
                borderColor: 'var(--color-input-border)',
                color: 'var(--color-text-primary)',
              }}
            >
              <option value="">-- Velg kamp --</option>
              {matches.map((m) => {
                const homeLabel = m.homeTeam ? (teams[m.homeTeam]?.name ?? m.homeTeam) : (m.homePlaceholder || '?')
                const awayLabel = m.awayTeam ? (teams[m.awayTeam]?.name ?? m.awayTeam) : (m.awayPlaceholder || '?')
                const stageLabel = stageNames[m.stage] || m.stage
                const existingResult = results.get(m.id)
                const resultLabel = existingResult ? ` (${existingResult.homeScore}-${existingResult.awayScore})` : ''
                return (
                  <option key={m.id} value={m.id}>
                    {stageLabel}: {homeLabel} vs {awayLabel}{resultLabel} (kamp {m.id})
                  </option>
                )
              })}
            </select>
          </div>

          {resultMatchId !== '' && (
            <>
              <div className="grid grid-cols-2 gap-4">
                <div className="flex flex-col gap-1">
                  <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Hjemmemål</label>
                  <input
                    type="number"
                    min={0}
                    value={resultHome}
                    onChange={(e) => setResultHome(e.target.value)}
                    placeholder="0"
                    className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                    style={{
                      backgroundColor: 'var(--color-surface-card)',
                      borderColor: 'var(--color-input-border)',
                      color: 'var(--color-text-primary)',
                    }}
                    required
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-sm font-medium" style={{ color: 'var(--color-text-secondary)' }}>Bortemål</label>
                  <input
                    type="number"
                    min={0}
                    value={resultAway}
                    onChange={(e) => setResultAway(e.target.value)}
                    placeholder="0"
                    className="rounded-lg border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 sm:py-2"
                    style={{
                      backgroundColor: 'var(--color-surface-card)',
                      borderColor: 'var(--color-input-border)',
                      color: 'var(--color-text-primary)',
                    }}
                    required
                  />
                </div>
              </div>

              <div className="flex items-center gap-4">
                <button
                  type="submit"
                  disabled={resultLoading}
                  className="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors disabled:opacity-50 sm:py-2"
                  style={{ backgroundColor: 'var(--color-primary)' }}
                >
                  {resultLoading ? 'Lagrer...' : 'Lagre resultat'}
                </button>

                {resultSuccess && (
                  <p className="text-sm font-medium" style={{ color: 'var(--color-success)' }}>
                    Resultat lagret!
                  </p>
                )}
              </div>
            </>
          )}

          {resultError && (
            <p className="mt-2 text-sm" style={{ color: 'var(--color-danger)' }}>
              Feil: {resultError}
            </p>
          )}
        </form>
      </div>
      </>
      )}
    </>
  )
}
