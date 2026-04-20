# Betting Groups (Betting-grupper)

## TL;DR

> **Quick Summary**: Add multi-tenancy via "betting groups" to the World Cup betting app. Predictions and leaderboards become scoped per group. Admin creates groups and invites users to specific groups. Post-login flow routes users based on group membership count (0→waiting, 1→auto, 2+→selector).
>
> **Deliverables**:
> - New EF Core entities: BettingGroup, BettingGroupMember
> - Modified entities: Prediction (add BettingGroupId), Invitation (add BettingGroupId)
> - Data migration seeding a "Default" group for all existing data
> - New BettingGroupsController (CRUD + members)
> - All scoped endpoints (predictions, leaderboard, match predictions) require group context
> - Frontend: BettingGroupContext, group selector, waiting page, admin group management
> - Active group stored in localStorage, sent via `X-Group-Id` header
>
> **Estimated Effort**: Large (10-14 hours)
> **Parallel Execution**: YES - 5 waves
> **Critical Path**: Models + Migration → Backend endpoints → Frontend context + API → UI components → Polish

---

## Context

### Current State
- Global entities: User, Prediction (unique: UserId+MatchId), Invitation (unique: Email), MatchResult
- Auth: Google OAuth → JWT with UserId, Email, Name, Role. No group info in token.
- Frontend: React+TS with AuthContext, PredictionsContext, ResultsContext, MatchesContext
- Admin panel: manages invitations, match overrides, result overrides
- API client: `src/api/client.ts` with typed `request<T>()` helper using Bearer token

### Key Design Decisions (Final)
1. Only system admin creates betting groups
2. Users invited to a SPECIFIC group (can be in multiple)
3. Post-login: 0 groups → waiting, 1 → auto-select, 2+ → selector page
4. Group switching via user dropdown in Header
5. Matches/results stay global; predictions + leaderboard scoped per group
6. Active group in localStorage, sent as `X-Group-Id` header
7. Backend validates group membership on every scoped endpoint
8. Migration: conditionally create "Default" group only if users exist; on fresh DB, skip seeding (no data to migrate)
9. JWT unchanged — group selection is post-auth, not in token
10. Removing a user from group keeps their predictions (soft); deleting a group cascades
11. **Invitation lifecycle**: Invitations are consumed (deleted) when a user auto-joins a group on login. Removing a member from a group also deletes any matching invitation for that user+group, preventing auto-rejoin on next login.

### Files That Change

**Backend - New files:**
- `api/WorldCup.Api/Models/BettingGroup.cs`
- `api/WorldCup.Api/Models/BettingGroupMember.cs`
- `api/WorldCup.Api/Controllers/BettingGroupsController.cs`
- `api/WorldCup.Api/DTOs/BettingGroupDto.cs` (request/response DTOs)
- `api/WorldCup.Api/Migrations/<timestamp>_AddBettingGroups.cs` (generated)

**Backend - Modified files:**
- `api/WorldCup.Api/Models/Prediction.cs` — add BettingGroupId FK + nav property
- `api/WorldCup.Api/Models/Invitation.cs` — add BettingGroupId FK + nav property
- `api/WorldCup.Api/Data/AppDbContext.cs` — add DbSets, update indexes, configure relationships
- `api/WorldCup.Api/Controllers/PredictionsController.cs` — read group from header, scope queries
- `api/WorldCup.Api/Controllers/ResultsController.cs` — scope leaderboard + match predictions by group
- `api/WorldCup.Api/Controllers/InvitationsController.cs` — add BettingGroupId to invite flow
- `api/WorldCup.Api/Controllers/AuthController.cs` — return user's groups in auth response
- `api/WorldCup.Api/Services/ResultFetcherService.cs` — scoring loop unchanged (points calc is per-prediction, already global)

**Frontend - New files:**
- `src/context/BettingGroupContext.tsx` — active group state, group list, switcher
- `src/pages/WaitingPage.tsx` — "no groups" waiting screen
- `src/pages/GroupSelectorPage.tsx` — multi-group picker
- `src/components/GroupSwitcher.tsx` — dropdown item for Header menu

**Frontend - Modified files:**
- `src/api/client.ts` — add `X-Group-Id` header to requests, add group API functions
- `src/types/index.ts` — add BettingGroup, BettingGroupMember types
- `src/context/AuthContext.tsx` — store user's groups from auth response
- `src/context/PredictionsContext.tsx` — re-fetch when active group changes
- `src/components/Header.tsx` — add group switcher to dropdown
- `src/components/Leaderboard.tsx` — show group name, uses group-scoped endpoint
- `src/components/AdminPanel.tsx` — add group CRUD section, scope invitations per group
- `src/main.tsx` — add BettingGroupProvider, routing for waiting/selector pages
- `src/App.tsx` — minor: wrap with group context if not done in main

---

## API Endpoint Design

### New Endpoints (BettingGroupsController)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/groups` | User | List groups for authenticated user |
| GET | `/api/groups/{id}` | User | Get single group (must be member) |
| POST | `/api/groups` | Admin | Create betting group |
| PUT | `/api/groups/{id}` | Admin | Update group name |
| DELETE | `/api/groups/{id}` | Admin | Delete group (cascades) |
| GET | `/api/groups/{id}/members` | Admin | List members of a group |
| POST | `/api/groups/{id}/members` | Admin | Add user to group by email |
| DELETE | `/api/groups/{id}/members/{userId}` | Admin | Remove user from group |
| GET | `/api/admin/groups` | Admin | List ALL groups with member counts |

### Modified Endpoints

