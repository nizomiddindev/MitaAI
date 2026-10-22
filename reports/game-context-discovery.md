# Game & Story Context Discovery Report (Read-Only Investigation)

**Date**: 2026-10-21

**Scope**: Read-only inspection of `src/MitaAI.Plugin`, `MiSide_Dev/BepInEx/interop/*.dll` (`Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `UnityEngine.AnimationModule.dll`), `MiSide_Dev/MiSideFull_Data/globalgamemanagers`, and `MiSide_Dev/BepInEx/LogOutput*.log`.
**Runtime Modifications**: None. No plugin, launcher, voice, test, configuration, shared-documentation, or game/save files were modified.

---

## Executive Summary & Verification Legend

Every signal below is categorized into three explicit tiers so future implementation does not confuse compiled metadata with runtime truth:

1. **Verified in Runtime & Code**: Already called by `MitaAI.Plugin` (`v0.5.2`/`v0.6.1`) or directly observed in `MiSide_Dev/BepInEx/LogOutput*.log`.
2. **Verified in Interop Metadata (Unverified at Runtime)**: Exact IL2CPP wrapper types, properties, methods, and fields confirmed in `MiSide_Dev/BepInEx/interop/Assembly-CSharp.dll` (`804` types), `Assembly-CSharp-firstpass.dll` (`40` types), or `UnityEngine.AnimationModule.dll`, plus build scene paths confirmed in `globalgamemanagers`.
3. **Inferred Meaning (Requires In-Game Verification)**: What a field/component likely represents in gameplay and what specific values it holds during live scenes.

---

## 1. Active Scene & Player's Actual Room

### 1.1 Active Scene (Chapter / Level)

| Tier | Signal / Member | Type / Signature | Assembly / Source |
| :--- | :--- | :--- | :--- |
| **Verified in Runtime & Code** | `SceneManager.GetActiveScene().name` & `.handle` | `string`, `int` | `UnityEngine.CoreModule.dll` — used in `src/MitaAI.Plugin/Plugin.cs:149-162`, `305`, `751` |
| **Verified in Runtime Logs** | Build scene list (`25` scenes) | `Scene 1 - RealRoom` .. `Scene 20 - FightMita`, `SceneMenu`, `SceneLoading`, `SceneAihasto`, `MinigameShooter` | `MiSide_Dev/MiSideFull_Data/globalgamemanagers` & `LogOutput.log` (`Scene 3 - WeTogether`) |
| **Verified in Interop Metadata** | `GlobalGame.nameLoadedScene` / `GlobalGame.LoadingLevel` / `GlobalGame.levelLoad` | `static string` / `static string` / `static int` | `Assembly-CSharp-firstpass.dll` (`GlobalGame`) |
| **Verified in Interop Metadata** | `World.nameLocation` (reachable via `GlobalTag.world`) | `string` property on `World : MonoBehaviour` | `Assembly-CSharp.dll` (`World`, `GlobalTag`) |

#### Verified Build Scene List (`globalgamemanagers`)
- **Non-gameplay / Menu**: `SceneAihasto`, `SceneLoading`, `SceneMenu`
- **Story / Gameplay Chapters**:
  - `Scene 1 - RealRoom`, `Scene 2 - InGame`, `Scene 3 - WeTogether`, `Scene 4 - StartSecret`, `Scene 5 - StartHorror`, `Scene 6 - BasementFirst`, `Scene 7 - Backrooms`, `Scene 8 - ReRooms`, `Scene 9 - ChibiMita`, `Scene 10 - ManekenWorld`, `Scene 11 - Backrooms`, `Scene 12 - Freak`, `Scene 13 - HelloCore`, `Scene 14 - MobilePlayer`, `Scene 15 - BasementAndDeath`, `Scene 16 - TheEnd`, `Scene 17 - Dreamer`, `Scene 18 - 2D`, `Scene 19 - Glasses`, `Scene 20 - FightMita`, `MinigameShooter`

#### Critical Limitation: Scene Name $\neq$ Room Name
- A Unity scene in MiSide represents an entire story chapter (e.g., `Scene 3 - WeTogether`), which contains the **entire apartment** (bedroom, main hall/living room, kitchen, and bathroom/corridor) in a single loaded scene.
- Passing only `SceneManager.GetActiveScene().name` to the LLM does **not** tell it which room the player or Mita is currently standing in (matching the failure noted in `HANDOFF.md` where Mita guessed the wrong room).

---

### 1.2 Player's Actual Room Within the Apartment

| Tier | Signal / Member | Type / Signature | Assembly / Source |
| :--- | :--- | :--- | :--- |
| **Verified in Interop Metadata** | `Location34_Communication.roomPlayer` | `string` property (`get_roomPlayer` / `set_roomPlayer`), updated by `void PlayerInRoom(string _r)` | `Assembly-CSharp.dll` (`Location34_Communication : MonoBehaviour`) |
| **Verified in Interop Metadata** | `Location21_Communication.roomPlayer` | `int` property (`get_roomPlayer` / `set_roomPlayer`), paired with `Location34_Communication main` | `Assembly-CSharp.dll` (`Location21_Communication : MonoBehaviour`) |
| **Verified in Interop Metadata** | `AudioRoom_Room.inRoom` & `AudioRoom_Room.gameObject.name` | `bool inRoom` (`CheckInRoom()`), `Vector3 sizeRoom`, `LayerMask layerPlayer` | `Assembly-CSharp.dll` (`AudioRoom_Room : MonoBehaviour`) |
| **Inferred Meaning (Unverified)** | Exact runtime string values of `Location34_Communication.roomPlayer` and `AudioRoom_Room.gameObject.name` | Expected room identifiers for Kitchen, Bedroom, Hall/Living Room, Bathroom/Toilet | **Requires in-game logging** |

#### Limitations
1. **`Location34_Communication` is chapter-scoped**: It is the shared free-roam apartment controller for Chapters 3 and 4 (`Scene 3 - WeTogether` and `Scene 4 - StartSecret`, and wrapped by `Location21_Communication` in Peaceful Mode), not every horror/backrooms chapter.
2. **String literals are unverified**: Whether `roomPlayer` stores English names (`"Kitchen"`, `"Hall"`, `"Room"`, `"Toilet"`), Russian names, or short codes is not stored in IL2CPP metadata headers and must be logged in-game.
3. **`AudioRoom_Room` overlap/gaps**: `AudioRoom_Room` triggers acoustic reverb/occlusion boxes across scenes. While `inRoom == true` indicates the player is inside that acoustic volume, doorway transitions could briefly match zero or two overlapping boxes, and the GameObject naming convention must be checked in-game.

#### Proposed Minimal Verification Test (Scene & Room)
- In a non-invasive diagnostic log line triggered on `E` interaction (or a periodic debug probe when `DebugLogging` is enabled), log:
  1. `SceneManager.GetActiveScene().name` and `GlobalTag.world?.nameLocation`
  2. Active `Location34_Communication` instance's `roomPlayer` string (if present)
  3. All active `AudioRoom_Room` instances where `inRoom == true` (`ObjectPath(room.transform)`)
- Walk through the Bedroom $\rightarrow$ Hall $\rightarrow$ Kitchen $\rightarrow$ Bathroom in `Scene 3 - WeTogether` and record the exact logged values.

---

## 2. Active Mita Identity

### 2.1 Existing Binding & Candidate Identity Signals

| Tier | Signal / Member | Type / Signature | Assembly / Source |
| :--- | :--- | :--- | :--- |
| **Verified in Runtime & Code** | `Plugin.ScanScene` / `Plugin.FindTarget` | `MitaPerson` via `Resources.FindObjectsOfTypeAll(Il2CppType.Of<MitaPerson>())` + aim/distance/LOS selection | `src/MitaAI.Plugin/Plugin.cs:251-310` |
| **Verified in Runtime Logs** | `MitaPerson` hierarchy path | `World/Quests/Quest 1 Start/RotateOnPlayerMita/Mita` (`id=74602` in `Scene 3 - WeTogether`) | `MiSide_Dev/BepInEx/LogOutput.log` |
| **Verified in Interop Metadata** | `GlobalTag.mita` | `static GameObject` | `Assembly-CSharp.dll` (`GlobalTag`) |
| **Verified in Interop Metadata** | `GlobalGame.clothMita` & `GlobalGame.clothVariantMita` | `static string`, `static int` | `Assembly-CSharp-firstpass.dll` (`GlobalGame`) |
| **Verified in Interop Metadata** | Chapter/Variant Companion Controllers | `Location7_Chibi`, `Location7_MitaCapRepeat`, `Location8_Slouch`, `Location11_BlackMita`, `Location12_LongNeck`, `Location15_MitaKind_Follow`, `Location17_MitaWalk`, `Location18_Mita`, `Location19`, `Location6_MitaKiller`, `Location20_FightMita` | `Assembly-CSharp.dll` |
| **Inferred Meaning (Unverified)** | Mapping from `(scene.name, ObjectPath(mita.transform), active Location* controller, GlobalGame.clothMita)` to canonical persona identity | Distinguishes Default/Crazy Mita, Kind Mita, Cappie (`MitaCap`), Chibi Mita, Short-Haired Mita, Sleepy Mita (`Location17`), 2D Mita (`Location18`), Mila (`Location19`), Creepy/Original Mita (`Location12`) | **Requires per-chapter in-game verification** |

#### Limitations
1. **`mita.gameObject.name` is usually just `"Mita"`**: As proven by `LogOutput.log` (`path=World/Quests/Quest 1 Start/RotateOnPlayerMita/Mita`), the leaf GameObject name does not distinguish which Mita persona is active.
2. **Multiple `MitaPerson` instances in one scene**: Some chapters (e.g., `Scene 7 - Backrooms` with Chibi/Cappie, `Scene 8 - ReRooms`, `Scene 15 - BasementAndDeath` with Kind Mita vs. Crazy Mita) can contain more than one Mita or mannequin/illusion variants. `FindTarget()` already binds the aimed `MitaPerson` (`this.target`), so identity resolution must inspect the **bound `target` instance's transform path and nearby/parent controller**, not just `GlobalTag.mita`.

#### Proposed Minimal Verification Test (Mita Identity)
- When `FindTarget()` binds `this.target` (`MitaPerson`), log:
  1. `ObjectPath(target.transform)`
  2. `target.meshHead?.sharedMesh?.name` and `target.faceLayer?.sharedMaterial?.name`
  3. `GlobalGame.clothMita` and `GlobalGame.clothVariantMita`
  4. Whether `GlobalTag.mita` equals `target.gameObject`
- Compare across at least two chapters (e.g., `Scene 3 - WeTogether` and a later chapter save) before adding persona switching.

---

## 3. Mita's Current Activity or Animation (Raw State vs. Verified Meaning)

### 3.1 Raw Animation, Locomotion, Pose, Held-Object, and Expression Signals

| Tier | Signal / Member | Type / Signature | Assembly / Source |
| :--- | :--- | :--- | :--- |
| **Verified in Interop Metadata** | `MitaPerson.animMita` | `UnityEngine.Animator` | `Assembly-CSharp.dll` (`MitaPerson`) |
| **Verified in Interop Metadata** | `Animator.GetCurrentStateName(int layerIndex)` / `GetNextStateName(int layerIndex)` | `string` (exposed in `UnityEngine.AnimationModule.dll` interop!) | `UnityEngine.AnimationModule.dll` (`Animator`) |
| **Verified in Interop Metadata** | `Animator.GetCurrentAnimatorClipInfo(int layerIndex)` | `Il2CppStructArray<AnimatorClipInfo>` $\rightarrow$ `.clip.name` (`string`) | `UnityEngine.AnimationModule.dll` (`Animator`, `AnimatorClipInfo`) |
| **Verified in Interop Metadata** | `Animator_FunctionsOverride` (`Location34_Communication.mitaAnimator`, `MitaEvent.mitaAnimator`) | `Animator anim`, `AnimatorOverrideController animOver`, `AnimationClip animationNextFrameInstant` | `Assembly-CSharp.dll` (`Animator_FunctionsOverride : MonoBehaviour`) |
| **Verified in Interop Metadata** | `MitaPerson` Locomotion & Magnet State | `NavMeshAgent nma` (`.velocity`, `.remainingDistance`, `.enabled`), `Transform targetMoveTo`, `float nmaSpeed`, `Transform magnetTarget`, `AnimationClip animIdleAfter` | `Assembly-CSharp.dll` (`MitaPerson`) |
| **Verified in Interop Metadata** | `MitaPerson` Held Item Bones | `Transform boneRightItem`, `Transform boneLeftItem`, `Transform fixedRightItem`, `Transform fixedLeftItem` | `Assembly-CSharp.dll` (`MitaPerson`) |
| **Verified in Interop Metadata** | `Location34_Communication` Ambient State | `Il2CppReferenceArray<GameObject> objectInHandsMita`, `int indexSwitchAnimation`, `int playTypeOhterAnimation`, `bool mitaCanWalk`, `bool mitaNeedToPoint`, `bool interactiveNow`, `GameObject objectLookAfter` | `Assembly-CSharp.dll` (`Location34_Communication`) |
| **Verified in Interop Metadata** | `MitaPerson` Face & Gaze | `string startEmotion`, `bool faceEmotion`, `int indexTextureFaceLayerNow`, `Character_Look lookLife` (`Transform lookObject`, `Transform lookObjectEyes`, `bool startPriorityPlayer`, `float dotLook`) | `Assembly-CSharp.dll` (`MitaPerson`, `Character_Look`, `EmotionType`) |

#### `EmotionType` Enum Values (Verified in `Assembly-CSharp.dll`)
`none = 0`, `off = 1`, `smile = 2`, `angry = 3`, `quest = 4`, `smileteeth = 5`, `sad = 6`, `smilestrange = 7`, `shy = 8`, `smileobvi = 9`, `smiletonque = 10`, `smilecringe = 11`, `sleep = 12`, `halfsleep = 13`, `surprise = 14`, `emptiness = 15`, `deactiveEmotion = 16`, `suspicion = 17`, `trytoque = 18`, `discontent = 19`, `ajar = 20`, `catchQuest = 21`, `arrogance = 22`, `surpriseo = 23`.

### 3.2 Distinguishing Raw State Names from Verified Activity Meaning

> [!IMPORTANT]
> **Do not treat raw clip or state names as self-explanatory story context.**
> MiSide heavily uses `AnimatorOverrideController` (`Animator_FunctionsOverride.animOver`) and generic state slots (e.g., overriding a base idle/action slot with a scene-specific `AnimationClip`).

| Layer | What It Actually Tells Us | What Remains Inferred Until Tested |
| :--- | :--- | :--- |
| **Raw Animator State / Clip** (`GetCurrentStateName(0)`, `GetCurrentAnimatorClipInfo(0)[0].clip.name`) | The exact asset name of the `AnimationClip` or state machine node currently playing on layer 0. | Whether that clip name is human-readable (e.g., `"Mita Cut Carrot"` vs. `"Anim_04"`) and whether `GetCurrentStateName` resolves an ICALL cleanly in IL2CPP without throwing `EntryPointNotFoundException`. |
| **Locomotion** (`nma.velocity.magnitude`, `targetMoveTo`) | Whether Mita is physically walking/pathfinding (`velocity > 0.05f`) and which transform she is walking toward (`targetMoveTo.name`). | Why she is walking there unless correlated with the active quest hierarchy. |
| **Hand Attachments** (`boneRightItem`, `boneLeftItem`, `fixedRightItem`, `fixedLeftItem`, `objectInHandsMita`) | Which 3D prop GameObjects are currently parented/active in Mita's hands. | Whether an attached prop is actively visible (`activeInHierarchy`) vs. pre-parented and hidden (`activeSelf == false`). |
| **High-Level Activity Meaning** | Requires combining `(active quest node + held active prop + clip name + Location* component state)`. | Must be mapped empirically per scene rather than guessed from clip strings alone. |

#### Proposed Minimal Verification Test (Activity & Animation)
- On interaction with Mita, log in a `try/catch` block:
  1. `target.animMita.GetCurrentAnimatorClipInfo(0)` clip names and weights
  2. `target.animMita.GetCurrentStateName(0)` (wrapped in `try/catch` in case IL2CPP stripped the internal call)
  3. `target.nma != null ? target.nma.velocity.magnitude : -1f` and `target.targetMoveTo?.name`
  4. Active children under `boneRightItem`, `boneLeftItem`, `fixedRightItem`, `fixedLeftItem`
  5. `target.startEmotion` and `target.indexTextureFaceLayerNow`

---

## 4. Whether Scripted Dialogue / Cutscenes Currently Own the Interaction

### 4.1 Currently Implemented & Verified Gates (`MitaAI.Plugin`)

| Tier | Signal / Member | Current Usage in `src/MitaAI.Plugin` | What It Detects |
| :--- | :--- | :--- | :--- |
| **Verified in Runtime & Code** | `GameController.cutsceneRun` (`bool`) | `NativePresentation.StoryBusy` (`NativePresentation.cs:114-117`) | Explicit cutscene flag managed by `GameController`. |
| **Verified in Runtime & Code** | `GameController.dialogueTextNow` (`Dialogue_3DText`) | `NativePresentation.StoryBusy` (`NativePresentation.cs:116`) | Active 3D floating subtitle/dialogue line currently speaking. |
| **Verified in Runtime & Code** | `GameController.dialogueChanger` (`DialogueChanger.play`) | `NativePresentation.StoryBusy` (`NativePresentation.cs:117`) | Active native player dialogue choice prompt. |
| **Verified in Runtime & Code** | `GameController.isPaused` (`bool`) | `NativePresentation.IsPaused` (`NativePresentation.cs:43`) | Native Escape pause menu state. |
| **Verified in Runtime & Code** | `PlayerMove.dontMove`, `stopMouseMove`, `blockInteractive`, `animationLoopRun`, `animationFast`, `cameraOtherTarget`, `lookTarget` | `PlayerInputLease.CanAcquire` & `GameHasTakenControl` (`PlayerInputLease.cs:18-31`) | Native script locking player movement, camera, or interaction. |
| **Verified in Runtime & Code** | `PlayerMove.objectCastInteractive` & `objectCastHandInteractive` | `Plugin.FindPlayer` (`Plugin.cs:344`) | Native `E` prompt owns the interaction key. |

### 4.2 Additional Candidate Ownership Signals in Interop (Unverified at Runtime)

`HANDOFF.md` (`line 135`) explicitly notes: *"These are observed native control gates, not a guarantee that every silent quest animation is detectable."* Inspection of `Assembly-CSharp.dll` reveals additional ownership flags that may cover silent scripted moments where `cutsceneRun` is `false` and `dialogueTextNow` is transiently `null` between lines:

| Tier | Signal / Member | Type / Signature | Why It Matters |
| :--- | :--- | :--- | :--- |
| **Verified in Interop Metadata** | `PlayerMove.animationRun` & `PlayerMove.animationHandRun` | `bool` properties on `PlayerMove` | `PlayerInputLease` currently checks `animationLoopRun` and `animationFast`, but **not** `animationRun`, `animationHandRun`, or `scrAnimationNow != null` (`ObjectAnimationPlayer`). |
| **Verified in Interop Metadata** | `PlayerMove.sit` & `PlayerMove.timeCameraFreeze` | `int`, `float` on `PlayerMove` | Indicates when the player is seated (e.g., eating at the table, sofa, console/card game) or camera-frozen. |
| **Verified in Interop Metadata** | `GameController.firstPersonPlay`, `photomode`, `tetrisPlay`, `isLoadingPause`, `canCastOff` | `bool` properties on `GameController` | Sub-modes/minigames where free-roam companion chat should yield. |
| **Verified in Interop Metadata** | `Location34_Communication.interactiveNow` & `mitaForwardBlock` | `bool` properties on `Location34_Communication` | Native free-roam communication controller's own busy/blocking flags. |
| **Verified in Interop Metadata** | `World.cutsceneStart` (`Playable_Animation`) / `Playable_Animation.scrpd` (`PlayableDirector`) | `Playable_Animation` / `UnityEngine.Playables.PlayableDirector` | Timeline cutscenes driven by `PlayableDirector` (`scrpd.state == PlayState.Playing`). |
| **Verified in Interop Metadata** | `MitaEvent` active state | `MitaEvent : MonoBehaviour` (`animClipsWalk`, `mitaAnimator`, `eventFinishWalk`) | Scripted Mita walk/action sequences between dialogue lines. |

#### Proposed Minimal Verification Test (Scripted Ownership)
- During a known scripted sequence in `Scene 3 - WeTogether` (e.g., putting away groceries, cutting carrots, or walking to the bathroom where Mita walks/acts silently between lines), log a snapshot whenever `StoryBusy` transitions or when `E` is pressed:
  - `player.animationRun`, `player.animationHandRun`, `player.scrAnimationNow?.name`, `player.sit`
  - `controller.firstPersonPlay`, `controller.canCastOff`
  - `loc34?.interactiveNow`, `loc34?.mitaForwardBlock`
- Verify which flag remains `true` during silent gaps between `dialogueTextNow` lines without falsely blocking normal free-roam standing/walking.

---

## 5. Small Set of Useful Story Events & Quest-Progress Indicators

Rather than tracking hundreds of scene-specific booleans, the interop assemblies expose **five high-leverage, low-complexity story context signals**:

### Signal A: Native Story Dialogue Memory (`GameController.dialoguesMemory`)
- **Verified Interop Signature**:
  - `GameController.dialoguesMemory` $\rightarrow$ `Il2CppSystem.Collections.Generic.List<TextDialogueMemory>`
  - `TextDialogueMemory` fields/properties: `string text`, `Color clr`, `float offset`
  - Populated by `GameController.DialogueAdd(Dialogue_3DText _componentDialogue, string _text)`
- **Why It Is High-Value**: Whenever Mita or the player speaks a native scripted story line, `GameController` records it into `dialoguesMemory`. Reading the last $3\text{–}5$ entries provides immediate, exact ground truth of what just happened in the story without hardcoding every quest beat.
- **Unverified / Requires Testing**: How many lines are kept in `dialoguesMemory`, whether it persists across sub-quests within a scene, and whether `clr` reliably distinguishes Mita's lines from the player's lines.

### Signal B: Active Quest Hierarchy Path (`World/Quests/...`)
- **Verified in Runtime Logs**:
  - `LogOutput.log` already proved that in `Scene 3 - WeTogether`, Mita lives at `World/Quests/Quest 1 Start/RotateOnPlayerMita/Mita`.
- **Why It Is High-Value**: MiSide structures its chapter progression as child GameObjects under `World/Quests` (e.g., `Quest 1 Start`, etc.) that activate/deactivate as quests advance.
- **How to Read**:
  1. Walk `target.transform` parents up to `World/Quests` (already available via `Plugin.ObjectPath(target.transform)`!).
  2. Enumerate direct active children of the scene's `World/Quests` transform (`child.gameObject.activeInHierarchy`).
- **Unverified / Requires Testing**: Exact child names under `World/Quests` as the player progresses through `Scene 3 - WeTogether` and `Scene 4 - StartSecret`.

### Signal C: Player's Held Item, Key Inventory, and Aimed Object
- **Verified Interop Signatures**:
  - `PlayerMove.item` (`GameObject`), `PlayerMove.keepItem` (`bool`), `PlayerMove.hideItem` (`bool`)
  - `GlobalTag.playerRightItem`, `GlobalTag.playerLeftItem` (`static GameObject`)
  - `GameController.inventoryKeysCase` (`Il2CppSystem.Collections.Generic.List<GameObject>`)
  - `PlayerMove.objectCast` (`GameObject`), `PlayerMove.objectCastInteractive` (`GameObject`)
  - `GlobalGame.namePlayer` (`static string` — the player's in-game profile name)
- **Why It Is High-Value**: Lets Mita know if the player is currently holding an item (e.g., hairbrush, scissors, groceries, battery) or what object the player was just looking at, plus the player's chosen name (`GlobalGame.namePlayer`).

### Signal D: Generic Quest Counter Components (`Events_IntMemory` & `DialogueChanger.fileQuest`)
- **Verified Interop Signatures**:
  - `Events_IntMemory : MonoBehaviour` $\rightarrow$ `int intMemory`
  - `DialogueChanger : MonoBehaviour` $\rightarrow$ `string fileQuest`, `bool overQuest`
- **Why It Is High-Value**: `Events_IntMemory` is MiSide's reusable counter component for multi-step objectives (e.g., finding $N$ items), and `DialogueChanger.fileQuest` identifies the localization/quest table entry of active/recent dialogue branches.

### Signal E: Chapter 3 / Chapter 4 Specific Activity Milestones (Optional Enrichment)
- **Verified Interop Signatures (`Assembly-CSharp.dll`)**:
  - `Location3` (`bool sous`, `Transform pointTeleportKitchen`)
  - `Location3CutCarrot` (`bool play`, `int indexCarrotFall`, `int indexSpeak`)
  - `Location3FoodMita` (`int indexFood`, `Animator_FunctionsOverride animMita`)
  - `Location3WalkToToilet` (`bool doorMainOpen`, `bool doorToiletOpen`)
  - `Location4TableCardGame`, `Location4_Tamagotchi`, `Location4Fight`
  - `Location21_World` (`int dayNow`), `Location21_Cooking` (`bool game`, `int food`, `int progressQuest`)
- **Limitation**: Chapter-specific classes only apply to their respective scenes and should only be queried as optional enrichment after Signals A–C are verified.

---

## 6. Smallest Practical Implementation Recommendation

Do **not** build a heavy per-chapter reflection framework or hook dozens of methods. Instead, implement a **two-step, zero-hook read-only snapshot** inside `MitaAI.Plugin`:

### Step 1: Diagnostic Snapshot Verification (Before Prompt Injection)
Add a small, exception-safe helper (e.g., `GameContextSnapshot.Capture(PlayerMove player, GameController controller, MitaPerson target)`) that runs **only when the player presses `E` to talk to Mita** (zero per-frame overhead) and logs a single structured line to `BepInEx/LogOutput.log`:
1. **Scene & Room**:
   - `SceneManager.GetActiveScene().name`
   - Active `Location34_Communication.roomPlayer` (if present in scene)
   - Active `AudioRoom_Room` GameObject names where `inRoom == true`
2. **Quest & Identity**:
   - `Plugin.ObjectPath(target.transform)` (already extracts `World/Quests/Quest 1 Start/...`)
   - Active direct child names of `World/Quests`
   - `GlobalGame.namePlayer` and `GlobalGame.clothMita`
3. **Activity & Held Props**:
   - `target.animMita.GetCurrentAnimatorClipInfo(0)` first clip name + `target.nma?.velocity.magnitude`
   - Active child names under `target.boneRightItem` / `target.boneLeftItem` / `player.item?.name`
4. **Recent Story Dialogue**:
   - Last 3 entries of `controller.dialoguesMemory` (`text`)
5. **Candidate Busy Flags**:
   - `player.animationRun`, `player.animationHandRun`, `player.scrAnimationNow != null`, `player.sit`, `loc34?.interactiveNow`

### Step 2: Compact Prompt Context Line (After 1 In-Game Verification Pass)
Once a single in-game test confirms the logged strings in `Scene 3 - WeTogether`:
- Pass a compact, factual `[Game Context]` block in `DialogueRequest` (or `PromptBuilder`) containing only **verified** fields (e.g., Chapter name, Room name, Active Quest node name, Active held items, and the last 2–3 native story dialogue lines from `dialoguesMemory`), instructing the model to treat raw animation clip names as hints rather than literal actions.
- If `player.animationRun || player.animationHandRun || player.scrAnimationNow != null` proves to cleanly cover silent scripted quest interactions, add them to `PlayerInputLease.GameHasTakenControl`.

---

## 7. Files Changed

- `reports/game-context-discovery.md` (created; only file modified in `D:\MitaAI`)
