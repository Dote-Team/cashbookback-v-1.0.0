"""Idle auto-logout: activity keeps the session alive, idle sessions die (refresh included)."""
import os
ROOT_USERNAME = os.environ.get("CASHBOOK_ROOT_USERNAME", "root")
ROOT_PASSWORD = os.environ["CASHBOOK_ROOT_PASSWORD"]
DEMO_PASSWORD = os.environ["CASHBOOK_DEMO_PASSWORD"]
# The test moves LastActivityAt back in time directly in the database (instead of waiting 5 minutes).
SQL_SERVER = os.environ.get("CASHBOOK_SQL_SERVER", r"localhost\SQLEXPRESS")
SQL_DATABASE = os.environ.get("CASHBOOK_SQL_DATABASE", "cash")

import json, time, urllib.request, urllib.error, subprocess

BASE = "http://localhost:5003/API"
results = []


def call(method, path, token=None, body=None):
    h = {}
    data = None
    if token:
        h["Authorization"] = "Bearer " + token
    if body is not None:
        data = json.dumps(body).encode(); h["Content-Type"] = "application/json"
    r = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    try:
        with urllib.request.urlopen(r) as resp:
            raw = resp.read().decode()
            return resp.status, json.loads(raw) if raw.startswith("{") else raw, dict(resp.headers)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw), dict(e.headers)
        except Exception:
            return e.code, raw, dict(e.headers)


def check(name, cond, info=""):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + str(info)[:300]))


def login(u, p):
    s, r, _ = call("POST", "/User/login", body={"username": u, "password": p})
    assert s == 200, (u, s, r)
    return r["result"]


def sql(query):
    out = subprocess.run(["sqlcmd", "-S", SQL_SERVER, "-E", "-d", SQL_DATABASE, "-b", "-h", "-1", "-Q", "SET NOCOUNT ON; " + query],
                         capture_output=True, text=True)
    assert out.returncode == 0, out.stdout + out.stderr
    return out.stdout.strip()


def age_session(session_id, minutes):
    sql(f"UPDATE Sessions SET LastActivityAt = DATEADD(MINUTE, -{minutes}, SYSUTCDATETIME()) WHERE Id = '{session_id}'")


# login tells the frontend how long the idle window is
me = login("demo4", DEMO_PASSWORD)
check("login returns idleTimeoutMinutes = 5", me.get("idleTimeoutMinutes") == 5, me.get("idleTimeoutMinutes"))

# activity endpoint works and reports the window
s, r, _ = call("POST", "/User/activity", me["accessToken"])
check("activity endpoint 200", s == 200 and r["result"]["idleTimeoutMinutes"] == 5, (s, r))

# 4 minutes idle: still inside the window, and the request counts as new activity
me = login("demo4", DEMO_PASSWORD)
age_session(me["sessionId"], 4)
s, _, _ = call("GET", "/Permission/me", me["accessToken"])
check("4 min idle: still logged in", s == 200, s)
age = sql(f"SELECT DATEDIFF(SECOND, LastActivityAt, SYSUTCDATETIME()) FROM Sessions WHERE Id = '{me['sessionId']}'")
check("request refreshes LastActivityAt", int(age) < 10, age)

# 6 minutes idle: rejected, with the idle header and an Arabic message
me = login("demo4", DEMO_PASSWORD)
age_session(me["sessionId"], 6)
s, r, h = call("GET", "/Permission/me", me["accessToken"])
check("6 min idle: 401", s == 401, s)
check("idle header X-Session-Expired: idle", h.get("X-Session-Expired") == "idle", h)
check("idle message in errorMessages", isinstance(r, dict) and "عدم النشاط" in r["errorMessages"][0], r)
s, _, _ = call("POST", "/User/activity", me["accessToken"])
check("activity after idle does not revive the session", s == 401, s)
s, _, _ = call("POST", "/User/refresh-token", body={"refreshToken": me["refreshToken"]})
check("refresh token rejected after idle logout", s == 401, s)
# audit rows are written in the background, give the writer a few seconds
for _ in range(10):
    audit = sql(f"SELECT COUNT(*) FROM AuditLogs WHERE SessionId = '{me['sessionId']}' AND Action = 'user.session.idle_timeout'")
    if int(audit) >= 1:
        break
    time.sleep(0.5)
check("idle logout written to audit log", int(audit) >= 1, audit)

# refresh by itself is background traffic: it does not keep an idle session alive
me = login("demo4", DEMO_PASSWORD)
age_session(me["sessionId"], 6)
s, _, _ = call("POST", "/User/refresh-token", body={"refreshToken": me["refreshToken"]})
check("refresh on idle session rejected", s == 401, s)
s, _, _ = call("GET", "/Permission/me", me["accessToken"])
check("access token dead after idle refresh attempt", s == 401, s)

# other sessions of the same user are not affected
a = login("demo4", DEMO_PASSWORD)
b = login("demo4", DEMO_PASSWORD)
age_session(a["sessionId"], 6)
call("GET", "/Permission/me", a["accessToken"])
s, _, _ = call("GET", "/Permission/me", b["accessToken"])
check("idle logout of one device leaves the other session alive", s == 200, s)

print(f"\n{sum(results)}/{len(results)} passed")
