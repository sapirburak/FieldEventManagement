# Bonus Question — Offline Mode Support

> **Version:** 1.0 | **Date:** July 2026

---

## Problem Definition

A field technician may enter an area without network coverage, continue working (changing statuses, adding notes), and return to coverage after an unknown period of time. All actions performed while disconnected must reach the server — while handling conflicts against changes made concurrently by the dispatcher.

---

## 1. Local Storage on the Client Side

```
Technician's Browser
├── Service Worker         ← intercepts all network requests, enables PWA
├── Cache API              ← stores Angular files (JS, CSS, HTML)
└── IndexedDB              ← stores structured data
    ├── events             ← events assigned to the technician
    ├── offline_queue      ← actions performed while offline, awaiting dispatch
    └── sync_metadata      ← timestamp of last sync
```

**Why IndexedDB and not LocalStorage:**

| Criterion | LocalStorage | IndexedDB |
|-----------|:-----------:|:---------:|
| Maximum size | ~5MB | Hundreds of MB |
| Data structure | String only | Structured objects |
| Search/index | ❌ | ✅ |
| Asynchronous operation | ❌ | ✅ |
| Access from Service Worker | ❌ | ✅ |

Every action the technician performs while offline is not sent to the server — it is recorded as a **Command** in the queue:

```json
{
  "id": "uuid-local-1",
  "type": "UpdateStatus",
  "eventId": "event-abc",
  "payload": { "newStatus": "InProgress" },
  "timestamp": "2026-07-11T18:32:00Z",
  "clientVersion": 3
}
```

The `clientVersion` is the event version the technician last saw — critical for conflict detection.

---

## 2. Sync on Reconnection

```
Technician regains coverage
        │
        ▼
Service Worker detects network (online event)
        │
        ▼
Step A: Pull — GET /api/events?assignedTo=me&since=<last_sync>
        │
        ▼
Step B: Conflict Detection — compare clientVersion to server version
        │
        ▼
Step C: Push — POST /api/sync (batch of all offline_queue entries)
        │
        ▼
Step D: Resolve — handle conflicts (automatic or manual)
        │
        ▼
Step E: Clear Queue — flush offline_queue
```

**Sync request format:**

```http
POST /api/sync
Authorization: Bearer <jwt>

{
  "lastSyncTimestamp": "2026-07-11T18:00:00Z",
  "operations": [
    { "type": "UpdateStatus", "eventId": "abc", "clientVersion": 3, "payload": { "newStatus": "InProgress" } },
    { "type": "AddNote",      "eventId": "abc", "clientVersion": 3, "payload": { "text": "Door is locked" } }
  ]
}
```

**Server response:**

```json
{
  "accepted": ["uuid-local-2"],
  "conflicts": [
    {
      "operationId": "uuid-local-1",
      "serverVersion": 5,
      "serverState": { "status": "Completed", "updatedBy": "dispatcher@company.com" },
      "clientState": { "status": "InProgress" }
    }
  ],
  "serverUpdates": []
}
```

---

## 3. Conflict Resolution

A **conflict** occurs when the technician's event version differs from the server's — meaning someone else changed the event while the technician was disconnected.

### Three Approaches

**Approach 1 — Server-Wins:** The server always wins; the technician's changes are discarded. Simple to implement, poor user experience.

**Approach 2 — Last-Write-Wins (timestamp):** Whoever changed last wins. **Problematic** — device clocks are not synchronized (clock drift).

**Approach 3 — Smart Merge by Operation Type (Recommended):**

| Operation Type | Approach | Rationale |
|----------------|----------|-----------|
| `AddNote` | Automatic merge ✅ | Append-only — both notes are kept |
| `UpdateStatus` | Alert the technician ⚠️ | Single State Machine — human decision required |

**Concrete scenario:**

```
Server (after technician disconnected):
  Dispatcher moved event → Completed (version 5)

Technician (offline):
  Changed event → InProgress (knew about version 3)

On reconnection:
  ├── Completed → InProgress? Not valid in State Machine
  ├── Server rejects
  └── UI shows: "This event was marked Completed by the dispatcher while you were offline"
              [Accept] [Contact Dispatcher]
```

---

## 4. Required System Changes

| Component | Change |
|-----------|--------|
| **Angular** | Service Worker + IndexedDB wrapper + Sync Manager |
| **Angular** | Optimistic UI — update UI immediately before server confirmation |
| **API** | Add `POST /api/sync` endpoint accepting a batch of operations |
| **API** | Add `ETag` / `RowVersion` to each event |
| **Domain** | `FieldEvent` holds `RowVersion` for version identification |
| **DB** | Add `RowVersion` column to the Events table |
| **SignalR** | Send updates to the technician with version for storage in IndexedDB |

---

> **Core Principle:** The secret of Offline support is not "storing data" — it is **storing operations** (Commands), syncing by version rather than by clock, and deciding upfront what merges automatically and what requires human intervention.
