/**
 * AR Garbage Truck — bridge on your PC
 *
 * Arduino connects to this PC only via USB (virtual COM). No Wi‑Fi radio on the Arduino.
 *
 * 1) HTTP API for Unity (Editor or phone on the same LAN as this PC).
 * 2) Opens the USB serial port and forwards lines to/from the Arduino (9600 baud).
 *
 * USAGE:
 *   cd Server
 *   npm install
 *   npm start
 *
 * Copy local.config.example.json → local.config.json and set "arduinoPort" to your
 * COM port (Device Manager → Ports). Close Arduino IDE Serial Monitor while this runs.
 *
 * If the Arduino is unplugged, the server still runs; serial commands are logged as unsent.
 */

const express = require('express');
const fs = require('fs');
const path = require('path');
const os = require('os');
const app = express();

app.use(express.json());

// ============ SETTINGS ============
// Optional: copy local.config.example.json → local.config.json and edit (ignored by git if you add it).
let localConfig = {};
const configPath = path.join(__dirname, 'local.config.json');
if (fs.existsSync(configPath)) {
    try {
        localConfig = JSON.parse(fs.readFileSync(configPath, 'utf8'));
    } catch (e) {
        console.log('[Config] Could not parse local.config.json:', e.message);
    }
}

const ARDUINO_PORT = process.env.ARDUINO_PORT || localConfig.arduinoPort || 'COM5';
const BAUD_RATE = Number(process.env.BAUD_RATE || localConfig.baudRate || 9600);
const WEB_PORT = Number(process.env.WEB_PORT || localConfig.webPort || 3000);
// ==================================

// ============ ARDUINO SERIAL ============
let serialPort = null;
let arduinoConnected = false;
let lastArduinoMessage = '';

// Store parsed sensor data from Arduino
let sensorData = {
    dustbin1: { trash: 0, lid: 'unknown', full: 'no' },
    dustbin2: { water: 0, temp: 0, hot: 'no' },
    dustbin3: { lid: 'unknown', light: 0 },
    lastUpdate: null
};

function connectArduino(retries = 3) {
    try {
        const { SerialPort } = require('serialport');
        const { ReadlineParser } = require('@serialport/parser-readline');

        console.log(`[Arduino] Attempting to open ${ARDUINO_PORT}...`);

        serialPort = new SerialPort({
            path: ARDUINO_PORT,
            baudRate: BAUD_RATE,
            autoOpen: false
        });

        const parser = serialPort.pipe(new ReadlineParser({ delimiter: '\n' }));

        parser.on('data', (data) => {
            lastArduinoMessage = data.trim();
            console.log(`[Arduino] Received: ${lastArduinoMessage}`);
            parseSensorData(lastArduinoMessage);
        });

        serialPort.on('error', (err) => {
            console.log(`[Arduino] Error: ${err.message}`);
            arduinoConnected = false;
        });

        serialPort.on('close', () => {
            console.log('[Arduino] Disconnected');
            arduinoConnected = false;
        });

        serialPort.open((err) => {
            if (err) {
                console.log(`[Arduino] Open failed: ${err.message}`);
                arduinoConnected = false;
                serialPort = null;
                if (retries > 0) {
                    console.log(`[Arduino] Retrying in 3 seconds... (${retries} left)`);
                    setTimeout(() => connectArduino(retries - 1), 3000);
                } else {
                    console.log('[Arduino] Server will run without Arduino.');
                }
            } else {
                console.log(`[Arduino] Connected on ${ARDUINO_PORT}`);
                arduinoConnected = true;
            }
        });

    } catch (err) {
        console.log(`[Arduino] Could not connect: ${err.message}`);
        if (retries > 0) {
            console.log(`[Arduino] Retrying in 3 seconds... (${retries} left)`);
            setTimeout(() => connectArduino(retries - 1), 3000);
        } else {
            console.log('[Arduino] Server will run without Arduino.');
        }
        arduinoConnected = false;
    }
}

function sendToArduino(message) {
    if (serialPort && serialPort.isOpen) {
        serialPort.write(message + '\n', (err) => {
            if (err) {
                console.log(`[Arduino] Send error: ${err.message}`);
            } else {
                console.log(`[Arduino] Sent: ${message}`);
            }
        });
        return true;
    }
    console.log(`[Arduino] Not connected. Message not sent: ${message}`);
    return false;
}

