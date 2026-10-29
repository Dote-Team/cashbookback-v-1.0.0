"""Batch 1 security checks: signed attachment links, upload validation, JWT claims, rate limit, CORS, body limit."""
import os
ROOT_USERNAME = os.environ.get("CASHBOOK_ROOT_USERNAME", "root")
ROOT_PASSWORD = os.environ["CASHBOOK_ROOT_PASSWORD"]
DEMO_PASSWORD = os.environ["CASHBOOK_DEMO_PASSWORD"]

import base64, json, sys, uuid, urllib.request, urllib.error, zlib, struct

BASE = "http://localhost:5003"
BIZ1 = "a0000000-0000-4000-8000-000000000001"
BOOK = lambda n: "b0000000-0000-4000-8000-%012d" % n
results = []


def req(method, path, token=None, body=None, fields=None, files=None, headers=None, raw=False):
    h = dict(headers or {})
    data = None
    if token:
        h["Authorization"] = "Bearer " + token
    if body is not None:
        data = json.dumps(body).encode(); h["Content-Type"] = "application/json"
    if fields is not None or files is not None:
        b = uuid.uuid4().hex
        out = b""
        for k, v in (fields or {}).items():
            out += f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode()
        for name, content in (files or []):
            out += f'--{b}\r\nContent-Disposition: form-data; name="Files"; filename="{name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + content + b"\r\n"
        out += f"--{b}--\r\n".encode()
        data = out; h["Content-Type"] = "multipart/form-data; boundary=" + b
    r = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    try:
        with urllib.request.urlopen(r) as resp:
            content = resp.read()
            return resp.status, (content if raw else _j(content)), dict(resp.headers)
    except urllib.error.HTTPError as e:
        content = e.read()
        return e.code, _j(content), dict(e.headers)


def _j(c):
    try:
        return json.loads(c.decode() or "{}")
    except Exception:
        return c[:200]


def check(name, cond, info=""):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + str(info)[:300]))


def png():
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 0, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(b"\x00\x00")) + chunk(b"IEND", b"")


s, r, _ = req("POST", "/API/User/login", body={"username": ROOT_USERNAME, "password": ROOT_PASSWORD})
root = r["result"]["accessToken"]
payload = json.loads(base64.urlsafe_b64decode(root.split(".")[1] + "=="))
check("access token has issuer and audience", payload.get("iss") == "cashbook-api" and payload.get("aud") == "cashbook-client", payload)
check("access token lifetime 60 minutes", 3500 < payload["exp"] - payload["nbf"] <= 3600, payload)

desc = "b1-" + uuid.uuid4().hex[:8]
tx = {"type": "cash in", "date": "2026-10-02T10:00:00", "amount": "500", "currency": "IQD", "bookId": BOOK(1), "description": desc}

s, r, _ = req("POST", f"/API/Transaction?businessId={BIZ1}", root, fields=tx, files=[("receipt.jpg", b"<html><script>alert(1)</script></html>")])
check("html disguised as jpg rejected (400)", s == 400, (s, r))
s, r, _ = req("POST", f"/API/Transaction?businessId={BIZ1}", root, fields=tx, files=[("big.png", png() + b"\x00" * (11 * 1024 * 1024))])
check("file over 10MB rejected (400)", s == 400, (s, r))
s, r, _ = req("POST", f"/API/Transaction?businessId={BIZ1}", root, fields=tx, files=[("x.png", png())] * 6)
check("more than 5 files rejected (400)", s == 400, (s, r))
s, r, _ = req("POST", f"/API/Transaction?businessId={BIZ1}", root, fields=tx, files=[("receipt.jpeg", png())])
check("valid png accepted", s == 200, (s, r))

s, r, _ = req("GET", f"/API/Transaction?bookId={BOOK(1)}&businessId={BIZ1}&take=50", root)
item = next(t for t in r["result"]["data"] if t["description"] == desc)
att = item["attachments"][0]
check("attachment link is signed", "?exp=" in att["files"] and "&sig=" in att["files"], att)
check("stored extension follows content (.png)", att["fileName"].endswith(".png"), att)

name = att["fileName"]
s, body, h = req("GET", "/API/Files/" + att["files"], raw=True)
check("signed link serves file without login", s == 200 and body[:4] == b"\x89PNG", s)
check("nosniff header on files", h.get("X-Content-Type-Options") == "nosniff", h)
s, _, _ = req("GET", "/API/Files/" + name)
check("unsigned link without login -> 401", s == 401, s)
s, _, _ = req("GET", "/API/Files/" + att["files"].replace("sig=", "sig=A"))
check("tampered signature -> 401", s == 401, s)
exp = att["files"].split("exp=")[1].split("&")[0]
s, _, _ = req("GET", "/API/Files/" + att["files"].replace("exp=" + exp, "exp=" + str(int(exp) + 3600)))
check("changed expiry -> 401", s == 401, s)
s, _, _ = req("GET", "/API/Files/" + name, root)
check("unsigned link with authorized token -> 200", s == 200, s)
s, r, _ = req("POST", "/API/User/login", body={"username": "demo4", "password": DEMO_PASSWORD})
d4 = r["result"]["accessToken"]
s, _, _ = req("GET", "/API/Files/" + name, d4)
check("unsigned link with unauthorized user -> 403", s == 403, s)
s, _, _ = req("GET", "/API/Files/..%2Fappsettings.json")
check("path traversal -> 404", s == 404, s)

s, r, _ = req("GET", "/API/Fingerprint/status", headers={"Origin": "https://evil.example"})
s2, r2, h2 = req("OPTIONS", "/API/User/login", headers={"Origin": "https://evil.example", "Access-Control-Request-Method": "POST"})
check("CORS rejects unknown origin", "Access-Control-Allow-Origin" not in h2, h2)
s2, r2, h2 = req("OPTIONS", "/API/User/login", headers={"Origin": "http://localhost:3000", "Access-Control-Request-Method": "POST"})
check("CORS allows localhost in Development", h2.get("Access-Control-Allow-Origin") == "http://localhost:3000", h2)

codes = [req("POST", "/API/User/refresh-token", body={"refreshToken": "x"})[0] for _ in range(32)]
check("refresh endpoint rate limited (429)", 429 in codes, codes)

# Kestrel يرد 413 ويغلق الاتصال، وقد يُغلق قبل أن يقرأ العميل الرد: كلاهما رفض للطلب.
try:
    s, r, _ = req("POST", f"/API/Transaction?businessId={BIZ1}", root, fields=tx, files=[("a.png", png() + b"\x00" * (65 * 1024 * 1024))])
except ConnectionError as e:
    s, r = 413, repr(e)
check("request over 60MB rejected (413 or 400)", s in (400, 413), (s, r))

print(f"\n{sum(results)}/{len(results)} passed")
