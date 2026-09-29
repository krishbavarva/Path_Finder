# Unity ↔ Arduino connection

Unity does **not** connect to the Arduino with a cable. The **PC** is in the middle: Unity talks to software on the PC; that software uses **USB serial** to the Arduino.

```
┌─────────────┐     HTTP (Wi‑Fi or localhost)      ┌──────────────────┐     USB serial      ┌───────────┐
│ Unity       │  ───────────────────────────────►  │ Node (server.js) │  ────────────────►  │ Arduino  │
│ Editor/phone│  ◄──────────────────────────────  │ COM port, 9600   │  ◄───────────────  │           │
└─────────────┘         JSON responses             └──────────────────┘   line-based text   └───────────┘
```

- **Arduino** firmware: `Arduino/GarbageTruckReceiver.ino` (see also `ardiunoConnection.md` for wiring).
- **Bridge** on PC: `Server/server.js` (must be running; `local.config.json` sets the COM port).
- **Unity** client: `Scripts/ServerCommunicator.cs` + `GameManager` (see `UNITY.md` for scene setup).

---

## 1. Why this shape?

| Approach | How it works |
|----------|----------------|
| **Recommended** | Unity → HTTP → Node → **USB** → Arduino. One program (Node) owns the COM port. Works from **Editor** (`127.0.0.1`) or **phone** (PC LAN IP). |
| **Optional (Editor only)** | Unity `ArduinoCommunicator` opens COM **directly**. **Stop Node** first, or only one of them can use the port. |

There is **no Wi‑Fi module required on the Arduino** for the recommended path—only the USB cable to the PC.

---

## 2. Unity → Arduino (commands)

`GameManager` sends **one line of text per command** (same strings the sketch listens for). They go to the PC as JSON, then Node writes the **plain text line + newline** to serial.

| Unity flow (typical) | String sent to Arduino |
|----------------------|-------------------------|
| Start route | `TRUCK_START` |
| Dustbin 1 collected | `DUSTBIN1_COLLECTED` |
| Dustbin 2 collected | `DUSTBIN2_COLLECTED` |
| Dustbin 3 collected | `DUSTBIN3_COLLECTED` |
| Route finished | `ROUTE_COMPLETE` |

**Unity HTTP call used by `ServerCommunicator`:**

- `POST /api/command`  
  Body: `{ "command": "<exact string above>" }`

Other server routes (`/api/truck/start`, `/api/dustbin/:n/collected`, etc.) forward the same strings to serial.

---

## 3. Arduino → Unity (sensor data)

The Arduino answers with **one line per response**, using a **pipe** format (`prefix|key:value|...`). The Node server **parses** these lines and stores values in memory. Unity does **not** read serial; it uses **HTTP GET**.

**Arduino → PC (serial, 9600 baud, `\n` terminated):**

| When | Example line | Meaning |
|------|----------------|---------|
| After `TRUCK_START` / `READ_ALL` | `SENSORS|trash:72|water:50|temp:24.5|lid1:closed|lid2:open|light:380` | Snapshot for all bins (keys must match `server.js`) |
| After `DUSTBIN1_COLLECTED` | `OK_DUSTBIN1|trash:72|lid:closed|full:no` | Bin 1 |
| After `DUSTBIN2_COLLECTED` | `OK_DUSTBIN2|water:50|temp:24.5|hot:no` | Bin 2 |
| After `DUSTBIN3_COLLECTED` | `OK_DUSTBIN3|lid:open|light:380` | Bin 3 |
| Optional | `OK_ROUTE_DONE` | Not parsed for sensors; may appear in `/api/status` `lastMessage` |

**Unity ← PC (HTTP):**

- `GET /api/sensors/1` → `{ "dustbin": 1, "data": { "trash", "lid", "full" } }`
- `GET /api/sensors/2` → `{ "dustbin": 2, "data": { "water", "temp", "hot" } }`
- `GET /api/sensors/3` → `{ "dustbin": 3, "data": { "lid", "light" } }`
- `GET /api/status` → server up, Arduino serial connected flag, last raw line

`GameManager` pulls sensor JSON after each dustbin using `serverURL + "/api/sensors/" + number`. Ensure a command has been handled by the Arduino **before** the GET if you need fresh values (ordering is usually fast enough on a LAN).

---

## 4. End-to-end checklist

1. **PC:** `npm start` in `Server/`, correct **`arduinoPort`** in `local.config.json`.
2. **PC:** Arduino USB plugged in; **Serial Monitor closed**.
3. **Sketch:** `GarbageTruckReceiver.ino` uploaded; baud **9600**.
4. **Unity:** `ServerCommunicator.serverURL` → `http://127.0.0.1:3000` (Editor) or `http://<PC_IP>:3000` (phone).
5. **Unity:** `GameManager.serverComm` assigned; **`ArduinoCommunicator` empty** unless you intentionally use direct serial.

---

## 5. Related docs

- **`UNITY.md`** — Unity scene, Player Settings, troubleshooting.
- **`ardiunoConnection.md`** — LCD, ultrasonic, pot, LDR, LED pin map (`ardiunoCircuit.png` if you use it).