| Endpoint | Change |
|----------|--------|
| `GET /api/predictions` | Reads `X-Group-Id` header, filters by BettingGroupId |
| `PUT /api/predictions/{matchId}` | Reads `X-Group-Id` header, sets BettingGroupId on prediction |
| `GET /api/predictions/match/{matchId}` | Reads `X-Group-Id` header, filters to group members |
| `GET /api/results/leaderboard` | Reads `X-Group-Id` header, scopes to group members' predictions |
| `GET /api/results/points` | Reads `X-Group-Id` header, filters user's predictions in that group |
| `POST /api/invitations` | Requires BettingGroupId in body; adds user to group on login |
| `GET /api/invitations` | Admin: optionally filter by group |
| `POST /api/auth/google` | Response includes `groups: BettingGroupDto[]` for the user |

### X-Group-Id Header Pattern

```csharp
// Frontend sends on every scoped request:
// X-Group-Id: <guid>

// Backend helper (reusable across controllers):
private async Task<(Guid groupId, bool isValid)> ValidateGroupMembership()
{
    var groupIdStr = Request.Headers["X-Group-Id"].FirstOrDefault();
    if (!Guid.TryParse(groupIdStr, out var groupId)) return (Guid.Empty, false);

    var userId = GetAuthenticatedUserId();
    if (userId is null) return (Guid.Empty, false);

    var isMember = await dbContext.BettingGroupMembers
        .AnyAsync(m => m.BettingGroupId == groupId && m.UserId == userId.Value);

    return (groupId, isMember);
}
```

---

## Data Model Details

### BettingGroup
```csharp
public class BettingGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<BettingGroupMember> Members { get; set; } = [];
}
```

### BettingGroupMember
```csharp
public class BettingGroupMember
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public BettingGroup BettingGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
```

### Prediction (modified)
```csharp
// Add to existing Prediction.cs:
public Guid BettingGroupId { get; set; }
public BettingGroup BettingGroup { get; set; } = null!;
```

### Invitation (modified)
```csharp
// Add to existing Invitation.cs:
public Guid BettingGroupId { get; set; }
public BettingGroup BettingGroup { get; set; } = null!;
```

### AppDbContext Changes
```csharp
public DbSet<BettingGroup> BettingGroups => Set<BettingGroup>();
public DbSet<BettingGroupMember> BettingGroupMembers => Set<BettingGroupMember>();

// In OnModelCreating:

// BettingGroupMember: unique (BettingGroupId, UserId)
modelBuilder.Entity<BettingGroupMember>()
    .HasIndex(m => new { m.BettingGroupId, m.UserId })
    .IsUnique();

modelBuilder.Entity<BettingGroupMember>()
    .HasOne(m => m.BettingGroup)
    .WithMany(g => g.Members)
    .HasForeignKey(m => m.BettingGroupId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<BettingGroupMember>()
    .HasOne(m => m.User)
    .WithMany()
    .HasForeignKey(m => m.UserId)
    .OnDelete(DeleteBehavior.Cascade);

// Prediction: change unique index to (BettingGroupId, UserId, MatchId)
// REPLACE the existing index:
modelBuilder.Entity<Prediction>()
    .HasIndex(p => new { p.BettingGroupId, p.UserId, p.MatchId })
    .IsUnique();

modelBuilder.Entity<Prediction>()
    .HasOne(p => p.BettingGroup)
    .WithMany()
    .HasForeignKey(p => p.BettingGroupId)
    .OnDelete(DeleteBehavior.Cascade);

// Invitation: change unique index to (Email, BettingGroupId)
// REPLACE the existing index:
modelBuilder.Entity<Invitation>()
    .HasIndex(i => new { i.Email, i.BettingGroupId })
    .IsUnique();

modelBuilder.Entity<Invitation>()
    .HasOne(i => i.BettingGroup)
    .WithMany()
    .HasForeignKey(i => i.BettingGroupId)
    .OnDelete(DeleteBehavior.Cascade);

// BettingGroup
modelBuilder.Entity<BettingGroup>()
    .HasOne(g => g.CreatedByUser)
    .WithMany()
    .HasForeignKey(g => g.CreatedByUserId)
    .OnDelete(DeleteBehavior.Restrict);
```

---

## Migration Strategy

The migration must handle existing data. Use a two-step approach within a single EF migration:

### Step 1: Schema changes (auto-generated by EF)
- Add BettingGroups table
- Add BettingGroupMembers table
- Add BettingGroupId column to Predictions (nullable initially)
- Add BettingGroupId column to Invitations (nullable initially)
- Drop old unique indexes, create new ones

### Step 2: Data seed (in the migration's Up method, using raw SQL)

**Fresh-DB-safe approach**: All seed SQL is conditional — only runs if data exists. On a fresh DB (no users, no predictions, no invitations), the migration only creates the schema and skips seeding entirely. The Default group is created on first admin login if no groups exist yet.

```sql
-- Only create "Default" group if there are existing users (i.e., NOT a fresh DB)
INSERT INTO BettingGroups (Id, Name, CreatedByUserId, CreatedAt)
SELECT
    '00000000-0000-0000-0000-000000000001',
    'Default',
    Id,
    datetime('now')
FROM Users WHERE IsAdmin = 1
LIMIT 1;

-- Assign all existing users to Default group (no-op if no users)
INSERT INTO BettingGroupMembers (Id, BettingGroupId, UserId, JoinedAt)
SELECT lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))),
    '00000000-0000-0000-0000-000000000001', Id, datetime('now')
FROM Users
WHERE EXISTS (SELECT 1 FROM BettingGroups WHERE Id = '00000000-0000-0000-0000-000000000001');

-- Set all existing predictions to Default group (no-op if no predictions)
UPDATE Predictions SET BettingGroupId = '00000000-0000-0000-0000-000000000001'
WHERE EXISTS (SELECT 1 FROM BettingGroups WHERE Id = '00000000-0000-0000-0000-000000000001');

-- Set all existing invitations to Default group (no-op if no invitations)
UPDATE Invitations SET BettingGroupId = '00000000-0000-0000-0000-000000000001'
WHERE EXISTS (SELECT 1 FROM BettingGroups WHERE Id = '00000000-0000-0000-0000-000000000001');
```

