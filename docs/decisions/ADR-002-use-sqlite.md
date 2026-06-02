# ADR-002: Use SQLite for Local Database & Persistence

## Status
Accepted

---

## Context
The application is a standalone desktop engineering tool. It requires storage for catalog datasets (crane specifications, manufacturer load charts) and user planning records (site layouts, obstacles, green zones, and elements). Because this application runs locally on the user's computer without requiring an internet connection or corporate server setup, we need a lightweight, serverless relational database engine.

---

## Decision
We chose **SQLite** as the database engine, managed through **Entity Framework Core (EF Core 9)**.

---

## Consequences

### Positive
* **Zero Configuration:** Users do not need to install or maintain a SQL server instance. The database is a single file generated dynamically at `%APPDATA%/CraneOptimizer/craneoptimizer.db`.
* **Standard SQL Queries:** Allows us to use standard LINQ queries and ORM relationships via EF Core.
* **Easy Portability:** Database files can be easily backed up, copied, or moved.
* **Cascade Deletes:** Supports standard database cascade delete triggers.

### Negative
* **Concurrency Limitations:** SQLite does not support highly concurrent write operations. Because of this, simultaneous queries from background GA solver threads must be managed carefully to avoid locking.
* **Migration Overhead:** Schema modifications require creating and compiling EF Core migration files.
