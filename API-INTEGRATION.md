# دليل ربط الواجهة الأمامية بالـ API

> **نظام الحسابات** — محافظ متعددة العملات + سجل تدقيق شامل
> الإصدار: 2.0 · التاريخ: 15 أيلول 2026

هذه الوثيقة موجّهة لمطوّر الواجهة الأمامية. تشرح **ما تغيّر في الـAPI**، و**ما يجب تعديله في الواجهة الحالية**، مع شيفرة TypeScript جاهزة للنسخ.

---

## 0. أدوات جاهزة

| الملف          | الموقع                          | الاستخدام                                                                                              |
| -------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `swagger.json` | جذر المشروع                     | مواصفة OpenAPI كاملة (71 عملية، 47 مساراً). استوردها في Postman أو Insomnia أو استخدمها لتوليد الأنواع |
| `Swagger UI`   | `http://localhost:5003/swagger` | واجهة تفاعلية — تعمل في بيئة التطوير فقط                                                               |

**عنوان الـAPI:** `http://localhost:5003`
**CORS المسموح:** `http://localhost:3000` · `http://127.0.0.1:5500` · `https://backsmartgis.live`

---

## 1. الخلاصة: ما يجب تغييره في الواجهة الحالية

راجعت الواجهة الحالية (`services/`). **لا يوجد أي كسر** — كل النداءات الحالية تعمل. لكن هناك **ثلاثة نقائص** تمنع ظهور الميزات الجديدة، وميزات كاملة تحتاج شاشات.

### 1.1 تعديلات مطلوبة (بلا كسر)

| #   | الملف                                                 | المطلوب                                                                                | لماذا                                                                        |
| --- | ----------------------------------------------------- | -------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1   | `services/Transaction.ts` → `AddTransactionPayload`   | إضافة `Currency` و `ExchangeRate` و `ExchangeDate`                                     | بدونها **لا يمكن إنشاء حركة دولار إطلاقاً** — القيمة الافتراضية `IQD` دائماً |
| 2   | `services/Transaction.ts` → `TransactionDataResponse` | إضافة `currency` و `exchangeRate` و `exchangeDate` و `newBalanceIqd` و `newBalanceUsd` | الـAPI يُرجعها فعلاً لكن الواجهة تتجاهلها                                    |
| 3   | `services/Books.ts` → نوع الخزنة                      | إضافة `balanceIqd` و `balanceUsd`                                                      | شاشة الخزائن تعرض رصيداً واحداً بدل رصيدين                                   |

### 1.2 ميزات تحتاج شاشات جديدة

| الميزة                 | عدد النقاط | حالة الواجهة  |
| ---------------------- | ---------- | ------------- |
| شاشة سجل التدقيق       | 7          | ❌ غير موجودة |
| نافذة سعر الصرف اليومي | 4          | ❌ غير موجودة |
| واجهة النسخ الاحتياطي  | 3          | ❌ غير موجودة |
| سجل تعديلات الحركة     | 2          | ❌ غير موجودة |

---

## 2. المصادقة

### 2.1 الدخول

```
POST /API/User/login
Content-Type: application/json

{ "email": "user@example.com", "password": "…", "deviceToken": "web-abc123" }
```

| الحقل         | إلزامي     | ملاحظة                                                    |
| ------------- | ---------- | --------------------------------------------------------- |
| `username`    | واحد منهما | يقبل اسم المستخدم **أو** البريد                           |
| `email`       | واحد منهما | متوافق مع الحسابات القديمة                                |
| `password`    | ✅         |                                                           |
| `deviceToken` | ✅         | إلزامي — **غيابه يرجع 400**. أي معرّف ثابت للجهاز/المتصفح |

### 2.2 استجابة الدخول

```ts
export interface LoginResponse {
  email: string;
  username: string;
  accessToken: string;
  refreshToken: string;
  sessionId: string;
  name: string;
  profileImage: string | null;
  id: string;
  isSuperAdmin: boolean;
  canManageExchangeRate: boolean;
  exchangeRateBusinessIds: string[];
}
```

**ثلاثة حقول جديدة مهمة للواجهة:**

- `canManageExchangeRate` — يحدّد إظهار نافذة سعر الصرف لهذا المستخدم
- `exchangeRateBusinessIds` — المنشآت التي يُدير سعر صرفها
- `isSuperAdmin` — إظهار شاشة سجل التدقيق الكاملة

### 2.3 بقية النقاط

```
POST /API/User/refresh-token   { refreshToken }
POST /API/User/logout
```

### 2.4 في كل طلب لاحق

```
Authorization: Bearer <accessToken>
```

لا حاجة لإرسال `deviceToken` مرة أخرى — التوكن يحمل `sessionId` والتحقق يتم في الخادم.

> **ملاحظة:** يوجد تقييد لمحاولات الدخول المتكرّرة. عند تجاوز الحد يرجع `429`.

---

## 3. المغلف العام للاستجابة

**كل** نقطة نهاية ترجع نفس الشكل:

```ts
export interface APIResponse<T = unknown> {
  statusCode: number;
  isSuccess: boolean;
  errorMessages: string[];
  result: T;
}

export interface PaginatedResponse<T> {
  totalRecords: number;
  skip: number; // ⚠️ رقم الصفحة الحالية — لا إزاحة
  take: number;
  data: T[];
}
```

> ⚠️ **تحذير مهم — `skip` ليس إزاحة!**
> رغم اسمه، `skip` هو **رقم الصفحة** وهو يبدأ من `1` لا من `0`.
>
> - إرسال `skip=1` → الصفحة الأولى ✅
> - إرسال `skip=0` → الخادم يرفعه إلى `1`
> - استجابة `skip: 3` تعني **«أنت في الصفحة الثالثة»** وليس «تجاوزنا 3 سطور»
>
> الحساب في الخادم: `Skip((page - 1) * take)`.
> في سجل التدقيق الحقل اسمه `page` وهو بنفس المعنى (يبدأ من 1) — استخدم `page` هناك و`skip` في بقية القوائم.

