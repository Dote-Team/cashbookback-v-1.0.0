@echo off
chcp 65001 >nul
rem تثبيت البرنامج الوسيط للبصمة كخدمة ويندوز تبدأ تلقائياً مع تشغيل الجهاز.
rem شغّل هذا الملف بزر الفأرة الأيمن ثم "Run as administrator".

net session >nul 2>&1
if errorlevel 1 (
    echo يجب تشغيل هذا الملف كمسؤول: زر الفأرة الأيمن ثم Run as administrator
    pause
    exit /b 1
)

set "SERVICE=CashbookFingerprintBridge"
set "EXE=%~dp0CashbookFingerprintBridge.exe"

if not exist "%EXE%" (
    echo لم يُعثر على CashbookFingerprintBridge.exe بجانب هذا الملف.
    pause
    exit /b 1
)

sc query "%SERVICE%" >nul 2>&1
if not errorlevel 1 (
    echo الخدمة مثبتة مسبقاً، يُعاد تشغيلها...
    sc stop "%SERVICE%" >nul 2>&1
    timeout /t 2 /nobreak >nul
    sc start "%SERVICE%"
    goto check
)

sc create "%SERVICE%" binPath= "\"%EXE%\"" start= auto DisplayName= "Cashbook Fingerprint Bridge"
sc description "%SERVICE%" "البرنامج الوسيط لقارئ البصمة ZK9500 - نظام الحسابات"
rem إعادة التشغيل تلقائياً إن توقفت الخدمة لأي سبب.
sc failure "%SERVICE%" reset= 86400 actions= restart/5000/restart/5000/restart/30000
sc start "%SERVICE%"

:check
timeout /t 3 /nobreak >nul
echo.
echo افتح الرابط التالي للتأكد: يجب أن يظهر "connected": true
echo http://127.0.0.1:52181/status
start "" "http://127.0.0.1:52181/status"
pause
