# Shushka — WhatsApp Digital Receipt Service

Replaces thermal paper receipts with WhatsApp messages.

When a sale is finalised on **Kopot Barak**, the print job flows into this service instead of to paper. The service decodes it, classifies the document type, and acts accordingly:

- **חשבונית עסקה (invoice)** — if a customer phone is found, WhatsApp opens immediately with the receipt pre-filled. No popup, no clicks.
- **מספר הזמנה (order)** — popup with options: send to customer, send to store phone, save locally, print to thermal printer.
- **Internal print** (Z report, cashier login, etc.) — popup shows the full text of the printout so the cashier can read it and decide: send to owner, send to store, save, or just close.

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
GetDocumentType  →  חשבונית עסקה / מספר הזמנה / internal
        │
        ├─ Receipt + phone found  →  WhatsApp opens silently (no popup)
        │
        ├─ Receipt + no phone     →  popup: enter phone → WhatsApp
        │
        ├─ Order                  →  popup: שלח ללקוח | שלח לחנות | שמור | הדפסה | דלג
        │
        └─ Internal               →  popup: shows full print text
                                      שלח לבעלים | שלח לחנות | שלח למספר | שמור | סגור
```

**Human send is intentional** — keeps the store off the WhatsApp Business API (no templates, no opt-in, no per-message cost, no ban risk).

---

## Installation guide

Installation is a two-part process:
1. **Build** the exe on the development machine (the machine with the code)
2. **Deploy** the exe to the cash computer (no .NET needed there)

---

### Part 1 — Build the exe (on the development machine)

Open PowerShell and run from the `shushka-pos-receipts` folder:

```powershell
dotnet publish src\ShushkaReceipt\ShushkaReceipt.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    --output .\publish-output
```

The `publish-output` folder will contain two files:
- `ShushkaReceipt.exe` — the complete self-contained app (~80 MB, no .NET needed on the POS)
- `appsettings.json` — configuration template

---

### Part 2 — Deploy to the cash computer

Copy these **3 files** to the cash computer (USB drive, network share, etc.):

| File | Where to copy it on the cash computer |
|---|---|
| `publish-output\ShushkaReceipt.exe` | `C:\Program Files\Shushka\` |
| `publish-output\appsettings.json` | `C:\ProgramData\Shushka\` |
| `register-task.ps1` | Anywhere (e.g. Desktop) — delete after install |

> Only these two files are needed — ignore any other files in `publish-output`.

---

### Part 3 — Configure (on the cash computer)

Open `C:\ProgramData\Shushka\appsettings.json` in Notepad and fill in your details:

```json
{
  "Shushka": {
    "StorePhone":         "972501234567",
    "OwnerPhone":         "972521234567",
    "ThermalPrinterName": "EPSON TM-T20III",
    "LocalSavePath":      "C:\\קופה\\"
  }
}
```

| Key | Format | Notes |
|---|---|---|
| `StorePhone` | E.164, e.g. `972501234567` | Store WhatsApp — where orders, Z reports go |
| `OwnerPhone` | E.164, e.g. `972521234567` | Owner WhatsApp — for end-of-day reports |
| `ThermalPrinterName` | Exact Windows printer name | Leave `""` to disable the Print button |
| `LocalSavePath` | Folder path | Where saved prints are stored (default `C:\קופה\`) |

> **Tip:** You can leave `StorePhone` and `OwnerPhone` empty — the popup will ask  
> for the number the first time it's needed and save it automatically.

---

### Part 4 — Register the Task Scheduler task (on the cash computer)

Open **PowerShell as Administrator** (right-click PowerShell → "Run as administrator"), then run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
cd C:\Users\YourUser\Desktop    # wherever you put register-task.ps1
.\register-task.ps1
```

This registers a Task Scheduler task that starts Shushka automatically at every logon — no Windows Service, no Session 0 isolation, full access to the user's desktop.

> **Why Task Scheduler, not a Windows Service?**  
> Windows Services run in Session 0 — an isolated desktop with no UI.  
> The tray icon, dispatch popup, and WhatsApp launcher all require the user's  
> desktop session. Task Scheduler with "run only when logged on" provides this.

To remove the task later:
```powershell
.\register-task.ps1 -Uninstall
```

---

### Part 5 — Create the virtual printer (on the cash computer)

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

### Part 6 — Configure Kopot Barak

In Kopot Barak printer settings:

| Setting | Value |
|---|---|
| **Printer mode** | `6 - Windows` |
| **Printer name** | `Shushka WhatsApp` (exact, character-for-character) |
| **Enable to override printer on error** | `True` ← keep this during rollout |

Restart Kopot Barak after saving.

> The "override on error" setting is your safety net: if Shushka is down,  
> Kopot Barak falls back to the thermal printer automatically. No sale is ever blocked.

---

### Part 7 — Verify

