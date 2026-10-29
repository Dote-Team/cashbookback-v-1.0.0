# البرنامج الوسيط لقارئ البصمة ZK9500

المتصفح لا يستطيع الوصول إلى قارئ USB مباشرة. هذا البرنامج الصغير يعمل على **كل جهاز عليه قارئ بصمة**، يلتقط صورة البصمة ويعيدها لصفحة الدخول. المطابقة لا تتم هنا، بل في الخادم دائماً.

```
[ZK9500] ─USB─ [هذا البرنامج: 127.0.0.1:52181] ←─ [المتصفح] ─HTTPS→ [الخادم: المطابقة SourceAFIS]
```

## التثبيت على جهاز المستخدم (عند وصول الجهاز)

1. **ثبّت تعريف القارئ** من ZKTeco (ZKFinger SDK for Windows). هذا يضع `libzkfp.dll` (نسخة 64-bit) في `C:\Windows\System32`.
2. **وصّل القارئ**. يجب أن يضيء القارئ وأن يظهر في مدير الأجهزة.
3. **ابنِ البرنامج** على جهاز التطوير:
   ```
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
   ```
   ثم انسخ مجلد `publish` إلى جهاز المستخدم، مثلاً إلى `C:\CashbookBridge`.
4. **اضبط `appsettings.json`** بجانب الملف التنفيذي: ضع عنوان الواجهة الفعلي في `AllowedOrigins`. لا يقبل البرنامج أي صفحة غير مدرجة هنا.
5. **جرّبه**: شغّل `CashbookFingerprintBridge.exe`، ثم افتح `http://127.0.0.1:52181/status` في المتصفح. يجب أن يظهر `"connected": true` مع أبعاد الصورة.
6. **ثبّته كخدمة** يبدأ تلقائياً مع ويندوز (من موجّه أوامر بصلاحيات المدير):
   ```
   sc create CashbookFingerprintBridge binPath= "C:\CashbookBridge\CashbookFingerprintBridge.exe" start= auto
   sc start CashbookFingerprintBridge
   ```

## إعداد الخادم

في ملف `.env` للخادم:
```
Fingerprint__EncryptionKey=<ولّده بـ: dotnet cashbook.dll --generate-secret>
Fingerprint__Matcher=sourceafis
```
تحقق من الجاهزية: `GET /API/Fingerprint/status` يجب أن يعيد `"available": true`.

## أول تجربة بالجهاز الفعلي (معايرة)

1. سجّل الدخول بكلمة المرور، ثم سجّل بصمة إصبعك من صفحة «بصمتي» (ثلاث لمسات).
2. سجّل الخروج وادخل بالبصمة عدة مرات، وجرّب إصبعاً آخر غير مسجّل للتأكد من رفضه.
3. حد التطابق الافتراضي `Fingerprint__MatchThreshold=40`. إن رُفض صاحب البصمة كثيراً فخفّضه قليلاً (مثلاً 30). وإن أردت تشدداً أكبر فارفعه (مثلاً 60).

## التجربة بلا جهاز (للتطوير فقط)

- البرنامج: `CashbookFingerprintBridge.exe --simulate`. يعيد صورة ثابتة لكل اسم إصبع (`/capture?finger=a`).
- الخادم في بيئة Development: `Fingerprint__Matcher=dev`. هذا المحرّك يطابق الصور المتطابقة تماماً فقط، ويرفض الخادم تشغيله في الإنتاج.

## المسارات

| المسار | الوصف |
|---|---|
| `GET /status` | حالة القارئ: متصل أم لا، أبعاد الصورة، رسالة الخطأ |
| `POST /capture?timeoutSeconds=15` | ينتظر وضع الإصبع ويعيد `{width, height, dpi, pixels}`. الحقل `pixels` صورة رمادية خام بصيغة Base64 |

## استكشاف الأخطاء

| الرسالة | الحل |
|---|---|
| `libzkfp.dll غير موجودة` | ثبّت تعريف ZK9500. البرنامج 64-bit فيحتاج نسخة 64-bit من المكتبة |
| `لا يوجد قارئ بصمة متصل` | افحص كابل USB، وجرّب منفذاً آخر |
| `تعذّر فتح القارئ` | أغلق أي برنامج آخر يستخدم القارئ (مثل برنامج ZKTeco التجريبي) |
| `origin not allowed` | أضف عنوان الواجهة إلى `AllowedOrigins` |
| الدخول بالبصمة يرفض صاحبها | ضع الإصبع في منتصف القارئ، أو أعد تسجيله، أو خفّض `MatchThreshold` |
