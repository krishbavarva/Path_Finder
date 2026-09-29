# Unity setup — path-finder (AR garbage truck)

This Unity project talks to **hardware on an Arduino** that is **plugged into your PC with USB**. The PC runs a small **Node.js server** that owns the COM port and exposes HTTP endpoints. Unity never opens the serial port when you use `ServerCommunicator`.

```
Unity (Editor or phone)  ──HTTP──►  PC: Server/server.js  ──USB serial──►  Arduino
```

- **How Unity links to the Arduino (protocol, COM, messages):** `UNITY_ARDUINO_CONNECTION.md`
- **Hardware pin wiring:** `ardiunoConnection.md`

---

## 1. Prerequisites

- Unity version compatible with your AR / XR stack (project uses AR-related gameplay and TMP).
- **TextMesh Pro** imported (Window → TextMeshPro → Import TMP Essential Resources if prompted).
- On the PC: **Node.js** installed to run `Server/server.js`.

---

## 2. Run the PC bridge before play mode

From the repository root:

```bash
cd Server
npm install
npm start
```

1. Copy `Server/local.config.example.json` to `Server/local.config.json`.
2. Set `arduinoPort` to your board’s COM port (Windows: Device Manager → Ports).
3. Close **Arduino IDE Serial Monitor** while the server is running (only one app may use the COM port).

When the server starts, it prints URLs such as `http://192.168.x.x:3000`. Use the one that matches how you run Unity (see below).

---

## 3. Scene wiring

1. Create or open your scene.
2. Add an empty GameObject (e.g. `GameManager`) and attach **`GameManager`**.
3. Add another object (e.g. `ServerBridge`) and attach **`ServerCommunicator`**.
4. Wire **GameManager** Inspector fields:
   - **Truck Controller** → object with **`TruckController`** (waypoints, dustbin indices).
   - **Server Communicator** → the object with **`ServerCommunicator`**.
   - **Start Button**, **Status Text**, **Sensor Text** (TextMeshPro) as in your UI.
5. **`Arduino Communicator`** on `GameManager`:
   - Leave **empty** for normal play (Node uses USB).
   - Use only for Editor experiments with **direct** serial, and **stop Node first** to avoid COM conflicts.

---

## 4. `ServerCommunicator` — `serverURL`

| Where Unity runs | Set `serverURL` to |
|------------------|--------------------|
| **Editor on the same PC as the server** | `http://127.0.0.1:3000` |
| **Android phone** (server on Wi‑Fi PC) | `http://<PC_LAN_IP>:3000` (same value the server prints; phone and PC on the same network) |

Do not add a trailing slash.

---

## 5. Android Player Settings (HTTP)

If you use `http://` (not HTTPS), allow cleartext on Android:

1. **Edit → Project Settings → Player → Android** (Android tab).
2. In **Other Settings**, enable **Internet Access** (Required).
3. Depending on Unity version, enable **Uses Cleartext Traffic** or add a network security config that permits HTTP to your PC IP.

---

## 6. What happens at runtime

- **Start** → `GameManager` sends `TRUCK_START` through the server to the Arduino.
- **Dustbin reached** → `DUSTBINn_COLLECTED`; UI may call `GET /api/sensors/{n}` for live values cached on the server from Arduino lines like `OK_DUSTBINn|…`.
- **Route done** → `ROUTE_COMPLETE`.

Arduino line protocol matches `Server/server.js` (`OK_DUSTBIN*`, `SENSORS`, etc.).

---

## 7. Quick troubleshooting

| Issue | What to check |
|--------|----------------|
| “Server disconnected” / HTTP errors | Server running? Firewall allowing port **3000**? Correct `serverURL`? |
| Commands not reaching Arduino | Correct COM in `local.config.json`? Serial Monitor closed? Board connected? |
| Phone cannot reach PC | Phone Wi‑Fi same LAN as PC? `serverURL` is PC **LAN** IP, not `127.0.0.1` |
| COM “access denied” | Stop the other program (Node **or** Unity `ArduinoCommunicator`, not both on same port). |

---

## 8. Scripts reference (folder `Scripts/`)

| Script | Role |
|--------|------|
| `GameManager` | UI, route flow, sends commands via `ServerCommunicator` (or fallback serial). |
| `ServerCommunicator` | HTTP client to the PC server (`/api/status`, `/api/command`). |
| `TruckController` | AR truck path and dustbin events. |
| `ArduinoCommunicator` | Optional direct USB from Editor; conflicts with Node if both use COM. |
| `BluetoothManager` | Optional direct phone ↔ Bluetooth module (separate from USB/Node path). |
| `WebcamBackground` | Camera / background behavior as configured in your scene. |