1. Check the **system tray** — you should see a **green circle**.  
   Red = service not listening (see Troubleshooting below).

2. Print a test receipt from Kopot Barak.

3. For an invoice with a customer phone: **WhatsApp opens immediately** with the receipt.  
   For an order or unknown document: the **dispatch popup** appears.

---

### After rollout stabilises

Once you're confident everything works:
- Set **"Enable to override printer on error"** = `False` in Kopot Barak to stop printing paper.
- Or leave it on indefinitely as a fallback — your choice.

---

## Updating the software

When a new version is ready:

1. Build the new `ShushkaReceipt.exe` on the dev machine (same publish command as Part 1).
2. Stop the running task: right-click tray icon → יציאה.
3. Replace `C:\Program Files\Shushka\ShushkaReceipt.exe` with the new file.
4. Start the task: double-click `ShushkaReceipt.exe` or run `Start-ScheduledTask ShushkaReceipt` in PowerShell.

> The config at `C:\ProgramData\Shushka\appsettings.json` is never touched during updates — your phone numbers and settings are safe.

---

## Settings (tray icon)

Right-click the tray icon → **הגדרות** to open the settings form without editing JSON:

- **מספרי טלפון** — store phone and owner phone
- **שליחה אוטומטית** — toggle auto-send (invoices always auto-send when phone is found; this toggle applies to orders)
- **מדפסת תרמית** — choose the thermal printer from a dropdown of all installed Windows printers
- **שמירה מקומית** — folder path for saved prints

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Tray icon **red** or missing | Service didn't start / port 9100 in use | Run `ShushkaReceipt.exe` manually; check the log |
| **No popup** after printing | Printer name mismatch in Kopot Barak | Double-check exact name, restart Kopot Barak |
| Popup appears but **WhatsApp doesn't open** | WhatsApp Desktop not installed / not logged in | Install or log in to WhatsApp Desktop |
| **Print button greyed out** | `ThermalPrinterName` is empty or wrong | Set it in Settings (tray icon) or in `appsettings.json` |
| Script blocked: *"cannot be loaded"* | PowerShell execution policy | Run `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` first |
| **סגור** closes popup but phone was never saved | Normal — first-time store/owner phone entry | Click the button again; it will prompt and save |

**Log file:** `C:\ProgramData\Shushka\receipt-jobs.log`  
Every job is logged: type, bytes received, phone found/not, cashier's choice. Rotates at 5 MB.

---

## Development setup

```powershell
git clone https://github.com/abokish/shushka-pos-receipts.git
cd shushka-pos-receipts

dotnet build
dotnet test          # 92 tests
dotnet run --project src/ShushkaReceipt
```

Requires .NET 9 SDK and Windows (Windows Forms + Win32 P/Invoke).

---

## Open items

- [ ] ESC/POS GS command hardening — `GS V` (paper cut) and `GS v 0` (raster logo) are not handled. If a receipt includes a printed logo, the byte stream could be misread. Harden `ReceiptDecoder` when a logo-bearing receipt is captured.
- [ ] Full system test on the live POS machine.

---

## Architecture

```
shushka-pos-receipts/
├── src/ShushkaReceipt/
│   ├── Config/
│   │   └── ShushkaConfig.cs           — all configurable values
│   ├── Forms/
│   │   ├── DispatchForm.cs            — per-job popup (3 modes: CustomerNoPhone / Order / Internal)
│   │   ├── PhoneInputHelper.cs        — phone validation + E.164 conversion
│   │   ├── PhonePromptDialog.cs       — on-demand phone entry sub-dialog
│   │   └── SettingsForm.cs            — tray icon settings form
│   ├── Services/
│   │   ├── AppSettingsWriter.cs       — reads/writes appsettings.json at runtime
│   │   ├── FileJobLogger.cs           — per-job audit log with rotation
│   │   ├── LocalSaveService.cs        — saves print content to a local folder
│   │   ├── ReceiptDecoder.cs          — ESC/POS strip + CP862 decode + RTL bidi fix
│   │   ├── ReceiptParser.cs           — GetDocumentType + BuildMessage + ExtractCustomerPhone
│   │   ├── ThermalPrinterService.cs   — Win32 raw printing (RAW datatype)
│   │   ├── TrayAndHotkeyService.cs    — STA thread, green/red tray icon
│   │   └── WhatsAppService.cs         — BuildWhatsAppLink + LaunchDeepLink
│   ├── AppState.cs                    — listener-alive flag + event
│   ├── Worker.cs                      — TcpListener loop, document-type routing
│   └── Program.cs                     — DI setup; config from ProgramData or BaseDirectory
├── tests/ShushkaReceipt.Tests/        — 92 unit tests
├── install-service.ps1                — full build + deploy (for dev machine)
├── register-task.ps1                  — Task Scheduler setup only (for cash computer)
└── README.md
```
