# FIFA World Cup 2026 Tipping App

A match prediction app for the FIFA World Cup 2026. Invite friends, predict full-time scores, chat, and compete across leagues.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                     Browser                         │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │           React Frontend (Vite)               │  │
│  │                                               │  │
│  │  ┌─────────┐  ┌──────────┐  ┌─────────────┐  │  │
│  │  │  Login  │  │  Match   │  │   Admin     │  │  │
│  │  │  Page   │  │  Cards   │  │   Panel     │  │  │
│  │  └────┬────┘  └────┬─────┘  └──────┬──────┘  │  │
│  │       │             │               │         │  │
│  │  ┌────▼─────────────▼───────────────▼──────┐  │  │
│  │  │   API Client (JWT) + SignalR (chat)     │  │  │
│  │  └────────────────┬────────────────────────┘  │  │
│  └───────────────────┼───────────────────────────┘  │
└──────────────────────┼──────────────────────────────┘
                       │ HTTP / WebSocket :5173 → :5211
                       │
┌──────────────────────▼──────────────────────────────┐
│              .NET 10 Web API                        │
│                                                     │
│  ┌────────────────┐  ┌──────────────────────────┐   │
│  │  Google Auth   │  │  JWT Bearer Middleware   │   │
│  │  Validation    │  │                          │   │
│  └───────┬────────┘  └────────────┬─────────────┘   │
│          │                        │                  │
│  ┌───────▼────────────────────────▼─────────────┐   │
│  │        Controllers + ChatHub (SignalR)        │   │
│  │  ┌──────┐  ┌─────────────┐  ┌────────────┐   │   │
│  │  │ Auth │  │ Predictions │  │  Results  │   │   │
│  │  └──┬───┘  └──────┬──────┘  └─────┬──────┘   │   │
│  └─────┼─────────────┼───────────────┼───────────┘   │
│        │             │               │               │
│  ┌─────▼─────────────▼───────────────▼───────────┐   │
│  │             EF Core + SQL Server              │   │
│  │  ┌───────┐  ┌────────────┐  ┌─────────────┐  │   │
│  │  │ Users │  │ Predictions│  │MatchResults │  │   │
│  │  └───────┘  └────────────┘  └─────────────┘  │   │
│  └───────────────────────────────────────────────┘   │
│                                                     │
│  External: WC2026 results API · Open-Meteo weather  │
│  Config/flags: Azure App Configuration (optional)   │
└──────────────────────────────────────────────────────┘
```

### Auth Flow

```
User → Google Sign-In → Google JWT → Backend validates →
  ├─ Admin email?                → Allow, issue app JWT
  ├─ Existing user?              → Allow, issue app JWT
  ├─ Email invitation pending?   → Allow, auto-join league(s), issue app JWT
  ├─ Valid invite-link token?    → Allow, auto-join league, issue app JWT
  └─ None of the above           → 403 Forbidden → Waiting page
```

## Tech Stack

| Layer     | Technology                                             |
|-----------|--------------------------------------------------------|
| Frontend  | React 19, TypeScript 6, Vite 8, Tailwind CSS 4, React Router 7 |
| Backend   | .NET 10, ASP.NET Core, EF Core                         |
| Database  | SQL Server (Azure SQL in production, LocalDB for local dev) |
| Realtime  | SignalR (live league chat)                             |
| Auth      | Google Sign-In → JWT Bearer                            |
| Config    | Azure App Configuration + Microsoft.FeatureManagement (optional) |
| Testing   | Vitest + jsdom (frontend), xUnit (backend)             |

## Getting Started

### Prerequisites

- [Node.js](https://nodejs.org/) 20+
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- A SQL Server instance — [LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) or Docker works for local development
- A [Google OAuth Client ID](https://console.cloud.google.com/apis/credentials)

### Backend Setup

```bash
cd api/WorldCup.Api

# Copy and fill in config
cp appsettings.Development.json.example appsettings.Development.json
# Edit appsettings.Development.json with your values:
#   - ConnectionStrings:DefaultConnection (a SQL Server connection string)
#   - Jwt:Key (min 32 characters)
#   - Google:ClientId
#   - Admin:Email