`errorMessages` مصفوفة رسائل **جاهزة للعرض مباشرة** — لا حاجة لترجمتها (أغلبها عربية، وبعض رسائل نقاط سعر الصرف إنجليزية). حوّلها إلى نص هكذا:

```ts
const message = response.errorMessages?.join(" — ") ?? "حدث خطأ غير متوقع";
```

---

> ⚠️ **رمز HTTP ليس دائماً مطابقاً لـ`statusCode` داخل الجسم.**
> نقاط الإنشاء تُرجع **`200`** بينما الجسم يقول `"statusCode": 201` — والاستثناء الوحيد `POST /API/User/register` الذي يُرجع `201` فعلاً.
> كلها `2xx` فتُعامل كنجاح، لكن **لا تبنِ اختباراً آلياً على `201`** مع هذه النقاط.

## 4. الحركات — أهم قسم

### 4.1 إنشاء حركة

```
POST /API/Transaction?businessId={businessId}
Content-Type: multipart/form-data
```

```ts
export type CurrencyCode = "IQD" | "USD";

export interface AddTransactionPayload {
  Type: string; // 'cash in' | 'cash out'
  Date: string; // ISO
  Amount: number;
  Currency: CurrencyCode; // ← جديد
  ExchangeRate?: number; // ← جديد
  ExchangeDate?: string; // ← جديد
  Description?: string;
  ContactId?: string;
  CategoryId?: string;
  PaymentMethodId?: string;
  BookId: string;
  CustomFieldValues?: string; // JSON.stringify([{ value, customFieldId }])
  Files?: File[];
}
```

> ⚠️ **`CustomFieldValues` يجب أن تكون مصفوفة JSON من كائنات**، بهذا الشكل بالضبط:
> `[{"customFieldId":"…","value":"…"}]`.
> أي صيغة أخرى — مصفوفة نصوص، JSON مبتور، رقم مكان نصّ، أو `customFieldId` فارغ أو غائب —
> يرفضها الخادم برمز `400` ورسالة عربية واضحة. لا ترسل النص غير مُسلسَل.

### 4.2 القواعد الإلزامية — **هذه ما يرفض الطلب بـ400**

| الحالة                         | `Date` | `ExchangeRate` | `ExchangeDate` | النتيجة               |
| ------------------------------ | ------ | -------------- | -------------- | --------------------- |
| إيداع دولار `USD` + `cash in`  | ✅     | **✅ إلزامي**  | **✅ إلزامي**  | يُضاف لرصيد الدولار   |
| سحب دولار `USD` + `cash out`   | ✅     | **✅ إلزامي**  | ❌ **ممنوع**   | يُخصم من رصيد الدولار |
| إدخال دينار `IQD` + `cash in`  | ✅     | ❌             | ❌             | يُضاف لرصيد الدينار   |
| إخراج دينار `IQD` + `cash out` | ✅     | ❌             | ❌             | يُخصم من رصيد الدينار |

> ⚠️ **إرسال `ExchangeDate` مع سحب دولار يُرفض.** يجب إخفاء حقل «تاريخ الصرف» في النموذج عند اختيار نوع السحب.
> ⚠️ **إرسال `ExchangeRate` مع دينار يُرفض.**

**عند التعديل (PUT):** لا تُرسل الحقول الفارغة — القيمة `null` تعني «اتركها كما هي» في بعض الحقول، لكن إرسال `ExchangeRate` مع حركة دينار يُرفض.

### 4.3 دقة المبالغ

| العملة | المراتب المسموحة | مقبول                 | مرفوض       |
| ------ | ---------------- | --------------------- | ----------- |
| `USD`  | مرتبتان          | `1500.00` · `10.12`   | `10.123`    |
| `IQD`  | ثلاث مراتب       | `500000` · `1000.125` | `1000.1234` |

اقصرّ الإدخال في الواجهة على المراتب المسموحة قبل الإرسال.

### 4.4 شكل الحركة في الاستجابة

```ts
export interface TransactionDataResponse {
  id: string;
  type: string;
  date: string;
  description: string | null;
  amount: number;
  currency: CurrencyCode; // ← جديد
  exchangeRate: number | null; // ← جديد
  exchangeDate: string | null; // ← جديد
  categoryId: string | null;
  paymentMethod: PaymentMethodData | null;
  category: CategoryData | null;
  contact: ContactData | null;
  userId: string;
  user: UserData;
  bookId: string;
  customFieldId: string;
  createdAt: string;
  updatedAt: string;

  // الأرصدة — القديم يبقى للتوافق، والجديد هو المستخدم فعلاً
  newBalance: number; // ⚠️ قديم — يعرض رصيد الدينار فقط
  newBalanceIqd: number; // ← استخدم هذا
  newBalanceUsd: number; // ← استخدم هذا

  customFieldValues: CustomFieldValueData[];
  attachments: AttachmentData[];
}
```

> **مهم:** `newBalance` ما زال موجوداً للتوافق مع الكود القديم، لكنه **يعرض رصيد الدينار فقط**. استخدم `newBalanceIqd` و `newBalanceUsd` في كل عرض جديد.

### 4.5 مثال كامل — إيداع دولار

```ts
const form = new FormData();
form.append("Type", "cash in");
form.append("Date", "2026-09-15T10:00:00");
form.append("Amount", "1000.00");
form.append("Currency", "USD");
form.append("ExchangeRate", "1320.000");
form.append("ExchangeDate", "2026-09-14T00:00:00"); // إلزامي مع الإيداع
form.append("BookId", bookId);
form.append("Description", "إيداع دولار");
form.append(
  "CustomFieldValues",
  JSON.stringify([{ customFieldId: fieldId, value: "INV-1001" }]),
);

await fetch(`${BASE_URL}/API/Transaction?businessId=${businessId}`, {
  method: "POST",
  headers: { Authorization: `Bearer ${token}` },
  body: form,
});
```

### 4.6 بقية نقاط الحركات

