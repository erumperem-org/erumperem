# BarSystem — Generic Bar System for Unity

A modular bar system for Unity designed for health, stamina, corruption, and any other resource that can be represented by a minimum, maximum, and current value.

The architecture is designed so that **only the View layer interacts directly with `UnityEngine.UI.Slider`**.

State management, behaviors, persistence, configuration, and rendering are separated into independent responsibilities, making the system easier to maintain, extend, and test.

---

## Features

BarSystem currently provides:

* Generic bar state management through `BarModel`
* Reusable behaviors through `IBarBehavior`
* Health, stamina, and corruption implementations
* Optional regeneration
* Automatic value growth over time
* On-demand resource consumption
* Optional smooth visual transitions
* JSON-based persistence
* Configurable default values through `ScriptableObject`
* Decoupled UI rendering through `IBarView`
* Custom Inspector testing tools for the included bar types
* Support for creating additional bar types without modifying the Core layer

---

## Installation

Copy the entire `BarSystem` folder into your Unity project's `Assets` directory.

For example:

```text
Assets/
└── BarSystem/
```

To create a bar in a scene:

1. Create a standard Unity UI `Slider`:

   ```text
   Canvas > UI > Slider
   ```

2. Add `UISliderBarView` to the same GameObject as the `Slider`.

3. Create a `BarConfigSO` through:

   ```text
   Assets > Create > BarSystem > Bar Config
   ```

4. Configure:

   * `Id`
   * `Min Default`
   * `Max Default`
   * `Current Default`
   * `Bar Color`

5. Add the desired installer to a GameObject:

   * `HealthBarInstaller`
   * `StaminaBarInstaller`
   * `CorruptionBarInstaller`

6. Assign the `BarConfigSO` and `UISliderBarView` references in the Inspector.

The installer creates and connects the runtime components automatically.

---

# Architecture

The system is divided into the following layers:

```text
BarSystem/
│
├── Core/
│   ├── BarModel
│   ├── BarController
│   ├── BarSaveData
│   ├── IBarBehavior
│   └── IBarView
│
├── Behaviors/
│   ├── RegenBehavior
│   ├── DrainOnUseBehavior
│   └── GrowthOverTimeBehavior
│
├── Persistence/
│   ├── IBarStateRepository
│   └── JsonFileBarStateRepository
│
├── Config/
│   └── BarConfigSO
│
├── View/
│   ├── UISliderBarView
│   └── SmoothedBarView
│
├── Bars/
│   ├── Health/
│   │   └── HealthBarInstaller
│   │
│   ├── Stamina/
│   │   └── StaminaBarInstaller
│   │
│   └── Corruption/
│       └── CorruptionBarInstaller
│
└── Editor/
    ├── HealthBarInstallerEditor
    ├── StaminaBarInstallerEditor
    └── CorruptionBarInstallerEditor
```

Each layer has a specific responsibility.

---

## Core

The Core layer contains the fundamental abstractions and runtime state of the system.

It does not contain any UI-specific implementation.

### `BarModel`

Stores the runtime state of a bar:

```text
Id
Min
Max
Current
Normalized
```

It is responsible for:

* Clamping values between `Min` and `Max`
* Changing the current value
* Changing the maximum value
* Maintaining the normalized value
* Notifying listeners when values change
* Notifying listeners when the minimum or maximum is reached

The normalized value is calculated as:

```text
(Current - Min) / (Max - Min)
```

and is exposed through:

```csharp
Model.Normalized
```

The model also exposes:

```csharp
OnValueChanged
OnMaxChanged
OnReachedMin
OnReachedMax
```

### Changing a value

Use:

```csharp
Model.SetCurrent(value);
```

or:

```csharp
Model.ApplyDelta(amount);
```

For example:

```csharp
Model.ApplyDelta(-10f);
```

reduces the bar by 10.

```csharp
Model.ApplyDelta(10f);
```

increases it by 10.

---

## `BarController`

`BarController` connects the three fundamental parts of a bar:

```text
BarModel
    +
IBarBehavior
    +
IBarView
```

Its responsibilities are:

* Updating behaviors
* Listening to changes in the model
* Updating all registered views
* Managing behaviors and views
* Keeping the Model independent from rendering logic

The controller should receive a tick from the owning Unity component:

```csharp
_controller.Tick(Time.deltaTime);
```

---

# Behaviors

Behaviors represent reusable rules that can modify a bar over time.

Every behavior implements:

```csharp
IBarBehavior
```

with:

```csharp
void Tick(float deltaTime, BarModel model);
```

This allows bar functionality to be composed instead of requiring a different `BarModel` subclass for every resource type.

---

## `RegenBehavior`

Applies a value continuously every second.

Example:

```csharp
new RegenBehavior(10f);
```

adds 10 units per second.

It can also receive an activation condition:

```csharp
new RegenBehavior(
    10f,
    isActive: () => canRegenerate
);
```

This is used by stamina so that regeneration only starts after the configured idle delay.

Although primarily intended for regeneration, a negative rate can also be used for continuous depletion.

---

## `DrainOnUseBehavior`

Represents resource consumption triggered by an external gameplay event.

It does **not** modify the model automatically during `Tick`.

Instead, consumption is explicitly requested:

```csharp
_drain.Consume(Model, amount);
```

This makes it useful for:

* Running
* Attacking
* Dodging
* Casting abilities
* Performing resource-consuming actions

It is currently used by `StaminaBarInstaller`.

---

## `GrowthOverTimeBehavior`

Automatically increases a bar over time.

Example:

```csharp
new GrowthOverTimeBehavior(0.5f);
```

adds `0.5` units per second.

It is currently used by the corruption bar.

A negative rate could also be used to create automatic decay.

---

# Included Bar Types

The package currently contains three example/composed bar types.

| Bar        | Behaviors                                          | Direct Operations                   |
| ---------- | -------------------------------------------------- | ----------------------------------- |
| Health     | Optional `RegenBehavior`                           | `ApplyDamage`, `ApplyHeal`          |
| Stamina    | `DrainOnUseBehavior` + conditional `RegenBehavior` | `Consume`                           |
| Corruption | Optional `GrowthOverTimeBehavior`                  | `AddCorruption`, `ReduceCorruption` |

These installers are intentionally thin.

They mainly determine which reusable components should be combined for each resource.

---

# Health Bar

`HealthBarInstaller` creates a health bar using:

```text
BarModel
+
optional RegenBehavior
+
IBarView
+
optional SmoothedBarView
+
IBarStateRepository
```

### Damage

```csharp
healthBar.ApplyDamage(10f);
```

### Healing

```csharp
healthBar.ApplyHeal(10f);
```

Damage and healing are direct gameplay events rather than behaviors because they happen at specific moments instead of continuously every frame.

---

# Stamina Bar

`StaminaBarInstaller` combines:

```text
BarModel
+
DrainOnUseBehavior
+
conditional RegenBehavior
+
IBarView
+
optional SmoothedBarView
+
IBarStateRepository
```

### Consuming stamina

```csharp
staminaBar.Consume(20f);
```

Calling `Consume` also resets the regeneration timer.

Stamina regeneration begins only after:

```csharp
_secondsBeforeRegen
```

seconds have passed since the last consumption.

The regeneration rate is controlled by:

```csharp
_regenPerSecond
```

---

# Corruption Bar

`CorruptionBarInstaller` combines:

```text
BarModel
+
optional GrowthOverTimeBehavior
+
IBarView
+
optional SmoothedBarView
+
IBarStateRepository
```

### Add corruption

```csharp
corruptionBar.AddCorruption(10f);
```

### Reduce corruption

```csharp
corruptionBar.ReduceCorruption(10f);
```

Automatic corruption growth can be enabled or disabled through:

```csharp
_growOverTime
```

and its rate is controlled by:

```csharp
_growthPerSecond
```

---

# Configuration

## `BarConfigSO`

`BarConfigSO` contains the design-time configuration of a bar.

It currently stores:

```text
Id
MinDefault
MaxDefault
CurrentDefault
BarColor
```

Create one through:

```text
Assets > Create > BarSystem > Bar Config
```

### Id

`Id` must uniquely identify the bar.

