"""End-to-end checks against the dev API (cashbook_dev database)."""
import os
ROOT_PASSWORD = os.environ["CASHBOOK_ROOT_PASSWORD"]
DEMO_PASSWORD = os.environ["CASHBOOK_DEMO_PASSWORD"]

import base64
import json
import sys
import urllib.error
import urllib.request
import uuid

BASE = "http://localhost:5003/API"
BIZ1 = "a0000000-0000-4000-8000-000000000001"
BIZ2 = "a0000000-0000-4000-8000-000000000002"
BOOK = lambda n: "b0000000-0000-4000-8000-%012d" % n

results = []


def call(method, path, token=None, body=None, form=None):
    url = BASE + path
    headers = {}
    data = None
    if token:
        headers["Authorization"] = "Bearer " + token
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if form is not None:
        boundary = uuid.uuid4().hex
        parts = []
        for k, v in form.items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n')
        parts.append(f"--{boundary}--\r\n")
        data = "".join(parts).encode()
        headers["Content-Type"] = "multipart/form-data; boundary=" + boundary
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read().decode() or "{}"
            return r.status, json.loads(raw) if raw.strip().startswith("{") or raw.strip().startswith("[") else raw
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw


def check(name, cond, info=""):
    results.append((name, bool(cond)))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + str(info)[:300]))


def login(user, pwd):
    s, r = call("POST", "/User/login", body={"username": user, "password": pwd, "deviceToken": "e2e"})
    return s, r


def tok(user, pwd):
    s, r = login(user, pwd)
    assert s == 200, (user, s, r)
    return r["result"]["accessToken"], r["result"]


def sample(seed, w=256, h=288):
    px = bytes(((i * 7 + seed * 13) % 256) for i in range(w * h))
    return {"width": w, "height": h, "dpi": 500, "pixels": base64.b64encode(px).decode()}


root, rootInfo = tok("root", ROOT_PASSWORD)
check("super admin login level=1", rootInfo["level"] == 1 and rootInfo["isSuperAdmin"], rootInfo)
owner, ownerInfo = tok("aaaa", DEMO_PASSWORD)
check("migrated owner is admin level=2", ownerInfo["level"] == 2, ownerInfo)

s, r = call("POST", "/User/register", body={"username": "x", "password": "Abcdefg1"})
check("self registration closed (410)", s == 410, (s, r))

# --- IDOR on reads: demo4 has no access to business 2
d4, _ = tok("demo4", DEMO_PASSWORD)
s, r = call("GET", f"/Transaction?bookId={BOOK(5)}&businessId={BIZ1}", d4)
check("read transactions of foreign book via own businessId -> 403", s == 403, (s, r))
s, r = call("GET", f"/TransactionHistory/ByBook?bookId={BOOK(5)}&businessId={BIZ1}", d4)
check("read history of foreign book -> 403", s == 403, (s, r))
s, r = call("GET", f"/Category?businessId={BIZ2}", d4)
check("categories of foreign business -> 403", s == 403, (s, r))
s, r = call("POST", "/Category", d4, body={"name": "hack", "businessId": BIZ2})
check("create category in foreign business -> 403", s == 403, (s, r))

# --- owner reads with balances
s, r = call("GET", f"/Transaction?bookId={BOOK(1)}&businessId={BIZ1}&take=5", owner)
check("owner lists transactions", s == 200 and r["result"]["totalRecords"] > 0, (s, r))
total_before = r["result"]["totalRecords"] if s == 200 else 0
s, r = call("GET", f"/Book/{BOOK(1)}", owner)
bal_before = r["result"]["balanceIqd"] if s == 200 else None
check("owner book details", s == 200, (s, r))
s, r = call("GET", f"/Book?businessId={BIZ1}", owner)
check("owner book list (4 books)", s == 200 and r["result"]["totalRecords"] == 4, (s, r))
s, r = call("GET", "/Business", owner)
check("owner sees 5 businesses", s == 200 and r["result"]["totalRecords"] == 5, (s, r))

# --- inheritance
uniq = uuid.uuid4().hex[:6]
admin_perms = ["book.view", "transaction.view", "transaction.create", "transaction.edit", "history.view", "users.manage", "archive.view"]
s, r = call("POST", "/UserManagement", root, body={"username": "adm" + uniq, "name": "Admin Test", "password": "Admin@1234",
                                                    "grants": [{"bookId": BOOK(1), "permissions": admin_perms}]})