| الطريقة  | المسار                                                                    | ملاحظة                       |
| -------- | ------------------------------------------------------------------------- | ---------------------------- |
| `GET`    | `/API/Transaction?businessId=&bookId=&skip=&take=`                        | قائمة الحركات                |
| `GET`    | `/API/Transaction/{id}`                                                   | حركة واحدة                   |
| `GET`    | `/API/Transaction/RawByBookId`                                            | **جديد** — بيانات خام للخزنة |
| `PUT`    | `/API/Transaction/{id}?businessId=`                                       | تعديل — كل الحقول اختيارية   |
| `DELETE` | `/API/Transaction/{id}?businessId=`                                       | حذف                          |
| `POST`   | `/API/Transaction/{id}/DuplicateToBook?targetBookId=&businessId=&isMove=` | نسخ أو نقل                   |

> **عند النجاح:** التعديل والحذف يؤثران على الأرصدة فوراً — أعد جلب الخزنة بعد كل عملية.

---

## 5. الخزائن — رصيدان لا رصيد واحد

```ts
export interface BookData {
  id: string;
  name: string;
  businessId: string;
  balance: number; // ⚠️ قديم — رصيد الدينار فقط
  balanceIqd: number; // ← استخدم هذا
  balanceUsd: number; // ← استخدم هذا
  createdAt: string;
  updatedAt: string;
}
```

### تفاصيل الخزنة

```
GET /API/Book/{id}
```

```ts
export interface BookDetailsData {
  id: string;
  name: string;
  businessId: string;

  cashInTotalIqd: number;
  cashOutTotalIqd: number;
  balanceIqd: number;

  cashInTotalUsd: number;
  cashOutTotalUsd: number;
  balanceUsd: number;

  // الحقول القديمة — للتوافق فقط
  cashInTotal: number;
  cashOutTotal: number;
  balance: number;

  setting: SettingData | null;
  createdAt: string;
  updatedAt: string;
}
```

**عرض الواجهة المقترح:**

```
الخزنة: الخزنة الرئيسية
├─ الدينار العراقي    الداخل 5,000,000 · الخارج 900,000 · الرصيد 4,100,000 IQD
└─ الدولار الأمريكي   الداخل 1,000.00  · الخارج 0.00       · الرصيد 1,000.00 USD
```

### بقية نقاط الخزائن

| الطريقة  | المسار                       |
| -------- | ---------------------------- |
| `GET`    | `/API/Book?businessId=`      |
| `POST`   | `/API/Book`                  |
| `PUT`    | `/API/Book/{id}`             |
| `DELETE` | `/API/Book/{id}`             |
| `PATCH`  | `/API/Book/{bookId}/setting` |

> **`DELETE` يرجع `409`** إذا كانت الخزنة مرتبطة بحركات أو أسعار صرف، مع رسالة عربية جاهزة للعرض. أظهرها كما هي.

---

## 6. نافذة سعر الصرف اليومي — ميزة جديدة كاملة

### 6.1 القواعد

- سعر الصرف **يومي ولكل خزنة على حدة**.
- من يملك الصلاحية؟ `isSuperAdmin` أو `canManageExchangeRate` من استجابة الدخول.
- المدير الرئيسي يُدير كل الخزائن؛ `portfolio_manager` يُدير خزائنه في `exchangeRateBusinessIds` فقط.
- **مالك المنشأة `owner` لا يُدير سعر الصرف** — هذا مطلوب صريحاً في المتطلّب.

### 6.2 خدمة جاهزة للنسخ

```ts
// services/ExchangeRate.ts
import { api } from "@/services/api";

export interface ExchangeRateStatus {
  bookId: string;
  bookName: string;
  currency: "IQD" | "USD";
  rateDate: string;
  rate: number | null;
  isSet: boolean; // هل سُجّل سعر لهذا التاريخ؟
}

export interface CurrentExchangeRate {
  bookId: string;
  currency: "IQD" | "USD";
  rate: number | null;
  rateDate: string | null;
  isToday: boolean; // هل السعر يخص اليوم؟
  hasValue: boolean; // هل يوجد سعر أصلاً؟
}

export interface ExchangeRateHistoryItem {
  id: string;
  bookId: string;
  currency: "IQD" | "USD";
  rate: number;
  rateDate: string;
  setByUserId: string | null;
  setByUserName: string | null;
  createdAt: string;
  updatedAt: string;
}

export const ExchangeRate = api.injectEndpoints({
  endpoints: (build) => ({
    ManagedExchangeRates: build.query<
      ExchangeRateStatus[],
      { businessId: string }
    >({
      query: (params) => ({
        url: "/API/ExchangeRate/managed",
        method: "GET",
        params,
      }),
      providesTags: ["ExchangeRate"],
    }),
    CurrentExchangeRate: build.query<
      CurrentExchangeRate,
      { bookId: string; currency?: "IQD" | "USD" }
    >({
      query: (params) => ({
        url: "/API/ExchangeRate/current",
        method: "GET",
        params, // bookId إلزامي · currency افتراضاً USD
      }),
      providesTags: ["ExchangeRate"],
    }),
    ExchangeRateHistory: build.query<
      ExchangeRateHistoryItem[],
      { bookId: string; currency?: "IQD" | "USD"; from?: string; to?: string }
    >({
      query: (params) => ({
        url: "/API/ExchangeRate/history",
        method: "GET",
        params,
      }),
      providesTags: ["ExchangeRate"],
    }),
    SetExchangeRate: build.mutation<
      unknown,
      {
        bookId: string;
        rate: number;
        currency?: "IQD" | "USD";
        rateDate?: string;
      }
    >({
      query: (body) => ({ url: "/API/ExchangeRate", method: "POST", body }),
      invalidatesTags: ["ExchangeRate"],
    }),
  }),
  overrideExisting: true,
});

export const {
  useManagedExchangeRatesQuery,
  useCurrentExchangeRateQuery,
  useLazyCurrentExchangeRateQuery,
  useExchangeRateHistoryQuery,
  useSetExchangeRateMutation,
} = ExchangeRate;
```

### 6.3 وسائط كل نقطة

| النقمة                          | الوسائط                                                         |
| ------------------------------- | --------------------------------------------------------------- |
| `GET /API/ExchangeRate/managed` | `businessId` **إلزامي** — يرجع `403` إن لم تكن عضواً في المنشأة |
| `GET /API/ExchangeRate/current` | `bookId` إلزامي · `currency` (افتراضاً `USD`)                   |
| `GET /API/ExchangeRate/history` | `bookId` إلزامي · `currency` · `from` · `to`                    |
| `POST /API/ExchangeRate`        | جسم الطلب `{ bookId, currency?, rate, rateDate? }`              |

