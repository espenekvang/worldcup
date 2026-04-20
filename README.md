# FIFA World Cup 2026 Tipping App

A match prediction app for the FIFA World Cup 2026. Invite friends, predict full-time scores, and compete.

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
│  │  │         API Client (JWT Auth)           │  │  │
│  │  └────────────────┬────────────────────────┘  │  │
│  └───────────────────┼───────────────────────────┘  │
└──────────────────────┼──────────────────────────────┘
                       │ HTTP :5173 → :5211
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
│  │              Controllers                      │   │
│  │  ┌──────┐  ┌─────────────┐  ┌────────────┐   │   │
│  │  │ Auth │  │ Predictions │  │  Results  │   │   │
│  │  └──┬───┘  └──────┬──────┘  └─────┬──────┘   │   │
│  └─────┼─────────────┼───────────────┼───────────┘   │
│        │             │               │               │
│  ┌─────▼─────────────▼───────────────▼───────────┐   │
│  │          EF Core + SQLite                     │   │
│  │  ┌───────┐  ┌────────────┐  ┌─────────────┐  │   │
│  │  │ Users │  │ Predictions│  │MatchResults │  │   │
│  │  └───────┘  └────────────┘  └─────────────┘  │   │
│  └───────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────┘
```

### Auth Flow

```
User → Google Sign-In → Google JWT → Backend validates →
  ├─ Admin email? → Allow, issue app JWT
  ├─ Invited to a league? → Allow, issue app JWT
  └─ Neither?     → 403 Forbidden → Waiting page
```

## Tech Stack

| Layer    | Technology                              |
|----------|-----------------------------------------|
| Frontend | React 19, TypeScript 6, Vite 8, Tailwind CSS 4 |
| Backend  | .NET 10, ASP.NET Core, EF Core          |
| Database | SQLite (local development)              |
| Auth     | Google Sign-In → JWT Bearer             |
| Testing  | Vitest + jsdom                          |

## Getting Started

### Prerequisites

- [Node.js](https://nodejs.org/) 20+
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- A [Google OAuth Client ID](https://console.cloud.google.com/apis/credentials)

### Backend Setup

```bash
cd api/WorldCup.Api

# Copy and fill in config
cp appsettings.Development.json.example appsettings.Development.json
# Edit appsettings.Development.json with your values:
#   - Jwt:Key (min 32 characters)
#   - Google:ClientId
#   - Admin:Email

# Apply migrations and run
dotnet ef database update
dotnet run
```

The API runs on `http://localhost:5211`.

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
npm test
```

## Features

- **Match Overview** — Browse all World Cup 2026 matches grouped by date and stage
- **Predictions** — Submit full-time score predictions for each match
- **Live Results** — Automatic result fetching with point calculation
- **Leaderboard** — Compete with friends based on prediction accuracy
- **Ligaer** — Opprett og administrer separate ligaer med egne medlemmer og poengtavler
- **Google Sign-In** — Secure authentication via Google accounts
- **Invitation System** — Only invited users can access the app
- **Admin Panel** — Invite/remove users, manage leagues, override knockout match teams, and manually set match results

## Project Structure

```
worldcup/
├── api/WorldCup.Api/        # .NET backend
│   ├── Controllers/          #   Auth, Predictions, Results, Invitations, BettingGroups
│   ├── Models/               #   User, Prediction, Invitation, MatchResult, BettingGroup, BettingGroupMember
│   ├── DTOs/                 #   Request/response objects
│   ├── Services/             #   ResultFetcher, Scoring, MatchSchedule
│   ├── Data/                 #   EF Core DbContext
│   └── Migrations/           #   Database migrations
├── src/                      # React frontend
│   ├── api/                  #   API client
│   ├── components/           #   UI components
│   ├── context/              #   Auth, Predictions, Results, Matches, BettingGroup state
│   ├── data/                 #   Match/team/venue data
│   ├── pages/                #   Login, GroupSelector, Waiting pages
│   └── types/                #   TypeScript types
└── public/                   # Static assets
```