**Note on GUID format**: SQLite + EF Core stores GUIDs as TEXT with hyphens (e.g., `xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx`). The `INSERT INTO BettingGroupMembers` uses a SQL expression that generates RFC 4122-compliant v4 UUIDs with hyphens to match this format.

### Step 3: Make columns non-nullable
After data is populated, alter columns to be NOT NULL (EF migration handles this if you add the migration with the property as required and seed data in the migration).

**Practical approach**: Generate the migration with BettingGroupId as required (non-nullable) but manually edit the migration to:
1. Add column as nullable first
2. Run conditional SQL to populate (only if data exists)
3. Make column NOT NULL
4. Add FK constraint and indexes

**Fresh DB handling in application code** (Task 7): When admin logs in and no groups exist, `AuthController` creates a "Default" betting group automatically and adds the admin as its first member. This ensures the first admin always has a group to work with.

---

## Task Dependency Graph

| Task | Depends On | Reason |
|------|------------|--------|
| 1. Backend Models | None | Foundation entities needed by everything |
| 2. AppDbContext + Migration | 1 | Needs models to configure EF relationships |
| 3. BettingGroupsController | 1, 2 | CRUD endpoints need entities and DbContext |
| 4. Modify PredictionsController | 1, 2 | Needs BettingGroupId on Prediction model |
| 5. Modify ResultsController | 1, 2 | Leaderboard scoping needs group FK |
| 6. Modify InvitationsController | 1, 2 | Invitation flow needs group FK |
| 7. Modify AuthController | 1, 2 | Must return groups in login response |
| 8. Frontend types + API client | 3, 4, 5, 6, 7 | Client must match finalized backend API |
| 9. BettingGroupContext | 8 | Needs API functions for group operations |
| 10. Modify AuthContext | 8 | Must store groups from login response |
| 11. WaitingPage + GroupSelectorPage | 9, 10 | Routing depends on group context |
| 12. Modify main.tsx routing | 9, 10, 11 | Needs all pages and contexts |
| 13. Modify Header + GroupSwitcher | 9 | Needs active group context |
| 14. Modify PredictionsContext | 9 | Must re-fetch on group switch |
| 15. Modify Leaderboard | 9 | Uses group-scoped API |
| 16. Modify AdminPanel | 8, 9 | Group CRUD + scoped invitations |
| 17. Integration testing + polish | All | Verify entire flow end-to-end |

## Parallel Execution Graph

```
Wave 1 (No dependencies - start immediately):
└── Task 1: Backend models (BettingGroup, BettingGroupMember, modify Prediction, Invitation)

Wave 2 (After Wave 1):
└── Task 2: AppDbContext configuration + EF migration with data seed

Wave 3 (After Wave 2 - all backend controllers in parallel):
├── Task 3: BettingGroupsController (new)
├── Task 4: Modify PredictionsController
├── Task 5: Modify ResultsController (leaderboard + points + match predictions)
├── Task 6: Modify InvitationsController
└── Task 7: Modify AuthController (return groups)

Wave 4 (After Wave 3 - frontend foundation then UI in parallel):
├── Task 8: Frontend types + API client updates
│   Then (sequentially after 8):
├── Task 9: BettingGroupContext
├── Task 10: Modify AuthContext
│   Then (sequentially after 9+10):
├── Task 11: WaitingPage + GroupSelectorPage
├── Task 12: Modify main.tsx routing
├── Task 13: Header + GroupSwitcher
├── Task 14: Modify PredictionsContext
├── Task 15: Modify Leaderboard
└── Task 16: Modify AdminPanel (group management section)

Wave 5 (After Wave 4):
└── Task 17: Integration testing + polish
```

**Practical note**: Wave 4 has internal dependencies. The realistic execution is:
- 8 first (API client), then 9+10 in parallel, then 11-16 in parallel.

**Critical Path**: 1 → 2 → 3/4/5 → 8 → 9 → 12 → 17

---

## Detailed Tasks

### Task 1: Backend Models

**Description**: Create new model files and modify existing ones.

**New files**:
- `api/WorldCup.Api/Models/BettingGroup.cs` — as specified in Data Model section
- `api/WorldCup.Api/Models/BettingGroupMember.cs` — as specified in Data Model section

**Modified files**:
- `api/WorldCup.Api/Models/Prediction.cs`:
  - Add `public Guid BettingGroupId { get; set; }`
  - Add `public BettingGroup BettingGroup { get; set; } = null!;`
- `api/WorldCup.Api/Models/Invitation.cs`:
  - Add `public Guid BettingGroupId { get; set; }`
  - Add `public BettingGroup BettingGroup { get; set; } = null!;`

**Acceptance Criteria**:
- All four model files compile with `dotnet build`
- BettingGroup has: Id, Name, CreatedByUserId, CreatedAt, CreatedByUser nav, Members collection
- BettingGroupMember has: Id, BettingGroupId, UserId, JoinedAt, nav properties
- Prediction has BettingGroupId + BettingGroup nav property
- Invitation has BettingGroupId + BettingGroup nav property

**QA Scenario**:
1. Run `dotnet build` in `api/WorldCup.Api/` → expect exit code 0, no errors
2. Verify files exist: `ls api/WorldCup.Api/Models/BettingGroup.cs api/WorldCup.Api/Models/BettingGroupMember.cs`
3. Grep for properties: `grep "BettingGroupId" api/WorldCup.Api/Models/Prediction.cs` → expect match

**Commit**: `feat: add BettingGroup and BettingGroupMember models, extend Prediction and Invitation with group FK`

---

### Task 2: AppDbContext + Migration

**Description**: Update DbContext with new DbSets, relationships, and indexes. Generate and customize EF migration.

