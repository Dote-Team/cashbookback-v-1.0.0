# اختبارات التكامل

تختبر الـ API وهو يعمل فعلاً على قاعدة بيانات تطوير: الصلاحيات، والأرشفة، والأرصدة، والبصمة، والأمان، والجلسات. لا تُشغَّل أبداً على قاعدة الإنتاج، لأنها تنشئ مستخدمين وقيوداً وتؤرشف خزنة.

## المتطلبات

قاعدة التطوير `cash` على SQL Express، وملف `.env.local` (مستثنى من Git) فيه الاتصال والأسرار وحساب السوبر أدمن وحد الدخول المرفوع للاختبارات. لإنشاء القاعدة من الصفر:

1. احذف القاعدة القديمة إن وُجدت، ثم `dotnet run --launch-profile http`: الـ API ينشئ القاعدة وحساب السوبر أدمن بنفسه.
2. `sqlcmd -S localhost\SQLEXPRESS -d cash -E -i Seed\SeedDemoData.sql -f 65001`: البيانات التجريبية، والحسابات `aaaa` و`demo1` إلى `demo8` بكلمة مرور السوبر أدمن نفسها.
3. Python 3.10 أو أحدث، دون أي حزم إضافية.

## التشغيل

```
dotnet run --launch-profile http
```

في نافذة أخرى:

```
export CASHBOOK_ROOT_USERNAME=$(grep ^AdminUser__Username= .env.local | cut -d= -f2-)
export CASHBOOK_ROOT_PASSWORD=$(grep ^AdminUser__Password= .env.local | cut -d= -f2-)
export CASHBOOK_DEMO_PASSWORD=$CASHBOOK_ROOT_PASSWORD
python tests/integration/test_permissions_archive.py
python tests/integration/test_security.py
python tests/integration/test_sessions_export.py
python tests/integration/test_reference_data.py
python tests/integration/test_idle_timeout.py
```

| الملف | ما يختبره |
|---|---|
| `test_permissions_archive.py` | توارث الصلاحيات، وعزل الخزائن (IDOR)، والأرشفة، وثبات الأرصدة، والدخول بالبصمة |
| `test_security.py` | روابط المرفقات الموقّعة، وفحص الملفات المرفوعة، وJWT، وCORS، وتحديد المعدل، وحد حجم الطلب |
| `test_sessions_export.py` | إبطال الجلسة فوراً عند الخروج أو التعطيل أو تغيير كلمة المرور، والتصدير |
| `test_reference_data.py` | أن التعديل الجزئي لا يمحو الحقول غير المرسلة، وفحص بيانات الإضافة |
| `test_idle_timeout.py` | الخروج التلقائي بعد 5 دقائق بلا نشاط، ورفض تجديد الرمز بعده. يحتاج `sqlcmd` لأنه يرجّع وقت آخر نشاط في القاعدة بدل الانتظار (`CASHBOOK_SQL_SERVER` و`CASHBOOK_SQL_DATABASE` اختياريان) |

## ملاحظات

- اختبارات البصمة في `test_permissions_archive.py` ترسل صوراً مصطنعة، فتحتاج `Fingerprint__Matcher=dev`. إن كان `.env.local` على `sourceafis` (قارئ حقيقي) فشغّل الـ API للاختبار من نسخة منه فيها `dev` عبر `DOTENV_PATH`، لأن ملف `.env` يتقدم على متغيرات البيئة.
- `test_security.py` يتحقق من حد تجديد الرمز (30 في الدقيقة). عند تشغيل كل الملفات متتالية قد يصل `test_idle_timeout.py` أو غيره إلى هذا الحد فيعيد 429؛ انتظر دقيقة بين الملفات.
- `test_permissions_archive.py` يؤرشف الخزنة 4. قبل إعادة تشغيله أرجع الخزائن المؤرشفة في قاعدة التطوير فقط:
  ```sql
  UPDATE Books SET IsArchived = 0, ArchivedAt = NULL, ArchivedByUserId = NULL, ArchiveReason = NULL WHERE IsArchived = 1;
  ```
- اختبار تحديد المعدل في `test_security.py` يستهلك حصة دقيقة كاملة من طلبات تجديد الرمز.
