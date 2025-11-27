# CLAUDE.md - AI Assistant Guide for 2D Puzzle Game

## Project Overview

**Project Name:** 2D Puzzle Game Assignment
**Game Type:** Match-3 Bubble Tea Topping Puzzle with Time Attack
**Unity Version:** Unity 6000.4.0a4 (Unity 6 Alpha)
**Platform:** Mobile/Touch-enabled
**Theme:** Bubble Tea Shop (Feng Cha inspired)

This is a polished 2D physics-based puzzle game where players connect matching bubble tea toppings by dragging their finger. The game features a 60-second time limit, combo systems, special toppings, and a swap skill mechanic.

---

## Repository Structure

```
2D-Puzzle-Game-Assignment/
├── Assets/
│   ├── Script/              # All C# game scripts (12 files)
│   │   ├── Game Manager.cs  # Central game coordinator
│   │   ├── Logic.cs         # Chain validation & clearing
│   │   ├── Controller.cs    # Input handling & visual feedback
│   │   ├── Spawner.cs       # Topping spawn system
│   │   ├── Score.cs         # Score calculation & display
│   │   ├── Topping.cs       # Base topping behavior
│   │   ├── Bomb.cs          # Special explosive topping
│   │   ├── Dual Connect.cs  # Wildcard topping
│   │   ├── Pop Up.cs        # World-space popup system
│   │   ├── Swap Skill.cs    # Special ability system
│   │   ├── Straw.cs         # Rotating straw physics
│   │   └── Straw Tip.cs     # Straw interaction zone
│   ├── Prefab/              # Reusable game objects (11 prefabs)
│   │   ├── [Topping].prefab # Agar, Boba, Coffee, Coconut, etc.
│   │   ├── Chain.prefab     # Line renderer for connections
│   │   ├── Dot.prefab       # Connection indicators
│   │   └── Pointer.prefab   # Finger cursor visual
│   ├── Sprite/              # 2D graphics (40+ files)
│   │   ├── Toppings/        # Visual assets for each topping type
│   │   ├── UI/              # Buttons, popups, icons
│   │   └── Backgrounds/     # Scene backgrounds
│   ├── Audio/               # Sound effects and music (9 files)
│   │   ├── Au⊹0 - Re_memorize.wav  # Menu BGM
│   │   ├── LucaProject - Queen of Heart.wav  # Gameplay BGM
│   │   └── [Various SFX].wav
│   ├── Thrid-Party/         # External assets (NOTE: typo in name)
│   │   ├── FPS Gaming Font/
│   │   ├── Thaleah_PixelFont/
│   │   └── Hit Impact Effects/
│   ├── Puzzle Scene.unity   # Main (and only) game scene
│   ├── *.anim/.controller   # Animation assets
│   └── *.physicsMaterial2D  # Physics materials
├── Packages/               # Unity package manifest
├── ProjectSettings/        # Unity project configuration
├── .gitignore             # Version control exclusions
└── AGENTS.md              # Unity Code Assist metadata
```

---

## Codebase Architecture

### Design Patterns

1. **Singleton Pattern**
   - `ScoreManager.Instance`, `PopUp.Instance`, `Spawner.Instance`
   - Provides global access to managers
   - Used sparingly for truly global systems

2. **Component-Based Architecture**
   - All scripts inherit from `MonoBehaviour`
   - Loose coupling via `GetComponent<>()`
   - Unity's standard component pattern

3. **Observer/Event Pattern**
   - Example: `Bomb.Exploded` static event
   - `Logic` subscribes/unsubscribes in `OnEnable/OnDisable`
   - Decouples special topping behavior

4. **Object Pooling (Implicit)**
   - `PopUp` reuses single GameObject
   - Changes sprites instead of instantiate/destroy
   - Performance optimization for frequent UI

5. **Coroutine-Based Animation**
   - Extensive use of `IEnumerator` for timing
   - Non-blocking animations and sequences
   - Used throughout for smooth transitions