**Modified file**: `api/WorldCup.Api/Data/AppDbContext.cs`
- Add `DbSet<BettingGroup>` and `DbSet<BettingGroupMember>`
- In `OnModelCreating`:
  - Remove old `Prediction` unique index on `(UserId, MatchId)`
  - Add new unique index on `(BettingGroupId, UserId, MatchId)`
  - Add `Prediction → BettingGroup` relationship (Cascade delete)
  - Remove old `Invitation` unique index on `(Email)`
  - Add new unique index on `(Email, BettingGroupId)`
  - Add `Invitation → BettingGroup` relationship (Cascade delete)
  - Add `BettingGroupMember` unique index on `(BettingGroupId, UserId)`
  - Add `BettingGroupMember` relationships to `BettingGroup` and `User`
  - Add `BettingGroup → CreatedByUser` relationship (Restrict delete)

**Generate migration**:
```bash
cd api/WorldCup.Api
dotnet ef migrations add AddBettingGroups
```

**Then manually edit the migration** to handle existing data:
1. Add BettingGroups table
2. Add BettingGroupMembers table
3. Add BettingGroupId to Predictions as nullable
4. Add BettingGroupId to Invitations as nullable
5. Insert default group SQL (use a well-known GUID: `00000000-0000-0000-0000-000000000001`)
6. Insert BettingGroupMembers for all existing users
7. Update all Predictions to have the default BettingGroupId
8. Update all Invitations to have the default BettingGroupId
9. Make BettingGroupId NOT NULL on both tables
10. Drop old indexes, create new ones with BettingGroupId
11. Add FK constraints

**Acceptance Criteria**:
- `dotnet ef database update` succeeds on a fresh DB (no users — schema created, no Default group seeded, no errors)
- `dotnet ef database update` succeeds on an existing DB with data (all existing predictions/invitations get Default group)
- Unique constraint `(BettingGroupId, UserId, MatchId)` enforced on Predictions
- Unique constraint `(Email, BettingGroupId)` enforced on Invitations
- Unique constraint `(BettingGroupId, UserId)` enforced on BettingGroupMembers
- All existing users appear in BettingGroupMembers for the Default group (on existing DB only)

**QA Scenario**:
1. Delete existing DB: `rm api/WorldCup.Api/worldcup.db` (if exists)
2. Run `dotnet ef database update` in `api/WorldCup.Api/` → expect exit code 0
3. Verify schema: `sqlite3 worldcup.db ".tables"` → expect BettingGroups, BettingGroupMembers in output
4. Verify no Default group on fresh DB: `sqlite3 worldcup.db "SELECT COUNT(*) FROM BettingGroups;"` → expect `0`
5. For existing DB test: restore a backup with users/predictions, run `dotnet ef database update` → expect exit code 0
6. Verify seed: `sqlite3 worldcup.db "SELECT COUNT(*) FROM BettingGroupMembers;"` → expect same count as Users table

**Commit**: `feat: add EF migration for betting groups with data seed for existing records`

---

### Task 3: BettingGroupsController

**Description**: New controller for group CRUD and member management. Admin-only for create/update/delete/member management. Authenticated users can list their own groups.

**New file**: `api/WorldCup.Api/Controllers/BettingGroupsController.cs`

**New DTOs** (in `DTOs/BettingGroupDto.cs`):
```csharp
public record BettingGroupResponse(Guid Id, string Name, int MemberCount, DateTime CreatedAt);
public record BettingGroupDetailResponse(Guid Id, string Name, DateTime CreatedAt, List<BettingGroupMemberResponse> Members);
public record BettingGroupMemberResponse(Guid UserId, string Name, string Email, string? Picture, DateTime JoinedAt);
public record CreateBettingGroupRequest(string Name);
public record UpdateBettingGroupRequest(string Name);
public record AddGroupMemberRequest(string Email);
```

**Endpoints**:
- `GET /api/groups` — returns groups where user is member (non-admin) or all groups (admin)
- `POST /api/groups` [Admin] — create group, auto-add admin as member
- `PUT /api/groups/{id}` [Admin] — update name
- `DELETE /api/groups/{id}` [Admin] — delete (cascade)
- `GET /api/groups/{id}/members` [Admin] — list members
- `POST /api/groups/{id}/members` [Admin] — add member by email (find User by email, create BettingGroupMember)
- `DELETE /api/groups/{id}/members/{userId}` [Admin] — remove member **and delete any matching invitation for that user+group** (prevents auto-rejoin on next login)

**Edge cases**:
- Adding a user who doesn't have an account yet: return 404 with message "User not found. Invite them first."
- Adding a user already in the group: return 409
- Deleting last admin from a group: allow (group still exists, other members remain)
- Non-admin calling GET /api/groups: returns only groups they're a member of
- Removing a member: also delete matching Invitation row (WHERE Email = user.Email AND BettingGroupId = groupId)

**Acceptance Criteria**:
- All CRUD operations work
- Admin-only endpoints return 403 for non-admins
- GET /api/groups for regular user returns only their groups
- Member add/remove works correctly
- Proper error responses (404, 409, 400)
- Removing a member also deletes their invitation for that group

**QA Scenario**:
1. Run `dotnet build` → expect exit code 0
2. Start API, then use curl:
   - `curl -X POST /api/groups -H "Authorization: Bearer <admin-token>" -d '{"name":"Test"}' ` → expect 200 with group JSON
   - `curl /api/groups -H "Authorization: Bearer <user-token>"` → expect only groups user is member of
   - `curl -X POST /api/groups/{id}/members -H "Authorization: Bearer <admin-token>" -d '{"email":"user@test.com"}'` → expect 200
   - `curl -X POST /api/groups/{id}/members -d '{"email":"user@test.com"}'` (same email again) → expect 409
   - `curl -X DELETE /api/groups/{id}/members/{userId}` → expect 200, verify invitation also deleted: `sqlite3 worldcup.db "SELECT COUNT(*) FROM Invitations WHERE Email='user@test.com' AND BettingGroupId='{id}';"` → expect 0

