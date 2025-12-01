# WiseSub Pricing Tiers Plan

> **Last Updated**: December 1, 2025  
> **Status**: Planning

## Overview

WiseSub offers three subscription tiers designed to provide clear value progression from free manual tracking to full AI-powered subscription management with cancellation assistance.

## Pricing Summary

| Tier | Monthly | Annual (20% off) | Target User |
|------|---------|------------------|-------------|
| **Free** | $0 | - | Casual users, trial |
| **Pro** | $15 | $144/year ($12/mo) | Individuals with 5+ subscriptions |
| **Premium** | $25 | $240/year ($20/mo) | Power users, families |

---

## Tier 1: Free - $0/month

**Tagline**: *"Manual subscription tracking"*

**Target**: Users who want to try the product before committing, or those with few subscriptions.

### Limits
| Resource | Limit |
|----------|-------|
| Email Accounts | 1 (connect but no AI scanning) |
| Tracked Subscriptions | 5 |

### Features

| Category | Feature | Included |
|----------|---------|----------|
| **Entry** | Manual Subscription Entry | ✅ |
| **Entry** | AI Email Scanning | ❌ |
| **Entry** | Initial 12-month Scan | ❌ |
| **Dashboard** | Basic Dashboard | ✅ |
| **Dashboard** | Category Grouping | ✅ |
| **Dashboard** | Monthly Spending Total | ✅ |
| **Dashboard** | Advanced Filters | ❌ |
| **Alerts** | Renewal Alerts (7-day) | ✅ |
| **Alerts** | Renewal Alerts (3-day) | ❌ |
| **Alerts** | Price Change Alerts | ❌ |
| **Alerts** | Trial Ending Alerts | ❌ |
| **Alerts** | Unused Subscription Alerts | ❌ |
| **Alerts** | Custom Alert Timing | ❌ |
| **Insights** | Spending by Category | ❌ |
| **Insights** | Renewal Timeline | ❌ |
| **Insights** | Spending Benchmarks | ❌ |
| **Tools** | Cancellation Assistant | ❌ |
| **Tools** | PDF Export | ❌ |
| **Tools** | Savings Tracker | ❌ |
| **Support** | Email Support | Community |
| **Data** | GDPR Data Export | ✅ |

---

## Tier 2: Pro - $15/month

**Tagline**: *"Never miss a renewal again"*

**Target**: Individuals with multiple subscriptions who want automatic tracking.

### Limits
| Resource | Limit |
|----------|-------|
| Email Accounts | 3 |
| Tracked Subscriptions | Unlimited |

### Features

| Category | Feature | Included |
|----------|---------|----------|
| **Entry** | Manual Subscription Entry | ✅ |
| **Entry** | AI Email Scanning | ✅ |
| **Entry** | Initial 12-month Scan | ✅ |
| **Dashboard** | Full Dashboard | ✅ |
| **Dashboard** | Category Grouping | ✅ |
| **Dashboard** | Monthly Spending Total | ✅ |
| **Dashboard** | Advanced Filters | ✅ |
| **Alerts** | Renewal Alerts (7-day) | ✅ |
| **Alerts** | Renewal Alerts (3-day) | ✅ |
| **Alerts** | Price Change Alerts | ✅ |
| **Alerts** | Trial Ending Alerts | ✅ |
| **Alerts** | Unused Subscription Alerts | ✅ |
| **Alerts** | Custom Alert Timing | ❌ |
| **Insights** | Spending by Category | ✅ |
| **Insights** | Renewal Timeline (12 months) | ✅ |
| **Insights** | Spending Benchmarks | ❌ |
| **Tools** | Cancellation Assistant | ❌ |
| **Tools** | PDF Export | ✅ (Monthly) |
| **Tools** | Savings Tracker | ✅ |
| **Support** | Email Support | 48h response |
| **Data** | GDPR Data Export | ✅ |

---

## Tier 3: Premium - $25/month

**Tagline**: *"Complete subscription control"*

**Target**: Power users, families, anyone who wants full automation including cancellation.

### Limits
| Resource | Limit |
|----------|-------|
| Email Accounts | Unlimited |
| Tracked Subscriptions | Unlimited |

### Features

| Category | Feature | Included |
|----------|---------|----------|
| **Entry** | Manual Subscription Entry | ✅ |
| **Entry** | AI Email Scanning | ✅ |
| **Entry** | Initial 12-month Scan | ✅ |
| **Entry** | Real-time Scanning | ✅ |
| **Dashboard** | Full Dashboard | ✅ |
| **Dashboard** | Category Grouping | ✅ |
| **Dashboard** | Monthly Spending Total | ✅ |
| **Dashboard** | Advanced Filters | ✅ |
| **Dashboard** | Custom Categories | ✅ |
| **Alerts** | Renewal Alerts (7-day) | ✅ |
| **Alerts** | Renewal Alerts (3-day) | ✅ |
| **Alerts** | Price Change Alerts | ✅ |
| **Alerts** | Trial Ending Alerts | ✅ |
| **Alerts** | Unused Subscription Alerts | ✅ |
| **Alerts** | Custom Alert Timing | ✅ |
| **Alerts** | Daily Digest Option | ✅ |
| **Insights** | Spending by Category | ✅ |
| **Insights** | Renewal Timeline (12 months) | ✅ |
| **Insights** | Spending Benchmarks | ✅ |
| **Insights** | Spending Forecasts | ✅ |
| **Tools** | Cancellation Assistant | ✅ |
| **Tools** | Cancellation Templates | ✅ |
| **Tools** | PDF Export | ✅ (Unlimited) |
| **Tools** | Savings Tracker | ✅ |
| **Tools** | Duplicate Detection | ✅ |
| **Support** | Priority Support | 24h response |
| **Data** | GDPR Data Export | ✅ |

---

## Implementation Requirements

### Backend Changes
1. Update `SubscriptionTier` enum: Free, Pro, Premium
2. Update `TierService` with new limits and features
3. Add `TierFeature` flags for each feature
4. Implement feature gating in services
5. Add Stripe integration for payments

### Database Changes
1. Add `PriceId` field for Stripe
2. Add `SubscriptionStartDate`, `SubscriptionEndDate`
3. Add `IsAnnual` flag

### API Changes
1. Add pricing endpoints
2. Add Stripe webhook handlers
3. Add upgrade/downgrade endpoints

### Frontend Changes
1. Pricing page with tier comparison
2. Upgrade prompts when limits hit
3. Billing management page