check("super admin creates admin", s == 201, (s, r))
adm_id = r["result"]["id"] if s == 201 else None
adm, admInfo = tok("adm" + uniq, "Admin@1234")
check("new admin level=2", admInfo["level"] == 2, admInfo)

s, r = call("POST", "/UserManagement", adm, body={"username": "u" + uniq, "name": "User Test", "password": "User@12345",
                                                   "grants": [{"bookId": BOOK(1), "permissions": ["transaction.view", "transaction.create"]}]})
check("admin creates user with subset", s == 201, (s, r))
u_id = r["result"]["id"] if s == 201 else None
s, r = call("POST", "/UserManagement", adm, body={"username": "v" + uniq, "name": "X", "password": "User@12345",
                                                   "grants": [{"bookId": BOOK(1), "permissions": ["transaction.archive"]}]})
check("admin cannot grant permission he lacks", s == 400, (s, r))
s, r = call("POST", "/UserManagement", adm, body={"username": "w" + uniq, "name": "X", "password": "User@12345",
                                                   "grants": [{"bookId": BOOK(1), "permissions": ["users.manage"]}]})
check("admin cannot grant users.manage", s == 400, (s, r))
s, r = call("POST", "/UserManagement", adm, body={"username": "z" + uniq, "name": "X", "password": "User@12345",
                                                   "grants": [{"bookId": BOOK(2), "permissions": ["book.view"]}]})
check("admin cannot grant on book outside his scope", s == 400, (s, r))

usr, usrInfo = tok("u" + uniq, "User@12345")
check("user level=3", usrInfo["level"] == 3, usrInfo)
s, r = call("POST", "/UserManagement", usr, body={"username": "q" + uniq, "name": "X", "password": "User@12345", "grants": []})
check("user cannot create users", s == 403, (s, r))
s, r = call("GET", f"/Permission/me?businessId={BIZ1}", usr)
check("user permissions endpoint", s == 200 and BOOK(1) in r["result"]["books"], (s, r))
s, r = call("GET", "/UserManagement", adm)
check("admin sees only his users", s == 200 and r["result"]["totalRecords"] == 1, (s, r))

tx = {"type": "cash in", "date": "2026-10-01T10:00:00", "amount": "1000", "currency": "IQD", "bookId": BOOK(1), "description": "e2e-" + uniq}
s, r = call("POST", f"/Transaction?businessId={BIZ1}", usr, form=tx)
check("user creates transaction", s == 200, (s, r))
s, r = call("GET", f"/Transaction?bookId={BOOK(2)}&businessId={BIZ1}", usr)
check("user cannot read book 2", s == 403, (s, r))

# cascade: remove transaction.create from admin -> user loses it
perms_wo_create = [p for p in admin_perms if p != "transaction.create"]
s, r = call("PUT", f"/UserManagement/{adm_id}/permissions", root, body={"grants": [{"bookId": BOOK(1), "permissions": perms_wo_create}]})
check("super admin edits admin permissions", s == 200, (s, r))
s, r = call("POST", f"/Transaction?businessId={BIZ1}", usr, form=tx)
check("cascade: user lost transaction.create", s == 403, (s, r))

# --- archive
s, r = call("GET", f"/Transaction?bookId={BOOK(1)}&businessId={BIZ1}&take=500&sortBy=Date&sortDirection=desc", owner)
mine = [t for t in r["result"]["data"] if t["description"] == "e2e-" + uniq]
check("created transaction visible", len(mine) == 1, len(mine))
tid = mine[0]["id"] if mine else None
s, r = call("GET", f"/Book/{BOOK(1)}", owner)
bal_with = r["result"]["balanceIqd"]
check("balance includes new +1000", abs(bal_with - bal_before - 1000) < 0.001, (bal_before, bal_with))
s, r = call("DELETE", f"/Transaction/{tid}?reason=test", owner)
check("DELETE transaction archives it", s == 200 and r["result"]["archived"], (s, r))
s, r = call("GET", f"/Book/{BOOK(1)}", owner)
check("archived entry removed from balance", abs(r["result"]["balanceIqd"] - bal_before) < 0.001, r["result"]["balanceIqd"])
s, r = call("GET", f"/Transaction?bookId={BOOK(1)}&businessId={BIZ1}", owner)
check("archived entry hidden from list", r["result"]["totalRecords"] == total_before, r["result"]["totalRecords"])
s, r = call("GET", f"/Archive?businessId={BIZ1}&type=transaction&bookId={BOOK(1)}", owner)
check("archive view shows it", s == 200 and any(x["id"] == tid for x in r["result"]["data"]), (s, r))
s, r = call("GET", f"/TransactionHistory/ByTransaction?transactionId={tid}&businessId={BIZ1}", owner)
ops = [h.get("operation") for h in r["result"]] if s == 200 else []
check("history has POST and ARCHIVE", "POST" in ops and "ARCHIVE" in ops, (s, ops))
s, r = call("POST", f"/Trash/{uuid.uuid4()}/restore", owner)
check("restore disabled (410)", s == 410, (s, r))
s, r = call("GET", f"/Archive?businessId={BIZ1}&type=transaction", usr)
check("user without archive.view denied", s == 403, (s, r))

