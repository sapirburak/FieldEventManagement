# Field Event Management System

A real-time field event management system consisting of three main components:

| Component | Description | Technology |
|-----------|-------------|------------|
| **FieldEventManagement.Agent** | Edge agent that receives events from external sources and forwards them to the API | ASP.NET Core 10, SQLite |
| **FieldEventManagement.API** | Central server for managing events, users, and permissions | ASP.NET Core 10, SQL Server, SignalR |
| **field-event-app** | Graphical user interface for dispatchers and technicians | Angular 17 |

---

## Prerequisites

Before running the system, make sure you have the following tools installed:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/) and npm
- [Angular CLI 17](https://angular.io/cli): `npm install -g @angular/cli@17`
- **SQL Server** – required for the API (see Connection String configuration below)

---

## Architecture & Ports

```
[External Source / Field Device]
        |
        | POST /api/agent/events  (X-Api-Key header)
        ▼
[Agent  :5093 / :7256]  ──── SQLite (local) ────►  [API  :5279 / :7257]
                                                            |
                                                     SQL Server DB
                                                            |
                                                     SignalR EventHub
                                                            |
                                                   [Angular :4200]
```

---

## Database Configuration (SQL Server)

Open `src/FieldEventManagement.API/appsettings.json` and update the Connection String:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=<server-name>;Database=FieldEventManagement;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

> The database schema is created automatically on first run via `EnsureCreated`.
> No manual migrations needed.

---

## Running the System

All three components must be started **simultaneously**, each in a separate terminal window.

### 1. API – Central Server

```powershell
cd src\FieldEventManagement.API
dotnet run
```

The server listens on:
- `http://localhost:5279`
- `https://localhost:7257`

### 2. Agent – Field Edge Service

```powershell
cd src\FieldEventManagement.Agent
dotnet run
```

The agent listens on:
- `http://localhost:5093`
- `https://localhost:7256`

> **Agent settings** are located in `src/FieldEventManagement.Agent/appsettings.json`:
> ```json
> "AgentSettings": {
>   "BackendUrl": "https://localhost:7257",
>   "ExpectedApiKey": "SuperSecretFieldAgentKey2026"
> }
> ```

### 3. Angular – User Interface

```powershell
cd field-event-app
npm install
npm start
```

The application opens at `http://localhost:4200`.

---

## Sending a Field Event (Example)

To simulate an event from the field, send a `POST` to the agent with the API key in the header:

```http
POST http://localhost:5093/api/agent/events
X-Api-Key: SuperSecretFieldAgentKey2026
Content-Type: application/json

{
  "title": "Power Failure",
  "description": "Power outage at 10 Herzl Street",
  "source": "FieldSensor-01",
  "location": "Tel Aviv"
}
```

The agent returns `202 Accepted` immediately — the event is saved to SQLite and forwarded to the API asynchronously.

---

## Adding Initial Users

The database is created empty. Before logging in, insert base users manually using SQL Server Management Studio (SSMS) or any query tool:

```sql
USE FieldEventManagement;

INSERT INTO Users (Id, Username, PasswordHash, Role) VALUES
  (NEWID(), 'dispatcher1', 'Password123', 'Scheduler'),
  (NEWID(), 'tech1',       'Password123', 'Technician'),
  (NEWID(), 'tech2',       'Password123', 'Technician');
```

> **Note:** The `PasswordHash` field is stored as plain text in the current implementation (for testing purposes only). In a production environment, use `PasswordHasher`.

---

## Authentication (Login)

The API is protected by JWT. To obtain a token:

```http
POST http://localhost:5279/api/auth/login
Content-Type: application/json

{
  "username": "<username>",
  "password": "<password>"
}
```

The returned token must be sent in the `Authorization: Bearer <token>` header for all protected requests.

---

## Running Tests

### Unit Tests – Core (xUnit)

```powershell
cd src\FieldEventManagement.Core.Tests
dotnet test
```

### Tests – Angular (Karma)

```powershell
cd field-event-app
npm test
```

### Run All .NET Tests at Once

```powershell
cd src
dotnet test
```

---

## Project Structure

```
FieldEventManagementNew/
├── src/
│   ├── FieldEventManagement.Core/           # Domain – Entities, Interfaces, State Machine
│   ├── FieldEventManagement.Application/    # Application Services, DTOs
│   ├── FieldEventManagement.Infrastructure/ # EF Core, SQL Server, SignalR, JWT
│   ├── FieldEventManagement.API/            # ASP.NET Core Web API (Central Server)
│   ├── FieldEventManagement.Agent/          # ASP.NET Core Agent (Edge Service + SQLite)
│   └── FieldEventManagement.Core.Tests/     # Unit Tests – Core
└── field-event-app/                         # Angular 17 Frontend
```

---

## Key Features

- **State Machine** – Precisely defined state transitions (`Unassigned → Assigned → InProgress → Completed / Cancelled`)
- **Store-and-Forward** – The agent saves events to SQLite and forwards them after connectivity is restored
- **Real-Time** – Direct browser updates via SignalR (`/EventHub`)
- **Retry & Resilience** – The agent retries with Exponential Backoff without limit until success: failure 1 → 10s | failure 2 → 30s | failure 3 → 60s | failure 4+ → 5 minutes (cap). Success resets the counter
- **Audit Trail** – Every state change is recorded with a timestamp and the acting user's ID
