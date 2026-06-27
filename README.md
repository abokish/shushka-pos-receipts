# Shushka — WhatsApp Digital Receipt Service

Replaces thermal paper receipts at the store with WhatsApp messages.

When a sale is finalised on **Kopot Barak**, instead of printing to paper the receipt flows into this service. A popup appears on the cashier's screen. The cashier presses **Enter** → WhatsApp opens with the message pre-filled to the customer → cashier presses Send.

---

## How it works

```
Kopot Barak finalises a sale
        │  (prints to "Shushka WhatsApp" virtual printer)
        ▼
TcpListener on 127.0.0.1:9100
        │  receives raw ESC/POS bytes
        ▼
DecodeReceipt  →  strips ESC/POS commands, decodes CP862 Hebrew, fixes RTL bidi
        │
        ▼
BuildMessage   →  clean plain-text WhatsApp receipt
        │
        ▼
DispatchForm   →  always-on-top popup
   ┌────────────────────────────────────────┐
   │  טבע בוקיש  |  הזמנה 1504  |  ₪238   │
   │  טלפון: [052-1234567      ]           │
   │  [WhatsApp ↵]  [הדפסה F8]  [דלג Esc] │
   └────────────────────────────────────────┘
        │ Enter                │ F8              │ Esc
        ▼                      ▼                 ▼
   WhatsApp opens         Raw bytes →        Nothing sent
   with message           thermal printer
   pre-filled
        │
        ▼
   Cashier presses Send in WhatsApp
```

**Human send is intentional** — keeps the store off the WhatsApp Business API (no templates, no opt-in, no per-message cost, no ban risk).

---

## Installation guide

### Prerequisites

| What | Notes |
|---|---|
| Windows 10 / 11 | Already the case for the POS machine |
| WhatsApp Desktop | Installed and logged in to the store's account |
| Admin access | Needed only during installation |
| .NET 9 | Not needed — the publish produces a self-contained `.exe` |

---

### Step 1 — Build and install

Open **PowerShell as Administrator**, `cd` to this folder, then run:

```powershell
.\install-service.ps1
```

This publishes the app to `C:\ShushkaReceipt\bin\` and registers it in Task Scheduler to auto-start at every logon.

```powershell
.\install-service.ps1 -Update     # after a code change: republish and restart
.\install-service.ps1 -Uninstall  # remove completely
```

> **Why Task Scheduler, not a Windows Service?**  
> Windows Services run in Session 0 — an isolated desktop with no UI.  
> The tray icon, dispatch popup, and WhatsApp launcher all require the user's  
> desktop session. Task Scheduler with "run only when logged on" provides this.

---

### Step 2 — Configure `appsettings.json`

Open `C:\ShushkaReceipt\bin\appsettings.json` in Notepad.

Set the thermal printer name to the **exact** Windows printer name (copy-paste from Settings → Printers):

```json
"ThermalPrinterName": "EPSON TM-T20III"
```

Leave it empty (`""`) if you don't want the Print button — it will be greyed out.

Other settings you can adjust:

| Key | Default | Meaning |
|---|---|---|
| `ListenPort` | `9100` | TCP port for print jobs |
| `LogMaxSizeBytes` | `5242880` | Rotate log after 5 MB |
| `MessageClosing` | `תודה ולהתראות!` | Last line of every receipt |
| `MessageSeparator` | `——————————————————————` | Divider line |

After editing, restart: right-click tray icon → Exit, then run `ShushkaReceipt.exe`.  
Or: `.\install-service.ps1 -Update`

---

### Step 3 — Create the virtual printer

This is the "fake printer" that intercepts Kopot Barak's print jobs.

1. **Settings → Printers & scanners → Add device**
2. Click **"The printer I want isn't listed"**
3. Choose **"Add a local printer or network printer with manual settings"** → Next
4. **"Create a new port"** → Type: **Standard TCP/IP Port** → Next
5. Hostname: **`127.0.0.1`** — uncheck "Query the printer…" → Next
6. Device type: **Custom** → Settings:
   - Protocol: **Raw**
   - Port number: **9100**
   - ☐ Uncheck **SNMP Status Enabled**
   → OK → Next
7. Driver: **Generic → Generic / Text Only** → Next
8. Name the printer: **`Shushka WhatsApp`** → Finish

---

### Step 4 — Configure Kopot Barak

In Kopot Barak printer settings:

| Setting | Value |
|---|---|
| **Printer mode** | `6 - Windows` |
| **Printer name** | `Shushka WhatsApp` (exact, character-for-character) |
| **Enable to override printer on error** | `True` ← keep this on during rollout |

Restart Kopot Barak after saving.

> The "override on error" setting is your safety net: if this service is down,  
> Kopot Barak falls back to the thermal printer automatically. No sale is ever blocked.

---

### Step 5 — Verify

1. Look at the **system tray** — you should see a **green circle**.  
   Red = service not listening (see Troubleshooting).

2. Print a test receipt from Kopot Barak.

3. The **dispatch popup** appears within a second:
   - Phone pre-filled → press **Enter** → WhatsApp opens with message → press Send
   - No phone → type the number → press **Enter** → WhatsApp opens
   - **Esc** to skip without sending anything

---

### Step 6 — After rollout stabilises

Once you're confident:
- Set **"Enable to override printer on error"** = `False` in Kopot Barak to stop printing paper
- Or leave it on indefinitely as a fallback

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Tray icon **red** or missing | Service didn't start / port 9100 in use | Run `ShushkaReceipt.exe` manually; check the log |
| **No popup** after printing | Printer name mismatch in Kopot Barak | Double-check exact name, restart Kopot Barak |
| Popup appears but **WhatsApp doesn't open** | WhatsApp Desktop not installed / not logged in | Install or log in to WhatsApp Desktop |
| **Print button greyed out** | `ThermalPrinterName` is empty | Fill it in `appsettings.json`, restart |
| **Print button fails** | Printer name doesn't match exactly | Copy-paste from Windows Printers list |
| Phone **never pre-filled** | `ExtractCustomerPhone` placeholder pending | Needs a real receipt-with-phone to finalise |

**Log file:** `C:\ProgramData\ShushkaReceipt\receipt-jobs.log`  
Every job is logged: bytes received, phone found/not, cashier's choice. Rotates at 5 MB.

---

## Development setup

```bash
# Clone
git clone https://github.com/YOUR_USERNAME/shushka-pos-receipts.git
cd shushka-pos-receipts

