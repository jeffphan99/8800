# Space Ape Logistics Inc.

## Project Structure
All scripts are in: `Assets/Scenes/Scripts/`

---

## CORE GAMEPLAY

**GameManager.cs**
- Controls round timer, terminal breaking, monster spawning
- Main game loop and win/lose conditions
- Finds and tracks all terminals, monsters, doors
- Scales difficulty per round (break interval, monster count, speed)

**Terminal.cs**
- Handles terminal breaking/repairing
- WASD typing minigame for repairs
- Requires repair tool equipped to start repair
- Spawns fire particles when broken, stops on repair
- Role-restricted: only the assigned role can repair a given terminal

**PlayerHealth.cs**
- Player HP system with networked health variable
- Damage from monsters, death triggers spectator mode
- Owner setup (camera, UI, spawn warp) vs. non-owner component disabling

**PlayerRole.cs**
- Enum defining three player roles: Silverback, Neurochimp, WrenchMonkey
- Maps roles to display names and character skin indices

**PlayerRoleController.cs**
- Networked component that assigns and syncs the correct character skin to each player based on their role

---

## WEAPONS SYSTEM

**WeaponBase.cs** (Abstract)
- Base class for all weapons
- Defines PrimaryAction(), SecondaryAction(), UpdateStatusUI()
- Broadcasts shoot animation to other clients via WeaponSwitcher

**WeaponSwitcher.cs** (NetworkBehaviour)
- Switches between 4 weapon slots (1-4 keys), owner-only input
- Syncs current weapon index via NetworkVariable for all clients
- Broadcasts swap and shoot animations to non-owner clients via RPCs

**GunWeapon.cs** (FreezeGunWeapon)
- Shoots freeze beam at monsters
- Creates ice blocks around frozen enemies
- Ammo system with reload

**ForceGunWeapon.cs**
- Hold-to-charge weapon that launches a blast pushing monsters and players backward
- Push force scales with charge time; networked via ForceGunNetwork

**RepairWeapon.cs**
- Plays animation during terminal minigame
- Auto-detects when minigame is active
- Particle effects and sounds

**Flashlight.cs**
- Toggle on/off light
- Battery drain/recharge system
- Slows or suppresses light-sensitive monsters

**JukeboxWeapon.cs**
- Battery-powered device that plays music to taunt nearby monsters
- Drains battery over time; monsters prioritise the jukebox over players while active

**BananaWeapon.cs**
- Consumable that heals player to full
- Drops a banana peel on the ground when eaten

---

## CONSUMABLES

**BananaPeel.cs**
- Trips monsters (4 sec sleep)
- Slows player movement (2 sec)
- Arming delay prevents self-trip

---

## NETWORK HELPERS

**ClientNetworkTransform.cs**
- Owner-authoritative NetworkTransform so CharacterController clients control their own position

**FreezeGunNetwork.cs**
- Freezes a monster server-side and spawns ice block visuals on all clients

**ForceGunNetwork.cs**
- Broadcasts force gun push force to all affected clients

**NetworkManagerInitializer.cs**
- Activates the NetworkManager GameObject at scene startup if it is inactive

**PlayerAnimatorSync.cs**
- Syncs the player's Run animator bool across clients via an owner-writable NetworkVariable

**PlayerNoiseEmitter.cs**
- Creates a noise trigger sphere around sprinting players server-side to alert nearby monsters

---

## EFFECTS

**IceBlock.cs**
- Spawned by freeze gun around monsters
- Freezes monster animator and AI
- Shakes before shattering, re-enables monster on destroy

---

## MONSTER AI

**MonsterAI.cs** (Base)
- Patrol, chase, attack states
- Noise detection, sleep mechanic
- Light breaking (walks near lights and breaks them)
- Networked active state: only released when terminal repair fails

**MonsterListener.cs**
- Only detects players through noise and jukebox taunts — never visual detection

**MonsterStalker.cs**
- Vision cone + line-of-sight detection
- Enrages (speeds up) near bright lights or when hit by flashlight
- Calmed and slowed by nearby jukebox music

**MonsterWatcher.cs**
- Freezes when directly watched by a player; speeds up when unwatched
- Flashlight slows it down (does not stop it)
- Proximity aggro: attacks when player gets too close

---

## LIGHTING SYSTEM

**RoomLight.cs**
- Individual light control (on/off/broken)
- Can be toggled or broken by monsters

**LightSwitch.cs**
- Controls multiple lights in a zone
- Toggle lever animation with indicator light

**GlobalLightingController.cs**
- Global brightness control via material darkening (required for Synty emission materials)

---

## UI & MINIMAP

**MinimapManager.cs**
- Follows player overhead
- Tracks terminals (green=working, red=broken) and active monsters
- Creates icons automatically at runtime

**MinimapIcon.cs**
- Attaches icons to objects for the minimap with customizable colours and offsets

**ToolbarUI.cs** (ToolbarUI_Simple)
- Shows 4 weapon slots at the bottom of the screen
- Highlights selected weapon, shows names and key bindings

---

## WORLD INTERACTION

**Door.cs**
- Opens/closes with animation
- Configurable open position and speed
- Auto-wires with Interactable; monster cell doors opened on containment breach

**Interactable.cs**
- Generic interaction system (Press E)
- Shows UI prompt when in range
- Used by doors, terminals, etc.

---

## EFFECTS & POLISH

**StressController.cs**
- Drives a post-processing volume based on nearest active monster distance
- Increases vignette/distortion the closer a monster gets