### Core System Responsibilities

| System | Script | Primary Responsibilities |
|--------|--------|-------------------------|
| **Game Management** | `Game Manager.cs` | Game state, timer, UI orchestration, audio transitions |
| **Scoring** | `Score.cs` | Score calculation, combo multipliers, UI updates |
| **Input Handling** | `Controller.cs` | Touch/mouse input, line rendering, camera control |
| **Game Logic** | `Logic.cs` | Chain validation, matching rules, clearing, refill coordination |
| **Spawning** | `Spawner.cs` | Topping creation, ladle animation, weighted random selection |
| **Base Topping** | `Topping.cs` | Removal effects, sticky behavior, audio feedback |
| **Special: Bomb** | `Bomb.cs` | AOE clearing, explosion effects, event broadcasting |
| **Special: Wildcard** | `Dual Connect.cs` | Universal chain continuation, rainbow animation |
| **UI Feedback** | `Pop Up.cs` | Countdown, combo popups, world-space messages |
| **Special Ability** | `Swap Skill.cs` | Type swapping, cooldown management, charge system |
| **Physics** | `Straw.cs` + `Straw Tip.cs` | Rotating physics, wind effects, impulse application |

### Data Flow

```
User Input (Controller.cs)
    ↓
Logic Validation (Logic.cs)
    ↓
Chain Clearing (Topping.cs, Bomb.cs, etc.)
    ↓
Score Calculation (Score.cs)
    ↓
UI Updates (Game Manager.cs, Pop Up.cs)
    ↓
Refill Request (Spawner.cs)
```

---

## Coding Conventions

### File Naming
- **Scripts:** PascalCase with spaces (e.g., `Game Manager.cs`, `Dual Connect.cs`)
- **Prefabs:** PascalCase (e.g., `Boba.prefab`, `Pointer.prefab`)
- **Sprites:** PascalCase with spaces (e.g., `Plastic cup.png`)
- **Audio:** Mixed case (e.g., `Chain.wav`, `XP Orb.wav`)

### Code Naming Standards

```csharp
// Classes: PascalCase
public class GameManager : MonoBehaviour

// Public fields: camelCase with [Tooltip]
[Tooltip("Duration of the main gameplay timer in seconds.")]
public float gameplayDuration = 60f;

// Private fields: camelCase
private float remainingTime;

// Methods: PascalCase
public void StartGame()

// Coroutines: PascalCase with "Coroutine" suffix
private IEnumerator ShowPopupCoroutine()

// Constants: SCREAMING_SNAKE_CASE (if used)
private const float MAX_TIMER_EXTENSION = 1.5f;

// Unity callbacks: PascalCase (standard)
void Awake()
void Start()
void Update()
void OnTriggerEnter2D(Collider2D other)
```

### Code Style Guidelines

1. **Always Use Tooltips**
   ```csharp
   [Tooltip("Clear, descriptive explanation of this field's purpose.")]
   public GameObject targetObject;
   ```

2. **Group Related Fields with Headers**
   ```csharp
   [Header("Gameplay Settings")]
   public Controller controller;
   public Spawner spawner;

   [Header("Audio Settings")]
   public AudioSource menuBGM;
   public AudioClip timeExtensionSound;
   ```

3. **Use Range Attributes for Numeric Fields**
   ```csharp
   [Range(0, 5)]
   public int timerDecimals = 3;
   ```

4. **Defensive Programming**
   - Always null-check before accessing components
   - Validate input parameters
   - Use try-catch for critical sections
   - Log errors with `Debug.LogError()` or `Debug.LogWarning()`

5. **Coroutine Patterns**
   ```csharp
   private IEnumerator MyAnimationCoroutine()
   {
       // Animation logic
       yield return new WaitForSeconds(duration);
       // Completion logic
   }
   ```

