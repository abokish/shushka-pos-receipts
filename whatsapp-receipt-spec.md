# Shushka — WhatsApp Digital Receipt Service

**Build specification / handoff document**
Target environment: Claude Code (VS Code) on the store's Windows POS machine.
Stack: C# / .NET (Worker Service).

---

## 1. Goal

Replace the thermal paper receipt with a digital receipt delivered over WhatsApp.

Flow is **semi-automatic**:

1. A sale/order is finalized on the POS → the POS "prints" the receipt.
2. Our service captures the print job, decodes it, builds a clean WhatsApp message, and opens **WhatsApp Desktop** on the correct customer's chat with the message pre-filled.
3. The cashier presses **F6** → the message is sent.

The human-in-the-loop (F6) is intentional: it avoids the official WhatsApp Business API (templates, opt-in, per-message cost) and the ban risk of unofficial automation, because every message is sent manually from a normal WhatsApp.

---

## 2. Architecture (decided)

**Virtual network printer on localhost.**

- Create a Windows printer that uses a **Standard TCP/IP port → `127.0.0.1:9100`** (9100 = standard RAW printing port), with the **Generic / Text Only** driver (built-in, Microsoft-signed).
- Our **.NET Windows Service** runs a `TcpListener` on `127.0.0.1:9100`.
- POS "prints" → Windows opens a TCP connection to 127.0.0.1:9100 → raw ESC/POS bytes stream straight into our socket → we decode and act.

This is "a virtual printer that runs our logic," with the logic in the **port**, not in a driver.

### Why this and not the alternatives (do not re-litigate)

