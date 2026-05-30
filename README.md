# 🚦 FluxGuard — Distributed Adaptive Rate Limiting System

FluxGuard is a **production-grade, distributed and adaptive rate limiting system** built using **.NET (ASP.NET Core) and Redis**.
It protects APIs from abuse, ensures system stability under high load, and dynamically adjusts limits based on traffic and user type.

---

## 🧠 Why FluxGuard?

Modern APIs face:
* Traffic spikes
* Abuse (bots, brute-force attacks)
* Uneven load distribution
* Multi-tenant usage

FluxGuard solves this by acting as a **smart traffic controller**:
> Allow legitimate traffic ✅
> Block excessive requests ❌
> Adapt dynamically ⚡

---

## 🏗️ Architecture Overview

```text
Client
  ↓
ASP.NET Middleware (RateLimitMiddleware)
  ↓
RateLimitService (Decision Layer)
  ↓
IRateLimiter (Strategy Pattern)
  ↓
Redis (Distributed State via Lua Script)
```

---

## ⚙️ Core Features

### 🌍 Distributed Rate Limiting

* Uses Redis for shared state across multiple instances
* Ensures global consistency (no bypass via load balancer)

### 🧠 Adaptive Rate Limiting

* Supports dynamic policies:
  * Free vs Premium users
  * Per API key / user / endpoint
* Easily extendable for traffic-aware throttling

### ⚡ Token Bucket Algorithm

* Supports burst traffic
* Smooth rate control over time
* Accurate and efficient

### 🔒 Thread-Safe & Scalable

* In-memory version uses:
  * ConcurrentDictionary
  * Fine-grained locking (per client)
* Redis version uses:
  * Lua scripts for atomic operations

### 📊 Observability Ready

* Structured logging
* Debug + warning logs for allowed/denied requests

### 🛡️ Fault Tolerance

* Redis failure handling (fail-open strategy)
* Automatic reconnection

---


## 🔁 How It Works

### Step 1: Incoming Request

Middleware intercepts request and extracts `clientKey` (userId / API key / IP).

### Step 2: Policy Resolution

System determines applicable policy:

```text
Free User → Strict limits
Premium User → Higher limits
```

---

### Step 3: Rate Limit Check

Token Bucket logic:

* Each user has a bucket of tokens
* Tokens refill over time
* Each request consumes 1 token

---

### Step 4: Decision

| Condition        | Result                |
| ---------------- | --------------------- |
| Tokens available | Request allowed       |
| No tokens        | Request blocked (429) |

---

### Step 5: Response Headers

```http
X-RateLimit-Remaining: 5
Retry-After: 2
```

---

## 🧪 Example

### Request

```http
GET /api/products
X-User-Id: 123
```

---

### Allowed Response

```json
{
  "message": "Request successful"
}
```

---

### Rate Limited Response

```json
{
  "error": "Too many requests. Try again later."
}
```

---

## 🔥 Redis + Lua (Atomic Execution)

FluxGuard uses Lua scripts in Redis to ensure:
* Atomic updates (no race conditions)
* Accurate token calculation
* Distributed safety across instances

---

## ⚠️ Failure Handling

If Redis is unavailable:
* System defaults to **fail-open strategy**
* Requests are allowed to avoid system outage

---

## 🚀 Getting Started

### Prerequisites

* .NET 8+
* Redis (local or remote)

---

### Run Redis locally

```bash
docker run -p 6379:6379 redis
```

---

### Configure Connection

```json
"ConnectionStrings": {
  "Redis": "localhost:6379"
}
```

---

### Run Project

```bash
dotnet run
```

---

## 📈 Future Enhancements

* Adaptive throttling based on CPU usage
* Prometheus + Grafana monitoring
* API Gateway integration
* Multi-region rate limiting
* AI-based anomaly detection

---

## 🧠 Key Learnings

* Difference between in-memory vs distributed systems
* Importance of atomic operations
* Thread safety vs distributed consistency
* Designing scalable backend systems

---

## 🤝 Contributing

Feel free to fork and extend FluxGuard 🚀

---

## 📜 License

MIT License
