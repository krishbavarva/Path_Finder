
#include <LiquidCrystal.h>
#include <string.h>
#include <stdio.h>


LiquidCrystal lcd(12, 11, 5, 4, 3, 2);

const int trigPin = 7;
const int echoPin = 6;
const int potPin = A0;
const int tempPin = A1;   
const int photoPin = A2;

const int ledTrash_Green = 10;
const int ledLiquid_Blue = 9;
const int ledLight_Yellow = 8;


int potValue = 0;
int waterLevel = 0;
int waterPer = 0;
long duration = 0;
int distanceCm = 0;
int trashLevel = 0;
int lightValue = 0;
float tempC = 25.0f;

const int lightThreshold = 400;
const int BIN_HEIGHT = 200;

const unsigned long BAUD_RATE = 9600;

char serialLine[80];
uint8_t serialLineLen = 0;

unsigned long nextLcdPhaseMs = 0;
bool lcdPhaseWater = true;

unsigned long statusUntilMs = 0;
char statusL1[17];
char statusL2[17];

void setup() {
  Serial.begin(BAUD_RATE);

  lcd.begin(16, 2);
  lcd.print(F("System Start..."));
  delay(1000);
  lcd.clear();

  pinMode(trigPin, OUTPUT);
  pinMode(echoPin, INPUT);

  pinMode(ledTrash_Green, OUTPUT);
  pinMode(ledLiquid_Blue, OUTPUT);
  pinMode(ledLight_Yellow, OUTPUT);

  nextLcdPhaseMs = millis();
}

void loop() {
<<<<<<< HEAD
=======
  readSensors();
  processSerial();
  readSensors();
  processSerial();
  updateLcd();
}

void readSensors() {
  else if (potValue < 300) waterPer = 25;
  else if (potValue < 600) waterPer = 50;
  else if (potValue < 900) waterPer = 75;
  else waterPer = 100;

  if (waterPer > 75) digitalWrite(ledLiquid_Blue, HIGH);
  else digitalWrite(ledLiquid_Blue, LOW);

  digitalWrite(trigPin, LOW);
  delayMicroseconds(2);
  digitalWrite(trigPin, HIGH);
  delayMicroseconds(10);
  digitalWrite(trigPin, LOW);

  duration = pulseIn(echoPin, HIGH, 30000UL);
  distanceCm = (int)(duration * 0.034f / 2.0f);
  if (distanceCm <= 0 || distanceCm > BIN_HEIGHT) distanceCm = BIN_HEIGHT;

  trashLevel = map(distanceCm, 0, BIN_HEIGHT, 100, 0);
  if (trashLevel < 0) trashLevel = 0;
  if (trashLevel > 100) trashLevel = 100;

  if (trashLevel > 80) digitalWrite(ledTrash_Green, HIGH);
  else digitalWrite(ledTrash_Green, LOW);

  lightValue = analogRead(photoPin);
  if (lightValue < lightThreshold) digitalWrite(ledLight_Yellow, LOW);
  else digitalWrite(ledLight_Yellow, HIGH);

<<<<<<< HEAD
  lcd.clear();
  lcd.setCursor(0, 0);

const char* lidOpenClosed() {
  return (lightValue < lightThreshold) ? "closed" : "open";
}

void processSerial() {
  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      serialLine[serialLineLen] = '\0';
      if (serialLineLen > 0) {
        handleCommand(serialLine);
      }
      serialLineLen = 0;
    } else if (serialLineLen < (sizeof(serialLine) - 1)) {
      serialLine[serialLineLen++] = c;
    } else {
      serialLineLen = 0;
    }
  }
}

void handleCommand(char* cmd) {

  char* end = cmd + strlen(cmd) - 1;
  while (end > cmd && (*end == ' ' || *end == '\t')) {
    *end-- = '\0';
  }

  if (strcmp(cmd, "TRUCK_START") == 0) {
    sendSensorsAll();
    showStatusMsg("TRUCK_START", "Sensors sent");
  } else if (strcmp(cmd, "DUSTBIN1_COLLECTED") == 0) {
    sendOkDustbin1();
    showStatusMsg("Bin 1", "OK sent");
  } else if (strcmp(cmd, "DUSTBIN2_COLLECTED") == 0) {
    sendOkDustbin2();
    showStatusMsg("Bin 2", "OK sent");
  } else if (strcmp(cmd, "DUSTBIN3_COLLECTED") == 0) {
    sendOkDustbin3();
    showStatusMsg("Bin 3", "OK sent");
  } else if (strcmp(cmd, "ROUTE_COMPLETE") == 0) {
    Serial.println(F("OK_ROUTE_DONE"));
    showStatusMsg("Route", "COMPLETE");
  } else if (strcmp(cmd, "READ_ALL") == 0) {
    sendSensorsAll();
    showStatusMsg("READ_ALL", "Sensors sent");
  }
}

void sendOkDustbin1() {
  Serial.print(F("OK_DUSTBIN1|trash:"));
  Serial.print(trashLevel);
  Serial.print(F("|lid:"));
  Serial.print(lidOpenClosed());
  Serial.print(F("|full:"));
  Serial.println((trashLevel > 90) ? F("yes") : F("no"));
}

void sendOkDustbin2() {
  Serial.print(F("OK_DUSTBIN2|water:"));
  Serial.print(waterPer);
  Serial.print(F("|temp:"));
  Serial.print(tempC, 1);
  Serial.print(F("|hot:"));
  Serial.println((tempC > 35.0f) ? F("yes") : F("no"));
}

void sendOkDustbin3() {
  Serial.print(F("OK_DUSTBIN3|lid:"));
  Serial.print(lidOpenClosed());
  Serial.print(F("|light:"));
  Serial.println(lightValue);
}

void sendSensorsAll() {
  Serial.print(F("SENSORS|trash:"));
  Serial.print(trashLevel);
  Serial.print(F("|water:"));
  Serial.print(waterPer);
  Serial.print(F("|temp:"));
  Serial.print(tempC, 1);
  Serial.print(F("|lid1:"));
  Serial.print(lidOpenClosed());
  Serial.print(F("|lid2:"));
  Serial.print(lidOpenClosed());
  Serial.print(F("|light:"));
  Serial.println(lightValue);
}

void showStatusMsg(const char* a, const char* b) {
  snprintf(statusL1, sizeof(statusL1), "%.16s", a ? a : "");
  snprintf(statusL2, sizeof(statusL2), "%.16s", b ? b : "");
  statusUntilMs = millis() + 2000;
}

void updateLcd() {
  unsigned long now = millis();

  if (now < statusUntilMs) {
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print(statusL1);
    lcd.setCursor(0, 1);
    lcd.print(statusL2);
    return;
  }

  if (now < nextLcdPhaseMs) return;
  nextLcdPhaseMs = now + 2000;

  lcd.clear();
  if (lcdPhaseWater) {
    lcd.setCursor(0, 0);
    lcd.print(F("Light:"));
    lcd.print(lightValue < lightThreshold ? F("Dark") : F("Bright"));
    lcd.setCursor(0, 1);
    lcd.print(F("Water:"));
    lcd.print(waterPer);
    lcd.print('%');
  } else {
    lcd.setCursor(0, 0);
    lcd.print(F("Trash:"));
    lcd.print(trashLevel);
    lcd.print('%');
    lcd.setCursor(0, 1);
    lcd.print(trashLevel > 90 ? F("FULL!") : F("Space OK"));
  }
  lcdPhaseWater = !lcdPhaseWater;
}
>>>>>>> bfc217c0e6d6ef8bcdb6cc072a144d9fe6939490