| Option | Verdict | Reason |
|---|---|---|
| TCP loopback :9100 + TcpListener | **CHOSEN** | Generic, no driver signing, no firewall (loopback is not filtered by Windows Firewall), one job = one clean connection (no file races). |
| Direct SQL Server DB read | Viable fallback | Clean Unicode Hebrew + reliable phone, and access already works. Rejected for now in favor of print decoupling from DB schema. Phone availability is **equivalent** to print (both only have it when it's on the customer record). |
| Custom print driver (PDF-style) | Rejected | Requires WDK + **EV code-signing** (paid, per-product). No benefit: POS sends RAW, which bypasses driver rendering anyway. |
| Local Port → file + FileSystemWatcher | Rejected | Each job overwrites the file; read/write races; less clean than TCP. |
| PDF virtual printer | Rejected (proven) | POS sends RAW (not GDI) → produced **0 bytes**. |

---

## 3. Established facts about the POS

- POS: **Kopot Barak** by ProfitAge. It is a **.NET app that prints via `WritePrinter` with datatype = RAW** (spool doc name "My Visual Basic .NET RAW Document" = the signature of Microsoft KB322090's raw-printing sample). Confirmed by capture.
- POS printer setting: **`Printer mode: 6 - Windows`**, and **`Printer name`** is a free-text field → set it to the exact name of our virtual printer.
- **`Enable to override printer on error = True`** → can be used as a **fallback to the thermal printer** during rollout (if our capture fails, the receipt still prints on paper and no sale is blocked).
- Encoding: **CP862** (Hebrew). Text is **visually pre-reversed** (RTL bidi) in the byte stream.
- **Document types differ** (order vs. חשבונית עסקה vs. refund/זיכוי, etc.). The captured sample was an *order*. The parser must be tested against each document type the store actually uses.

---

## 4. Decoding logic (PROVEN — port from Python to C#)

Validated end-to-end on a real 1237-byte captured job.

### Algorithm

1. **Strip ESC/POS command bytes:**
   - `1B 21 nn` (ESC `!` n = select print mode) → skip 3 bytes.
   - Other `1B xx` (ESC) → skip 2 bytes (naive; see hardening note).
   - Keep everything else.
2. **Decode** remaining bytes as **CP862**.
3. **Bidi fix per line:** tokenize each line into Hebrew runs (`\u0590-\u05FF`) vs. non-Hebrew runs; reverse the **order of tokens**; reverse the **characters inside Hebrew runs only**; leave digit/Latin/punctuation runs as-is (so `054-6995623` stays correct, not `3265996-450`).

### Reference (proven Python)

```python
out = bytearray(); i = 0
while i < len(data):
    b = data[i]
    if b == 0x1b and i+1 < len(data) and data[i+1] == 0x21:
        i += 3; continue          # ESC ! n
    if b == 0x1b:
        i += 2; continue          # other ESC x
    out.append(b); i += 1
text = out.decode('cp862', errors='replace')

def fix(line):
    toks = re.findall(r'[\u0590-\u05FF]+|[^\u0590-\u05FF]+', line)
    res = ''
    for t in reversed(toks):
        res += t[::-1] if re.match(r'[\u0590-\u05FF]', t) else t
    return res.strip()
```

### C# notes

- CP862 on .NET 5+/Core requires the code-pages provider:
  ```csharp
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // NuGet: System.Text.Encoding.CodePages
  var cp862 = Encoding.GetEncoding(862);
  ```
- Hebrew range check: `c >= '\u0590' && c <= '\u05FF'`.

### Hardening note

The ESC-strip is naive. ESC/POS commands are variable-length. In particular **GS sequences** (`1D`) — paper cut (`GS V`), raster images / logos (`GS v 0`, which carries a length header) — will appear in some receipts. Skipping a fixed 2 bytes there will corrupt the stream. For production: handle GS commands explicitly, and detect+skip raster blobs by their length bytes.

---

## 5. Customer phone extraction (PENDING — placeholder for now)

**Status: not finalized.** We have only seen a receipt where the customer phone field was *empty* (customer had no phone on file). A real receipt **with** a phone is being captured to validate this.

Key requirements once we have the sample:

- **Anchor on the CUSTOMER-block `טלפון:` label**, which appears *after* `מספר לקוח` — **NOT** the store header phone. The store's own phone (`054-6995623`) is printed in the header and a blind regex would grab it by mistake.
- Israeli mobile regex (starting point): `0\d{1,2}-?\d{7}`.
- Convert to **E.164**: strip leading `0`, remove dashes, prepend `972` → `972XXXXXXXXX`.

`ExtractCustomerPhone(string decoded) → string?` should be an **isolated function** with a placeholder implementation, to be updated in one iteration after the real sample arrives.

**No-phone case** (walk-in, or no phone on file): **deferred** by decision. Future behavior = prompt the cashier for a number to send to. Not part of the first build.

---

## 6. Message format

Rebuild a clean message (do not forward the raw thermal layout — it won't align in WhatsApp's proportional font).

### Proven sample output (from the real captured order)

```
טבע בוקיש
הזמנה 1504
24/06/26  21:08
——————————————————————
תות שדה קפוא 2 קילו — ₪42.00
אננס קפוא 2 קילו — ₪52.00
פטל אדום קפוא לפי משקל — ₪59.00
פסיפלורה קפואה 2 קילו — ₪75.00
משלוח בערבה — ₪10.00
——————————————————————
סה"כ: ₪238.00
תודה ולהתראות!
```

### Field extraction (first cut)

- Store name = first decoded line.
- Order / document number: `מספר הזמנה\s+(\d+)` (will differ for other doc types).
- Date/time: `תאריך\s+([\d/]+)` … `שעה([\d:]+)`.
- Items: lines containing a price `(\d+\.\d{2})\s*\*`; description = the line minus the price token and minus the leading item code.
- Total: `לתשלום` line → `(\d+\.\d{2})`.

**Known parser edge case:** an item code shorter than 3 digits (e.g. delivery code `9`) glues to the description (`9משלוח בערבה`). The leading-code strip assumes 3–6 digits. Fix when hardening per document type.

---

## 7. WhatsApp delivery

- **Deep link:** `whatsapp://send?phone=972XXXXXXXXX&text=<urlencoded message>`
  - Opens WhatsApp Desktop on that chat with the **text pre-filled**.
  - The URI scheme prefills **text only** — it cannot pre-attach an image/file. (Text receipt is the chosen format; no attachment needed.)
- **F6 hotkey:** global hotkey → bring WhatsApp window to foreground → send (in text mode, "focus + Enter"). Implement via Win32 `RegisterHotKey` + `SendInput` (or a small AutoHotkey helper). Make the key **configurable** to avoid clashing with Kopot Barak shortcuts.
- **UX:** do not steal focus mid-sale. Stage the prepared message; F6 is what opens + sends.

---

## 8. Service skeleton (components to build)

- **Windows Service** (Worker Service template), always running, auto-start.
- `TcpListener` on `127.0.0.1:9100`; accept one connection at a time; read the full stream until the connection closes (= one complete job). Jobs are processed **one by one** (a backlog releases sequentially, never merged).
- `DecodeReceipt(byte[]) → string` — section 4 (proven).
- `ExtractCustomerPhone(string) → string?` — section 5 (**placeholder**).
- `BuildMessage(string decoded) → string` — section 6.
- `BuildWhatsAppLink(string phoneE164, string message) → Uri` — section 7.
- `LaunchDeepLink(Uri)` — `Process.Start` the `whatsapp://` link.
- **Global hotkey handler** (F6) — focus WhatsApp + send.
- **System tray icon: green = service alive, red = down.** This is for *visibility*, not reliability — see section 9.
- **Config file** (JSON): listen port, hotkey, phone-label anchors / regexes, message template strings, log path.
- **Logging** to file (every job: timestamp, bytes received, phone found/not, sent/staged).

---

## 9. Reliability / visibility (important design note)

Because delivery is semi-automatic, a crashed service is **silent**: no window pops up, which *feels like less work, not like a fault*. The cashier won't notice until a customer complains.

- Therefore the **tray status icon (green/red)** is required from day one — so the moment anyone glances, "no registered customers right now" vs. "service is dead" is instantly distinguishable.
- **Backlog behavior:** if the service was down and jobs queued in the spooler, on restart it will emit several prepared messages in quick succession. With F6, this is controlled — the cashier simply doesn't press F6 for customers who already left. Not merged, not lost.
- **Future (not MVP):** a heartbeat that WhatsApps the owner if the service is unresponsive for X minutes.

---

## 10. Windows port setup (5 steps, do on the POS machine)

1. Settings → Printers & scanners → **Add device** → "The printer I want isn't listed" → **Add a local printer or network printer with manual settings**.
2. **Create a new port** → type = **Standard TCP/IP Port**.
3. Hostname/IP = **`127.0.0.1`** (uncheck "Query the printer and automatically select the driver").
4. Device type = **Custom** → Settings → Protocol = **Raw**, Port number = **9100**. **Uncheck SNMP**.
5. Driver = **Generic → Generic / Text Only**. Name the printer e.g. **`Shushka WhatsApp`**.

Then set Kopot Barak **`Printer name`** to that exact name (character-for-character), and restart the POS app so it re-reads the printer name.

---

## 11. Rollout / fallback

- Keep the thermal printer as a **fallback via `Enable to override printer on error = True`** during the initial run, so a capture failure still yields a paper receipt and never blocks a sale.
- The service must always run (auto-start, restart-on-failure).
- Once stable → drop the paper.

---

## 12. Open items (carry into Claude Code)

1. **`ExtractCustomerPhone` is a placeholder** — finalize after a real receipt-with-phone is captured. Must anchor on the customer-block label, not the store header phone.
2. **No-phone flow** (prompt cashier) — deferred, future iteration.
3. **Test the decoder against every document type** the store uses (order, חשבונית עסקה, refund). Layouts differ.
4. **Harden ESC/POS stripping** for GS commands (cut, raster logo).
5. Decide whether thermal fallback stays long-term or is removed after stabilization.
