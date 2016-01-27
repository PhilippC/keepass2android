# InputStickAPI for Android OS

## About InputStick:
InputStick is an Android-compatible USB receiver. It allows to use your smartphone as a wireless keyboard, mouse, multimedia and game controller. 

##How does it work?
InputStick acts as a proxy between USB host and Android device:
* USB host detects is as a generic HID device. It knows nothing about Bluetooth interface. As a result, in most cases, there is no need to install any drivers or configure anything.
* Android device knows only about Bluetooth interface. Everything works with stock OS: root is NOT required, there is also no need to install customized OS.

`Android device <-(Bluetooth)-> InputStick <-(USB)-> PC`

![alt text](http://inputstick.com/images/how_2.png "How does it work diagram")

InputStick is detected as a generic keyboard and mouse (USB HID), it makes it compatible with wide range of hardware:
* PC (Windows, Linux, OS X),
* embedded systems (RaspberryPi etc.),
* consoles (PS3, Xbox360, only as keyboard, NOT game controller!),
* any USB-HID compatible USB host.

Remember: InputStick behaves EXACTLY as a USB keyboard and mouse - nothing more and nothing less. It is not able to put text directly into system clipboard etc.

## More info:
[Visit inputstick.com](http://inputstick.com)

[Download section](http://inputstick.com/download)

[GooglePlay](https://play.google.com/store/apps/developer?id=InputStick)

## Getting started:
Eclipse: Import InputStickAPI into workspace, add InputStickAPI as a library to your project: Project -> Properties -> Android -> Add.

It is recommended to start with: `com.inputstick.api.broadcast.InputStickBroadcast`
this can be as easy as a single line of code (InputStickUtility takes care of everything else):
```java
InputStickBroadcast.type(context, "text to type", "en-US);
```

If you need more control:

Managing connection:
`com.inputstick.api.basic.InputStickHID`
```java
connect(getApplication());
disconnect();
getState();
```

Implement callback:
`com.inputstick.api.InputStickStateListener`
```java
onStateChanged(int state); 
```

Keyboard interface:
`com.inputstick.api.basic.InputStickKeyboard`
```java
type("text to type", "en-US");
pressAndRelease(HIDKeycodes.ALT_LEFT, HIDKeycodes.KEY_ENTER);
```

Mouse interface:
`com.inputstick.api.basic.InputStickMouse`
```java
move((byte)10, (byte)5);
click(InputStickMouse.BUTTON_LEFT, 2); 
```

Consumer control interface:
`com.inputstick.api.basic.InputStickConsumer`
```java
consumerAction(InputStickConsumer.VOL_UP);
```

Gamepad interface:
`com.inputstick.api.basic.InputStickGamepad`
```java
customReport((byte)0x01, (byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00);
```

For more details and usage examples, please take a look at Android demo applications.
[Download section](http://inputstick.com/download)

## Known bugs:
Due to a bug in Bluetooth Stack in Android OS, calling type() methods from non-UI thread can result in missing characters when BT4.0 InputStick is used. Latest firmware (0.98) allows to create fix for the bug.

## InputStickUtility:
It is highly recommended that InputStickUtility application is installed (available on GooglePlay). It allows to make application development much easier. 
InputStickUtility provides background service that acts as a proxy between your application and InputStick. When InputStickUtility is used, you don't have to care about:
* enabling Bluetooth radio,
* discovering available devices,
* detecting type of InputStick device (BT2.1, BT4.0),
* password protection (if enabled).

![alt text](http://inputstick.com/images/apilayerssmall.png "InputStickUtility and API diagram")

There are two ways to communicate with InputStickUtility:
* Sending Broadcasts : simplest way to get started. InputStickUtility takes care of almost everything. Use `com.inputstick.api.broadcast.InputStickBroadcast`
* IPC connection: fast, low latency, gives you a lot of control, but makes application more complex. Use `com.inputstick.api.basic.InputStickHID`

## Direct connection:
If you want to avoid installing InputStickUtility application you can make direct connection from your application. You must be able to provide following parameters:
* Bluetooth MAC address of InputStick device,
* Bluetooth version (BT2.1, BT4.0),
* encryption key (if password protection is enabled).
Your application should also take care of notifying user about InputStick-related errors (connection failed, connection lost, etc.).
Your application must have `BLUETOOTH` and `BLUETOOTH_ADMIN` permissions. Since Android M, it is also necessary to have `LOCATION_COARSE` permission to scan for nearby Bluetooth devices.

## Keyboard layouts:
Always make sure that selected layout matches layout used by USB host (PC). Due to limitations of USB-HID it is not possible for InputStick to know what layout is use by the USB host. This must be manually provided by the user.
List of currently available keyboard layouts:
* da-DK 			- Danish (Denmark),
* de-CH 			- German (Switzerland),
* de-DE 			- German (Germany),
* de-DE (MAC) 	- German (Germany), Mac compatible version,
* en-DV 			- English (United States), Dvorak layout,
* en-GB 			- English (United Kingdom),
* en-US 			- English (United States),
* es-ES 			- Spanish (Spain),
* fi-FI 			- Finnish (Finland),
* fr-CH 			- French (Switzerland),
* fr-FR 			- French (France),
* he-IL 			- Hebrew (Israel),
* it-IT 			- Italian (Italy),
* nb-NO 			- Norwegian, Bokmal (Norway),
* pl-PL 			- Polish (Poland),
* pt-BR 			- Portuguese (Brazil),
* ru-RU 			- Russian (Russia),
* sk-SK 			- Slovak (Slovakia),
* sv-SE 			- Swedish (Sweden).

## Requirements (BT2.1 version):
* InputStick BT2.1 receiver, 
* Android 2.3 or later,
* Bluetooth 2.1 or later.

## Requirements (BT4.0 version):
* InputStick BT4.0 receiver,
* Android 4.3 or later,
* Bluetooth 4.0 (Bluetooth Low Energy).

## Technical limitations and things to consider:

USB device - InputStick
USB host - PC, game consoles, Raspberry Pi, etc.
HID - Human Interface Device (keyboard, mouse, gamepad, etc.)

InputStick communicates with USB host by sending HID reports for each interface (keyboard, mouse, consumer control).
HID report - data representing state or change of state of HID interface.

[Learn more: USB HID1.11 pdf](www.usb.org/developers/hidpage/HID1_11.pdf)


**Compatibility:**

USB host detects InputStick as a generic keyboard, mouse and consumer control composite device. It sees NO difference between physical keyboard/mouse and InputStick. Host does not know anything about Bluetooth interface.
In most cases, generic drivers for HID devices are used, there is no need to install any additional software or drivers.
If your USB host works with generic USB keyboard, it will most likely also work with InputStick. If necessary, you can make some adjustments to USB interface using InputStickUtility app (requires some knowledge about USB interface).


**NO feedback:**

In case of HID class devices, USB host does NOT provide any information about itself to USB device:
* type of hardware is unknown,
* OS is unknown,
* keyboard layout used by OS is unknown,
* there is no feedback whether characters were typed correctly.

Think of InputStick as of a blind person with en-US keyboard:
* you provide instructions, example: type "abC"
* InputStick executes the instruction by simulating user actions: press "A" key, release, press "B" key, release, press and hold "Shift" key, press "C" key, release all keys.
* InputStick has no way of knowing if these actions produced correct result

You (app user) must provide all necessary information and feedback!


**Typing speed:**

InputStick can type text way faster than any human. In some cases this can result in missing characters. Use slower typing speed when necessary.
Example: when PC is experiencing have CPU load, it is possible that in sometimes characters will be skipped (same thing will happen when using regular USB keyboard).


**Consumer control interface.**

Multimedia keys: allows to control media playback, system volume, launch applications.
Unfortunately there are differences between OSes in how consumer control actions are interpreted.

Example 1: there are 100 volume levels in Windows OS and 10 levels on Ubuntu. Increasing system volume by 1 will have different effect on each of them.

Example 2: when audio output is muted, increasing system volume by 1 can have different effects: Windows - volume level is increased by 1, but audio output will remain muted. Linux - volume level is uncreased by 1, audio output is unmuted.


**Bluetooth:**

Time required to establish connection:
* BT2.1 - usually 1-3 seconds,
* BT4.0 - usually less than a second.

Latency:
Bluetooth introduces additional latency (several ms in most cases, in some conditions can increase to several hundreds).

Range:
Walls and other obstacles will decrease performance of Bluetooth link. BT4.0 devices are generally more sensitive to this (due to Low Energy approach).