For example:

```text
player_health
player_stamina
player_corruption
```

The persistence system uses this value as the save identifier.

Avoid assigning the same `Id` to independent bars unless they are intentionally meant to share the same saved state.

---

# Persistence

Runtime state persistence is separated from design-time configuration.

This distinction is important:

### `BarConfigSO`

Defines the default configuration authored in the Unity Editor.

### `IBarStateRepository`

Defines how runtime state is stored and restored.

The included implementation is:

```csharp
JsonFileBarStateRepository
```

---

## `JsonFileBarStateRepository`

The default repository saves one JSON file per bar inside:

```csharp
Application.persistentDataPath
```

under the default folder:

```text
BarSystemSaves
```

The filename is generated using the bar's `Id`.

Conceptually:

```text
Application.persistentDataPath/
└── BarSystemSaves/
    ├── player_health.json
    ├── player_stamina.json
    └── player_corruption.json
```

Each save contains:

```text
Id
Current
Max
```

When an installer initializes a bar, it attempts to load an existing state.

If no saved state exists, it uses the defaults from `BarConfigSO`.

For example:

```csharp
float max = saved?.Max ?? _config.MaxDefault;
float current = saved?.Current ?? _config.CurrentDefault;
```

The repository is accessed through:

```csharp
IBarStateRepository
```

so the JSON implementation can later be replaced without changing the Core, Behaviors, or View layers.

Possible alternatives include:

* Save slots
* A centralized save file
* Binary serialization
* Cloud saves
* Database persistence
* A project-specific save system

---

# View Layer

Rendering is abstracted through:

```csharp
IBarView
```

The Core only knows this interface:

```csharp
public interface IBarView
{
    void SetNormalizedValue(float normalizedValue);
}
```

It does not know what visual component is being used.

---

## `UISliderBarView`

`UISliderBarView` is the concrete implementation included with the package.

It converts a normalized value into a Unity UI `Slider` value.

The Slider is configured to operate between:

```text
0
and
1
```

which allows every bar to use the same View regardless of its actual range.

For example:

```text
Health:      0 / 100
Stamina:    25 / 50
Corruption: 50 / 100
```

are converted into normalized values before reaching the UI.

This means gameplay values remain independent from their visual representation.

---

# Smooth Bar Transitions

Each included installer supports optional visual smoothing.

When smoothing is enabled:

```text
BarController
      ↓
SmoothedBarView
      ↓
UISliderBarView
```

When smoothing is disabled:

```text
BarController
      ↓
UISliderBarView
```

`SmoothedBarView` acts as a decorator around any `IBarView`.

It gradually interpolates its displayed value toward the actual value stored in `BarModel`.

The interpolation speed is configured through:

```csharp
_smoothingSpeed
```

Because smoothing is implemented as a View decorator, neither `BarModel` nor `UISliderBarView` needs to contain animation logic.

---

# Editor Testing Tools

The package includes Custom Inspectors for the three included installers.

These controls are available during **Play Mode** and allow the bars to be tested directly through the Inspector.

---

## Health Inspector

`HealthBarInstallerEditor` provides:

```text
Apply Damage
Heal
```

This allows health changes to be tested without creating temporary gameplay scripts.

---

## Stamina Inspector

`StaminaBarInstallerEditor` provides:

```text
Consume
Hold to Consume
```

The hold button continuously consumes stamina while the mouse button remains pressed.

This can be used to simulate behaviors such as running or another continuously stamina-consuming action.

---

## Corruption Inspector

`CorruptionBarInstallerEditor` provides:

```text
Add Corruption
Reduce Corruption
```

This allows both directions of corruption changes to be tested from the Inspector.

---

# Creating a New Bar Type

You normally do **not** need to modify:

```text
Core
Persistence
View
```

to create a new type of bar.

For example, suppose the game needs a mana bar.

First create a new `BarConfigSO`:

```text
Id: mana
Min Default: 0
Max Default: 100
Current Default: 100
```

Then create an installer that composes the required components.

For example:

```csharp
Model = new BarModel(
    _config.Id,
    _config.MinDefault,
    max,
    current
);

_controller = new BarController(Model);

_controller.AddBehavior(
    new RegenBehavior(_regenPerSecond)
);

_controller.AddView(view);
```

Gameplay operations can then directly modify the model:

```csharp
public void ConsumeMana(float amount)
{
    Model.ApplyDelta(-amount);
}
```

If the new bar requires behavior that does not already exist, create another implementation of:

```csharp
IBarBehavior
```

inside the `Behaviors` layer.

That behavior can then be reused by any future bar.

---

# Example: Adding a New Behavior

Suppose a resource should continuously decrease.

A behavior could be implemented as:

```csharp
using BarSystem.Core;

namespace BarSystem.Behaviors
{
    public class DecayBehavior : IBarBehavior
    {
        private readonly float _ratePerSecond;

        public DecayBehavior(float ratePerSecond)
        {
            _ratePerSecond = ratePerSecond;
        }

        public void Tick(float deltaTime, BarModel model)
        {
            model.ApplyDelta(-_ratePerSecond * deltaTime);
        }
    }
}
```

It could then be attached to any bar:

```csharp
_controller.AddBehavior(
    new DecayBehavior(5f)
);
```

No modification to `BarModel` would be required.

---

# Dependency Philosophy

The main architectural principle of BarSystem is that high-level compositions should depend on small abstractions rather than placing all responsibilities inside a single component.

Conceptually:

```text
                    Bars
                 /   |   \
                /    |    \
       Behaviors   View   Persistence
              \     |      /
               \    |     /
                    Core
```

`BarModel` does not know:

* Which type of bar it represents
* How it is rendered
* Where it is saved
* Whether it regenerates
* Whether it grows automatically
* What gameplay action changes it

It only manages valid bar state.

This makes the same model reusable for health, stamina, corruption, mana, rage, oxygen, temperature, durability, hunger, or other resources.

---

# Current Design Notes

## Threshold System

Earlier versions of BarSystem included references to threshold-based behavior and `ThresholdNotifierBehavior`.

That system has been removed from the current version.

The current package does **not** contain:

```text
ThresholdNotifierBehavior
Threshold configuration in BarConfigSO
Threshold-specific logic in HealthBarInstaller
Threshold-specific logic in CorruptionBarInstaller
```

If threshold functionality becomes necessary again, it can be introduced independently through an `IBarBehavior` or by subscribing directly to `BarModel` events without modifying the basic bar state implementation.

---

## Assembly Definitions

The current package does **not** include `.asmdef` files.

The folder separation still represents the intended architectural boundaries, but Unity does not currently enforce those boundaries at assembly level.

Assembly Definitions can be introduced later if stricter compile-time dependency separation or faster incremental compilation is required.

---

## Bar Color

`BarConfigSO` currently exposes:

```csharp
BarColor
```

as visual configuration data.

The current `UISliderBarView` does not automatically apply this color to the Slider graphics.

It is available for future View implementations or additional visual configuration logic.

---

# Known Limitations and Possible Extensions

The current implementation intentionally remains small and generic.

Possible extensions include:

* Centralized save-slot integration
* Save files containing multiple bars
* Additional `IBarView` implementations
* Filled Image support
* UI Toolkit support
* World-space bars
* Runtime color application
* Additional behaviors
* Threshold/event behaviors
* Runtime bar configuration
* Dependency injection
* Assembly Definitions
* Automated unit tests
* Additional editor/debugging tools

None of these require changing the fundamental `BarModel` abstraction.

---

# Summary

BarSystem separates a resource bar into four fundamental concepts:

```text
State       → BarModel
Rules       → IBarBehavior
Rendering   → IBarView
Persistence → IBarStateRepository
```

Specific bars are compositions of those concepts:

```text
Health
    = BarModel
    + optional RegenBehavior
    + View
    + Persistence

Stamina
    = BarModel
    + DrainOnUseBehavior
    + conditional RegenBehavior
    + View
    + Persistence

Corruption
    = BarModel
    + optional GrowthOverTimeBehavior
    + View
    + Persistence
```

This keeps each responsibility isolated while allowing new bar types and behaviors to be created by composition rather than by modifying the existing Core.