**Commit**: `feat: add BettingGroupsController with CRUD and member management`

---

### Task 4: Modify PredictionsController

**Description**: Scope all prediction queries by BettingGroupId from `X-Group-Id` header.

**Modified file**: `api/WorldCup.Api/Controllers/PredictionsController.cs`

**Changes**:
- Add private helper `ValidateGroupMembership()` that reads `X-Group-Id` header, validates GUID format, checks user is member of that group
- `GET /api/predictions`: Add `.Where(p => p.BettingGroupId == groupId)` filter
- `PUT /api/predictions/{matchId}`:
  - Validate group membership
  - Upsert now uses `(BettingGroupId, UserId, MatchId)` as lookup key
  - Set `BettingGroupId` on new predictions
- `GET /api/predictions/match/{matchId}`:
  - Filter predictions to group members only: `.Where(p => p.BettingGroupId == groupId)`
- All endpoints return 400 if `X-Group-Id` header missing or invalid
- All endpoints return 403 if user is not member of the group

**Acceptance Criteria**:
- Missing X-Group-Id → 400
- Invalid group ID → 400
- Non-member group → 403
- Predictions correctly scoped per group
- User can have different predictions for same match in different groups
- Upsert logic works with the three-part unique key

**QA Scenario**:
1. Run `dotnet build` → expect exit code 0
2. `curl /api/predictions -H "Authorization: Bearer <token>"` (no X-Group-Id) → expect 400
3. `curl /api/predictions -H "Authorization: Bearer <token>" -H "X-Group-Id: <valid-group>"` → expect 200 with predictions for that group only
4. `curl -X PUT /api/predictions/1 -H "Authorization: Bearer <token>" -H "X-Group-Id: <groupA>" -d '{"homeScore":2,"awayScore":1}'` → expect 200
5. `curl -X PUT /api/predictions/1 -H "Authorization: Bearer <token>" -H "X-Group-Id: <groupB>" -d '{"homeScore":0,"awayScore":3}'` → expect 200 (different prediction for same match)
6. Verify in DB: `sqlite3 worldcup.db "SELECT BettingGroupId, HomeScore, AwayScore FROM Predictions WHERE MatchId=1 AND UserId='<uid>';"` → expect 2 rows with different groups and scores

**Commit**: `feat: scope predictions endpoints by betting group`

---

### Task 5: Modify ResultsController

**Description**: Scope leaderboard, points, and match predictions by group.

**Modified file**: `api/WorldCup.Api/Controllers/ResultsController.cs`

**Changes**:
- Add same `ValidateGroupMembership()` helper
- `GET /api/results/leaderboard`:
  - Filter to users who are members of the group
  - Sum points only from predictions with matching BettingGroupId
  - Change from querying all Users to joining through BettingGroupMembers
- `GET /api/results/points`:
  - Filter to predictions with matching BettingGroupId
- `GET /api/results` — unchanged (results are global)
- `PUT /api/admin/results/{matchId}` — unchanged (results are global, scoring updates ALL predictions for that match regardless of group)

**Leaderboard query rewrite**:
```csharp
var leaderboard = await dbContext.BettingGroupMembers
    .Where(m => m.BettingGroupId == groupId)
    .Select(m => new LeaderboardEntry
    {
        Name = m.User.Name,
        Picture = m.User.Picture,
        TotalPoints = dbContext.Predictions
            .Where(p => p.UserId == m.UserId && p.BettingGroupId == groupId && p.Points != null)
            .Sum(p => (int?)p.Points) ?? 0,
        MatchCount = dbContext.Predictions
            .Count(p => p.UserId == m.UserId && p.BettingGroupId == groupId && p.Points != null)
    })
    .OrderByDescending(e => e.TotalPoints)
    .ThenBy(e => e.Name)
    .AsNoTracking()
    .ToListAsync();
```

**Acceptance Criteria**:
- Leaderboard shows only group members with group-scoped points
- Points endpoint returns only predictions from the active group
- Results endpoints (global) still work without group header
- Admin result setting still updates ALL predictions across all groups

**QA Scenario**:
1. Run `dotnet build` → expect exit code 0
2. `curl /api/results/leaderboard -H "Authorization: Bearer <token>" -H "X-Group-Id: <groupA>"` → expect leaderboard with only groupA members
3. `curl /api/results/points -H "Authorization: Bearer <token>" -H "X-Group-Id: <groupA>"` → expect points from groupA predictions only
4. `curl /api/results` → expect 200 (no auth/group required, global results)
5. Admin sets a result: `curl -X PUT /api/admin/results/1 -H "Authorization: Bearer <admin-token>" -d '{"homeScore":2,"awayScore":1}'` → verify points updated across ALL groups: `sqlite3 worldcup.db "SELECT BettingGroupId, Points FROM Predictions WHERE MatchId=1 AND Points IS NOT NULL;"` → expect rows from multiple groups

**Commit**: `feat: scope leaderboard and points by betting group`

---

### Task 6: Modify InvitationsController

**Description**: Invitations are now per-group. Creating an invitation requires a BettingGroupId.

**Modified files**:
- `api/WorldCup.Api/Controllers/InvitationsController.cs`
- `api/WorldCup.Api/DTOs/InvitationRequest.cs`
- `api/WorldCup.Api/DTOs/InvitationResponse.cs`

**Changes**:
- `InvitationRequest`: add `Guid BettingGroupId`
- `InvitationResponse`: add `Guid BettingGroupId`, `string GroupName`
- `POST /api/invitations`: validate BettingGroupId exists, check unique (Email, BettingGroupId), store group FK
- `GET /api/invitations`: optionally filter by `?groupId=<guid>` query param
- `DELETE /api/invitations/{id}`: unchanged

**Acceptance Criteria**:
- Creating invitation requires valid BettingGroupId
- Same email can be invited to multiple groups
- Duplicate (email, group) returns 409
- GET returns invitations with group info