### 6.4 التدفق في الواجهة

```
1. المستخدم يسحب دولاراً → افتح النموذج
2. GET /API/ExchangeRate/current?bookId=…   → املأ حقل سعر الصرف تلقائياً
3. افحص الحقول بترتيبها:
     • hasValue = false          → «لم يُثبَّت سعر صرف لهذه الخزنة»
     • hasValue = true, isToday = false → «آخر سعر مسجَّل كان بتاريخ … — ثبّت سعر اليوم»
     • isToday = true           → القيمة صالحة، املأ الحقل
4. في شاشة الإدارة: GET /API/ExchangeRate/managed?businessId=… ثم POST /API/ExchangeRate
     • استخدم isSet لتمييز الخزائن التي لم يُثبَّت لها سعر اليوم
```

> **قيد مهم:** سعر الصرف يُدار لخزائن الدولار. النقاط تتعامل مع العملة صراحةً (`currency`)، وقيمتها الافتراضية `USD`. إن أردت عرض سعر الدينار مرّر `currency=IQD`.

> **لماذا `current` مهم؟** يمنع المستخدم من كتابة سعر خاطئ، ويُعطي قيمة صحيحة افتراضية.

---

## 7. سجل التدقيق — شاشة جديدة كاملة

### 7.1 ما يعرضه

كل عملية جرت في النظام: من فتح شاشة حتى تعديل مبلغ، مع **القيمة القديمة والجديدة لكل حقل تغيّر**.

### 7.2 خدمة جاهزة للنسخ

```ts
// services/AuditLog.ts
import { api } from "@/services/api";

export interface AuditLogItem {
  id: number;
  occurredAt: string;
  correlationId: string;

  category: string; // Auth | Data | Access | System
  categoryLabel: string; // وسم عربي جاهز
  action: string; // 'transaction.update'
  actionLabel: string; // وسم عربي جاهز
  severity: string; // Info | Warning | Critical
  severityLabel: string; // وسم عربي جاهز
  summary: string | null; // جملة عربية جاهزة للعرض

  isSuccess: boolean;
  statusCode: number | null;

  userId: string | null;
  username: string | null;
  businessId: string | null;
  bookId: string | null;

  entityName: string | null;
  entityLabel: string | null;
  entityId: string | null;
  operation: string | null;
  operationLabel: string | null;

  httpMethod: string | null;
  path: string | null;
  ipAddress: string | null;
  durationMs: number | null;
  isSlow: boolean;
}

// تفاصيل السطر = ما سبق + هذه الحقول
export interface AuditLogDetail extends AuditLogItem {
  queryString: string | null;
  dataJson: string | null; // هنا فرق قبل/بعد
  error: string | null;
  userAgent: string | null;
  sessionId: string | null;
  deviceToken: string | null;
}

export interface AuditBreakdownItem {
  key: string;
  label: string;
  count: number;
}

export interface AuditDailyPoint {
  date: string;
  total: number;
  failures: number;
}

export interface AuditStats {
  from: string;
  to: string;
  rangeDays: number;
  total: number;
  failures: number;
  securityEvents: number;
  criticalEvents: number;
  slowRequests: number;
  distinctUsers: number;
  averageDurationMs: number;
  byCategory: AuditBreakdownItem[];
  bySeverity: AuditBreakdownItem[];
  byAction: AuditBreakdownItem[];
  byUser: AuditBreakdownItem[];
  daily: AuditDailyPoint[];
}

export interface AuditStatus {
  enabled: boolean;
  pendingInQueue: number;
  droppedTotal: number;
  writtenTotal: number;
  retentionDays: number;
  slowRequestMs: number;
  logReadRequests: boolean;
  totalRows: number;
  oldestRowUtc: string | null;
  newestRowUtc: string | null;
}

export interface AuditLogQueryParams {
  from?: string;
  to?: string;
  days?: number;
  userId?: string;
  username?: string;
  businessId?: string;
  bookId?: string;
  correlationId?: string;
  entityName?: string;
  entityId?: string;
  category?: string;
  action?: string;
  severity?: string;
  isSuccess?: boolean;
  onlyFailures?: boolean;
  onlySlow?: boolean;
  onlySecurityEvents?: boolean;
  search?: string;
  page?: number; // يبدأ من 1
  take?: number; // الافتراضي 25، الأقصى 200
  sortBy?: string;
  sortDirection?: "asc" | "desc";
}

export const AuditLog = api.injectEndpoints({
  endpoints: (build) => ({
    AuditLogData: build.query<
      {
        totalRecords: number;
        skip: number;
        take: number;
        data: AuditLogItem[];
      },
      AuditLogQueryParams
    >({
      query: (params) => ({ url: "/API/AuditLog", method: "GET", params }),
      providesTags: ["AuditLog"],
    }),
    AuditLogById: build.query<AuditLogDetail, { id: number }>({
      query: ({ id }) => ({ url: `/API/AuditLog/${id}`, method: "GET" }),
      providesTags: ["AuditLog"],
    }),
    AuditLogTrace: build.query<AuditLogItem[], { correlationId: string }>({
      query: ({ correlationId }) => ({
        url: `/API/AuditLog/trace/${correlationId}`,
        method: "GET",
      }),
      providesTags: ["AuditLog"],
    }),
    AuditLogStats: build.query<
      AuditStats,
      { days?: number; businessId?: string }
    >({
      query: (params) => ({
        url: "/API/AuditLog/stats",
        method: "GET",
        params,
      }),
      providesTags: ["AuditLog"],
    }),
    AuditLogVocabulary: build.query<Record<string, unknown>, void>({
      query: () => ({ url: "/API/AuditLog/vocabulary", method: "GET" }),
    }),
    AuditLogStatus: build.query<AuditStatus, void>({
      query: () => ({ url: "/API/AuditLog/status", method: "GET" }),
    }),
  }),
  overrideExisting: true,
});

export const {
  useAuditLogDataQuery,
  useLazyAuditLogDataQuery,
  useAuditLogByIdQuery,
  useAuditLogTraceQuery,
  useAuditLogStatsQuery,
  useAuditLogVocabularyQuery,
  useAuditLogStatusQuery,
} = AuditLog;
```

