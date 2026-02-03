# FsJump 3D Platformer - Development Plan

## Project Overview

Build a 3D platformer using Mibo's Elmish architecture, rendering Prototype.tmj level with PlatformerKit 3D models (scaled to 64x64x64 world units per tile), implementing player physics with collision detection, and supporting interactive objects (dangers, moving platforms, goals).

**Technical Stack:**
- **Framework:** MonoGame 3.8.*
- **Architecture:** Mibo (Elmish MVU pattern)
- **JSON Parsing:** JDeck
- **Language:** F# .NET 10
- **Cell Size:** 64x64x64 world units per tile
- **Assets:** PlatformerKit (153 FBX models) + colormap.png texture

**Design Decisions:**
1. Player character from spawn trigger tile (character-oobi.fbx)
2. Side-scrolling 2.5D view with fixed Z-axis camera
3. Ambient + directional lighting only (no full PBR)
4. 3 lives, respawn on death, game over overlay at 0 lives
5. Keyboard-only input (arrow keys)

---

## Slice 1: Load and Render Level
**Goal:** Display entire Prototype.tmj level in 3D with proper camera and lighting

### Tasks

**1.1 Create Level Data Module** (`src/FsJump.Core/Level.fs`)
- Define F# types matching Tiled map JSON structure
- Create JDeck decoder for `TiledMap`
- Implement `TileId → FBX Path` mapping function:
  ```fsharp
  // Example: tile 64 (character-oobi.png) → "PlatformerKit/character-oobi"
  let tileToFbxPath (tileId: int, tileset: Tileset) : string option
  ```

**1.2 Create Asset Loader Module** (`src/FsJump.Core/Assets.fs`)
- Function to load all required 3D models via Mibo's `Assets.model`
- Cache loaded models by tile ID
- Load colormap.png texture

**1.3 Update Game.fs - Level Loading**
- Implement `init` to load `Prototype.tmj` on startup
- Parse Base layer tiles into static entities
- Parse Objects layer entities (dangers, platforms, goal)
- Extract spawn point from Triggers layer

**1.4 Implement 2.5D Camera Setup**
- Fixed Z-axis position (e.g., Z = -500)
- Camera follows player's X/Y with smooth damping
- Perspective projection with fixed FOV

**1.5 Implement Basic Rendering**
- Setup PipelineRenderer with ambient + directional light
- Render all static tile entities at 64x64x64 scale
- Use colormap.png for model textures
- No player or interactivity yet

### Acceptance Criteria
- [ ] Game starts and loads Prototype.tmj
- [ ] All tiles from Base layer render as 3D blocks at correct positions
- [ ] Camera shows full level at fixed Z-axis
- [ ] Lighting is visible (ambient + directional)
- [ ] Colormap.png texture is applied to models

---

## Slice 2: Player Movement & Physics
**Goal:** Control a player character with arrow keys, including jumping and ground collision

### Tasks

**2.1 Add Player Entity**
- Load character model at spawn trigger position (tile 64 = character-oobi.fbx)
- Add player to entities list with type `Player`
- Track player state in Model: position, velocity, grounded flag

**2.2 Implement Input Mapping**
- Arrow keys: Left/Right/Up (jump)
- Mibo InputMap configuration
- Input subscription in `subscribe` function

**2.3 Implement Basic Physics**
- Gravity constant (e.g., -900 units/s²)
- Apply velocity to position on each Tick
- Ground collision detection using AABB with tile entities

**2.4 Implement Jump Logic**
- Jump only when grounded
- Apply upward velocity on jump input
- Set grounded flag based on collision results

**2.5 Update Camera Follow**
- Camera position lerps toward player X/Y
- Maintains fixed Z-axis offset

### Acceptance Criteria
- [ ] Player character appears at spawn position
- [ ] Arrow keys move player left/right
- [ ] Up arrow makes player jump (when on ground)
- [ ] Player falls with gravity when not grounded
- [ ] Player lands and stops on ground tiles
- [ ] Camera smoothly follows player
- [ ] Player cannot fall through ground tiles

---

## Slice 3: Interactive Objects
**Goal:** Handle moving platforms, rotating dangers, and static hazards

### Tasks

**3.1 Parse Object Types from Tiled Data**
- Detect `type="Danger"` → create danger entity (spikes)
- Detect `type="Threadmill"` → create moving platform (width=128)
- Detect `type="Objective"` → create goal entity (flowers-tall)
- Parse `properties`: `IsRotating=true` for rotating dangers

**3.2 Implement Moving Platform Logic**
- Store platform path (start/end positions)
- Animate position using sine wave oscillation
- Player stands on platform (inherits velocity when grounded on it)
- Threadmill moves horizontally (based on width)

**3.3 Implement Rotating Danger Logic**
- Rotate danger entity around Y-axis
- Speed: 180°/second (π radians/sec)
- Kills player on collision

**3.4 Implement Static Danger Logic**
- Spikes (trap-spikes.png, trap-spikes-large.png)
- Saw (saw.png)
- Static position, kills player on contact

**3.5 Collision with Player**
- AABB collision detection
- Danger entities kill player immediately
- Moving platforms support player standing on them

