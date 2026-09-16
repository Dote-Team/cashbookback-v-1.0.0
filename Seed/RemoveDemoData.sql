/* ==========================================================================
   إزالة البيانات التجريبية — نظام الحسابات (cashbook)
   ==========================================================================
   يحذف كل ما أدخله Seed\SeedDemoData.sql **ولا يمسّ أي شيء آخر**:
   لا يمسّ منشآت أخرى، ولا المستخدم المالك، ولا سجل التدقيق.

   التنفيذ:
     sqlcmd -S localhost\SQLEXPRESS -d cash -E -i Seed\RemoveDemoData.sql -f 65001

   ---------------------------------------------------------------------------
   ترتيب الحذف إلزامي: المفاتيح الأجنبية كلها NO_ACTION (لا حذف متسلسل)
   ما عدا Settings فإنها CASCADE مع Books. لذلك تُحذف الأبناء قبل الآباء.
   لا يُحذف شيء بالاعتماد على SetByUserId ولا على UserId، لأن المالك الحقيقي
   (المستخدم الذي أنشأ البيانات) يظهر في الحقلين، وحذفه أو حذف أسعاره خطأ.
   ========================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BizIds TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @BizIds (Id) VALUES
  ('A0000000-0000-4000-8000-000000000001'),
  ('A0000000-0000-4000-8000-000000000002'),
  ('A0000000-0000-4000-8000-000000000003'),
  ('A0000000-0000-4000-8000-000000000004'),
  ('A0000000-0000-4000-8000-000000000005');

DECLARE @UserIds TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @UserIds (Id) VALUES
  ('C0000000-0000-4000-8000-000000000001'),
  ('C0000000-0000-4000-8000-000000000002'),
  ('C0000000-0000-4000-8000-000000000003'),
  ('C0000000-0000-4000-8000-000000000004'),
  ('C0000000-0000-4000-8000-000000000005'),
  ('C0000000-0000-4000-8000-000000000006'),
  ('C0000000-0000-4000-8000-000000000007'),
  ('C0000000-0000-4000-8000-000000000008');

DECLARE @Books TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Books (Id)
SELECT Id FROM Books WHERE BusinessId IN (SELECT Id FROM @BizIds);

DECLARE @Tx TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Tx (Id)
SELECT Id FROM Transactions WHERE BookId IN (SELECT Id FROM @Books);

DECLARE @n int;
SELECT @n = COUNT(*) FROM @Books;

IF @n = 0 AND NOT EXISTS (SELECT 1 FROM @UserIds u JOIN Users x ON x.Id = u.Id)
BEGIN
    PRINT N'لا توجد بيانات تجريبية لحذفها — لا شيء تغيّر.';
    RETURN;
END

PRINT N'--- بدء إزالة البيانات التجريبية ---';

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1) قيم الحقول المخصصة: تشير إلى الحركات وإلى الحقول المخصصة معاً
    DELETE FROM CustomFieldValues
    WHERE TransactionId IN (SELECT Id FROM @Tx)
       OR CustomFieldId IN (SELECT Id FROM CustomFields WHERE BookId IN (SELECT Id FROM @Books));

    -- 2) المرفقات (لا تُنشئها البذور، لكن تُنظَّف احتياطاً لو أُضيفت يدوياً)
    DELETE FROM Attachements WHERE TransactionId IN (SELECT Id FROM @Tx);

    -- 3) سجل الحركات: يُحذف بالخزنة لا بالمستخدم
    DELETE FROM TransactionHistories WHERE BookId IN (SELECT Id FROM @Books);

    -- 4) الحركات
    DELETE FROM Transactions WHERE BookId IN (SELECT Id FROM @Books);

    -- 5) أسعار الصرف: بالخزنة فقط. SetByUserId يخصّ المالك الحقيقي فلا يُستعمل.
    DELETE FROM ExchangeRates WHERE BookId IN (SELECT Id FROM @Books);

    -- 6) الحقول المخصصة
    DELETE FROM CustomFields WHERE BookId IN (SELECT Id FROM @Books);

    -- 7) إعدادات الخزائن (تُحذف تلقائياً مع الخزنة، وهذا صريح للوضوح)
    DELETE FROM Settings WHERE BookId IN (SELECT Id FROM @Books);

    -- 8) الكيانات التابعة للمنشأة
    DELETE FROM Categories     WHERE BusinessId IN (SELECT Id FROM @BizIds);
    DELETE FROM PaymentMethods WHERE BusinessId IN (SELECT Id FROM @BizIds);
    DELETE FROM Contacts       WHERE BusinessId IN (SELECT Id FROM @BizIds);

    -- 9) العضويات
    DELETE FROM BusinessUsers WHERE BusinessId IN (SELECT Id FROM @BizIds);

    -- 10) الخزائن ثم المنشآت
    DELETE FROM Books      WHERE BusinessId IN (SELECT Id FROM @BizIds);
    DELETE FROM Businesses WHERE Id IN (SELECT Id FROM @BizIds);

    -- 11) جلسات المستخدمين التجريبيين ثم المستخدمون أنفسهم
    DELETE FROM Sessions WHERE UserId IN (SELECT Id FROM @UserIds);
    DELETE FROM Users    WHERE Id     IN (SELECT Id FROM @UserIds);

    COMMIT TRANSACTION;
    PRINT N'--- تمت الإزالة بنجاح. سجل التدقيق لم يُمسّ. ---';

    SELECT N'المنشآت المتبقية'  AS البند, COUNT(*) AS العدد FROM Businesses
    UNION ALL SELECT N'الخزائن المتبقية',   COUNT(*) FROM Books
    UNION ALL SELECT N'الحركات المتبقية',   COUNT(*) FROM Transactions
    UNION ALL SELECT N'المستخدمون المتبقون', COUNT(*) FROM Users
    UNION ALL SELECT N'سطور التدقيق',       COUNT(*) FROM AuditLogs;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'--- فشلت الإزالة، وتم التراجع عن كل شيء ---';
    THROW;
END CATCH