**QA Scenario**:
1. Run `dotnet build` → expect exit code 0
2. `curl -X POST /api/invitations -H "Authorization: Bearer <admin-token>" -d '{"email":"new@test.com","bettingGroupId":"<groupA>"}'` → expect 200
3. `curl -X POST /api/invitations -H "Authorization: Bearer <admin-token>" -d '{"email":"new@test.com","bettingGroupId":"<groupA>"}'` → expect 409 (duplicate)
4. `curl -X POST /api/invitations -H "Authorization: Bearer <admin-token>" -d '{"email":"new@test.com","bettingGroupId":"<groupB>"}'` → expect 200 (same email, different group)
5. `curl /api/invitations -H "Authorization: Bearer <admin-token>"` → expect both invitations with group names

**Commit**: `feat: scope invitations by betting group`

---

### Task 7: Modify AuthController

**Description**: After successful login, return the user's group memberships so frontend can decide routing.

**Modified files**:
- `api/WorldCup.Api/Controllers/AuthController.cs`
- `api/WorldCup.Api/DTOs/AuthResponse.cs`

**Changes to AuthResponse**:
```csharp
// Add:
public List<BettingGroupResponse> Groups { get; set; } = [];
```

**Changes to AuthController.GoogleLogin**:
- After user upsert and save, query user's groups:
```csharp
var groups = await dbContext.BettingGroupMembers
    .Where(m => m.UserId == user.Id)
    .Select(m => new BettingGroupResponse(m.BettingGroupId, m.BettingGroup.Name, 0, m.BettingGroup.CreatedAt))
    .ToListAsync();
```
- Include in response
- **Auto-join on login**: When user logs in and has pending invitation(s) for group(s), auto-create BettingGroupMember for each invited group (if not already a member), then **delete the consumed invitation(s)**
- **Fresh-DB bootstrap**: If admin logs in and no BettingGroups exist at all, auto-create a "Default" betting group owned by this admin and add admin as first member. This handles the fresh-DB case where migration couldn't seed a Default group (no users existed).

**Acceptance Criteria**:
- Login response includes `groups` array
- New user with invitation gets auto-added to the invited group(s)
- Consumed invitations are deleted after auto-join
- Existing user's groups are returned correctly
- On fresh DB, first admin login creates a Default group automatically

**QA Scenario**:
1. Run `dotnet build` → expect exit code 0
2. Fresh DB: admin logs in via `POST /api/auth/google` → expect response includes `groups` array with 1 entry ("Default")
3. Verify auto-created group: `sqlite3 worldcup.db "SELECT Name FROM BettingGroups;"` → expect "Default"
4. Invite user to groupA, user logs in → expect response includes groupA in groups array
5. Verify invitation consumed: `sqlite3 worldcup.db "SELECT COUNT(*) FROM Invitations WHERE Email='<user-email>' AND BettingGroupId='<groupA>';"` → expect 0
6. Verify membership created: `sqlite3 worldcup.db "SELECT COUNT(*) FROM BettingGroupMembers WHERE UserId='<uid>' AND BettingGroupId='<groupA>';"` → expect 1

**Commit**: `feat: return user groups in auth response, auto-join on login`

---

### Task 8: Frontend Types + API Client

**Description**: Add TypeScript types and API functions for betting groups.

**Modified files**:
- `src/types/index.ts` — add BettingGroup type
- `src/api/client.ts` — add X-Group-Id header to all requests, add group API functions

**New types**:
```typescript
export interface BettingGroup {
  id: string
  name: string
  memberCount: number
  createdAt: string
}

export interface BettingGroupMember {
  userId: string
  name: string
  email: string
  picture: string | null
  joinedAt: string
}
```

**API client changes**:
- Modify `request<T>()` to inject `X-Group-Id` from localStorage:
```typescript
function getActiveGroupId(): string | null {
  try {
    return localStorage.getItem('active_group_id')
  } catch {
    return null
  }
}

// In request():
const groupId = getActiveGroupId()
if (groupId) {
  headers['X-Group-Id'] = groupId
}
```

- **AuthResponse**: add `groups: BettingGroup[]`

- New API functions:
```typescript
export function getMyGroups(): Promise<BettingGroup[]>
export function createGroup(name: string): Promise<BettingGroup>
export function updateGroup(id: string, name: string): Promise<BettingGroup>
export function deleteGroup(id: string): Promise<void>
export function getGroupMembers(groupId: string): Promise<BettingGroupMember[]>
export function addGroupMember(groupId: string, email: string): Promise<BettingGroupMember>
export function removeGroupMember(groupId: string, userId: string): Promise<void>
```

**Acceptance Criteria**:
- All new types compile
- X-Group-Id header is sent on all requests when active group is set
- All group API functions match backend endpoints
- AuthResponse includes groups

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Grep for header injection: `grep "X-Group-Id" src/api/client.ts` → expect match in request function
3. Grep for all API functions: `grep -c "export function" src/api/client.ts` → expect count includes new group functions
4. Verify AuthResponse type: `grep "groups" src/api/client.ts` → expect BettingGroup[] in response type

**Commit**: `feat: add betting group types and API client functions`

---

### Task 9: BettingGroupContext

**Description**: React context managing the active group, group list, and switching logic.

**New file**: `src/context/BettingGroupContext.tsx`

**State**:
- `groups: BettingGroup[]` — user's groups (from auth or fetched)
- `activeGroup: BettingGroup | null` — currently selected group
- `setActiveGroup(group: BettingGroup): void` — switch group, persist to localStorage
- `isLoading: boolean`

**Logic**:
- On mount: read `active_group_id` from localStorage, match to groups list
- If stored group not in list, clear it
- When `groups` changes (login): if 1 group, auto-select; if stored matches, use it
- `setActiveGroup` saves to localStorage and updates state