s, r = call("DELETE", f"/Book/{BOOK(4)}", owner)
check("archive book", s == 200, (s, r))
s, r = call("GET", f"/Book?businessId={BIZ1}", owner)
check("archived book hidden (3 left)", r["result"]["totalRecords"] == 3, r["result"]["totalRecords"])
s, r = call("GET", f"/Transaction?bookId={BOOK(4)}&businessId={BIZ1}", owner)
check("archived book transactions inaccessible", s == 404, (s, r))
s, r = call("GET", f"/Archive?businessId={BIZ1}&type=book", owner)
check("archived book in archive view", s == 200 and any(x["id"].lower() == BOOK(4) for x in r["result"]["data"]), (s, r))
s, r = call("POST", "/Book", owner, body={"name": "x", "businessId": BIZ1})
check("admin cannot create books", s == 403, (s, r))

# --- tokens
s, full = login("u" + uniq, "User@12345")
refresh = full["result"]["refreshToken"]
s, r = call("GET", f"/Permission/me", refresh)
check("refresh token rejected as access token", s == 401, (s, r))
s, r = call("POST", "/User/refresh-token", body={"refreshToken": full["result"]["accessToken"]})
check("access token rejected as refresh token", s == 401, (s, r))
s, r = call("POST", "/User/refresh-token", body={"refreshToken": refresh})
check("refresh works", s == 200 and r.get("accessToken"), (s, r))
s, r2 = call("POST", "/User/refresh-token", body={"refreshToken": refresh})
check("old refresh token rejected after rotation", s == 401, (s, r2))

# --- fingerprint (dev matcher)
s, r = call("GET", "/Fingerprint/status")
check("fingerprint status available", s == 200 and r["result"]["available"], (s, r))
smp = sample(1)
s, r = call("POST", "/Fingerprint/enroll", usr, body={"fingerIndex": 1, "samples": [smp, smp, smp]})
check("self enroll fingerprint", s == 200, (s, r))
s, r = call("POST", "/Fingerprint/enroll", usr, body={"fingerIndex": 2, "samples": [smp, sample(2), smp]})
check("inconsistent samples rejected (422)", s == 422, (s, r))
s, r = call("POST", "/User/login/fingerprint", body={"username": "u" + uniq, "sample": smp, "deviceToken": "fp"})
check("login with fingerprint", s == 200 and r["result"]["accessToken"], (s, r))
s, r = call("POST", "/User/login/fingerprint", body={"username": "u" + uniq, "sample": sample(9)})
check("wrong fingerprint rejected", s == 401, (s, r))
s, r = call("POST", "/User/login/fingerprint", body={"username": "aaaa", "sample": smp})
check("someone else's finger rejected", s == 401, (s, r))
s, r = call("GET", f"/Fingerprint/user/{u_id}", adm)
check("admin lists his user's fingerprints", s == 200 and len(r["result"]) == 1, (s, r))
s, r = call("GET", f"/Fingerprint/user/{u_id}", d4)
check("stranger cannot list fingerprints", s == 403, (s, r))

# --- deactivate cascade
s, r = call("POST", f"/UserManagement/{adm_id}/deactivate", root)
check("deactivate admin cascades", s == 200 and r["result"]["deactivated"] == 2, (s, r))
s, r = login("u" + uniq, "User@12345")
check("deactivated user cannot login", s == 401, (s, r))
s, r = call("GET", "/Permission/me", usr)
check("deactivated user's session killed", s == 401, (s, r))

failed = [n for n, ok in results if not ok]
print(f"\n{len(results) - len(failed)}/{len(results)} passed")
sys.exit(1 if failed else 0)