### Acceptance Criteria
- [ ] Threadmill platform moves back and forth
- [ ] Player can stand on moving platform
- [ ] Player moves with platform
- [ ] Rotating spike danger rotates continuously
- [ ] Player dies when touching any danger
- [ ] Static dangers (spikes, saw) kill player on contact

---

## Slice 4: Game Flow & Lives System
**Goal:** Implement lives, death handling, win condition, and restart

### Tasks

**4.1 Add Lives System to Model**
- Track lives: start with 3
- Track game state: `Playing`, `Dead`, `GameOver`, `Victory`

**4.2 Implement Death Logic**
- On danger collision: decrement lives, respawn at spawn
- On falling off level: decrement lives, respawn at spawn
- Respawn: reset position to spawn point, zero velocity

**4.3 Implement Game Over State**
- When lives reach 0: show game over overlay
- Overlay displays "Game Over" and "Press R to Restart"
- Pause game updates

**4.4 Implement Win Condition**
- On goal collision: set state to `Victory`
- Show "Level Complete!" overlay
- "Press R to Restart" to replay level

**4.5 Implement Restart Function**
- Reset player to spawn point
- Reset lives to 3
- Reset all entity positions (moving platforms)
- Clear overlays, return to `Playing` state

**4.6 Handle Restart Input**
- R key triggers restart in `GameOver` or `Victory` states
- No effect in `Playing` state

### Acceptance Criteria
- [ ] Player starts with 3 lives
- [ ] Dying (danger or fall) reduces lives by 1
- [ ] Player respawns at spawn point after death
- [ ] At 0 lives: game over overlay appears
- [ ] Pressing R restarts level (full reset)
- [ ] Touching goal triggers victory state
- [ ] Victory overlay shows level complete message
- [ ] R key works in victory state to restart

---

## Slice 5: Polish & Decorations
**Goal:** Add decorations layer, refine gameplay feel, and final testing

### Tasks

**5.1 Render Decorations Layer**
- Parse Decorations tile layer from Prototype.tmj
- Render decoration entities (grass, flowers, trees, etc.)
- Use same 64x64x64 scale
- No collision (purely visual)

**5.2 Visual Polish**
- Add subtle animations (e.g., coin sparkle if added)
- Smooth camera damping (reduce jitter)
- Fade out/fade in transitions for death/respawn

**5.3 UI Overlays**
- Render lives count (hearts icon × 3)
- Game over screen with red background
- Victory screen with gold background
- Using 2D renderer overlay on top of 3D scene

**5.4 Performance Optimization**
- Frustum culling (only render visible tiles)
- Batch similar draw calls
- Profile frame rate

**5.5 Final Testing**
- Test all death scenarios (spikes, saw, rotating danger, falling)
- Test moving platform edge cases (jumping on/off)
- Test restart from both game over and victory
- Verify no memory leaks from asset loading

### Acceptance Criteria
- [ ] Decorations layer renders correctly
- [ ] Lives count visible in UI
- [ ] Game over screen looks polished
- [ ] Victory screen looks polished
- [ ] Game runs at 60 FPS
- [ ] No memory leaks after multiple restarts
- [ ] All mechanics work as expected

---

## File Structure After Implementation

```
src/FsJump.Core/
├── Game.fs              # Main Elmish loop, init/update/view/program
├── Level.fs            # Tiled map types + JDeck decoder
├── Assets.fs           # Model loading + tile→FBX mapping
├── Physics.fs          # Collision detection, gravity, movement
└── Types.fs            # Shared types (Entity, Player, GameState, Msg)
```

---

## Execution Order

1. **Slice 1** → Foundation: Load level, render scene
2. **Slice 2** → Core gameplay: Move and jump
3. **Slice 3** → Hazards: Dangers and platforms
4. **Slice 4** → Game flow: Lives, win/lose, restart
5. **Slice 5** → Polish: Visuals and final touches

Each slice produces a playable increment of game, allowing for testing and feedback between slices.

---

## Asset Reference

### Tileset → FBX Mapping
Tile image names in `PlatformerTiles/` match FBX file names in `PlatformerKit/`:
- Example: `character-oobi.png` → `PlatformerKit/character-oobi.fbx`
- Example: `block-grass-corner-low.png` → `PlatformerKit/block-grass-corner-low.fbx`

### Spawn Point
- Located in Triggers layer
- Uses tile 64 (character-oobi.png) for reference
- Player model: `character-oobi.fbx`

### Level Specifications (Prototype.tmj)
- **Dimensions:** 32 tiles × 15 tiles
- **Tile Size:** 64px × 64px
- **World Size:** 2048 units × 960 units
- **Cell Size:** 64x64x64 world units per tile

### Layers
1. **Base** (tilelayer id=4): Ground/platform tiles
2. **Objects** (objectgroup id=6):
   - Danger objects (spikes, saw, rotating dangers)
   - Threadmill (moving platform, width=128)
   - Goal objective (flowers-tall)
3. **Decorations** (tilelayer id=5): Visual decorations
4. **Triggers** (objectgroup id=7): Player spawn point

---

## Build Commands

```bash
# Build entire solution
dotnet build

# Run Desktop version
dotnet run --project src/FsJump.Desktop

# Format all F# files
dotnet fantomas .

# Build MonoGame content
dotnet mgcb src/FsJump.Core/Content/Content.mgcb

# Clean and restore
dotnet clean && dotnet restore
```