**Acceptance Criteria**:
- Active group persists across page refreshes
- Switching groups updates localStorage
- Auto-selects when only 1 group
- Clears state on logout

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Run `npm test` → expect existing tests pass
3. Browser: log in with 1 group → verify localStorage has `active_group_id` set automatically
4. Browser: refresh page → verify active group still selected (no group selector shown)

**Commit**: `feat: add BettingGroupContext for active group management`

---

### Task 10: Modify AuthContext

**Description**: Store groups from login response, pass to BettingGroupContext.

**Modified file**: `src/context/AuthContext.tsx`

**Changes**:
- `AuthUser` interface: add `groups: BettingGroup[]`
- `loginWithGoogle`: store groups from response
- `AuthContextValue`: expose `groups`
- On logout: clear groups

**Acceptance Criteria**:
- Groups available after login
- Groups cleared on logout
- Groups persisted in localStorage with user data

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: log in → inspect localStorage for user data → expect `groups` array present
3. Browser: log out → inspect localStorage → expect groups cleared

**Commit**: `feat: store user groups in AuthContext from login response`

---

### Task 11: WaitingPage + GroupSelectorPage

**Description**: Two new pages for the post-login group flow.

**New file**: `src/pages/WaitingPage.tsx`
- Simple centered message: "You haven't been added to any betting group yet. Contact the admin."
- Logout button
- Theme toggle

**New file**: `src/pages/GroupSelectorPage.tsx`
- Lists user's groups as clickable cards
- Clicking a group sets it as active and navigates to `/`
- Shows group name and member count

**Acceptance Criteria**:
- WaitingPage shows when user has 0 groups
- GroupSelectorPage shows when user has 2+ groups
- Selecting a group navigates to main app
- Both pages match existing visual style (use `var(--color-*)` CSS variables, same card/button patterns)

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: log in as user with 0 groups → expect WaitingPage with message and logout button visible
3. Browser: log in as user with 2+ groups (no active group in localStorage) → expect GroupSelectorPage with group cards
4. Browser: click a group card → expect redirect to main app, `active_group_id` set in localStorage

**Commit**: `feat: add WaitingPage and GroupSelectorPage for post-login flow`

---

### Task 12: Modify main.tsx Routing

**Description**: Add BettingGroupProvider and conditional routing based on group count.

**Modified file**: `src/main.tsx`

**Changes**:
- Wrap app with `BettingGroupProvider`
- Modify `ProtectedRoute`:
  - If user has 0 groups → render WaitingPage
  - If user has 2+ groups and no active group → render GroupSelectorPage
  - If user has 1 group → auto-select, render children
  - If active group is set → render children

```tsx
function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  const { groups, activeGroup } = useBettingGroup()

  if (!user) return <Navigate to="/login" replace />
  if (groups.length === 0) return <WaitingPage />
  if (!activeGroup) return <GroupSelectorPage />
  return <>{children}</>
}
```

**Acceptance Criteria**:
- 0 groups → WaitingPage
- 1 group → auto-selects, shows main app
- 2+ groups, none selected → GroupSelectorPage
- Active group set → main app

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: clear localStorage, log in as user with 1 group → expect main app (no selector shown)
3. Browser: clear localStorage, log in as user with 2 groups → expect GroupSelectorPage
4. Browser: select a group → expect main app with match cards visible

**Commit**: `feat: add group-aware routing with BettingGroupProvider`

---

### Task 13: Header + GroupSwitcher

**Description**: Add group switcher to the user dropdown menu.

**Modified file**: `src/components/Header.tsx`

**New file** (optional, could be inline): `src/components/GroupSwitcher.tsx`

**Changes to Header**:
- Show active group name next to or below the app title
- In the dropdown menu, add a "Switch group" option (only if user has 2+ groups)
- Clicking "Switch group" clears active group → triggers GroupSelectorPage via routing

**Acceptance Criteria**:
- Active group name visible in header
- "Switch group" appears in dropdown when 2+ groups
- Clicking it navigates to group selector
- Single-group users don't see the switch option

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: log in with 2+ groups, select one → expect group name visible in header area
3. Browser: click user avatar → expect "Switch group" option in dropdown
4. Browser: click "Switch group" → expect GroupSelectorPage shown
5. Browser: log in with 1 group → click user avatar → expect NO "Switch group" option

**Commit**: `feat: add group switcher to header dropdown`

---

### Task 14: Modify PredictionsContext

**Description**: Re-fetch predictions when active group changes.

**Modified file**: `src/context/PredictionsContext.tsx`

**Changes**:
- Import `useBettingGroup`
- Add `activeGroup` to the useEffect dependency that fetches predictions
- When activeGroup changes (or is null), clear and re-fetch
- If no active group, keep predictions empty

**Acceptance Criteria**:
- Switching groups triggers prediction re-fetch
- Predictions are empty when no active group
- No stale data from previous group shown

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: log in with 2 groups, select groupA → submit a prediction → verify prediction shown
3. Browser: switch to groupB via dropdown → verify predictions reload (groupA prediction not visible)
4. Browser: open Network tab → verify new GET /api/predictions request with X-Group-Id = groupB

**Commit**: `feat: re-fetch predictions on betting group switch`

---

### Task 15: Modify Leaderboard

**Description**: Leaderboard is now group-scoped (already handled by backend via X-Group-Id header). Frontend just needs to show group name and re-fetch on group change.

**Modified file**: `src/components/Leaderboard.tsx`

**Changes**:
- Import `useBettingGroup`, show active group name as subtitle
- Add `activeGroup?.id` to useEffect dependency to re-fetch on switch

**Acceptance Criteria**:
- Leaderboard shows group name
- Re-fetches when group changes
- Shows only group members' scores

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: log in, select groupA → open leaderboard → expect group name "groupA" visible as subtitle
3. Browser: switch to groupB → expect leaderboard refreshes, shows groupB name and only groupB members

