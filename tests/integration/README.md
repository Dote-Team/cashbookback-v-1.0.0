# اختبارات التكامل

تختبر الـ API وهو يعمل فعلاً على قاعدة بيانات تطوير: الصلاحيات، والأرشفة، والأرصدة، والبصمة، والأمان، والجلسات. لا تُشغَّل أبداً على قاعدة الإنتاج، لأنها تنشئ مستخدمين وقيوداً وتؤرشف خزنة.

## المتطلبات

قاعدة التطوير `cash` على SQL Express جاهزة بالبيانات التجريبية، وملف `.env.local` (مستثنى من Git) فيه الاتصال والأسرار وكلمات مرور الحسابات التجريبية وحد الدخول المرفوع للاختبارات. لإعادة بنائها من الصفر:

1. `DOTENV_PATH=.env.local dotnet ef database update 20261008002347_AddTrashRelatedJson`
2. أضف المستخدم `aaaa` بكلمة مرور الحسابات التجريبية، ثم `sqlcmd -S localhost\SQLEXPRESS -d cash -E -i Seed\SeedDemoData.sql -f 65001`
3. `DOTENV_PATH=.env.local dotnet ef database update` (يحوّل الأدوار القديمة إلى الصلاحيات الجديدة)
4. Python 3.10 أو أحدث، دون أي حزم إضافية.

## التشغيل

```
dotnet run --launch-profile http
```

في نافذة أخرى:

```
export $(grep -E "^CASHBOOK_(ROOT|DEMO)_PASSWORD=" .env.local | xargs)
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
