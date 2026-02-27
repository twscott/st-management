# 2026-02-27-date-data-query-fix-design.md

## Overview

Fix incorrect column names in DateDataService statistics query.

---

## Problem

The `GetStatsByTypeAsync` method in `DateDataService.cs` uses wrong column name `ClosePrice`:
- `weekall` table uses `EndPrice` for closing price
- `tradedata` table uses `StockPrice` for closing price

This causes the statistics query to return 0 for all values.

---

## Solution

Modify the SQL query to use correct column names based on table name:
- For `weekall`: use `EndPrice`
- For `tradedata`: use `StockPrice`

---

## Changes

**File:** `src/SST.StockImport.Services/DateDataService.cs`

Modify `GetStatsByTypeAsync` method to determine correct price column based on table name.