### 7.3 إظهار «قبل ← بعد» في شاشة التفاصيل

الحقل `dataJson` نص JSON. عندما يكون تعديلاً على بيانات يحتوي هذا الشكل:

```json
{
  "changes": [
    { "field": "Amount", "old": "900000.000", "new": "888888" },
    { "field": "Description", "old": "Office rent", "new": "FinalVerified" }
  ]
}
```

```tsx
interface FieldChange {
  field: string;
  old: string | null;
  new: string | null;
}

function renderChanges(dataJson: string | null) {
  if (!dataJson) return null;
  try {
    const parsed = JSON.parse(dataJson) as { changes?: FieldChange[] };
    if (!parsed.changes?.length) return <pre>{dataJson}</pre>;

    return (
      <table>
        <thead>
          <tr>
            <th>الحقل</th>
            <th>القيمة القديمة</th>
            <th>القيمة الجديدة</th>
          </tr>
        </thead>
        <tbody>
          {parsed.changes.map((c, i) => (
            <tr key={i}>
              <td>{c.field}</td>
              <td className="text-red-600">{c.old ?? "—"}</td>
              <td className="text-green-700">{c.new ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    );
  } catch {
    return <pre>{dataJson}</pre>;
  }
}
```

> **ملاحظة أمنية:** أي حقل حساس (كلمة مرور، توكن، مفتاح) يُستبدل تلقائياً بـ`***` قبل الحفظ. لا حاجة لإخفاء شيء في الواجهة.

### 7.4 تصدير CSV

`GET /API/AuditLog/export` يرجع ملفاً بترميز `UTF-8 BOM` يفتح صحيحاً في Excel العربي.

```ts
const res = await fetch(
  `${BASE_URL}/API/AuditLog/export?${new URLSearchParams(params as any)}`,
  { headers: { Authorization: `Bearer ${token}` } },
);
const blob = await res.blob();
const url = URL.createObjectURL(blob);
const a = document.createElement("a");
a.href = url;
a.download = "audit-log.csv";
a.click();
```

> ⚠️ استخدم `fetch` مباشرةً — لا تُمرر هذا الطلب عبر `api` لأنه يرجع ملفاً لا JSON.

### 7.5 القيود

| القيد         | القيمة     |
| ------------- | ---------- |
| أقصى مدى زمني | 366 يوماً  |
| أقصى حجم صفحة | 200 سطر    |
| أقصى تصدير    | 20,000 سطر |
| `page`        | يبدأ من 1  |

### 7.6 الصلاحيات

- **المدير الرئيسي:** يرى كل السطور.
- **غيره:** يرى سطور منشأته، أو سطور الخزائن التابعة لمنشأته.
- **طلب منشأة لا تملك عليها دور إدارة:** `403`.

---

## 8. النسخ الاحتياطي

| الطريقة | المسار                | الوصف                            |
| ------- | --------------------- | -------------------------------- |
| `POST`  | `/API/Backup/export`  | تصدير خزنة إلى ملف `cbk` مشفّر   |
| `POST`  | `/API/Backup/inspect` | معاينة محتوى الملف قبل الاستيراد |
| `POST`  | `/API/Backup/import`  | استيراد أو دمج الخزنة            |

- كلها `multipart/form-data` ما عدا التصدير فهو `application/json`.
- التصدير يتطلّب دور `owner` أو `partner`.
- الاستيراد فيه خياران: **إنشاء خزنة جديدة** أو **الدمج في خزنة موجودة**.

### 8.1 شكل الطلبات

```ts
// التصدير — JSON
POST /API/Backup/export
{ bookId: string, password?: string }

// المعاينة — multipart
POST /API/Backup/inspect
FormData: file (File), password?

// الاستيراد — multipart
POST /API/Backup/import
FormData: file (File), password?, businessId (Guid), targetBookId? (Guid), newBookName? (string)
```

| الحقل          | ملاحظة                                                                          |
| -------------- | ------------------------------------------------------------------------------- |
| `password`     | اختياري، لكن **إن صدّرت بكلمة مرور فيجب إرسالها نفسها عند الاستيراد والمعاينة** |
| `targetBookId` | املأه للدمج في خزنة موجودة                                                      |
| `newBookName`  | املأه لإنشاء خزنة جديدة                                                         |
| `businessId`   | إلزامي في الاستيراد                                                             |

> ⚠️ إرسال `targetBookId` **و** `newBookName` معاً — اختر واحداً فقط حسب اختيار المستخدم.

> **تدفق الواجهة الصحيح:** المعاينة أولاً (`inspect`) ثم عرض ملخّص للمستخدم (عدد الحركات، المرفقات، الأرصدة) ثم تأكيد الاستيراد.

---

## 9. سجل تعديلات الحركة

| الطريقة | المسار                                                 |
| ------- | ------------------------------------------------------ |
| `GET`   | `/API/TransactionHistory/ByBook?bookId=`               |
| `GET`   | `/API/TransactionHistory/ByTransaction?transactionId=` |

```ts
export interface TransactionHistoryData {
  id: string;
  transactionId: string | null; // ← صار قابلاً للعدم
  type: string;
  amount: number;
  date: string;
  description: string | null;
  exchangeRate: number | null; // ← جديد
  exchangeDate: string | null; // ← جديد
  userId: string;
  user: UserData | null;
  createdAt: string;
}
```

> ⚠️ `transactionId` صار **قابلاً للعدم** (`string | null`). راجع أي كود يستخدمه كمعرّف إلزامي.

---

## 10. الأدوار والصلاحيات

```ts
export const Roles = {
  Owner: "owner",
  Partner: "partner",
  Viewer: "viewer",
  Admin: "admin",
  Staff: "staff",
  DataOperator: "dataoperator",
  PrivateViewer: "privateviewer",
  PortfolioManager: "portfolio_manager",
} as const;
```

