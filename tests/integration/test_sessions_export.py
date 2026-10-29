"""Batch 2: session cache invalidation, export projection, expired session cleanup query."""
import os
ROOT_PASSWORD = os.environ["CASHBOOK_ROOT_PASSWORD"]
DEMO_PASSWORD = os.environ["CASHBOOK_DEMO_PASSWORD"]

import json, sys, uuid, time, urllib.request, urllib.error, subprocess

BASE = "http://localhost:5003/API"
BIZ1 = "a0000000-0000-4000-8000-000000000001"
BOOK = lambda n: "b0000000-0000-4000-8000-%012d" % n
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
            return resp.status, json.loads(raw) if raw.startswith("{") else raw
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw


def check(name, cond, info=""):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + str(info)[:300]))


def login(u, p):
    s, r = call("POST", "/User/login", body={"username": u, "password": p})
    assert s == 200, (u, s, r)
    return r["result"]


root = login("root", ROOT_PASSWORD)["accessToken"]

# logout invalidates the cached session immediately
t = login("demo4", DEMO_PASSWORD)["accessToken"]
s, _ = call("GET", "/Permission/me", t)
s2, _ = call("GET", "/Permission/me", t)
check("session works (cached on 2nd call)", s == 200 and s2 == 200, (s, s2))
s, _ = call("POST", "/User/logout", t)
s, _ = call("GET", "/Permission/me", t)
check("logout rejects token immediately despite cache", s == 401, s)

# deactivation invalidates immediately
uname = "c" + uuid.uuid4().hex[:8]
s, r = call("POST", "/UserManagement", root, {"username": uname, "name": uname, "password": "User@12345", "level": 3, "grants": [{"bookId": BOOK(1), "permissions": ["transaction.view"]}]})
check("create test user", s in (200, 201), (s, r))
uid = r["result"]["id"]
t = login(uname, "User@12345")["accessToken"]
call("GET", "/Permission/me", t)
s, r = call("POST", f"/UserManagement/{uid}/deactivate", root)
if s == 404 or s == 405:
    s, r = call("DELETE", f"/User/{uid}", root)
check("deactivate user", s == 200, (s, r))
s, _ = call("GET", "/Permission/me", t)
check("deactivated user's cached session rejected immediately", s == 401, s)

# password reset kills sessions immediately
uname2 = "c" + uuid.uuid4().hex[:8]
s, r = call("POST", "/UserManagement", root, {"username": uname2, "name": uname2, "password": "User@12345", "level": 3, "grants": [{"bookId": BOOK(1), "permissions": ["transaction.view"]}]})
uid2 = r["result"]["id"]
t = login(uname2, "User@12345")["accessToken"]
call("GET", "/Permission/me", t)
s, r = call("POST", f"/UserManagement/{uid2}/reset-password", root, {"password": "New@123456"})
check("reset password", s == 200, (s, r))
s, _ = call("GET", "/Permission/me", t)
check("token rejected right after password reset", s == 401, s)

# export projection still returns the same shape
s, r = call("GET", f"/Transaction/RawByBookId?bookId={BOOK(1)}&businessId={BIZ1}", root)
rows = r["result"] if s == 200 else []
check("export returns rows", s == 200 and len(rows) > 0, (s, str(r)[:200]))
check("export row shape", rows and all(k in rows[0] for k in ("type", "amount", "currency", "date", "category", "user", "customFieldValues")), rows[:1])
s2, r2 = call("GET", f"/Transaction?bookId={BOOK(1)}&businessId={BIZ1}&take=1", root)
check("export count equals active transactions", s2 == 200 and r2["result"]["totalRecords"] == len(rows), (r2["result"]["totalRecords"], len(rows)))

print(f"\n{sum(results)}/{len(results)} passed")
