"""Batch 4: explicit mapping keeps fields that were not sent; validation on create."""
import os
ROOT_USERNAME = os.environ.get("CASHBOOK_ROOT_USERNAME", "root")
ROOT_PASSWORD = os.environ["CASHBOOK_ROOT_PASSWORD"]
DEMO_PASSWORD = os.environ["CASHBOOK_DEMO_PASSWORD"]

import json, uuid, urllib.request, urllib.error

BASE = "http://localhost:5003/API"
BIZ1 = "a0000000-0000-4000-8000-000000000001"
BOOK = lambda n: "b0000000-0000-4000-8000-%012d" % n
results = []


def call(method, path, token=None, body=None):
    h = {"Content-Type": "application/json"}
    if token:
        h["Authorization"] = "Bearer " + token
    data = json.dumps(body).encode() if body is not None else None
    r = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    try:
        with urllib.request.urlopen(r) as resp:
            return resp.status, json.loads(resp.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw


def check(name, cond, info=""):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + str(info)[:300]))


s, r = call("POST", "/User/login", body={"username": ROOT_USERNAME, "password": ROOT_PASSWORD})
root = r["result"]["accessToken"]
u = uuid.uuid4().hex[:6]

s, r = call("POST", "/Contact", root, {"name": "c-" + u, "phone": "0770", "businessId": BIZ1})
check("create contact", s == 201, (s, r))
cid = r["result"]["id"]
s, r = call("PUT", f"/Contact/{cid}", root, {"name": "c2-" + u})
check("update contact name only", s == 200 and r["result"]["name"] == "c2-" + u, (s, r))
check("phone kept when not sent (was wiped by AutoMapper)", r["result"]["phone"] == "0770", r)
s, r = call("PUT", f"/Contact/{cid}", root, {"phone": ""})
check("phone can be cleared explicitly", s == 200 and r["result"]["phone"] == "" and r["result"]["name"] == "c2-" + u, (s, r))

s, r = call("POST", "/Category", root, {"name": "  ", "businessId": BIZ1})
check("category with empty name rejected (400)", s == 400, (s, r))
s, r = call("POST", "/Category", root, {"name": " cat-" + u + " ", "businessId": BIZ1})
check("create category (name trimmed)", s == 201, (s, r))
catid = r["result"]["id"]
s, r = call("PUT", f"/Category/{catid}", root, {})
check("empty update keeps name", s == 200 and r["result"]["name"] == "cat-" + u, (s, r))
s, r = call("GET", f"/Category/{catid}", root)
check("get category dto", s == 200 and r["result"]["businessId"] == BIZ1, (s, r))

s, r = call("POST", "/PaymentMethod", root, {"name": "pm-" + u, "businessId": BIZ1})
check("create payment method", s == 201, (s, r))

s, r = call("POST", "/CustomField", root, {"key": "f-" + u, "bookId": BOOK(1), "isRequired": False})
check("create custom field", s == 201, (s, r))
fid = r["result"]["id"]
s, r = call("PUT", f"/CustomField/{fid}", root, {"isRequired": True})
check("custom field: key kept, isRequired changed", s == 200 and r["result"]["key"] == "f-" + u and r["result"]["isRequired"] is True, (s, r))
call("DELETE", f"/CustomField/{fid}", root)

s, r = call("PUT", f"/Book/{BOOK(3)}", root, {"name": "صندوق المشتريات"})
check("book update returns dto", s == 200 and r["result"]["id"].lower() == BOOK(3) and r["result"]["name"] == "صندوق المشتريات", (s, r))

print(f"\n{sum(results)}/{len(results)} passed")
