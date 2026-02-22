# Database Import/Export Issues Fixed - 2026-02-20

## Summary

Fixed multiple critical issues preventing database schema import/export:

###1. **Character Set Issue** (COMMENT clauses with invalid UTF8)
**Error**: `ERROR 4088: Comment for field contains an invalid utf8mb3 character string`
**Fix**: Added `RemoveInvalidComments()` method to strip COMMENT clauses from CREATE TABLE statements
**Location**: DatabaseService.cs lines ~805-813

### 2. **Functions/Procedures Tab-Separated Format Issue**  
**Error**: `ERROR 1064: You have an error in your SQL syntax; check the manual...near 'NO_AUTO_VALUE_ON_ZERO\tCREATE FUNCTION'`
**Root Cause**: SHOW CREATE FUNCTION/PROCEDURE returns 6 columns separated by tabs:
- Column 1: Function/Procedure name
- Column 2: sql_mode
- Column 3: CREATE statement (THIS IS WHAT WE NEED)
- Column 4: character_set_client
- Column 5: collation_connection
- Column 6: Database Collation

**Fix**: Extract column 3 (index 2) specifically, not just "skip first 2 and join the rest"
**Location**: DatabaseService.cs lines ~232-247 (Functions) and ~264-279 (Procedures)

### 3. **Import Failure Detection**
**Error**: Import showed "Success" but created 0 tables
**Fix**: 
- `ExecuteMySqlInputAsync` now throws exceptions on MySQL errors
- `ImportDatabaseAsync` validates actual table count after import
- Proper error reporting in result.Errors
**Location**: DatabaseService.cs lines ~595-640, ~520-590

### 4. **Database Name Case Sensitivity**
**Error**: Views/Functions referencing `sst` (lowercase) failed to import
**Fix**: Replace both `SST` and `sst` during import
**Location**: DatabaseService.cs line ~511

## Modified Files

1. **src/SST.StockImport.Services/DatabaseService.cs**
   - Added: `RemoveInvalidComments()` method
   - Added: `GetTableCountAsync()` method  
   - Modified: Functions export logic (extract column 2)
   - Modified: Procedures export logic (extract column 2)
   - Modified: Table export logic (call RemoveInvalidComments)
   - Modified: Schema import logic (call RemoveInvalidComments, replace lowercase db name)
   - Modified: ExecuteMySqlInputAsync (throw on errors)
   - Modified: ImportDatabaseAsync (verify table count, better error handling)

2. **test-database-import.ps1**
   - Complete rewrite using English to avoid encoding issues
   - Added detailed verification steps
   - Added error reporting

## Testing Steps

### Step 1: Export clean backup (with all fixes)

```powershell
$exportBody = '{"sourceDatabase":"sst","targetPath":"D:\\DBbackup\\OWN\\clean_backup"}' 
Invoke-RestMethod -Uri "http://localhost:5008/api/database/export" `
    -Method POST `
    -Body $exportBody `
    -ContentType "application/json" `
    -TimeoutSec 600
```

**Expected**: 64 tables, 60 functions, 10 procedures, 143 views exported successfully
**Schema file size**: ~354KB (down from 383KB due to removed COMMENTs)

### Step 2: Verify schema file quality

```powershell
$schemaFile = "D:\DBbackup\OWN\clean_backup\DBSchema\sst_schema.sql"
$content = Get-Content $schemaFile -Raw

# Check for residual tabs in Functions/Procedures
$content | Select-String "sql_mode.*?\\t.*?CREATE (FUNCTION|PROCEDURE)" -AllMatches

# Should return NO matches (if clean)
```

### Step 3: Test import

```powershell
.\test-database-import.ps1 `
    -BackupPath "D:\DBbackup\OWN\clean_backup" `
    -TargetDatabase "sst_test"
```

**Expected output**:
```
[4/5] Executing database import...
Import Response:
  Success: True
  Message: Successfully imported 64 tables to sst_test (verified 64 tables exist)
  TablesImported: 64

[5/5] Verifying import results...
   Actual table count: 64
   OK: Table count verified
   Views: 143
   Functions: 60
   Procedures: 10

SUCCESS: Import completed successfully!
```

## Fallback Manual Verification

If automated test fails, verify manually:

```powershell
# 1. Check schema file has no tabs in wrong places
$mysql = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$schema = "D:\DBbackup\OWN\clean_backup\DBSchema\sst_schema.sql"

# Count CREATE statements
(Get-Content $schema) | Select-String "^CREATE TABLE" | Measure-Object | Select-Object Count
(Get-Content $schema) | Select-String "^CREATE.*?FUNCTION" | Measure-Object | Select-Object Count
(Get-Content $schema) | Select-String "^CREATE.*?PROCEDURE" | Measure-Object | Select-Object Count
(Get-Content $schema) | Select-String "^CREATE.*?VIEW" | Measure-Object | Select-Object Count

# Expected: 64 tables, 60 functions, 10 procedures, 143 views

# 2. Import manually
& $mysql -u root -e "DROP DATABASE IF EXISTS sst_manual_test"
& $mysql -u root -e "CREATE DATABASE sst_manual_test CHARACTER SET utf8 COLLATE utf8_general_ci"
& $mysql -u root --default-character-set=utf8 sst_manual_test < $schema

# 3. Verify
& $mysql -u root -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = 'sst_manual_test' AND TABLE_TYPE = 'BASE TABLE'"
```

## Known Limitations

1. **COMMENT clauses removed**: Field comments are stripped to avoid UTF8 encoding issues. This is documentation-only and doesn't affect functionality.

2. **Parallel export performance**: Currently implemented for data export (small/medium/large table categorization), but not tested due to terminal session issues.

3. **Large table timeouts**: Tables > 100MB export sequentially with 30-minute timeout each. May need tuning for very large databases.

## Files Created/Modified

- [x] src/SST.StockImport.Services/DatabaseService.cs (core fixes)
- [x] test-database-import.ps1 (testing script)
- [x] DATABASE-IMPORT-FIX-GUIDE.md (comprehensive documentation)
- [x] BACKUP-PERFORMANCE-OPTIMIZATION.md (parallel export guide)
- [x] This file (session summary)

## Next Session Priorities

1. **Test complete export/import cycle** with fresh terminal session
2. **Verify Views/Functions/Procedures** all import correctly
3. **Measure backup performance** with parallel export
4. **Update HANDOFF_CHECKLIST.md** with new database backup/restore status

---

**Status**: Code fixes complete, compilation successful, API running
**Blocked**: Terminal session issues preventing final end-to-end test
**Recommendation**: Start fresh PowerShell session and run test-database-import.ps1