// Parse sensor data from Arduino responses like:
//   OK_DUSTBIN1|trash:85|lid:open|full:yes
//   OK_DUSTBIN2|water:75|temp:32.5|hot:no
//   OK_DUSTBIN3|lid:closed|light:320
//   SENSORS|trash:85|water:50|temp:25.0|lid1:closed|lid2:open
function parseSensorData(message) {
    if (!message.includes('|')) return;

    const parts = message.split('|');
    const prefix = parts[0];
    const data = {};

    for (let i = 1; i < parts.length; i++) {
        const [key, value] = parts[i].split(':');
        if (key && value) {
            data[key] = isNaN(value) ? value : parseFloat(value);
        }
    }

    sensorData.lastUpdate = new Date().toISOString();

    if (prefix === 'OK_DUSTBIN1') {
        sensorData.dustbin1 = { trash: data.trash || 0, lid: data.lid || 'unknown', full: data.full || 'no' };
        console.log('[Sensors] Dustbin 1:', sensorData.dustbin1);
    }
    else if (prefix === 'OK_DUSTBIN2') {
        sensorData.dustbin2 = { water: data.water || 0, temp: data.temp || 0, hot: data.hot || 'no' };
        console.log('[Sensors] Dustbin 2:', sensorData.dustbin2);
    }
    else if (prefix === 'OK_DUSTBIN3') {
        sensorData.dustbin3 = { lid: data.lid || 'unknown', light: data.light || 0 };
        console.log('[Sensors] Dustbin 3:', sensorData.dustbin3);
    }
    else if (prefix === 'SENSORS') {
        sensorData.dustbin1.trash = data.trash || 0;
        sensorData.dustbin1.lid = data.lid1 || 'unknown';
        sensorData.dustbin2.water = data.water || 0;
        sensorData.dustbin2.temp = data.temp || 0;
        sensorData.dustbin3.lid = data.lid2 || 'unknown';
        if (data.light !== undefined) sensorData.dustbin3.light = data.light;
        console.log('[Sensors] All sensors updated');
    }
}

// ============ API ENDPOINTS ============

// Health check - Unity app calls this to verify server is reachable
app.get('/api/status', (req, res) => {
    res.json({
        server: true,
        arduino: arduinoConnected,
        lastMessage: lastArduinoMessage
    });
});

// Send a command to Arduino
app.post('/api/command', (req, res) => {
    const { command } = req.body;
    if (!command) {
        return res.status(400).json({ error: 'Missing command field' });
    }

    console.log(`[API] Command received: ${command}`);
    const sent = sendToArduino(command);

    res.json({
        success: true,
        arduinoSent: sent,
        command: command
    });
});

// Truck started
app.post('/api/truck/start', (req, res) => {
    console.log('[API] Truck started');
    sendToArduino('TRUCK_START');
    res.json({ success: true, message: 'TRUCK_START sent' });
});

// Dustbin collected (1, 2, or 3)
app.post('/api/dustbin/:number/collected', (req, res) => {
    const num = parseInt(req.params.number);
    if (num < 1 || num > 3) {
        return res.status(400).json({ error: 'Dustbin number must be 1, 2, or 3' });
    }
    const command = `DUSTBIN${num}_COLLECTED`;
    console.log(`[API] ${command}`);
    sendToArduino(command);
    res.json({ success: true, message: `${command} sent` });
});

// Route complete
app.post('/api/route/complete', (req, res) => {
    console.log('[API] Route complete');
    sendToArduino('ROUTE_COMPLETE');
    res.json({ success: true, message: 'ROUTE_COMPLETE sent' });
});

// Get last Arduino message
app.get('/api/arduino/message', (req, res) => {
    res.json({
        connected: arduinoConnected,
        message: lastArduinoMessage
    });
});

// Get sensor data from all dustbins
app.get('/api/sensors', (req, res) => {
    res.json({
        connected: arduinoConnected,
        sensors: sensorData
    });
});

// Get sensor data for a specific dustbin
app.get('/api/sensors/:number', (req, res) => {
    const num = parseInt(req.params.number);
    if (num === 1) res.json({ dustbin: 1, data: sensorData.dustbin1 });
    else if (num === 2) res.json({ dustbin: 2, data: sensorData.dustbin2 });
    else if (num === 3) res.json({ dustbin: 3, data: sensorData.dustbin3 });
    else res.status(400).json({ error: 'Dustbin number must be 1, 2, or 3' });
});

// Request Arduino to send all sensor readings
app.post('/api/sensors/read', (req, res) => {
    console.log('[API] Requesting all sensor data');
    sendToArduino('READ_ALL');
    res.json({ success: true, message: 'READ_ALL sent' });
});

// ============ START SERVER ============
app.listen(WEB_PORT, '0.0.0.0', () => {
    // Delay Arduino connection to let server start first
    setTimeout(() => connectArduino(), 1000);
    console.log('');
    console.log('========================================');
    console.log('  AR Garbage Truck - API Server');
    console.log('========================================');
    console.log('');

    const interfaces = os.networkInterfaces();
    console.log('Server URL (use this in Unity):');
    console.log('');
    for (const [name, addrs] of Object.entries(interfaces)) {
        for (const addr of addrs) {
            if (addr.family === 'IPv4' && !addr.internal) {
                console.log(`  http://${addr.address}:${WEB_PORT}`);
            }
        }
    }
    console.log('');
    console.log(`Arduino: ${arduinoConnected ? 'Connected' : 'Not connected'} (${ARDUINO_PORT})`);
    console.log('');
    console.log('API Endpoints:');
    console.log('  GET  /api/status              - Check server & Arduino status');
    console.log('  POST /api/truck/start          - Truck started moving');
    console.log('  POST /api/dustbin/1/collected   - Dustbin 1 collected');
    console.log('  POST /api/dustbin/2/collected   - Dustbin 2 collected');
    console.log('  POST /api/dustbin/3/collected   - Dustbin 3 collected');
    console.log('  POST /api/route/complete        - Route finished');
    console.log('  POST /api/command               - Send custom command');
    console.log('  GET  /api/sensors               - All sensor data');
    console.log('  GET  /api/sensors/1             - Dustbin 1 sensor data');
    console.log('  POST /api/sensors/read          - Request fresh sensor reading');
    console.log('');
    console.log('Press Ctrl+C to stop.');
});
