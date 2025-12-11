# Space Ape Logistics Inc.

## Project Structure
All scripts are in: `Assets/Scenes/Scripts/`

---

## CORE GAMEPLAY

**GameManager.cs**
- Controls round timer, terminal breaking, monster spawning
- Main game loop and win/lose conditions
- Finds and tracks all terminals, monsters, doors

**Terminal.cs**
- Handles terminal breaking/repairing
- WASD typing minigame for repairs
- Requires repair tool equipped to start repair
- Spawns fire particles when broken, stops on repair

**PlayerHealth.cs**
- Player HP system
- Damage from monsters
- Death triggers GameManager.OnPlayerDeath()

---

## WEAPONS SYSTEM

**WeaponBase.cs** (Abstract)
- Base class for all weapons
- Defines PrimaryAction(), SecondaryAction(), UpdateStatusUI()

**WeaponSwitcher.cs**
- Switches between 4 weapon slots (1-4 keys)
- Manages weapon activation/deactivation
- Shares UI references between weapons

**GunWeapon.cs** (FreezeGunWeapon)
- Shoots freeze beam at monsters
- Creates ice blocks around frozen enemies
- Ammo system with reload

**RepairWeapon.cs** (RepairToolWeapon)
- Plays animation during terminal minigame
- Auto-detects when minigame is active
- Particle effects and sounds

**Flashlight.cs** (FlashlightWeapon)
- Toggle on/off light
- Battery drain/recharge system
- Slows down light-sensitive monsters

**BananaWeapon.cs**
- Consumable that heals player to full
- Drops banana peel when eaten

---

## CONSUMABLES

**BananaPeel.cs**
- Trips monsters (4 sec sleep)
- Slows player movement (2 sec)
- Arming delay prevents self-trip

---

## EFFECTS

**IceBlock.cs**
- Spawned by freeze gun around monsters
- Freezes monster animator and AI
- Shakes before shattering
- Re-enables monster on destroy

---

## MONSTER AI

**MonsterAI.cs** (Base)
- Patrol, chase, attack states
- Noise detection
- Sleep mechanic
- Light breaking (walks near lights, breaks them)
- Flashlight interaction

**MonsterStalker.cs**
- Vision cone detection (sees player in front)
- Enrages near bright lights (speeds up)
- Line-of-sight required

**MonsterWatcher.cs**
- Proximity detection (gets close, aggros)
- Slows down when shone with light

---

## LIGHTING SYSTEM

**RoomLight.cs**
- Individual light control (on/off/broken)
- Can be toggled or broken by monsters

**LightSwitch.cs**
- Controls multiple lights in a zone
- Auto-finds nearby lights
- Toggle lever animation
- Indicator light shows on/off state

**GlobalLightingController.cs** (GlobalMaterialDarkener)
- Global brightness control
- Darkens all materials in scene, needed to edit emission materials from synty

---

## UI & MINIMAP

**MinimapManager.cs**
- Follows player overhead
- Tracks terminals (green=working, red=broken)
- Tracks monsters (only when active)
- Creates icons automatically

**MinimapIcon.cs**
- Attaches icons to objects for minimap
- Customizable colors and offsets

**ToolbarUI.cs** (ToolbarUI_Simple)
- Shows 4 weapon slots at bottom
- Highlights selected weapon
- Updates weapon names and keys (1-4)

---

## WORLD INTERACTION

**Door.cs**
- Opens/closes with animation
- Configurable open position and speed
- Auto-wires with Interactable component

**Interactable.cs**
- Generic interaction system (Press E)
- Shows UI prompt when in range
- Used by doors, terminals, etc.

---

## EFFECTS & POLISH

**StressController.cs**
- Post-processing volume based on monster distance
- Increases stress effect when monsters are close
