Hardwares
1.Core Components
|   1x Arduino Uno R3 (with USB A-to-B cable for power and programming)
|   1x Full-Size Breadboard (830 tie-points recommended)
2.Display
|   1x 16x2 LCD Display (HD44780 compatible)
|   1x 10kΩ Potentiometer (Rotary knob for adjusting the LCD screen contrast)
3.Sensors
|   1x HC-SR04 Ultrasonic Distance Sensor (To measure trash level)
|   1x Photoresistor / LDR (To measure ambient light)
|   1x Water Level Sensor (To measure Level of  water)
4.Indicators (LEDs)
|   1x Green LED (Trash Full Indicator)
|   1x Blue LED (Water Level Indicator)
|   1x Yellow LED (Light Status Indicator)
5.Register
|   4x 220Ω for LED & LCD
|   1x 10kΩ for Photoregister

Arduino UNO Connectivity & Pin Mapping
1. 16x2 LCD Display
| LCD Pin | Pin Name | Connection to Arduino / Breadboard |
| :--- | :--- | :--- |
| 1 | GND | Breadboard GND |
| 2 | VCC | Breadboard 5V |
| 3 | V0 (Contrast) | Middle pin (wiper) of the LCD Potentiometer |
| 4 | RS (Register Select)| Digital Pin 12 |
| 5 | R/W (Read/Write) | Breadboard GND |
| 6 | E (Enable) | Digital Pin 11 |
| 11 | D4 | Digital Pin 5 |
| 12 | D5 | Digital Pin 4 |
| 13 | D6 | Digital Pin 3 |
| 14 | D7 | Digital Pin 2 |
| 15 | LED+ (Anode) | 5V via 220Ω Resistor (crucial for physical build) |
| 16 | LED- (Cathode) | Breadboard GND |

2. HC-SR04 Ultrasonic Sensor (Trash Level)
| Sensor Pin | Connection to Arduino / Breadboard |
| :--- | :--- |
| VCC | Breadboard 5V |
| Trig | Digital Pin 7 |
| Echo | Digital Pin 6 |
| GND | Breadboard GND |

3. Indicator LEDs
| Component | Connection to Arduino / Breadboard |
| :--- | :--- |
| Green LED (Trash Full) | Digital Pin 10 (via current-limiting resistor to GND) |
| Blue LED (Water Level) | Digital Pin 9 (via current-limiting resistor to GND) |
| Yellow LED (Light Status) | Digital Pin 8 (via current-limiting resistor to GND) |

4. Analog Sensors
| Component | Connection to Arduino / Breadboard |
| :--- | :--- |
| Potentiometer (Water Level Simulation) | Middle Pin (Wiper) to Analog Pin A0