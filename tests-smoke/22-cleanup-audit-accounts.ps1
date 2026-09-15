# ============================================================================
#  Removes ONLY the throwaway accounts created by the smoke tests and the
#  security audits, plus the isolated test tenants they own.
#
#  Targets users whose Username starts with: tmp  /  usr  /  dual
#  and businesses whose EVERY member is one of those users.
#
#  Real accounts (superadmin, owner1, owner2, partner1, cashier1, cashier2,
#  viewer1, staff1) and their businesses are never touched.
#
#  Usage:  & .\tests-smoke\22-cleanup-audit-accounts.ps1
# ============================================================================

$ErrorActionPreference = 'Stop'
$server = 'localhost\SQLEXPRESS'
$database = 'cash'
$uploads = Join-Path $PSScriptRoot '..\wwwroot\uploads'
$fileList = Join-Path $env:TEMP 'orphan-uploads.txt'

function Sql([string]$query) {
    $out = sqlcmd -S $server -d $database -E -h -1 -W -Q $query
    return ($out | Where-Object { $_ -and $_ -notmatch 'rows affected' })
}

Write-Host "`n--- test accounts found ---" -ForegroundColor Cyan
Sql "SET NOCOUNT ON; SELECT CONVERT(varchar(40), Username) FROM Users WHERE Username LIKE 'tmp%' OR Username LIKE 'usr%' OR Username LIKE 'dual%' ORDER BY Username;" |
    ForEach-Object { Write-Host "  $_" }

Write-Host "`n--- test tenants found ---" -ForegroundColor Cyan
Sql @"
SET NOCOUNT ON;
DECLARE @Ids TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Ids (Id) SELECT Id FROM Users
WHERE Username LIKE 'tmp%' OR Username LIKE 'usr%' OR Username LIKE 'dual%';

SELECT CONVERT(varchar(60), b.Name) + '  (' + CONVERT(varchar(36), b.Id) + ')'
FROM Businesses b
WHERE NOT EXISTS (SELECT 1 FROM BusinessUsers bu WHERE bu.BusinessId = b.Id AND bu.UserId NOT IN (SELECT Id FROM @Ids));
"@ | ForEach-Object { Write-Host "  $_" }

# collect attachment file names BEFORE the rows disappear
Sql @"
SET NOCOUNT ON;
DECLARE @Ids TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Ids (Id) SELECT Id FROM Users
WHERE Username LIKE 'tmp%' OR Username LIKE 'usr%' OR Username LIKE 'dual%';

DECLARE @Bks TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Bks (Id)
SELECT bk.Id FROM Books bk
WHERE bk.BusinessId IN (
    SELECT b.Id FROM Businesses b
    WHERE NOT EXISTS (SELECT 1 FROM BusinessUsers bu WHERE bu.BusinessId = b.Id AND bu.UserId NOT IN (SELECT Id FROM @Ids))
);

SELECT CONVERT(varchar(400), a.Files)
FROM Attachements a
WHERE a.TransactionId IN (SELECT Id FROM Transactions WHERE BookId IN (SELECT Id FROM @Bks))
  AND a.Files IS NOT NULL AND a.Files <> '';
"@ | Out-File -Encoding ascii $fileList

Write-Host "`n--- deleting (FK-safe order) ---" -ForegroundColor Cyan
Sql @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @Ids TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Ids (Id) SELECT Id FROM Users
WHERE Username LIKE 'tmp%' OR Username LIKE 'usr%' OR Username LIKE 'dual%';

DECLARE @Biz TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Biz (Id)
SELECT b.Id FROM Businesses b
WHERE NOT EXISTS (SELECT 1 FROM BusinessUsers bu WHERE bu.BusinessId = b.Id AND bu.UserId NOT IN (SELECT Id FROM @Ids));

DECLARE @Bks TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Bks (Id) SELECT Id FROM Books WHERE BusinessId IN (SELECT Id FROM @Biz);

-- transactions in the doomed books, PLUS any authored by a doomed user
DECLARE @Txs TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Txs (Id) SELECT Id FROM Transactions WHERE BookId IN (SELECT Id FROM @Bks);
INSERT INTO @Txs (Id)
SELECT t.Id FROM Transactions t WHERE t.UserId IN (SELECT Id FROM @Ids)
  AND NOT EXISTS (SELECT 1 FROM @Txs x WHERE x.Id = t.Id);

DELETE FROM Attachements        WHERE TransactionId IN (SELECT Id FROM @Txs);
DELETE FROM CustomFieldValues   WHERE TransactionId IN (SELECT Id FROM @Txs);
DELETE FROM TransactionHistories WHERE TransactionId IN (SELECT Id FROM @Txs) OR BookId IN (SELECT Id FROM @Bks) OR UserId IN (SELECT Id FROM @Ids);
DELETE FROM Transactions        WHERE Id IN (SELECT Id FROM @Txs);
DELETE FROM CustomFields        WHERE BookId IN (SELECT Id FROM @Bks);
DELETE FROM ExchangeRates       WHERE BookId IN (SELECT Id FROM @Bks) OR SetByUserId IN (SELECT Id FROM @Ids);
DELETE FROM Settings            WHERE BookId IN (SELECT Id FROM @Bks);
DELETE FROM Books               WHERE Id IN (SELECT Id FROM @Bks);
DELETE FROM Categories          WHERE BusinessId IN (SELECT Id FROM @Biz);
DELETE FROM Contacts            WHERE BusinessId IN (SELECT Id FROM @Biz);
DELETE FROM PaymentMethods      WHERE BusinessId IN (SELECT Id FROM @Biz);
DELETE FROM BusinessUsers       WHERE BusinessId IN (SELECT Id FROM @Biz) OR UserId IN (SELECT Id FROM @Ids);
DELETE FROM Businesses          WHERE Id IN (SELECT Id FROM @Biz);
DELETE FROM Sessions            WHERE UserId IN (SELECT Id FROM @Ids);
DELETE FROM Users               WHERE Id IN (SELECT Id FROM @Ids);

COMMIT;

SELECT 'leftover test users: ' + CONVERT(varchar(10), COUNT(*)) FROM Users
WHERE Username LIKE 'tmp%' OR Username LIKE 'usr%' OR Username LIKE 'dual%';
"@ | ForEach-Object { Write-Host "  $_" }

Write-Host "`n--- removing orphaned upload files ---" -ForegroundColor Cyan
$files = Get-Content $fileList -ErrorAction SilentlyContinue |
    Where-Object { $_ -and $_.Trim() -ne '' -and $_ -notmatch 'rows affected' }
$removed = 0
foreach ($f in $files) {
    $name = [System.IO.Path]::GetFileName($f.Trim())   # traversal guard
    if (-not $name) { continue }
    $path = Join-Path $uploads $name
    if (Test-Path $path) { Remove-Item $path -Force; $removed++ }
}
Write-Host "  removed $removed file(s)"

Write-Host "`n--- accounts that remain ---" -ForegroundColor Green
Sql "SET NOCOUNT ON; SELECT CONVERT(varchar(40), u.Username) + '  |  ' + ISNULL((SELECT TOP 1 bu.Role FROM BusinessUsers bu WHERE bu.UserId = u.Id), '-') + '  |  ' + ISNULL(u.Email, '-') FROM Users u ORDER BY u.Username;" |
    ForEach-Object { Write-Host "  $_" }

Write-Host "`nDone.`n" -ForegroundColor Green