| المجموعة                                        | الأدوار                                                    |
| ----------------------------------------------- | ---------------------------------------------------------- |
| **Writers** — إنشاء وتعديل البيانات             | `owner`, `partner`, `admin`, `staff`, `dataoperator`       |
| **Management** — إدارة الأعضاء والنسخ الاحتياطي | `owner`, `partner`                                         |
| **TransactionDuplicate**                        | كل ما سبق + `viewer`, `privateviewer`, `portfolio_manager` |
| **سعر الصرف**                                   | `isSuperAdmin` فقط أو `portfolio_manager` على خزائنه       |

**مهم:** لا تعتمد على الأدوار وحدها في إخفاء الأزرار؛ اعتمد على حقول استجابة الدخول:

```tsx
const showExchangeRateWindow = user.isSuperAdmin || user.canManageExchangeRate;
const showFullAudit = user.isSuperAdmin;
```

### 10.1 نطاق الخزنة — وسمة `businessId` في نقاط الحركات والخزائن

**قاعدة واحدة تحكم كل نقاط الحركات والخزائن:**

> يُستخرج نطاق الصلاحية من **الخزنة نفسها**، لا من `businessId` الذي ترسله الواجهة.

عملياً:

- **استمر في إرسال `businessId` كما تفعل الآن** — النقاط تقبله ولن ينكسر شيء. لكن الخادم يتجاهله في التحقق ويقرأ منشأة الخزنة الحقيقية.
- إن أرسلت `businessId` لا يخصّ الخزنة فالنتيجة **403**. لا تحاول «إصلاح» الخطأ بإرسال منشأة أخرى: هذا هو الفرق نفسه الذي يمنع مستخدماً في منشأة من الكتابة في دفاتر منشأة أخرى.
- **نطاق الخزنة يُفحص في الكتابة كما في القراءة.** العضو بدور مقيَّد بخزائن (`staff`, `admin`, `dataoperator`, `privateviewer`, `portfolio_manager`) لا يستطيع إنشاء حركة في خزنة غير مسندة إليه ولو كانت في منشأته. لذلك **اعرض في شاشة إضافة الحركة خزائن المستخدم المسندة فقط**، وإلا سيرى خزائن ثم يقابل 403 عند الحفظ.
- `isSuperAdmin` **لا يتجاوز** هذا الفحص. المدير الرئيسي يحتاج عضوية في المنشأة ليقرأ خزائنها وحركاتها؛ ما يمنحه العلم بلا عضوية شيئان فقط: سجل التدقيق كاملاً، وسعر الصرف لكل الخزائن.

**النقاط التي تنطبق عليها القاعدة:** `POST/PUT/DELETE /API/Transaction` · `POST /API/Transaction/{id}/DuplicateToBook` · `PUT/DELETE /API/Book/{id}`.

**قيد إضافي على النسخ:** الخزنة الهدف يجب أن تكون في **المنشأة نفسها** للحركة المصدر، وإلا فـ403.

**قيد إضافي على تعديل الخزنة:** تغيير `businessId` في `PUT /API/Book/{id}` يعني **نقل الخزنة** إلى منشأة أخرى، ويشترط صلاحية إدارة على المنشأتين معاً. الأفضل ألا ترسل إلا منشأة الخزنة الحالية.

### 10.2 الحقول المخصصة — سبب رسالة «ليس لديك الصلاحية»

`GET /API/CustomField?bookId=` تُطلب في **كل صفحة محفظة**. وكانت ترفض بـ403 بعض المستخدمين الذين يملكون الصلاحية فعلاً، فتظهر رسالة الخطأ بلا سبب ظاهر.

**السبب كان في الخادم وأُصلح:** كانت قراءة الدور لا تحدّد المنشأة، فتأخذ دوراً غير محدَّد من بين كل عضويات المستخدم. فشريك في منشأة وقارئ في أخرى كان يُرفض في منشأته هو. الآن يُستخرج الدور من **منشأة الخزنة المطلوبة**.

**القاعدة الحالية:**

| العملية                                       | من يملكها                                                                                       |
| --------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| قراءة الحقول (`GET`)                          | كل من يقرأ **حركات** الخزنة — فالقيم تُعاد داخل الحركة أصلاً، فيتعذّر إرجاع القيمة ومنع تعريفها |
| إضافة وتعديل وحذف (`POST` · `PUT` · `DELETE`) | أدوار الكتابة: `owner` · `partner` · `admin` · `staff` · `dataoperator`                         |
| النطاق                                        | كلها مقيَّدة بالخزنة نفسها: `bookId` يحدّد المنشأة، والأدوار المقيَّدة تُفحص ضد `bookIds`       |

**ملاحظة للواجهة:** الرسالة الحالية تظهر عند **أي** رد بـ403 من **أي** طلب، بينما بعض الردود بـ403 **مقصودة** وليست خطأً — مثل طلب قائمة الأعضاء أو سجل التدقيق بدور `admin`، فهما للمالك والشريك فقط. الأدق ربط الرسالة بالطلب نفسه بدل إظهارها عامة، وإلا ظهرت أخطاء وهمية لمستخدم يعمل بشكل سليم.

---

## 11. رموز الأخطاء

| الرمز | المعنى                                                   | ماذا تفعل في الواجهة                                     |
| ----- | -------------------------------------------------------- | -------------------------------------------------------- |
| `400` | مدخلات خاطئة أو خرق قاعدة (مثل إيداع دولار بدون سعر صرف) | **اعرض `errorMessages` كما هي** — عربية جاهزة            |
| `401` | التوكن غير صالح أو الجلسة منتهية                         | جدّد التوكن، وإن فشل → اخرج لشاشة الدخول                 |
| `403` | لا تملك الصلاحية                                         | أظهر «لا تملك صلاحية هذه العملية»                        |
| `404` | العنصر غير موجود                                         | أظهر «العنصر غير موجود» وحدّث القائمة                    |
| `405` | طريقة HTTP غير مسموحة                                    | خطأ في الكود — راجع المسار                               |
| `409` | **تعارض حذف** — العنصر مرتبط ببيانات أخرى                | **اعرض الرسالة العربية كما هي** — تشرح ما يجب حذفه أولاً |
| `429` | محاولات دخول كثيرة                                       | أظهر «حاول بعد قليل»                                     |
| `500` | خطأ خادم                                                 | أظهر رسالة عامة وسجّل التفاصيل في الـconsole             |