# Build
dotnet build

# Run tests (74 tests)
dotnet test

# Run locally (not as a service)
dotnet run --project src/ShushkaReceipt
```

Requires .NET 9 SDK and Windows (uses Windows Forms + Win32 P/Invoke).

---

## Open items

- [ ] `ExtractCustomerPhone` — placeholder until a real receipt **with** a customer phone is captured. Once captured, update the regex anchor in `ReceiptParser.cs` and add a matching test.
- [ ] ESC/POS GS command hardening — `GS V` (paper cut) and `GS v 0` (raster logo) are not handled explicitly. If the receipt includes a logo, the stream could be misread. Harden `ReceiptDecoder.StripEscPos` when a logo-bearing receipt is captured.
- [ ] Full system test on the live POS machine.

---

## Architecture

```
shushka-pos-receipts/
├── src/ShushkaReceipt/
│   ├── Config/
│   │   └── ShushkaConfig.cs          — all configurable values (POCO)
│   ├── Forms/
│   │   ├── DispatchForm.cs           — per-job popup (Enter/F8/Esc)
│   │   └── PhoneInputHelper.cs       — phone validation + E.164 conversion
│   ├── Services/
│   │   ├── ReceiptDecoder.cs         — ESC/POS strip + CP862 decode + bidi fix
│   │   ├── ReceiptParser.cs          — BuildMessage + ExtractCustomerPhone
│   │   ├── WhatsAppService.cs        — BuildWhatsAppLink + LaunchDeepLink
│   │   ├── ThermalPrinterService.cs  — Win32 raw printing
│   │   ├── TrayAndHotkeyService.cs   — STA thread, green/red tray icon
│   │   └── FileJobLogger.cs          — per-job audit log with rotation
│   ├── AppState.cs                   — listener-alive flag + event
│   ├── Worker.cs                     — TcpListener loop, orchestrates pipeline
│   └── Program.cs                    — DI setup, Task Scheduler host
├── tests/ShushkaReceipt.Tests/       — 74 unit + sanity tests
├── install-service.ps1               — publish + Task Scheduler setup
└── README.md                         — this file
```
