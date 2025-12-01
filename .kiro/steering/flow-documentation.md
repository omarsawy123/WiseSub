---
inclusion: always
---

# Business Flow Documentation Maintenance

## Overview

This steering file ensures that `docs/BUSINESS_FLOWS.md` is kept up-to-date whenever business flows are added or modified.

## When to Update BUSINESS_FLOWS.md

You MUST update `docs/BUSINESS_FLOWS.md` when:

1. **Adding new API endpoints** that introduce new user-facing flows
2. **Creating new background services** or workers
3. **Modifying existing flows** (adding steps, changing sequence, new components)
4. **Adding external service integrations** (new OAuth providers, notification services, etc.)
5. **Changing component interactions** (service-to-service communication)

## Update Checklist

When updating the flow documentation:

- [ ] Update the "Last Updated" date at the top of the document
- [ ] Add or modify the flow diagram using ASCII art
- [ ] Update the Components list for the affected flow
- [ ] Update the Endpoints table if API endpoints changed
- [ ] Add an entry to the Version History table at the bottom
- [ ] Ensure consistency with actual implementation

## Existing Flows

The document currently contains 10 business flows:

1. **Authentication Flow** - Google OAuth, JWT tokens, login/logout
2. **Email Account Connection Flow** - Gmail OAuth, token storage
3. **Email Scanning Flow** - Background ingestion, queue processing
4. **AI Extraction Flow** - OpenAI classification and data extraction
5. **Subscription Management Flow** - CRUD, deduplication, history
6. **Alert Generation Flow** - Renewal, price change, trial alerts
7. **Email Notification Flow** - SendGrid delivery
8. **Dashboard & Insights Flow** - Spending analytics
9. **User Data Management Flow** - GDPR export/deletion
10. **Vendor Metadata Flow** - Vendor matching, enrichment, caching

## ASCII Diagram Format

Use these characters for diagrams:
- Box drawing: `┌ ┐ └ ┘ │ ─ ├ ┤ ┬ ┴ ┼`
- Arrows: `→ ← ↑ ↓ ▶ ◀ ▲ ▼` or `─>` `<─`
- Max width: 77 characters

## Example Flow Section

```markdown
## N. New Flow Name

### Overview
Brief description of what this flow does.

### Components
- `ServiceName` - What it does
- `RepositoryName` - Data access

### Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/resource` | Get resource |

### Flow Diagram
[ASCII diagram here]
```
