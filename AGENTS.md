# AGENTS.md

Guidance for AI coding agents and contributors working on OrderProcessor.

## 1. Project purpose

OrderProcessor is a learning project that demonstrates event-driven order processing in .NET. The API accepts an order and returns immediately. A separate background worker processes orders from a RabbitMQ queue.

The goal is for the developer (Pratik) to learn messaging by building the project himself. Agents should guide, explain, and review, not write the implementation unless explicitly asked.

## 2. Architecture

```
Client -> OrderApi (POST /order) -> RabbitMQ queue "order_placed" -> OrderWorker (BackgroundService)
                                              ^
                                              |
                                   OrderContracts (shared message type)
```

| Project          | Role                                                         |
|------------------|--------------------------------------------------------------|
| `src/OrderApi`      | ASP.NET Core minimal API. Publishes messages.             |
| `src/OrderWorker`   | Worker Service. Consumes messages with manual acks.       |
| `src/OrderContracts`| Shared message type (`OrderPlaced`). No RabbitMQ code.    |
| `docker-compose.yml`| Runs RabbitMQ (`rabbitmq:3-management`).                  |

Dependency rule: `OrderApi` and `OrderWorker` reference `OrderContracts`. `OrderContracts` must not reference RabbitMQ.

## 3. Tech stack

- .NET 10 SDK (SDK 10.0.114, runtime 10.0.14)
- RabbitMQ.Client **7.2.2** (async API only)
- RabbitMQ broker via Docker
- Solution file: `OrderProcessor.slnx` (the .slnx format, not .sln)
- Developer OS: CachyOS (Arch-based), KDE Plasma, JetBrains Rider on Linux Mint is also used per other notes

## 4. Current status

| Item                                              | Status      |
|---------------------------------------------------|-------------|
| Solution and project scaffold                     | Done        |
| `docker-compose.yml` with RabbitMQ                | Done        |
| `OrderApi` builds                                 | Done        |
| `POST /order` publishes to `order_placed`         | Done, verified (4 messages seen in Ready) |
| `OrderPlaced` record in `OrderContracts`          | Not confirmed |
| `OrderWorker` consumer (`BackgroundService`)      | In progress |
| Manual ack, prefetch 1, failure handling          | Not started |
| Idempotency (processed-message table)             | Not started |
| README                                            | Not started |

Update this table as work completes. Do not mark anything Done without the developer confirming it runs.

## 5. Current code state

### OrderApi (`src/OrderApi/Program.cs`)

- Creates one `ConnectionFactory`, one `IConnection`, and one `IChannel` at startup using top-level `await`.
- Routes: `GET /`, `GET /health`, `POST /order`.
- `POST /order` declares a durable queue `order_placed`, creates a persistent `BasicProperties`, publishes a hardcoded string, and returns `202 Accepted`.
- Known temporary behavior: the body is hardcoded (`"Order Placed!"`) and the endpoint does not read a request body. Both are planned to change to an `OrderPlaced` JSON payload.

### Credentials

Local development uses `user` / `password` in code and in `docker-compose.yml`. These are local-only values. Do not reuse them in any deployed environment, and move them to configuration before deployment.

## 6. Findings and decisions

Record new findings here as they come up.

**Environment**
- The initial build failed with `NETSDK1226` (missing Prune Package data). Root cause: the ASP.NET Core targeting pack was not installed.
- On CachyOS, install it with `sudo pacman -S aspnet-targeting-pack-10.0` (extra repo, version 10.0.14, matching the SDK). Avoid the `cachyos/aspnet-targeting-pack-bin` 10.0.15 package, which does not match the SDK.
- `apt` commands do not apply on this machine.

**RabbitMQ.Client 7.x**
- The API is async. Use `CreateConnectionAsync`, `CreateChannelAsync`, `QueueDeclareAsync`, `BasicPublishAsync`, and `BasicQosAsync`, awaiting each.
- `BasicPublishAsync` requires the `mandatory` argument. It has no default.
- Persistent messages use `BasicProperties { Persistent = true }`, passed as `basicProperties`. The queue must also be declared `durable: true`.
- Connections and channels are `IAsyncDisposable`, so use `await using`.
- Methods from 6.x (`CreateConnection`, `CreateModel`, `BasicPublish`) do not compile against 7.x.

**ASP.NET Core**
- `Results.Accepted(uri, value)` takes a URI as the first argument. Use `Results.Accepted(value: ...)` or `Results.Accepted()`.
- Route should be `/orders` per the plan. Current code uses `/order`. Decide and align the README, Bruno requests, and this file.

**Verified behavior**
- Three to four `POST /order` requests produced the matching number of Ready messages in `order_placed`, with no consumer running.

**Learning path notes**
- Delivery is at-least-once, not exactly-once. Manual acks prevent loss on crash but allow duplicates. Idempotency (Phase 5) handles duplicates.
- v1 rejects failed messages without requeue, so they are dropped. Retry and dead-letter queues are v2.

## 7. Open questions

- Is one shared `IChannel` safe for concurrent requests in `OrderApi`? Confirm in the RabbitMQ.Client 7.x docs before relying on it under load. A common approach is one channel per request or a small pool.
- Should the connection be disposed explicitly with a host lifetime hook rather than relying on top-level `await using`?
- Should `OrderPlaced` use a `decimal` amount and `DateTimeOffset` for `PlacedAt`? (Planned, not confirmed.)

## 8. Rules for agents

1. Explain before implementing. Prefer hints and checkpoints over full code, unless the developer explicitly asks for a sample.
2. Samples must be labeled as samples and kept generic (not copied into the real project).
3. Do not assume APIs. Verify method names and signatures against the installed version (7.2.2) or the docs. If unsure, say so.
4. Use the developer's preferences: short, direct answers. Fact-check before answering. Correct mistakes directly.
5. Do not commit or push without the developer's instruction.
6. Keep v1 scope. Do not add retries, DLQ, or idempotency until v1 is confirmed working.
7. Update sections 4 and 6 when the status changes.

## 9. Commands

```bash
# Start RabbitMQ
docker compose up -d

# Build everything
dotnet build

# Run the API
dotnet run --project src/OrderApi

# Run the worker
dotnet run --project src/OrderWorker

# RabbitMQ UI
# http://localhost:15672  (user / password)
```

## 10. Testing

- Manual testing with Bruno: `POST http://localhost:<port>/order`, expect `202`.
- Port is in `src/OrderApi/Properties/launchSettings.json`.
- Check queue depth in the RabbitMQ UI, Queues tab, `order_placed`.
- Automated tests (xUnit) are planned for v2.