**مثال معالجة موحّدة:**

```ts
const handleError = (error: any) => {
  const status = error?.status;
  const messages: string[] = error?.data?.errorMessages?.length
    ? error.data.errorMessages
    : error?.data?.message
      ? [error.data.message]
      : [];

  switch (status) {
    case 400:
    case 409:
      return messages.join(" — ") || "البيانات غير صحيحة";
    case 401:
      return "انتهت الجلسة — أعد تسجيل الدخول";
    case 403:
      return "لا تملك صلاحية هذه العملية";
    case 404:
      return "العنصر غير موجود";
    default:
      return messages.join(" — ") || "حدث خطأ غير متوقع";
  }
};
```

---

## 12. جدول مرجعي سريع — كل النقاط ووسائطها

> الوسائط المؤشَّرة بـ**إلزامي** يُرجع غيابها `400`. ما لم يُذكر فهو `[FromQuery]`.

### 12.1 المصادقة والمستخدمون

| الطريقة  | المسار                    | الوسائط                                                     |
| -------- | ------------------------- | ----------------------------------------------------------- |
| `POST`   | `/API/User/register`      | **جسم JSON** `RegisterationRequestDto`                      |
| `POST`   | `/API/User/login`         | **جسم JSON** `{ username?, email?, password, deviceToken }` |
| `POST`   | `/API/User/refresh-token` | **جسم JSON** `{ refreshToken }`                             |
| `POST`   | `/API/User/logout`        | —                                                           |
| `GET`    | `/API/User`               | `businessId` إلزامي · `skip`=1 · `take`=25 · `search`       |
| `GET`    | `/API/User/{id}`          | `businessId` إلزامي                                         |
| `PATCH`  | `/API/User/{id}`          | **multipart** `UserUpdateDto` — انتبه: ليس JSON             |
| `DELETE` | `/API/User/{id}`          | `businessId` إلزامي                                         |

### 12.2 المنشآت والأعضاء

| الطريقة  | المسار                                              | الوسائط                                          |
| -------- | --------------------------------------------------- | ------------------------------------------------ |
| `GET`    | `/API/BusinessUser`                                 | `businessId` إلزامي · `skip` · `take` · `search` |
| `GET`    | `/API/BusinessUser/{id}/details`                    | —                                                |
| `PUT`    | `/API/BusinessUser/{id}`                            | **جسم JSON** `UpdateBusinessUserDto`             |
| `DELETE` | `/API/BusinessUser/{id}`                            | `businessId` إلزامي                              |
| `DELETE` | `/API/BusinessUser/deleteByBusinessAndUser`         | `businessId` + `targetUserId` إلزاميان           |
| `DELETE` | `/API/BusinessUser/{businessUserId}/books/{bookId}` | مسار — إزالة خزنة من عضو                         |
| `POST`   | `/API/BusinessUser/exchange-owner`                  | `targetUserId` + `businessId` إلزاميان           |

> **`exchange-owner`** ينقل ملكية المنشأة ويبدّل الأدوار (`owner` ↔ `partner`). كان معطّلاً وأُصلح — متاح الآن.

### إضافة عضو — `POST /API/invitations`

> ضمن جدول الأعضاء أعلاه، وتفصيله هنا لأن له سلوكاً خاصاً.

```json
{
  "email": "user@example.com",
  "role": "staff",
  "businessId": "<guid>",
  "bookIds": ["<guid>"]
}
```

| الحقل     | ملاحظة                                                                                                 |
| --------- | ------------------------------------------------------------------------------------------------------ |
| `email`   | يجب أن يكون بريد **مستخدم مسجَّل** — غير المسجَّل يرجع `404`                                           |
| `role`    | من قائمة الأدوار القابلة للإسناد — ودور `owner` لا يُسنَد إلا من مالك المنشأة                          |
| `bookIds` | مطلوب للأدوار المقيَّدة بخزائن: `staff`, `dataoperator`, `admin`, `privateviewer`, `portfolio_manager` |

**الصلاحية:** `owner` أو `partner` في المنشأة، وإلا `403`.

**الاستجابة:**

```json
{
  "message": "member added successfully.",
  "emailSent": true,
  "emailNote": "أُرسل إشعار البريد إلى العضو."
}
```

> ⚠️ **العضو يُضاف مباشرة — لا يوجد تدفّق «قبول دعوة»**: لا جدول دعوات ولا رمز قبول ولا نقطة قبول.
> و`emailSent = false` تعني أن **الإشعار لم يصل، لا أن الإضافة فشلت** — العضوية محفوظة في الحالتين.
> اعرض `emailNote` للمستخدم ليعرف السبب. وضبط خادم البريد موصوف في `MAIL-SETUP.md`.

### 12.3 الخزائن

| الطريقة  | المسار                       | الوسائط                                                                       |
| -------- | ---------------------------- | ----------------------------------------------------------------------------- |
| `GET`    | `/API/Book`                  | `businessId` إلزامي · `skip` · `take` · `search` · `sortBy` · `sortDirection` |
| `GET`    | `/API/Book/{id}`             | مسار                                                                          |
| `POST`   | `/API/Book`                  | **جسم JSON** `CreateBookDto`                                                  |
| `PUT`    | `/API/Book/{id}`             | **جسم JSON** `UpdateBookDto`                                                  |
| `DELETE` | `/API/Book/{id}`             | `businessId` إلزامي — قد يرجع `409`                                           |
| `PATCH`  | `/API/Book/{bookId}/setting` | **جسم JSON** `UpdateSettingDto`                                               |

### 12.4 الحركات

