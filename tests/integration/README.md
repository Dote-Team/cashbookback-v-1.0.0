# اختبارات التكامل

تختبر الـ API وهو يعمل فعلاً على قاعدة بيانات تطوير: الصلاحيات، والأرشفة، والأرصدة، والبصمة، والأمان، والجلسات. لا تُشغَّل أبداً على قاعدة الإنتاج، لأنها تنشئ مستخدمين وقيوداً وتؤرشف خزنة.

## المتطلبات

1. قاعدة تطوير فيها البيانات التجريبية (`Seed/SeedDemoData.sql`) ومُرحَّلة إلى آخر Migration.
2. الحسابات التجريبية `demo1` إلى `demo8` بكلمة مرور واحدة، وسوبر أدمن باسم `root`.
3. ملف بيئة للتطوير يحتوي:
   ```
   ConnectionStrings__DefaultConnection=Server=localhost\SQLEXPRESS;Database=cashbook_dev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;
   AdminUser__Username=root
   AdminUser__Password=<كلمة مرور root>
   Fingerprint__Matcher=dev
   RateLimiting__LoginPerMinute=1000
   ```
   السطر الأخير ضروري، لأن الاختبارات تسجّل الدخول عشرات المرات في الدقيقة.
4. Python 3.10 أو أحدث، دون أي حزم إضافية.

## التشغيل

```
DOTENV_PATH=<ملف البيئة> ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile
```

في نافذة أخرى:

```
export CASHBOOK_ROOT_PASSWORD=<كلمة مرور root>
export CASHBOOK_DEMO_PASSWORD=<كلمة مرور الحسابات التجريبية>
python tests/integration/test_permissions_archive.py
python tests/integration/test_security.py
python tests/integration/test_sessions_export.py
python tests/integration/test_reference_data.py
```

| الملف | ما يختبره |
|---|---|
| `test_permissions_archive.py` | توارث الصلاحيات، وعزل الخزائن (IDOR)، والأرشفة، وثبات الأرصدة، والدخول بالبصمة |
| `test_security.py` | روابط المرفقات الموقّعة، وفحص الملفات المرفوعة، وJWT، وCORS، وتحديد المعدل، وحد حجم الطلب |
| `test_sessions_export.py` | إبطال الجلسة فوراً عند الخروج أو التعطيل أو تغيير كلمة المرور، والتصدير |
| `test_reference_data.py` | أن التعديل الجزئي لا يمحو الحقول غير المرسلة، وفحص بيانات الإضافة |

## ملاحظات

- `test_permissions_archive.py` يؤرشف الخزنة 4. قبل إعادة تشغيله أرجع الخزائن المؤرشفة في قاعدة التطوير فقط:
  ```sql
  UPDATE Books SET IsArchived = 0, ArchivedAt = NULL, ArchivedByUserId = NULL, ArchiveReason = NULL WHERE IsArchived = 1;
  ```
- اختبار تحديد المعدل في `test_security.py` يستهلك حصة دقيقة كاملة من طلبات تجديد الرمز.