6. **Component References**
   - Cache component references in `Awake()` or `Start()`
   - Use `GetComponent<>()` sparingly (not in `Update()`)
   - Prefer inspector-assigned references when possible

7. **Comments**
   - Clear inline comments explaining "why" not "what"
   - Summary comments for complex logic sections
   - XML documentation not required but appreciated

---

## Unity-Specific Conventions

### Tags and Layers

**Tags (Topping Types):**
- Agar
- Boba
- Taro
- Grass
- Ube
- Coffee
- Coconut

**Layers:**
- Default (0)
- Water (4)
- UI (5)
- Toppings (20)

**Important:** When creating new topping types, you MUST:
1. Create a new tag in TagManager
2. Assign the "Toppings" layer
3. Create a matching prefab
4. Add to spawner's weighted spawn list

### Physics Materials

- `Bouncy.physicsMaterial2D` - High restitution for bouncing
- `Abrasive.physicsMaterial2D` - High friction for grabbing

### Animation System

- Use Animator Controllers for state-based animations
- Animation clips stored at root Assets/ level
- Coordinate animations with coroutines for timing

---

## Common Development Tasks

### Adding a New Topping Type

1. **Create the sprite:** Add to `Assets/Sprite/`
2. **Create prefab:**
   - Duplicate existing topping prefab
   - Replace sprite
   - Update name
3. **Create tag:** ProjectSettings → Tags and Layers
4. **Add to spawner:**
   ```csharp
   // In Spawner.cs, add to spawnWeights array
   [System.Serializable]
   public class SpawnWeight {
       public GameObject toppingPrefab;
       public float weight = 1f;
   }
   ```
5. **Test matching:** Verify 3+ match and clear correctly

### Modifying Game Balance

**Key parameters to adjust:**

```csharp
// Game Manager.cs
public float gameplayDuration = 60f;  // Game length

// Logic.cs
public int timeExtensionChainThreshold = 9;  // Combo for time extension
public float timeExtensionAmount = 5f;       // Seconds added

// Score.cs
basePointsPerTopping = 10;    // Base score
lengthBonusPerUnit = 5;       // Physical distance bonus
multiplierThreshold1 = 6;     // 2x multiplier at 6+ chain
multiplierThreshold2 = 9;     // 3x multiplier at 9+ chain
```

### Adding New Sound Effects

1. **Import audio:** Place in `Assets/Audio/`
2. **Reference in script:**
   ```csharp
   [Header("Audio Settings")]
   [Tooltip("Sound to play when X happens.")]
   public AudioClip newSound;
   [Tooltip("Volume for new sound.")]
   [Range(0f, 1f)]
   public float newSoundVolume = 0.8f;
   ```
3. **Play sound:**
   ```csharp
   if (newSound != null)
   {
       AudioSource.PlayClipAtPoint(newSound, position, newSoundVolume);
   }
   ```

### Debugging Common Issues

**"Toppings not clearing":**
- Check tag spelling in Inspector
- Verify minimum 3 toppings
- Check for blocking toppings in chain
- Enable `Debug.Log` in `Logic.cs` → `ClearStoredCoroutine()`

**"Physics behaving oddly":**
- Verify correct physics material assigned
- Check layer collision matrix in ProjectSettings
- Ensure Rigidbody2D settings are correct
- Check Time.timeScale hasn't been modified

**"Audio not playing":**
- Verify AudioClip is assigned in Inspector
- Check AudioListener exists in scene (usually on Camera)
- Verify volume settings aren't zero
- Check audio mixer settings if using one

**"UI not responding":**
- Verify EventSystem exists in scene
- Check Canvas rendering mode
- Verify button has GraphicRaycaster
- Check if controller is disabled during interaction

---

## Game Mechanics Reference

### Matching Rules

1. **Minimum 3 toppings** of the same tag
2. **No blocking toppings** in the path between connected items
3. **Special toppings** have unique rules:
   - **Bomb:** Cannot start a chain, clears 2-unit radius on match
   - **Dual Connect:** Acts as wildcard, cannot start a chain