**Commit**: `feat: scope leaderboard display by active betting group`

---

### Task 16: Modify AdminPanel

**Description**: Add group management section to admin panel. Also scope invitation management per group.

**Modified file**: `src/components/AdminPanel.tsx`

**New section at top of AdminPanel**: "Manage Betting Groups"
- List all groups with member counts
- Create new group form (name input)
- Each group expandable: shows members, add/remove member forms
- Delete group button with confirmation

**Modify invitation section**:
- Add group selector dropdown when creating invitation
- Show which group each invitation is for

**Acceptance Criteria**:
- Can create/rename/delete groups
- Can add/remove members from groups
- Invitation creation requires group selection
- Invitation list shows group name
- All operations provide loading/error/success feedback

**QA Scenario**:
1. Run `npx tsc --noEmit` → expect exit code 0
2. Browser: log in as admin → open admin panel → expect "Manage Betting Groups" section visible
3. Create group: type name "Test Group", submit → expect group appears in list with 0 members
4. Add member: expand group, type email, submit → expect member appears in member list
5. Create invitation: select group from dropdown, type email, submit → expect invitation listed with group name
6. Delete group: click delete on "Test Group", confirm → expect group removed from list
7. Verify cascade: `sqlite3 worldcup.db "SELECT COUNT(*) FROM BettingGroupMembers WHERE BettingGroupId='<deleted-id>';"` → expect 0

**Commit**: `feat: add group management to admin panel`

---

### Task 17: Integration Testing + Polish

**Description**: End-to-end verification of the complete flow.

**Test scenarios** (execute in order, using browser + curl + sqlite3):

1. **Fresh DB migration**: Delete DB, run `dotnet ef database update` → expect exit code 0. Run `sqlite3 worldcup.db "SELECT COUNT(*) FROM BettingGroups;"` → expect 0 (no Default group on empty DB)
2. **First admin login bootstraps Default group**: `POST /api/auth/google` as admin → expect response with groups containing "Default". Verify: `sqlite3 worldcup.db "SELECT Name FROM BettingGroups;"` → "Default"
3. **Admin creates group via UI**: Browser: admin panel → create "Friends" group → expect group in list
4. **Admin invites user to group**: Browser: admin panel → create invitation for "user@test.com" to "Friends" → expect invitation in list with group name
5. **User login auto-joins**: `POST /api/auth/google` as invited user → expect groups includes "Friends". Verify invitation consumed: `sqlite3 worldcup.db "SELECT COUNT(*) FROM Invitations WHERE Email='user@test.com';"` → expect 0
6. **Prediction scoping**: Browser: user in groupA submits prediction for match 1. Switch to groupB → match 1 shows no prediction. Submit different prediction → verify both exist: `sqlite3 worldcup.db "SELECT BettingGroupId, HomeScore FROM Predictions WHERE MatchId=1 AND UserId='<uid>';"` → expect 2 rows
7. **Leaderboard scoping**: Browser: open leaderboard in groupA → expect only groupA members listed
8. **Group switching**: Browser: user with 2+ groups → click avatar → "Switch group" → expect GroupSelectorPage → select other group → predictions and leaderboard refresh
9. **0-group waiting page**: Remove user from all groups via admin API, user refreshes → expect WaitingPage
10. **Delete group cascades**: Admin deletes group via UI → verify: `sqlite3 worldcup.db "SELECT COUNT(*) FROM Predictions WHERE BettingGroupId='<deleted>';"` → expect 0

**Also verify**:
- Existing tests still pass (`npm test`, `dotnet test` if applicable)
- No regressions in match display, results, countdown
- Admin panel still works for match overrides and result setting (global)

**Acceptance Criteria**:
- All 10 scenarios pass manually
- Existing tests pass
- No console errors in frontend
- No unhandled exceptions in backend logs

**Commit**: `fix: address integration issues from betting groups feature` (if needed)

---

## Commit Strategy

Each task gets its own atomic commit. The sequence ensures the app compiles and runs after each commit (with degraded functionality until the full feature is complete):

1. `feat: add BettingGroup and BettingGroupMember models, extend Prediction and Invitation with group FK`
2. `feat: add EF migration for betting groups with data seed for existing records`
3. `feat: add BettingGroupsController with CRUD and member management`
4. `feat: scope predictions endpoints by betting group`
5. `feat: scope leaderboard and points by betting group`
6. `feat: scope invitations by betting group`
7. `feat: return user groups in auth response, auto-join on login`
8. `feat: add betting group types and API client functions`
9. `feat: add BettingGroupContext for active group management`
10. `feat: store user groups in AuthContext from login response`
11. `feat: add WaitingPage and GroupSelectorPage for post-login flow`
12. `feat: add group-aware routing with BettingGroupProvider`
13. `feat: add group switcher to header dropdown`
14. `feat: re-fetch predictions on betting group switch`
15. `feat: scope leaderboard display by active betting group`
16. `feat: add group management to admin panel`
17. `fix: address integration issues from betting groups feature` (if needed)

**Note on buildability**: After commit 2, the backend will require X-Group-Id on scoped endpoints. The frontend won't send it until commit 8+. During development, test backend changes via curl/Postman with the header manually set. The app won't be fully functional until commit 12+.

**Alternative**: Commits 1-7 can be squashed into a single backend commit, and 8-16 into a single frontend commit, if atomic per-file commits are too granular. The above gives maximum bisectability.

---

## Success Criteria

1. Admin can create betting groups and invite users to specific groups
2. Users see waiting page when not in any group
3. Users auto-enter when in exactly 1 group
4. Users see group selector when in 2+ groups
5. Users can switch groups via header dropdown
6. Predictions are scoped per group (same user can predict differently per group)
7. Leaderboard is scoped per group
8. Existing data migrated to "Default" group seamlessly
9. All existing functionality (match display, results, countdown) unaffected
10. No authentication changes required — group is post-login context only
