# PlayStation Controller Components

## Current GameInput bindings

| Action | Keyboard / Mouse | Generic Gamepad | DualSense / DualShock |
|---|---|---|---|
| Move | A / D | Left stick or D-pad | Left stick or D-pad |
| Look up | W | Right stick up | Right stick up |
| Jump | Space | South Button | X |
| Switch active locomotion player | Tab | North Button | Triangle |
| Attack animation + rumble | Left mouse button | West Button | Square |
| Pause | Escape | Start Button | Options / Start |

The `Look` action is independent from `Move`: the left stick and D-pad move the player but do not trigger `LookUp`. The left stick drives horizontal movement from its X axis only; vertical stick input is ignored, and horizontal input must reach the configurable **Horizontal Stick Movement Threshold** (default `0.35`) before moving. The D-pad and keyboard movement bindings are unchanged. Hold W on the keyboard or tilt the right stick upward while grounded to play the `isLookUp` animation state and temporarily suppress automatic head look-at targets.

Attack input only triggers the attack animation and a short rumble pulse; hit detection and combat behavior are intentionally not implemented.

Both controller components expose **Hardware Tests** buttons in their Inspector during Play Mode. These can test rumble, and the DualSense inspector also provides buttons for adaptive triggers, light bar, microphone LED, and player LEDs. A test requires a connected controller and still depends on Unity/driver transport support.

During Play Mode, changing the serialized light-bar color or rumble values applies the new values to a connected supported controller through `OnValidate`. DualSense player-LED and trigger-effect edits are also sent as soon as their values change. The *Apply ... On Connect* toggles still control connection-time initialization; turning a toggle on while playing also applies its current setting. New DualSense components start with matching continuous-resistance values on both adaptive triggers.


These components use Unity's Input System and are intended to demonstrate controller integration in a desktop Unity project. They do not replace Sony's console SDKs or claim tested compatibility with retail PlayStation hardware.

## DualSense (PS5)

Add `DualSenseController` to a GameObject. It automatically selects a connected DualSense. **Controller Index** is zero-based and selects among multiple matching devices. To explicitly select a runtime device, call `SetControllerOverride(gamepad)` with a device from `Gamepad.all`; pass `null` to return to automatic selection.

For input troubleshooting, enable **DEBUG FALLBACK → Debug Allow Generic Gamepad Fallback**. If no recognized DualSense is found, the component selects a generic Unity `Gamepad` so the standard sticks, D-pad, face buttons, and triggers can still be tested. Generic rumble can be tested with `SetMotorSpeeds`; DualSense-only features such as adaptive triggers, player LEDs, and mic LED remain unavailable until Unity exposes a recognized DualSense HID device. This option can be toggled during Play Mode.

Standard Input System controls are exposed as properties, including sticks, D-pad, triggers, face buttons, Options, Create, the PlayStation button, and the touchpad button. The component also attempts to read either of the two touchpad contacts, gyroscope, accelerometer, and battery level if the connected Input System layout exposes those controls. Optional sensor reads return `false` when the active layout does not expose that data.

DualSense output features include:
- Low- and high-frequency rumble.
- RGB light-bar color.
- Player LED mask and brightness.
- Microphone mute LED.
- Adaptive-trigger effects: off, continuous resistance, section resistance, vibration, and calibration.
- `SetFeedback(...)` combines rumble with any requested light-bar, player-LED, mic-LED, and adaptive-trigger updates into one HID output report. Prefer it when you need multiple effects to change together.

Example:

```csharp
using Portfolio.Input;
using UnityEngine;

public class DualSenseFeedbackExample : MonoBehaviour
{
    [SerializeField] private DualSenseController _controller;

    public void FireWeapon()
    {
        if (_controller == null || !_controller.IsConnected)
        {
            return;
        }

        _controller.SetFeedback(
            0.35f,
            0.75f,
            new Color(1f, 0.18f, 0.08f),
            DualSenseTriggerEffect.ContinuousResistance(2, 5),
            DualSenseTriggerEffect.Vibration(30, 1, 4, 1, 90),
            DualSenseMicLedState.On,
            playerLedMask: 0b00111,
            playerLedBrightness: DualSensePlayerLedBrightness.Medium);
    }

    public void ResetWeaponFeedback()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.StopFeedback();
    }
}
```

For serialized startup behavior, enable **Apply Light Bar Color On Connect**, **Apply Player LEDs On Connect**, or **Apply Trigger Effects On Connect** in the Inspector and configure their values.

### Transport note

The adaptive-trigger, player LED, mic LED, and combined feedback commands use a low-level `HIDO` output report. The report encoding is adapted from [UniSense](https://github.com/nullkal/UniSense); see [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). It targets the DualSense USB HID output-report format. Bluetooth output uses a different report format on some hosts, so advanced output methods return `false` and log a warning if the device rejects the command. Unity's built-in Input System also documents limitations for DualSense rumble and light-bar output over Bluetooth.

The return value of each output method tells the caller whether Unity accepted the device command; it cannot prove that physical hardware produced the expected effect.

## DualShock (PS4 and earlier)

Add `DualShockController` to a GameObject. It auto-detects supported DualShock-family devices through Unity's Input System. **Controller Index** is zero-based; use `SetControllerOverride(gamepad)` to select a specific runtime device, or pass `null` for automatic selection.

Enable **DEBUG FALLBACK → Debug Allow Generic Gamepad Fallback** to accept a generic Unity `Gamepad` when no matching PlayStation device is found. This is intended for input troubleshooting and allows the common gamepad controls and generic rumble to be tested, but does not add DualShock-specific hardware output to an unrecognized device. The fallback can be toggled during Play Mode.

The component exposes common gamepad input, touchpad-click and optional touch-position reads, rumble, light-bar color where supported, and combined rumble/light-bar updates for the HID DualShock 4 layout. Older models expose only the controls and output features supported by the model and Unity's current platform backend.

Example:

```csharp
using Portfolio.Input;
using UnityEngine;

public class DualShockFeedbackExample : MonoBehaviour
{
    [SerializeField] private DualShockController _controller;

    public void OnDamageTaken()
    {
        if (_controller == null || !_controller.IsConnected)
        {
            return;
        }

        _controller.SetRumbleAndLightBar(
            0.8f,
            0.45f,
            new Color(1f, 0.05f, 0.05f));
    }

    public void OnDamageSequenceEnded()
    {
        _controller?.StopRumble();
    }
}
```

## Scope

Both components monitor device add/remove/reconnect events and expose connection-change events. Input properties return neutral values when the component has no connected device. Select a matching device with the component's Controller Index or runtime `SetControllerOverride(gamepad)` method when handling multiple pads.

The debug fallback only searches Unity's `Gamepad.all`. Unity's Input System may represent an unrecognized HID as a `Joystick` instead of a `Gamepad`; this fallback cannot infer the intended mapping for such a device. Check **Window → Analysis → Input Debugger** to see the device type and layout. If it appears as a Joystick, it needs an explicit layout or manual control mapping rather than this Gamepad fallback.

Console targets need the appropriate Sony/Unity platform-specific input package and SDK integration. The desktop HID output-report implementation here is for portfolio demonstration and should not be treated as a console-ready controller backend.