### Scoring Formula

```
baseScore = basePointsPerTopping * chainCount
lengthScore = physicalChainLength * lengthBonusPerUnit
multiplier = 1x (3-5), 2x (6-8), or 3x (9+)
finalScore = (baseScore + lengthScore) * multiplier
```

### Time Extension System

- **Trigger:** 9+ topping combo
- **Bonus:** +5 seconds (configurable)
- **Maximum:** 1.5x original duration (90 seconds default)
- **Visual feedback:** Cyan timer flash
- **Audio:** Special sound effect
- **Extra points:** Bonus awarded

### Swap Skill

- **Charges:** 3 per game
- **Cooldown:** 10 seconds
- **Behavior:**
  1. Select first topping (stored)
  2. Select second topping (triggers swap)
  3. All instances of second type → first type
  4. Selected first topping → second type

---

## Testing Guidelines

### Manual Testing Checklist

- [ ] Basic matching (3, 4, 5+ toppings)
- [ ] Bomb explosion clears nearby toppings
- [ ] Dual Connect acts as wildcard
- [ ] Swap skill works with all topping types
- [ ] Timer counts down correctly
- [ ] Time extension triggers at 9+ combo
- [ ] Score calculation is accurate
- [ ] Audio plays correctly (BGM + SFX)
- [ ] UI transitions smoothly
- [ ] Restart functionality works
- [ ] Touch input is responsive
- [ ] Physics feels natural (no clipping)

### Common Edge Cases

1. **Bomb + Dual Connect chain:** Should work
2. **Maximum timer (90s):** Should cap correctly
3. **Empty spawn area:** Spawner maintains minimum 25 toppings
4. **Rapid clearing:** Refill system should handle cascades
5. **Straw wind during clearing:** Physics should remain stable

---

## Performance Considerations

### Current Optimizations

1. **Object Pooling:** PopUp system reuses GameObjects
2. **Component Caching:** References cached in Awake/Start
3. **Coroutine Usage:** Non-blocking animations
4. **Event System:** Decoupled bomb explosions
5. **LineRenderer:** Single instance for chain visualization

### Performance Guidelines

- **Avoid** `FindObjectOfType()` in Update loops
- **Cache** component references
- **Use** coroutines instead of Update() for timing
- **Limit** particle system emission rates
- **Optimize** physics by disabling unnecessary rigidbodies
- **Pool** frequently instantiated objects

---

## Known Issues & Quirks

### File System

- **Typo:** `Thrid-Party` folder should be `Third-Party` (not fixed to preserve paths)
- **Script names:** Use spaces (e.g., `Game Manager.cs`) - Unity handles this fine

### Code Patterns

- **Reflection in Straw.cs:** Uses reflection to read StrawTipPush duration - acceptable for once-per-initialization call
- **Magic numbers:** Some hardcoded values exist (0.8f, 2f, etc.) - can be extracted to constants if needed
- **Large methods:** Some methods (e.g., `ClearStoredCoroutine`) are 100+ lines - functional but could be refactored

### Unity Version

- **Alpha build:** Unity 6000.4.0a4 is an alpha release
- **API changes:** Some APIs may change in stable Unity 6
- **Stability:** Expect occasional editor crashes or quirks

---

## AI Assistant Guidelines

### When Making Changes

1. **Read before modifying:** Always read the full file before making changes
2. **Maintain conventions:** Follow existing naming and style patterns
3. **Add tooltips:** Every new public field needs a [Tooltip]
4. **Test assumptions:** Don't assume - verify component references exist
5. **Preserve formatting:** Match indentation and spacing style
6. **Update comments:** Keep comments in sync with code changes
7. **Consider dependencies:** Check what other scripts reference changed code

### When Adding Features

