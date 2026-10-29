# syntax=docker/dockerfile:1
# ============================================================================
#  cashbook — واجهة برمجة .NET 8
#  صورة إنتاجية من مرحلتين: البناء بمحرّك SDK، والتشغيل بخادم ASP.NET Core فقط
#  (الصورة النهائية لا تحتوي المترجم ولا أدوات البناء، فهي أصغر وأأمن).
# ============================================================================

# ---------------------------------------------------------------------------
#  المرحلة 1 — البناء
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# نسخ ملف المشروع وحده أولاً: ما دام لم يتغيّر، تُستعاد طبقة restore المخزّنة
# ولا يُنزَّل أي حزمة من الإنترنت عند كل بناء.
COPY cashbook.csproj ./
RUN dotnet restore cashbook.csproj

# ثم بقية الملفات (bin/ و obj/ و .env مُستثناة في .dockerignore)
COPY . .

RUN dotnet publish cashbook.csproj \
  --configuration Release \
  --no-restore \
  --output /app/publish \
  /p:UseAppHost=false

# ---------------------------------------------------------------------------
#  المرحلة 2 — التشغيل
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# curl لأجل HEALTHCHECK فقط (الصورة الأساسية لا تحتويها).
# DL3008 يطلب تثبيت رقم إصدار الحزمة، وذلك يهدم البناء لمجرّد صدور إصدار
# تصحيحي جديد في Debian — وهو ضرر أكبر من الفائدة المحدودة المرجوّة هنا.
# hadolint ignore=DL3008
RUN apt-get update \
  && apt-get install -y --no-install-recommends curl \
  && rm -rf /var/lib/apt/lists/*

# ملاحظات على الإعدادات:
#  • appsettings.json يثبّت Kestrel على http://localhost:5003 — أي على واجهة
#    الاسترجاع داخل الحاوية فقط، فلا يصل إليه أي طلب من خارجها.
#    المتغيّر Kestrel__Endpoints__Http__Url أدناه يتجاوز قيمة الملف ويجعل
#    الخادم ينصت على كل الواجهات، وبدونه لا يعمل الحاوي إطلاقاً.
#  • لا سرّ واحد مكتوب هنا. القيم الحسّاسة تُمرَّر وقت التشغيل متغيّراتِ بيئة
#    (أو من ملف .env يحمل أسماءً بالشكل Section__Key كما في GO-LIVE.md).
ENV ASPNETCORE_ENVIRONMENT=Production \
  Kestrel__Endpoints__Http__Url=http://+:8080 \
  ASPNETCORE_HTTP_PORTS=8080 \
  DOTNET_EnableDiagnostics=0 \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  DOTNET_NOLOGO=1 \
  TZ=Asia/Baghdad

# تُنسخ الملفات بمالك غير جذري مباشرةً. لو نسخناها كجذر ثم غيّرنا المالك بأمر
# chown لاحق، لتضاعف حجم الصورة تقريباً: كل ملف يُعاد نسخه في طبقة جديدة.
COPY --chown=app:app --from=build /app/publish ./

# مجلد المرفقات: يُثبّت كـ volume في docker-compose حتى لا تُفقد الصور
# المرفوعة عند إعادة بناء الصورة. وكونه ملكاً لـ app ضروري: عند أول إنشاء
# للـ volume تنسخ Docker صلاحيات المجلد من الصورة، فإن كان ملكه الجذر عجز
# التطبيق غير الجذري عن الكتابة فيه وفشل رفع المرفقات.
RUN mkdir -p /app/wwwroot/uploads \
  && chown -R app:app /app/wwwroot

# التشغيل بمستخدم غير جذري: صورة aspnet:8.0 تنصّب المستخدم app (uid 1654).
# هذا أهم مكسب أمني في الملف: لو نُفّذت ثغرة عن بعد ووصل المنفّذ إلى الحاوية،
# وجد نفسه بلا صلاحيات جذر. والمنفذ 8080 أكبر من 1024 فلا يلزم الجذر للربط.
USER app

EXPOSE 8080

# فحص جاهزية: يكفي أن يستجيب خادم HTTP بأي رمز (لا يشترط نجاح الطلب) —
# المسار / يعيد 404 وهذا طبيعي، فالمهم أن Kestrel يستقبل ويعالج.
# صيغة exec (قائمة JSON) بلا shell: رمز خروج curl هو نتيجة الفحص — صفر على
# أي رد (ولو 404 أو 500)، وغير صفر فقط إن رفض الاتصال أو انتهت المهلة.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD ["curl", "-s", "-o", "/dev/null", "http://127.0.0.1:8080/"]

ENTRYPOINT ["dotnet", "cashbook.dll"]
