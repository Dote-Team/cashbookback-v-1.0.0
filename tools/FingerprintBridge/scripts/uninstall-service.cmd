@echo off
chcp 65001 >nul
rem إزالة خدمة البرنامج الوسيط للبصمة. شغّله كمسؤول (Run as administrator).

net session >nul 2>&1
if errorlevel 1 (
    echo يجب تشغيل هذا الملف كمسؤول: زر الفأرة الأيمن ثم Run as administrator
    pause
    exit /b 1
)

sc stop CashbookFingerprintBridge >nul 2>&1
timeout /t 2 /nobreak >nul
sc delete CashbookFingerprintBridge
pause