1. **Analyze existing patterns:** Study how similar features are implemented
2. **Follow architecture:** Place code in the appropriate system
3. **Use existing systems:** Leverage ScoreManager, PopUp, etc. rather than duplicating
4. **Add configuration:** Make new features configurable via inspector
5. **Include audio/visual feedback:** Match the game's polish level
6. **Document changes:** Add clear comments and tooltips
7. **Test thoroughly:** Verify new features work with existing mechanics

### When Debugging

1. **Enable Debug.Log statements:** Many exist but are commented out
2. **Check Inspector assignments:** Many issues are missing references
3. **Verify Unity version compatibility:** This project uses Unity 6 alpha
4. **Test in Play mode:** Many issues only appear at runtime
5. **Check console:** Unity console shows errors, warnings, and logs
6. **Review git changes:** Use `git status` and `git diff` to see what changed

### When Refactoring

1. **Don't over-engineer:** Keep solutions simple and focused
2. **Preserve functionality:** Ensure behavior remains unchanged
3. **Test after each change:** Incremental testing prevents cascading issues
4. **Maintain backwards compatibility:** Don't break existing prefab references
5. **Document reasons:** Explain why refactoring was necessary
6. **Keep commits atomic:** One logical change per commit

### Communication Style

- **Be concise:** Explain changes clearly but briefly
- **Reference file paths:** Use format `filename.cs:line_number`
- **Show code context:** Include surrounding code when explaining changes
- **List changes:** Use bullet points for multiple modifications
- **Warn about impacts:** Note if changes affect other systems

---

## Quick Reference

### File Locations

| Asset Type | Path |
|------------|------|
| Scripts | `Assets/Script/` |
| Prefabs | `Assets/Prefab/` |
| Sprites | `Assets/Sprite/` |
| Audio | `Assets/Audio/` |
| Scene | `Assets/Puzzle Scene.unity` |
| Fonts | `Assets/Thrid-Party/` |
| Packages | `Packages/manifest.json` |
| Settings | `ProjectSettings/` |

### Important Unity Packages

```json
{
  "com.unity.feature.2d": "2.0.1",
  "com.unity.ugui": "2.0.0",
  "com.unity.inputsystem": "1.15.0",
  "com.unity.timeline": "1.8.9",
  "com.unity.visualscripting": "1.9.9"
}
```

### Key Inspector References

Most scripts require Inspector assignments. Common patterns:

- **Game Manager:** References Controller, Spawner, PopUp, UI panels, audio sources
- **Logic:** References Spawner, GameManager, popup prefabs
- **Controller:** References LineRenderer, dot/pointer prefabs
- **Spawner:** References topping prefabs array with weights

### Contact & Documentation

- **Unity Version:** 6000.4.0a4 (Alpha)
- **Git Repository:** Current working directory
- **Related Files:**
  - `.github/copilot-instructions.md` - GitHub Copilot guidance
  - `AGENTS.md` - Unity Code Assist metadata
  - This file (`CLAUDE.md`) - Comprehensive AI assistant guide

---

## Version History

- **Initial Version:** Created during comprehensive codebase analysis
- **Last Updated:** 2025-11-27
- **Unity Version at Creation:** Unity 6000.4.0a4

---

## Final Notes for AI Assistants

This is a **well-structured, production-quality** Unity project with:

- ✅ Clean component-based architecture
- ✅ Comprehensive tooltips and documentation
- ✅ Professional asset organization
- ✅ Modern Unity practices (coroutines, events, new Input System)
- ✅ Rich gameplay mechanics with polish
- ✅ Defensive programming with error handling

When working on this codebase:

1. **Respect the existing quality level** - maintain high standards
2. **Follow the established patterns** - consistency is key
3. **Test thoroughly** - this game is polished, keep it that way
4. **Document your changes** - future developers (and AIs) will thank you
5. **Ask before major refactors** - the current architecture works well

Remember: This project uses Unity 6 alpha, so some APIs and behaviors may differ from stable Unity versions. Always test changes in Play mode.

---

**End of CLAUDE.md**
