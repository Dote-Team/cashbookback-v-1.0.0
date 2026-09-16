/* ==========================================================================
   بيانات تجريبية شاملة — نظام الحسابات (cashbook)
   ==========================================================================
   تُنشئ مجموعة كاملة ومترابطة لتشغيل كل شاشات الواجهة الأمامية:

     5 منشآت · 20 خزنة · 8 مستخدمين تجريبيين · 21 عضوية بأدوار مختلفة
     60 فئة · 30 طريقة دفع · 60 جهة اتصال · 40 حقلاً مخصصاً · 20 إعداد خزنة
     2500 حركة · 2875 سطر سجل حركة · 1060 سعر صرف يومي

   التنفيذ:
     sqlcmd -S localhost\SQLEXPRESS -d cash -E -i Seed\SeedDemoData.sql -f 65001

   الإزالة الكاملة:
     sqlcmd -S localhost\SQLEXPRESS -d cash -E -i Seed\RemoveDemoData.sql -f 65001

   ---------------------------------------------------------------------------
   قواعد العمل المُحترَمة (مأخوذة من الشيفرة الفعلية لا من التخمين)

   1. Transactions.Type يُخزَّن **مُطبَّعاً** إلى 'cash in' أو 'cash out'
      (TransactionTypes.Normalize في Models\Constants\TransactionTypes.cs).
      الرصيد يُحسب بالاتجاه: cash in = +1، cash out = -1.
   2. Transactions.Currency و ExchangeRates.Currency يُخزَّنان **نصاً**
      'IQD' أو 'USD' (لا أرقاماً) — العمود nvarchar(3).
   3. الدولار فقط يحمل ExchangeRate. الدينار يحمله NULL دائماً.
   4. ExchangeDate يُكتب للإيداع بالدولار (cash in) فقط، ويُترك NULL
      للسحب بالدولار (cash out) — السحب يرفض تاريخ صرف.
   5. الرصيد **مشتقّ لا مخزَّن**: لا يوجد عمود رصيد في أي جدول.
   6. كل خزنة تحتاج صف Setting واحداً — يُنشَأ تلقائياً في التطبيق.
   7. ExchangeRates له فهرس فريد على (BookId, Currency, RateDate).
   8. BusinessUsers.BookIds يُخزَّن **مصفوفة JSON** بمعرّفات صغيرة الحروف،
      مثال: ["b0000000-0000-4000-8000-000000000001"]
      وهو مطلوب للأدوار المقيَّدة بخزائن، وإلا لم يرَ العضو أي خزنة.
   9. كل المعرّفات uniqueidentifier **بلا Identity** — تُولَّد هنا صراحةً.

   ---------------------------------------------------------------------------
   المعرّفات الثابتة (لتسهيل التتبّع والإزالة)

     a0000000-…01..05   المنشآت
     b0000000-…01..20   الخزائن
     c0000000-…01..08   المستخدمون التجريبيون
     d0000000-…         الفئات
     e0000000-…         العضويات وطرق الدفع ومنشآت أخرى
     f0000000-…         جهات الاتصال
     2a000000-…         الحقول المخصصة
     3a000000-…         إعدادات الخزائن
     المعرّفات المتبقّية (الحركات، أسعار الصرف) عشوائية لكثرة عددها،
     وتُحذف بالربط عبر BookId لا بالمعرّف.

   ملاحظة: كلمة مرور كل المستخدمين التجريبيين هي نفس كلمة مرور المستخدم
   المالك (@OwnerUsername) — تُنسخ بصمة bcrypt نفسها.
   ========================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

--------------------------------------------------------------------------
-- الإعدادات القابلة للتغيير
--------------------------------------------------------------------------
DECLARE @OwnerUsername  sysname = N'aaaa';   -- <<< المالك: يجب أن يكون مسجَّلاً
DECLARE @SideCount      int     = 50;         -- إجمالي الحركات = @SideCount × @SideCount
DECLARE @DemoEmailHost  nvarchar(100) = N'cashbook.local';

DECLARE @Total int = @SideCount * @SideCount;
DECLARE @Now   datetime2 = GETDATE();

--------------------------------------------------------------------------
-- 0) تحقّق مسبق: المالك موجود؟ والبيانات غير مُدخلة سابقاً؟
--------------------------------------------------------------------------
DECLARE @OwnerId uniqueidentifier = (SELECT Id FROM Users WHERE Username = @OwnerUsername);
DECLARE @PwdHash nvarchar(400) = (SELECT Password FROM Users WHERE Username = @OwnerUsername);
DECLARE @msg     nvarchar(2048);

IF @OwnerId IS NULL OR @PwdHash IS NULL
BEGIN
    SET @msg = N'المستخدم «' + @OwnerUsername + N'» غير موجود. سجّل حساباً أولاً (POST /API/User/register) ثم أعد تشغيل السكربت، أو غيّر قيمة @OwnerUsername في أعلى الملف.';
    ; THROW 50000, @msg, 1;
END

IF EXISTS (SELECT 1 FROM Businesses WHERE Id = 'A0000000-0000-4000-8000-000000000001')
BEGIN
    SET @msg = N'البيانات التجريبية موجودة مسبقاً. شغّل Seed\RemoveDemoData.sql أولاً ثم أعد المحاولة.';
    ; THROW 50001, @msg, 1;
END

PRINT N'--- بدء إدخال البيانات التجريبية ---';

BEGIN TRY
    BEGIN TRANSACTION;

    ----------------------------------------------------------------------
    -- 1) المنشآت الخمس
    ----------------------------------------------------------------------
    DECLARE @biz TABLE (rn int PRIMARY KEY, Id uniqueidentifier, Name nvarchar(300));

    INSERT INTO @biz (rn, Id, Name) VALUES
      (1, 'A0000000-0000-4000-8000-000000000001', N'شركة الرافدين للتجارة العامة'),
      (2, 'A0000000-0000-4000-8000-000000000002', N'مؤسسة دجلة للمقاولات'),
      (3, 'A0000000-0000-4000-8000-000000000003', N'شركة بغداد للأدوية والمستلزمات الطبية'),
      (4, 'A0000000-0000-4000-8000-000000000004', N'مجموعة الفرات للمواد الإنشائية'),
      (5, 'A0000000-0000-4000-8000-000000000005', N'شركة النخيل للمواد الغذائية');

    INSERT INTO Businesses (Id, Name, CreatedAt, UpdatedAt)
    SELECT Id, Name,
           DATEADD(DAY, -(420 - rn * 15), @Now),
           DATEADD(DAY, -(420 - rn * 15), @Now)
    FROM @biz;

    ----------------------------------------------------------------------
    -- 2) الخزائن العشرون (الصناديق)
    ----------------------------------------------------------------------
    DECLARE @bk TABLE (rn int PRIMARY KEY, Id uniqueidentifier, BizRn int, Name nvarchar(300));

    INSERT INTO @bk (rn, Id, BizRn, Name) VALUES
      -- شركة الرافدين: تجارة عامة — صندوق رئيسي وصناديق تخصصية
      ( 1, 'B0000000-0000-4000-8000-000000000001', 1, N'الصندوق الرئيسي'),
      ( 2, 'B0000000-0000-4000-8000-000000000002', 1, N'صندوق المبيعات'),
      ( 3, 'B0000000-0000-4000-8000-000000000003', 1, N'صندوق المشتريات'),
      ( 4, 'B0000000-0000-4000-8000-000000000004', 1, N'صندوق المصاريف النقدية'),
      -- مؤسسة دجلة: مقاولات
      ( 5, 'B0000000-0000-4000-8000-000000000005', 2, N'خزنة المشاريع'),
      ( 6, 'B0000000-0000-4000-8000-000000000006', 2, N'خزنة الرواتب'),
      ( 7, 'B0000000-0000-4000-8000-000000000007', 2, N'مصروفات الموقع'),
      -- شركة بغداد: أدوية
      ( 8, 'B0000000-0000-4000-8000-000000000008', 3, N'الخزنة المركزية'),
      ( 9, 'B0000000-0000-4000-8000-000000000009', 3, N'صندوق التحصيل'),
      (10, 'B0000000-0000-4000-8000-000000000010', 3, N'صندوق المستوردات'),
      -- مجموعة الفرات: مواد إنشائية
      (11, 'B0000000-0000-4000-8000-000000000011', 4, N'خزنة المعرض'),
      (12, 'B0000000-0000-4000-8000-000000000012', 4, N'خزنة المخزن'),
      (13, 'B0000000-0000-4000-8000-000000000013', 4, N'صندوق التوصيل'),
      (14, 'B0000000-0000-4000-8000-000000000014', 4, N'خزنة المقاولات'),
      (15, 'B0000000-0000-4000-8000-000000000015', 4, N'خزنة المشتريات'),
      (16, 'B0000000-0000-4000-8000-000000000016', 4, N'خزنة المصاريف'),
      -- شركة النخيل: مواد غذائية
      (17, 'B0000000-0000-4000-8000-000000000017', 5, N'الصندوق العام'),
      (18, 'B0000000-0000-4000-8000-000000000018', 5, N'صندوق التوزيع'),
      (19, 'B0000000-0000-4000-8000-000000000019', 5, N'خزنة الفروع'),
      (20, 'B0000000-0000-4000-8000-000000000020', 5, N'صندوق التالف');

    INSERT INTO Books (Id, Name, BusinessId, CreatedAt, UpdatedAt)
    SELECT k.Id, k.Name, b.Id,
           DATEADD(DAY, -(350 - k.rn * 10), @Now),
           DATEADD(DAY, -(350 - k.rn * 10), @Now)
    FROM @bk k JOIN @biz b ON b.rn = k.BizRn;

    -- الإعدادات: صف واحد لكل خزنة — التطبيق يُنشئها تلقائياً عند إنشاء الخزنة.
    -- التنويع مقصود: شركة الأرباح تُلزم الفئات، وغيرها يُخفيها.
    INSERT INTO Settings (Id, CategoryStatus, PaymentMethodStatus, ContactStatus, BookId, CreatedAt, UpdatedAt)
    SELECT CAST('3A000000-0000-4000-8000-' + RIGHT('000000000000' + CAST(k.rn AS varchar(12)), 12) AS uniqueidentifier),
           CASE WHEN k.rn % 4 = 0 THEN 0 ELSE 1 END,
           CASE WHEN k.rn % 5 = 0 THEN 0 ELSE 1 END,
           CASE WHEN k.rn % 3 = 0 THEN 0 ELSE 1 END,
           k.Id, @Now, @Now
    FROM @bk k;

    ----------------------------------------------------------------------
    -- 3) المستخدمون التجريبيون الثمانية
    ----------------------------------------------------------------------
    DECLARE @usr TABLE (rn int PRIMARY KEY, Id uniqueidentifier, Username nvarchar(100), Name nvarchar(200));

    INSERT INTO @usr (rn, Id, Username, Name) VALUES
      (1, 'C0000000-0000-4000-8000-000000000001', N'demo1', N'أحمد الجبوري'),
      (2, 'C0000000-0000-4000-8000-000000000002', N'demo2', N'محمد العبيدي'),
      (3, 'C0000000-0000-4000-8000-000000000003', N'demo3', N'علي الشمري'),
      (4, 'C0000000-0000-4000-8000-000000000004', N'demo4', N'حسين الربيعي'),
      (5, 'C0000000-0000-4000-8000-000000000005', N'demo5', N'فاطمة الزبيدي'),
      (6, 'C0000000-0000-4000-8000-000000000006', N'demo6', N'عمر الدليمي'),
      (7, 'C0000000-0000-4000-8000-000000000007', N'demo7', N'ياسر الجميلي'),
      (8, 'C0000000-0000-4000-8000-000000000008', N'demo8', N'نور التميمي');

    INSERT INTO Users (Id, Name, Username, Email, Password, CreatedAt, UpdatedAt, ProfileImage, IsSuperAdmin)
    SELECT u.Id, u.Name, u.Username,
           u.Username + N'@' + @DemoEmailHost,
           @PwdHash,                       -- نفس بصمة bcrypt ⇒ نفس كلمة المرور
           DATEADD(DAY, -(400 - u.rn * 20), @Now),
           DATEADD(DAY, -(400 - u.rn * 20), @Now),
           NULL, 0
    FROM @usr u;

    ----------------------------------------------------------------------
    -- 4) العضويات — مالك واحد لكل منشأة + أدوار متنوّعة لعرض الصلاحيات
    --    المالك هو @OwnerUsername في المنشآت الخمس.
    ----------------------------------------------------------------------
    DECLARE @mem TABLE (rn int PRIMARY KEY, BizRn int, UserRn int, Role varchar(30), BookRns varchar(200));

    INSERT INTO @mem (rn, BizRn, UserRn, Role, BookRns) VALUES
      -- 1) شركة الرافدين
      ( 1, 1, NULL, 'owner',           NULL),          -- المالك الحقيقي
      ( 2, 1, 1,    'partner',         NULL),
      ( 3, 1, 2,    'viewer',          NULL),
      ( 4, 1, 3,    'staff',           '1,2'),
      ( 5, 1, 4,    'portfolio_manager','1,2,3,4'),
      -- 2) مؤسسة دجلة
      ( 6, 2, NULL, 'owner',           NULL),
      ( 7, 2, 2,    'partner',         NULL),
      ( 8, 2, 5,    'admin',           '5,6,7'),
      ( 9, 2, 6,    'dataoperator',    '5'),
      -- 3) شركة بغداد
      (10, 3, NULL, 'owner',           NULL),
      (11, 3, 1,    'viewer',          NULL),
      (12, 3, 5,    'privateviewer',   '8,9'),
      (13, 3, 7,    'staff',           '10'),
      -- 4) مجموعة الفرات
      (14, 4, NULL, 'owner',           NULL),
      (15, 4, 3,    'partner',         NULL),
      (16, 4, 8,    'portfolio_manager','11,12,13,14'),
      (17, 4, 6,    'viewer',          NULL),
      -- 5) شركة النخيل
      (18, 5, NULL, 'owner',           NULL),
      (19, 5, 7,    'partner',         NULL),
      (20, 5, 4,    'admin',           '17,18,19'),
      (21, 5, 8,    'staff',           '20');

    -- BookIds يُبنى كمصفوفة JSON من أرقام الخزائن المكتوبة في العمود BookRns.
    INSERT INTO BusinessUsers (Id, UserId, BusinessId, BookIds, Role)
    SELECT CAST('E0000000-0000-4000-8000-' + RIGHT('000000000000' + CAST(m.rn AS varchar(12)), 12) AS uniqueidentifier),
           CASE WHEN m.UserRn IS NULL THEN @OwnerId ELSE u.Id END,
           b.Id,
           CASE WHEN m.BookRns IS NULL THEN NULL ELSE
                (SELECT N'["' + STRING_AGG(LOWER(CAST(k.Id AS nvarchar(40))), N'","')
                        WITHIN GROUP (ORDER BY k.rn) + N'"]'
                 FROM @bk k
                 WHERE N',' + m.BookRns + N',' LIKE N'%,' + CAST(k.rn AS nvarchar(4)) + N',%')
           END,
           m.Role
    FROM @mem m
    JOIN @biz b ON b.rn = m.BizRn
    LEFT JOIN @usr u ON u.rn = m.UserRn;

    ----------------------------------------------------------------------
    -- 5) الفئات (12 لكل منشأة) وطرق الدفع (6) وجهات الاتصال (12)
    ----------------------------------------------------------------------
    DECLARE @cname TABLE (i int PRIMARY KEY, Name nvarchar(200));
    INSERT INTO @cname (i, Name) VALUES
      ( 1, N'مبيعات نقدية'),      ( 2, N'مبيعات آجلة'),
      ( 3, N'مشتريات بضاعة'),     ( 4, N'رواتب وأجور'),
      ( 5, N'إيجار'),             ( 6, N'كهرباء وماء'),
      ( 7, N'إنترنت واتصالات'),   ( 8, N'وقود وصيانة سيارات'),
      ( 9, N'صيانة وأدوات'),      (10, N'ضيافة'),
      (11, N'نقل وشحن'),          (12, N'مصاريف إدارية');

    INSERT INTO Categories (Id, Name, BusinessId, CreatedAt, UpdatedAt)
    SELECT CAST('D0000000-0000-4000-8000-' + RIGHT('000000000000' + CAST(b.rn * 100 + c.i AS varchar(12)), 12) AS uniqueidentifier),
           c.Name, b.Id, @Now, @Now
    FROM @biz b CROSS JOIN @cname c;

    DECLARE @pname TABLE (i int PRIMARY KEY, Name nvarchar(200));
    INSERT INTO @pname (i, Name) VALUES
      (1, N'نقد'), (2, N'صك مصرفي'), (3, N'حوالة مصرفية'),
      (4, N'بطاقة دفع'), (5, N'زين كاش'), (6, N'آسيا حوالة');

    INSERT INTO PaymentMethods (Id, Name, BusinessId, CreatedAt, UpdatedAt)
    SELECT CAST('E1000000-0000-4000-8000-' + RIGHT('000000000000' + CAST(b.rn * 100 + p.i AS varchar(12)), 12) AS uniqueidentifier),
           p.Name, b.Id, @Now, @Now
    FROM @biz b CROSS JOIN @pname p;

    DECLARE @kname TABLE (i int PRIMARY KEY, Name nvarchar(200), Phone nvarchar(50));
    INSERT INTO @kname (i, Name, Phone) VALUES
      ( 1, N'أحمد الجبوري',          N'+9647712345601'),
      ( 2, N'شركة الأمل للتجارة',    N'+9647712345602'),
      ( 3, N'محمد العبيدي',          N'+9647712345603'),
      ( 4, N'علي الشمري',            N'+9647712345604'),
      ( 5, N'مؤسسة الوفاء',          N'+9647712345605'),
      ( 6, N'حسين الربيعي',          N'+9647712345606'),
      ( 7, N'فاطمة الزبيدي',         N'+9647712345607'),
      ( 8, N'عمر الدليمي',           N'+9647712345608'),
      ( 9, N'شركة السلام العامة',    N'+9647712345609'),
      (10, N'ياسر الجميلي',          N'+9647712345610'),
      (11, N'مخازن النور',           N'+9647712345611'),
      (12, N'كرار الأسدي',           N'+9647712345612');

    INSERT INTO Contacts (Id, Name, Phone, BusinessId, CreatedAt, UpdatedAt)
    SELECT CAST('F0000000-0000-4000-8000-' + RIGHT('000000000000' + CAST(b.rn * 100 + k.i AS varchar(12)), 12) AS uniqueidentifier),
           k.Name, k.Phone, b.Id, @Now, @Now
    FROM @biz b CROSS JOIN @kname k;

    ----------------------------------------------------------------------
    -- 6) الحقول المخصصة — حقلان لكل خزنة (رقم الوصل إلزامي + ملاحظات)
    ----------------------------------------------------------------------
    INSERT INTO CustomFields (Id, [Key], BookId, IsRequired)
    SELECT CAST('2A000000-0000-4000-8000-' + RIGHT('000000000000' + CAST(k.rn * 10 + f.i AS varchar(12)), 12) AS uniqueidentifier),
           f.[Key], k.Id, f.IsRequired
    FROM @bk k
    CROSS JOIN (VALUES (1, N'رقم الوصل', 1), (2, N'ملاحظات إضافية', 0)) f(i, [Key], IsRequired);

    ----------------------------------------------------------------------
    -- 7) أسعار الصرف — سعر أسبوعي لكل خزنة على مدى سنة (53 أسبوعاً)
    --    الدينار يضعف تدريجياً: الأقدم أرخص (1315) والأحدث أغلى (~1471).
    --    الفهرس الفريد على (BookId, Currency, RateDate) محترَم: تاريخ واحد لكل خزنة.
    ----------------------------------------------------------------------
    ;WITH weeks AS (
        SELECT TOP (53) w = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1
        FROM sys.all_objects
    )
    INSERT INTO ExchangeRates (Id, BookId, Currency, Rate, RateDate, SetByUserId, CreatedAt, UpdatedAt)
    SELECT NEWID(), k.Id, N'USD',
           CAST(1315 + ((52 - weeks.w) * 3) + (ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 9) AS decimal(18,3)),
           DATEADD(DAY, -(weeks.w * 7), CAST(@Now AS date)),
           @OwnerId,
           DATEADD(DAY, -(weeks.w * 7), @Now),
           DATEADD(DAY, -(weeks.w * 7), @Now)
    FROM @bk k CROSS JOIN weeks;

    ----------------------------------------------------------------------
    -- 8) الحركات — 2500 حركة موزّعة على 20 خزنة وعلى 365 يوماً
    --    توزيع متساوٍ على الخزائن (n % 20) لضمان تغطية كل خزنة في العرض،
    --    وتوزيع عشوائي للعملة والنوع والمبلغ والتاريخ.
    ----------------------------------------------------------------------
    ;WITH n1 AS (SELECT TOP (@SideCount) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM sys.all_objects),
          n2 AS (SELECT TOP (@SideCount) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS j FROM sys.all_objects),
          grid AS (SELECT n = ROW_NUMBER() OVER (ORDER BY n1.i, n2.j) FROM n1 CROSS JOIN n2)
    SELECT n,
           rcur  = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 100,   -- العملة: 70% دينار
           rtype = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 100,   -- النوع: 55% إيداع
           ramt  = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 1000,  -- المبلغ
           rcent = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 100,   -- كسور الدولار
           rdays = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 365,   -- الأيام الماضية
           rhour = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 10,    -- ساعة اليوم
           rcat  = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 100,   -- إغفال الفئة 15%
           rpm   = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 100,   -- إغفال طريقة الدفع 20%
           rcon  = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 100,   -- إغفال الجهة 30%
           rrate = ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 10     -- تذبذب سعر الصرف
    INTO #r
    FROM grid;

    -- المسجّلون: كل عضو بدور كاتب في المنشأة، لتنويع فلتر «المستخدم».
    IF OBJECT_ID('tempdb..#rec') IS NOT NULL DROP TABLE #rec;
    SELECT BusinessId, UserId,
           rn  = ROW_NUMBER() OVER (PARTITION BY BusinessId ORDER BY UserId),
           cnt = COUNT(*)     OVER (PARTITION BY BusinessId)
    INTO #rec
    FROM BusinessUsers
    WHERE BusinessId IN (SELECT Id FROM @biz)
      AND LOWER(Role) IN ('owner', 'partner', 'staff', 'admin', 'dataoperator');

    IF OBJECT_ID('tempdb..#cat') IS NOT NULL DROP TABLE #cat;
    SELECT BusinessId, Id, Name,
           rn  = ROW_NUMBER() OVER (PARTITION BY BusinessId ORDER BY Name),
           cnt = COUNT(*)     OVER (PARTITION BY BusinessId)
    INTO #cat
    FROM Categories WHERE BusinessId IN (SELECT Id FROM @biz);

    IF OBJECT_ID('tempdb..#pm') IS NOT NULL DROP TABLE #pm;
    SELECT BusinessId, Id,
           rn  = ROW_NUMBER() OVER (PARTITION BY BusinessId ORDER BY Name),
           cnt = COUNT(*)     OVER (PARTITION BY BusinessId)
    INTO #pm
    FROM PaymentMethods WHERE BusinessId IN (SELECT Id FROM @biz);

    IF OBJECT_ID('tempdb..#con') IS NOT NULL DROP TABLE #con;
    SELECT BusinessId, Id, Name,
           rn  = ROW_NUMBER() OVER (PARTITION BY BusinessId ORDER BY Name),
           cnt = COUNT(*)     OVER (PARTITION BY BusinessId)
    INTO #con
    FROM Contacts WHERE BusinessId IN (SELECT Id FROM @biz);

    INSERT INTO Transactions
        (Id, Type, [Date], Description, Amount, Currency, ExchangeRate, ExchangeDate,
         CategoryId, PaymentMethodId, BookId, UserId, ContactId, CreatedAt, UpdatedAt)
    SELECT
        NEWID(),
        CASE WHEN r.rtype < 55 THEN N'cash in' ELSE N'cash out' END,
        t.Dt,
        LEFT(ISNULL(c.Name, N'حركة نقدية')
             + CASE WHEN k.Name IS NULL THEN N'' ELSE N' — ' + k.Name END,
             300),
        CASE WHEN r.rcur < 70
             THEN CAST(25000 + (r.ramt * 7500) AS decimal(18,6))
             ELSE CAST(100 + (r.ramt * 20) + (r.rcent / 100.0) AS decimal(18,6)) END,
        CASE WHEN r.rcur < 70 THEN N'IQD' ELSE N'USD' END,
        -- سعر الصرف للدولار فقط، متوافق مع جدول الأسعار الأسبوعي
        CASE WHEN r.rcur < 70 THEN NULL
             ELSE CAST(1315 + ((364 - r.rdays) * 0.4286) + r.rrate AS decimal(18,3)) END,
        -- تاريخ الصرف للإيداع بالدولار فقط؛ السحب بالدولار يرفضه
        CASE WHEN r.rcur >= 70 AND r.rtype < 55 THEN t.Dt ELSE NULL END,
        c.Id, p.Id, k2.Id, rc.UserId, k.Id,
        t.Dt, t.Dt
    FROM #r r
    JOIN @bk k2 ON k2.rn = 1 + (r.n % 20)                       -- توزيع متساوٍ على الخزائن
    JOIN @biz b ON b.rn = k2.BizRn
    JOIN #rec rc ON rc.BusinessId = b.Id AND rc.rn = 1 + (r.n % rc.cnt)
    CROSS APPLY (SELECT Dt = DATEADD(HOUR, 8 + r.rhour,
                                    DATEADD(DAY, -r.rdays, @Now))) t
    LEFT JOIN #cat c ON c.BusinessId = b.Id AND c.rn = 1 + (r.n % c.cnt) AND r.rcat >= 15
    LEFT JOIN #pm  p ON p.BusinessId = b.Id AND p.rn = 1 + (r.n % p.cnt) AND r.rpm  >= 20
    LEFT JOIN #con k ON k.BusinessId = b.Id AND k.rn = 1 + (r.n % k.cnt) AND r.rcon >= 30;

    ----------------------------------------------------------------------
    -- 9) سجل الحركات — سطر POST لكل حركة، وسطر PUT لنحو سُبع الحركات تعديلاً
    --    PUT يحمل From (المبلغ القديم) و To (الجديد) كما تفعل الشيفرة فعلاً.
    ----------------------------------------------------------------------
    -- ⚠ مصيدة مُجرَّبة: لا تستخدم CHECKSUM(NEWID()) في شرط WHERE.
    --   مُدقّق الاستعلام يقيّمه **مرة واحدة** لا لكل سطر، فيطابق كل السطور أو
    --   لا شيئاً منها. جُرِّب فعلياً: أعاد 0 من 2500. أما في قائمة SELECT
    --   فيعمل لكل سطر (أعاد 14% كما هو متوقّع). البديل الصحيح: CHECKSUM(t.Id)
    --   فهو ثابت لكل سطر ومتوزّع جيداً، ويجعل النتيجة قابلة للتكرار.
    INSERT INTO TransactionHistories
        (Id, Operation, Description, Type, [From], [To], Amount, ExchangeRate, ExchangeDate,
         BookId, TransactionId, UserId, CreatedAt, UpdatedAt)
    SELECT NEWID(), N'POST', t.Description, t.Type, NULL, NULL, t.Amount,
           t.ExchangeRate, t.ExchangeDate, t.BookId, t.Id, t.UserId, t.CreatedAt, t.UpdatedAt
    FROM Transactions t
    WHERE t.BookId IN (SELECT Id FROM @bk);

    -- تعديلات لاحقة: الربع من الحركات تعرّضت لتعديل مبلغ
    INSERT INTO TransactionHistories
        (Id, Operation, Description, Type, [From], [To], Amount, ExchangeRate, ExchangeDate,
         BookId, TransactionId, UserId, CreatedAt, UpdatedAt)
    SELECT NEWID(), N'PUT', t.Description, t.Type,
           t.Amount, t.Amount + CASE WHEN t.Currency = N'USD' THEN 250 ELSE 500000 END,
           t.Amount + CASE WHEN t.Currency = N'USD' THEN 250 ELSE 500000 END,
           t.ExchangeRate, t.ExchangeDate, t.BookId, t.Id, t.UserId,
           DATEADD(DAY, 1, t.CreatedAt), DATEADD(DAY, 1, t.CreatedAt)
    FROM Transactions t
    WHERE t.BookId IN (SELECT Id FROM @bk)
      AND ABS(CAST(CHECKSUM(t.Id) AS bigint)) % 100 < 15;

    ----------------------------------------------------------------------
    -- 10) قيم الحقول المخصصة — لـ 40% من الحركات في كل خزنة
    ----------------------------------------------------------------------
    INSERT INTO CustomFieldValues (Id, Value, TransactionId, CustomFieldId)
    SELECT NEWID(),
           N'INV-' + RIGHT('000000' + CAST(ABS(CAST(CHECKSUM(NEWID()) AS bigint)) % 1000000 AS varchar(6)), 6),
           t.Id, cf.Id
    FROM Transactions t
    CROSS APPLY (SELECT TOP 1 f.Id FROM CustomFields f
                 WHERE f.BookId = t.BookId AND f.[Key] = N'رقم الوصل'
                 ORDER BY f.Id) cf
    WHERE t.BookId IN (SELECT Id FROM @bk)
      AND ABS(CAST(CHECKSUM(t.Id) AS bigint)) % 100 < 40;

    INSERT INTO CustomFieldValues (Id, Value, TransactionId, CustomFieldId)
    SELECT NEWID(),
           N'تم التسليم خلال يومين',
           t.Id, cf.Id
    FROM Transactions t
    CROSS APPLY (SELECT TOP 1 f.Id FROM CustomFields f
                 WHERE f.BookId = t.BookId AND f.[Key] = N'ملاحظات إضافية'
                 ORDER BY f.Id) cf
    WHERE t.BookId IN (SELECT Id FROM @bk)
      AND ABS(CAST(CHECKSUM(t.Id) AS bigint)) % 100 < 12;

    COMMIT TRANSACTION;
    PRINT N'--- تم إدخال البيانات بنجاح ---';

    ----------------------------------------------------------------------
    -- ملخّص
    ----------------------------------------------------------------------
    SELECT N'المنشآت'          AS البند, COUNT(*) AS العدد FROM Businesses WHERE Id IN (SELECT Id FROM @biz)
    UNION ALL SELECT N'الخزائن',            COUNT(*) FROM Books           WHERE Id IN (SELECT Id FROM @bk)
    UNION ALL SELECT N'المستخدمون',         COUNT(*) FROM Users           WHERE Id IN (SELECT Id FROM @usr)
    UNION ALL SELECT N'العضويات',           COUNT(*) FROM BusinessUsers   WHERE BusinessId IN (SELECT Id FROM @biz)
    UNION ALL SELECT N'الفئات',             COUNT(*) FROM Categories      WHERE BusinessId IN (SELECT Id FROM @biz)
    UNION ALL SELECT N'طرق الدفع',          COUNT(*) FROM PaymentMethods  WHERE BusinessId IN (SELECT Id FROM @biz)
    UNION ALL SELECT N'جهات الاتصال',       COUNT(*) FROM Contacts        WHERE BusinessId IN (SELECT Id FROM @biz)
    UNION ALL SELECT N'الحقول المخصصة',     COUNT(*) FROM CustomFields    WHERE BookId IN (SELECT Id FROM @bk)
    UNION ALL SELECT N'إعدادات الخزائن',    COUNT(*) FROM Settings        WHERE BookId IN (SELECT Id FROM @bk)
    UNION ALL SELECT N'أسعار الصرف',        COUNT(*) FROM ExchangeRates   WHERE BookId IN (SELECT Id FROM @bk)
    UNION ALL SELECT N'الحركات',            COUNT(*) FROM Transactions    WHERE BookId IN (SELECT Id FROM @bk)
    UNION ALL SELECT N'سجل الحركات',        COUNT(*) FROM TransactionHistories WHERE BookId IN (SELECT Id FROM @bk)
    UNION ALL SELECT N'قيم الحقول المخصصة', COUNT(*) FROM CustomFieldValues WHERE TransactionId IN
                    (SELECT Id FROM Transactions WHERE BookId IN (SELECT Id FROM @bk));

    -- أرصدة الخزائن كما ستعرضها الواجهة تماماً (الرصيد مشتقّ لا مخزَّن)
    SELECT b.Name AS المنشأة, k.Name AS الخزنة,
           SUM(CASE WHEN t.Currency = N'IQD' AND t.Type = N'cash in'  THEN t.Amount ELSE 0 END)
         - SUM(CASE WHEN t.Currency = N'IQD' AND t.Type = N'cash out' THEN t.Amount ELSE 0 END) AS رصيد_الدينار,
           SUM(CASE WHEN t.Currency = N'USD' AND t.Type = N'cash in'  THEN t.Amount ELSE 0 END)
         - SUM(CASE WHEN t.Currency = N'USD' AND t.Type = N'cash out' THEN t.Amount ELSE 0 END) AS رصيد_الدولار,
           COUNT(t.Id) AS الحركات
    FROM @bk k
    JOIN @biz b ON b.rn = k.BizRn
    LEFT JOIN Transactions t ON t.BookId = k.Id
    GROUP BY b.Name, k.Name
    ORDER BY b.Name, k.Name;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'--- فشل الإدخال، وتم التراجع عن كل شيء ---';
    THROW;
END CATCH