| الطريقة  | المسار                                  | الوسائط                                                                |
| -------- | --------------------------------------- | ---------------------------------------------------------------------- |
| `GET`    | `/API/Transaction`                      | `bookId` + `businessId` إلزاميان · `skip`=1 · `take`=25 · `amount` · … |
| `GET`    | `/API/Transaction/RawByBookId`          | `bookId` + `businessId` إلزاميان                                       |
| `GET`    | `/API/Transaction/{id}`                 | مسار                                                                   |
| `POST`   | `/API/Transaction`                      | `businessId` إلزامي · **multipart** `CreateTransactionDto`             |
| `PUT`    | `/API/Transaction/{id}`                 | `businessId` إلزامي · **multipart** `UpdateTransactionDto`             |
| `DELETE` | `/API/Transaction/{id}`                 | `businessId` إلزامي                                                    |
| `POST`   | `/API/Transaction/{id}/DuplicateToBook` | `targetBookId` + `businessId` إلزاميان · `isMove`                      |

> ⚠️ **الحركات كلها `multipart/form-data`** ما عدا القراءة. لا تُرسل JSON في `POST` أو `PUT` للحركات.

### 12.5 سعر الصرف

| الطريقة | المسار                      | الوسائط                                               |
| ------- | --------------------------- | ----------------------------------------------------- |
| `GET`   | `/API/ExchangeRate/managed` | `businessId` إلزامي                                   |
| `GET`   | `/API/ExchangeRate/current` | `bookId` إلزامي · `currency`=`USD`                    |
| `GET`   | `/API/ExchangeRate/history` | `bookId` إلزامي · `currency`=`USD` · `from` · `to`    |
| `POST`  | `/API/ExchangeRate`         | **جسم JSON** `{ bookId, currency?, rate, rateDate? }` |

### 12.6 سجل التدقيق

| الطريقة | المسار                                | الوسائط                                  |
| ------- | ------------------------------------- | ---------------------------------------- |
| `GET`   | `/API/AuditLog`                       | كل حقول `AuditLogQueryParams` (اختيارية) |
| `GET`   | `/API/AuditLog/{id}`                  | `businessId` اختياري                     |
| `GET`   | `/API/AuditLog/trace/{correlationId}` | `businessId` اختياري                     |
| `GET`   | `/API/AuditLog/stats`                 | نفس وسائط البحث                          |
| `GET`   | `/API/AuditLog/export`                | نفس وسائط البحث — يرجع **ملف CSV**       |
| `GET`   | `/API/AuditLog/vocabulary`            | —                                        |
| `GET`   | `/API/AuditLog/status`                | —                                        |

### 12.7 النسخ الاحتياطي وسجل التعديلات

| الطريقة | المسار                                  | الوسائط                                                                          |
| ------- | --------------------------------------- | -------------------------------------------------------------------------------- |
| `POST`  | `/API/Backup/export`                    | **جسم JSON** `{ bookId, password? }`                                             |
| `POST`  | `/API/Backup/inspect`                   | **multipart** `file`, `password?`                                                |
| `POST`  | `/API/Backup/import`                    | **multipart** `file`, `password?`, `businessId`, `targetBookId?`, `newBookName?` |
| `GET`   | `/API/TransactionHistory/ByBook`        | `bookId`                                                                         |
| `GET`   | `/API/TransactionHistory/ByTransaction` | `transactionId`                                                                  |

### 12.8 نقاط CRUD الأخرى

`Business` · `Category` · `Contact` · `CustomField` · `PaymentMethod` كلها تتبع نفس النمط:

```
GET    /API/{Name}?businessId=&skip=1&take=25&search=
GET    /API/{Name}/{id}
POST   /API/{Name}            → [FromBody] JSON
PUT    /API/{Name}/{id}       → [FromBody] JSON
DELETE /API/{Name}/{id}?businessId=
```

للتأكد من الأسماء الدقيقة للحقول، ارجع إلى `swagger.json`.

---

## 13. قائمة تحقق للربط

### المرحلة الأولى — لا شيء ينكسر

- [ ] استورد `swagger.json` في Postman
- [ ] تأكد أن الدخول يعمل ويعيد `accessToken` و `canManageExchangeRate`
- [ ] أضف `currency` إلا `AddTransactionPayload`
- [ ] أضف الحقول الجديدة إلى `TransactionDataResponse`
- [ ] أضف `balanceIqd` و `balanceUsd` إلى نوع الخزنة
- [ ] اعرض رصيدين في شاشة الخزائن

### المرحلة الثانية — ميزة الدولار

- [ ] أضف حقل اختيار العملة في نموذج الحركة
- [ ] أظهر حقلي `ExchangeRate` و `ExchangeDate` عند اختيار `USD`
- [ ] **أخفِ `ExchangeDate` عند اختيار «سحب دولار»**
- [ ] قيّد المراتب العشرية حسب العملة
- [ ] اختبر: إيداع دولار بدون سعر صرف → يجب أن يرجع `400` برسالة عربية
- [ ] اختبر: سحب دولار مع تاريخ صرف → يجب أن يرجع `400`

### المرحلة الثالثة — الميزات الجديدة

- [ ] نافذة سعر الصرف (تظهر فقط لـ`canManageExchangeRate`)
- [ ] شاشة سجل التدقيق + عرض «قبل ← بعد»
- [ ] زر تصدير CSV
- [ ] واجهة النسخ الاحتياطي (معاينة ثم استيراد)
- [ ] شاشة سجل تعديلات الحركة

---

## 14. ملاحظات أخيرة

1. **`Swagger` معطَّل على الإنتاج** — يعمل في بيئة التطوير فقط. اعتمد على `swagger.json` المرفق.
2. **`newBalance` و `balance` و `cashInTotal` حقول قديمة** — تعمل لكنها تعرض الدينار فقط. لا تبنِ عليها.
3. **مولّد البيانات التجريبية غير موجود** — لا يوجد أمر `--seed`. للتجربة، إما أدخل البيانات من الواجهة أو استورد خزنة من ملف `cbk`.
4. **الخادم يحتاج `AdminUser` مضبوطاً** لتعليم حساب كمدير رئيسي؛ على قاعدة بيانات جديدة لا يوجد أي Super Admin.
5. **عند أي `409`** الرسالة العربية تشرح بالضبط ما يجب حذفه أولاً — اعرضها للمستخدم كما هي.