# Apply migrations and run
dotnet ef database update
dotnet run
```

The API runs on `http://localhost:5211`.

Feature flags and other settings can optionally be pulled from Azure App
Configuration by setting `APP_CONFIGURATION_ENDPOINT`; when it is unset the app
falls back to the `FeatureManagement` section of `appsettings.json`, so no Azure
setup is needed locally. If `SLACK_WEBHOOK_URL` is set, warnings and errors are
forwarded to Slack.

### Frontend Setup

```bash
# Install dependencies
npm install

# Create .env
echo 'VITE_GOOGLE_CLIENT_ID=<your-google-client-id>' > .env

# Run dev server
npm run dev
```

The app runs on `http://localhost:5173`.

### Running Tests

```bash
# Frontend (Vitest)
npm test

# Backend (xUnit)
dotnet test
```

## Features

- **Match Overview** — Browse all World Cup 2026 matches grouped by date and stage, with the current/next round selected by default
- **Predictions** — Submit full-time score predictions for each match
- **Live Results** — Automatic result fetching from the WC2026 API with point calculation, including knockout fixtures resolved as the group stage completes
- **Match Details** — Per-match page with a countdown, team statistics and head-to-head history, and a weather forecast (via Open-Meteo)
- **TV Channels** — NRK/TV2 broadcast badges for knockout matches
- **Leaderboard** — Compete with friends based on prediction accuracy
- **Ligaer** — Opprett og administrer separate ligaer med egne medlemmer og poengtavler
- **Chat** — Sanntids ligachat (SignalR) med markdown og emoji-reaksjoner
- **Google Sign-In** — Secure authentication via Google accounts
- **Invitation System** — Inviter brukere på e-post, eller del en revokerbar invitasjonslenke (`/invite/<token>`) som lar hvem som helst bli med i ligaen
- **Feature Flags** — Slå av/på funksjoner (f.eks. AI-prediksjoner, betalte ligaer) per liga uten ny utrulling
- **Theming** — Lys/mørk modus
- **Admin Panel** — Inviter/fjern brukere, lag og revoker invitasjonslenker, administrer ligaer, overstyr sluttspillkamper, sett kampresultater manuelt og kjør diagnostikk mot resultat-API-et

## Project Structure

```
worldcup/
├── api/WorldCup.Api/        # .NET backend
│   ├── Controllers/          #   Auth, Predictions, Results, Matches, Invitations,
│   │                         #   InviteLinks, BettingGroups, Chat, Broadcast,
│   │                         #   Weather, TeamStats, FeatureFlags, AdminDiagnostics
│   ├── Hubs/                 #   ChatHub (SignalR)
│   ├── Models/               #   User, Prediction, Invitation, BettingGroup(+Member/InviteLink),
│   │                         #   MatchResult, ChatMessage(+Reaction), ApiCallLog, PendingMatchFetch
│   ├── DTOs/                 #   Request/response objects
│   ├── Services/             #   ResultFetcher, Scoring, MatchSchedule, Wc2026ApiClient,
│   │                         #   Weather, TeamStats, TeamCodeMapper, MatchFileWriter, DatabaseMigration
│   ├── Features/             #   Feature-flag filters (BettingGroupFilter)
│   ├── Logging/              #   Slack log provider
│   ├── Data/                 #   EF Core DbContext + seed data
│   └── Migrations/           #   Database migrations
├── tests/WorldCup.Api.Tests/ # Backend xUnit tests
├── src/                      # React frontend
│   ├── api/                  #   API client
│   ├── components/           #   UI components
│   ├── context/              #   Auth, Predictions, Results, Matches, BettingGroup, Chat, FeatureFlags state
│   ├── data/                 #   Match/team/venue data
│   ├── pages/                #   Login, MatchDetails, Chat, Waiting, Invite pages
│   └── types/                #   TypeScript types
└── public/                   # Static assets
```
