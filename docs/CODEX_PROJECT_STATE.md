# BORN TO DIG — Codex Project State

Last reviewed: 2026-08-19
Review scope: current working tree on branch `test`; gameplay changes listed below were already present before this knowledge-management setup.

## Project Overview

BORN TO DIGは、FPSプレイヤーがツルハシで岩を掘り、お宝を露出させて取得するUnity製の採掘MVPである。Unity project名は `Project_Dig`。

- Unity: `6000.5.7f1`
- Render pipeline: URP `17.5.0`
- Input System: `1.20.0`; `activeInputHandler: 1`（New Input System）
- UI: uGUI/TMP `2.5.0` と一部IMGUI
- Test Framework package: `1.7.0`
- Git branch at review: `test`

## Current MVP Goal

コードとSceneから確認できる現在の最小ゲームループは次のとおり。

`一人称で移動する → ツルハシで岩を掘る → 金塊を露出させる → 中央注視してEで取得する → MVP CLEAR`

現在の未コミットworking treeでは、`VoxelRockMVP.unity` 上のVoxelRockインスタンスが **132個のDestructiblePebbleによるテスト用岩集合**へ置き換えられている。VoxelRock実装自体はコードとして残るが、現在の同Sceneには配置されていない。

「Voxel方式とPebble方式のどちらを最終採用するか」は、確認できる正式仕様がないため未確定。現在のSceneとコードから、Pebble集合を既存のFPS・採掘・金塊取得フローに統合して評価中だと推測される。

## Architecture

主要なデータフロー:

```text
FpsCharacterController ── movement / camera / cursor
PickaxeViewModel ───────── swing visual and timing
Main Camera + MiningTool ─ center ray
    ├─ DestructiblePebble.TakeDamage()
    │    └─ Fractured prefab生成 → Rigidbody有効化 → 2–4秒で削除
    └─ VoxelRock.Mine()             [実装あり、現在Sceneには未配置]
         └─ VoxelGrid減算 → MarchingCubes → Mesh + MeshCollider更新

VoxelRock.DensityRemoved ───────────┐
Pebble Broken events                ├─ GoldNuggetMVP露出 → 取得
  → PebbleGoldExposureTrackerTest ──┘       └─ MVPGameManager → MVPUI → CLEAR

VoxelRock / ClickableVoxelRock events → MiningSkillProgression
```

Runtimeコードは基本的に `Assembly-CSharp`、Editorコードは `Assembly-CSharp-Editor` へ入る。ゲーム用asmdef/asmrefはなく、確認できたasmdefは `Assets/ai.meshy/Editor/Script/MeshyAssembly.asmdef` のみ。

## Important Files

### 採掘・Voxel

- `Assets/BornToDig/VoxelRock/Scripts/MiningTool.cs` — Main Camera中央Rayから採掘入力を処理し、DestructiblePebbleまたはVoxelRockへ通知する。
- `Assets/BornToDig/VoxelRock/Scripts/VoxelRock.cs` — 元MeshのVoxel化、採掘、ランタイムMesh/Collider更新、密度サンプルAPI、`DensityRemoved` event。
- `Assets/BornToDig/VoxelRock/Scripts/VoxelGrid.cs` — 密度グリッド、トリリニアサンプル、勾配、球状密度減算。
- `Assets/BornToDig/VoxelRock/Scripts/VoxelMeshVoxelizer.cs` — 閉じた元Meshから密度グリッドを生成する。
- `Assets/BornToDig/VoxelRock/Scripts/MarchingCubes.cs` — 密度グリッドから三角形MeshDataを生成する。
- `Assets/BornToDig/VoxelRock/Editor/VoxelRockMvpSceneBuilder.cs` — VoxelRock MVP Sceneを再生成するEditorツール。既存Sceneを上書きし得る。
- `Assets/BornToDig/VoxelRock/Editor/VoxelRockFpsCompatibility.cs` — FPS統合とCamera/AudioListener/照準の重複防止。
- `Assets/BornToDig/VoxelRock/Editor/VoxelRockMvpVerifier.cs` — VoxelRock採掘スモークテスト。現在SceneにVoxelRockがないため、そのままでは前提不一致。

### 破壊可能Pebble

- `Assets/BornToDig/DestructiblePebbles/Runtime/DestructiblePebble.cs` — HP、破壊Prefab生成、打撃Impulse/Torque、`Broken` event。
- `Assets/BornToDig/DestructiblePebbles/Runtime/FracturedPebbleInstance.cs` — 破片Rootを2–4秒後に削除する。
- `Assets/BornToDig/DestructiblePebbles/Editor/DestructiblePebbleInstaller.cs` — A/B/CのIntact/Fractured Prefab生成とScene配置。再実行は既存Assetを更新し得る。
- `Assets/BornToDig/DestructiblePebbles/Editor/DestructiblePebbleVerifier.cs` — 単体PebbleのEdit/Play検証。
- `Assets/BornToDig/DestructiblePebbles/Test/Editor/PebbleRockTestGenerator.cs` — Seed `20260818` でA/B/C各44、計132個のテスト集合を生成する。
- `Assets/BornToDig/DestructiblePebbles/Test/Editor/PebbleRockTestVerifier.cs` — 集合構成、参照、掘削、破片寿命、金塊取得/CLEARのEdit/Play検証。
- `Assets/BornToDig/DestructiblePebbles/Test/Runtime/PebbleGoldExposureTrackerTest.cs` — 金塊付近のPebbleの `Broken` eventを監視し、50%破壊で既存金塊へ露出率を通知するテスト専用bridge。

### お宝・UI

- `Assets/BornToDig/GoldNuggetMVP/Runtime/GoldNuggetMVP.cs` — Voxel密度または外部報告から露出を判定し、中央注視・距離・E入力で取得する。
- `Assets/BornToDig/GoldNuggetMVP/Runtime/MVPGameManager.cs` — 取得数、0.75秒後のCLEAR確定、UI通知。
- `Assets/BornToDig/GoldNuggetMVP/Runtime/MVPUI.cs` — TMPによる目的、取得prompt、CLEAR表示と日本語font割当。
- `Assets/BornToDig/GoldNuggetMVP/Editor/GoldNuggetMvpInstaller.cs` — PrefabとScene構成を再生成する。既存調整を上書きし得る。
- `Assets/BornToDig/GoldNuggetMVP/Editor/GoldNuggetMvpVerifier.cs` — 金塊の埋没/露出/注視/取得/UI/CLEARを検証する。

### 環境テスト

- `Assets/PurePoly/Mining_Pack/` — PurePoly Mining Packのインポート済み素材一式。既存のゲームプレイSceneとは分離して扱う。
- `Assets/BornToDig/EnvironmentIntegration/Editor/PurePolyMiningPackEnvironmentSceneBuilder.cs` — PurePoly Prefabから独立した視覚確認Sceneを生成するEditorツール。配置物をIgnore Raycast Layerへ設定し、Colliderを無効化する。
- `Assets/BornToDig/EnvironmentIntegration/Scenes/PurePolyMiningPackEnvironmentTest.unity` — 地面、岩、洞窟、植生、確認用Camera/Lightだけを持つ環境素材の視覚確認Scene。`VoxelRockMVP` を置換するゲームプレイSceneではない。

### プレイヤー・入力・Skill

- `Assets/FpsCharacterMVP/Runtime/FpsCharacterController.cs` — CharacterControllerによる移動、視点、ジャンプ、スプリント、カーソル制御。
- `Assets/FpsCharacterMVP/Runtime/PickaxeViewModel.cs` — ツルハシ表示と連続スイング。採掘判定は持たない。
- `Assets/FpsCharacterMVP/Runtime/DwarfVisualSlot.cs` — 将来の外見Prefab用slot。
- `Assets/FpsCharacterMVP/Runtime/CharacterMvpHud.cs` — IMGUIの操作説明と照準。
- `Assets/MiningSkillMVP/Runtime/MiningSkillProgression.cs` — Voxel採掘量からSkill Point、Tab画面、採掘/スイングinterval同期。Sceneロード後にbootstrapが自動生成する。
- `Assets/InputSystem_Actions.inputactions` — Player/UI Action Map。ただし主要Runtimeは `Keyboard.current`、`Mouse.current`、`Gamepad.current` を直接参照する。

## Scenes

- `Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity` — 現在の開発対象Scene。working treeではVoxelRockを外し、`PebbleRockCluster_Test`、FPS、Main Camera/MiningTool、金塊、Manager、TMP UI、Ground、Directional Lightを持つ。
- `Assets/Scenes/SampleScene.unity` — 旧32³ `ClickableVoxelRock` と `FlyCameraController` を含む別テスト系。現行FPS/Pebble Sceneと混同しない。
- `Assets/BornToDig/EnvironmentIntegration/Scenes/PurePolyMiningPackEnvironmentTest.unity` — PurePoly Mining Packの背景素材だけを確認する独立Scene。FPS、採掘、Pebble、金塊フローは含まない。

Build Settingsに登録されているSceneは現在 `SampleScene` のみ。`VoxelRockMVP` を確認する場合は明示的に開く。

## Prefabs

- `Assets/FpsCharacterMVP/Prefabs/MVP_FPS_Player.prefab` — FPS Player、CameraPivot、Main Camera、Pickaxe、HUD/照準の基礎構成。
- `Assets/BornToDig/GoldNuggetMVP/Prefabs/GoldNugget_MVP.prefab` — Layer `Ignore Raycast`、Trigger Collider、`GoldNuggetMVP`。Rigidbodyなし。
- `Assets/BornToDig/DestructiblePebbles/Prefabs/Rock_A_Intact.prefab`、`Rock_B_Intact.prefab`、`Rock_C_Intact.prefab` — 通常表示、Collider、`DestructiblePebble`。常設Rigidbodyなし。
- 対応する `Rock_A/B/C_Fractured.prefab` — 各5破片、Convex MeshCollider、待機中Kinematic Rigidbody、`FracturedPebbleInstance`。
- `Assets/BornToDig/DestructiblePebbles/Test/Prefabs/PebbleRockCluster_Test.prefab` — Intact 132個だけで構成するテスト専用集合。通常時Rigidbodyなし。

## Mining System

`PickaxeViewModel` は見た目とスイング、`MiningTool` は判定を担当する。左クリックholdまたはGamepad Right Triggerを一定intervalで読み、Main Camera中央からRaycastする。

現在Sceneの `MiningTool` 値:

- Distance: `15.186258`
- Radius: `0.585`
- Strength: `1.528`
- Interval: `0.48`
- `showCrosshair: false`（HUD側の照準を使用）
- `requirePreviouslyLockedCursor: true`

Hit先が `DestructiblePebble` の子なら `TakeDamage()`、それ以外で `VoxelRock.RockCollider` と一致すれば `VoxelRock.Mine()` を呼ぶ。カーソル再ロックclickを誤採掘にしないgateを維持する。

## Rock / Voxel System

Voxel方式は、元岩Meshを `VoxelMeshVoxelizer` で密度化し、`MarchingCubes` でランタイムMeshを生成する。`VoxelRock.Mine()` が球状に密度を減算し、変更時だけMeshとMeshColliderを更新する。

コード上の既定値はResolution `48`、Iso Level `0.5`、Bounds Padding `0.06`。元Modelは `Assets/BornToDig/VoxelRock/Models/BORN_TO_DIG_Rock.fbx`。このモデルはRead/Writeとimport scaleがVoxel生成に影響する。

現在のworking-tree SceneにはVoxelRockインスタンスがない。Voxel方式をSceneへ戻す場合は、Pebbleテストとの共存/置換意図、金塊の `voxelRock` 参照、採掘Ray遮蔽を確認する。

## Destruction System

Intact PebbleのHP既定値は `2.5`。破壊時に対応するFractured Prefabを同じ見かけのtransformで生成し、5個のRigidbodyをnon-kinematicにして弱いImpulse/Torqueを与え、Rootを既定3秒で削除する。大量配置ではIntactのみを置き、FracturedやRigidbodyを事前常設しない。

現在の集合テストはA/B/C各44個、計132個。Scene Rootは `(7.35, 1.08, -3.05)`、金塊はおよそ `(7.49, 1.05, -3.13)`。`PebbleGoldExposureTrackerTest` は半径 `0.92` 内だけをevent購読し、破壊率 `0.5` を露出閾値としている。

`Assets/BornToDig/DestructiblePebbles/Test` は本番システムではなく、評価用に隔離された範囲である。

## Treasure System

`GoldNuggetMVP` は2つの露出入力を扱う。

- Voxel方式: Collider周囲14方向を密度sampleし、既定50%が空間なら露出。
- Pebble test方式: `ReportExternalExposure()` が受け取った最大露出率を保持し、50%で露出。

露出後、Main Camera中心RayがTriggerへ当たり、距離が `2.75m` 以内ならpromptを出す。EまたはGamepad buttonWestで取得し、Renderer/Colliderを無効化する。`MVPGameManager` が1個取得を確定し、0.75秒後に `MVP CLEAR` を表示する。Inventory、複数お宝、Save、Scene遷移は未実装。

## Player

`MVP_FPS_Player` はCharacterController方式。WASD/矢印移動、Mouse look、Space jump、Left Shift sprint、Escでcursor解除、左clickで再取得。`CameraPivot` 子のMain Cameraに `MiningTool` とAudioListenerが付く。

有効なFPS Player、Main Camera、AudioListener、照準は各1つを維持する。`MiningSkillBootstrap` も重複Scene配置しない。

## UI

- `CharacterMvpHud`: IMGUIの操作説明と照準。
- `MiningSkillProgression`: IMGUIのSkill HUD/Tab画面。
- `MVP_UI`: Screen Space Overlay Canvas。TMPの `ObjectiveText`、`PickupPrompt`、`ClearPanel`、`ClearTitle`、`ClearSubtitle`。
- 日本語Font: `Assets/BornToDig/GoldNuggetMVP/Fonts/NotoSansJP-VF.ttf` からRuntime TMP fontを生成する。

HUDとMVP_UIは責務が異なるため、片方を重複と誤認して削除しない。

## Important Decisions

- 採掘判定とツルハシ見た目を分離する。
- VoxelRockは既存密度グリッドを単一source of truthにし、お宝露出用に小さなsample APIを公開する。
- Pebble方式でも既存のGold/Manager/UI/CLEARを再利用し、露出だけをtest bridgeから渡す。
- Pebble破壊はevent-driven。露出判定を毎frame全探索しない。
- Intactは軽量、Fracturedは破壊時だけ生成して短時間で削除する。
- FPS/Camera/AudioListener/照準の重複を避ける。
- New Input Systemのdevice直接参照を既存MVP方式として維持し、全面的なAction Asset移行は別判断とする。
- PurePoly環境素材の確認SceneはゲームプレイSceneから分離し、背景用インスタンスはIgnore RaycastかつCollider無効のdisplay-onlyとする。

## Known Problems

- 現在の `VoxelRockMVP.unity` にはVoxelRockがないため、`VoxelRockMvpVerifier.VerifyBatch()` はScene前提を満たさず失敗する。Voxel方式の回帰検証は専用Sceneを用意するか、意図を確認して現Scene構成を戻す必要がある。
- `MiningSkillProgression` は `VoxelRock.DensityRemoved` と旧 `ClickableVoxelRock.VoxelsRemoved` だけを購読する。DestructiblePebbleの破壊ではSkill Pointが増えない。
- Build Settingsは `SampleScene` だけを登録しているため、Player buildが現在のPebble統合Sceneを直接開始しない。
- 既存の検証記録ではUnity 6000.5の `Rock_A_Fragment_01` Convex Mesh生成時に256 polygon上限のpartial hull警告が報告されている。現ターンでは再実行していないため、再現性と物理影響は未確認。

## Technical Debt

- ゲームコードにasmdefがなく、ほぼ全Runtime/Editorコードが大きな既定assemblyへ入る。
- IMGUI HUDとTMP Canvasが併存する。現在は役割分担されているが、将来UIを統一する場合は入力pause、照準、Fontの責務を整理する必要がある。
- Scene/Prefab builderとinstallerは便利だが、手調整済みScene/Prefabを上書きできる。冪等性と差分保護は保証されていない。
- Pebble集合と露出bridgeは `Test` 配下であり、本番化の設計・性能条件は未確定。

## Do Not Break

- `MiningTool` のVoxel/Pebble振り分けとcursor gate。
- Voxelの密度減算後にMeshFilterとMeshColliderが同一更新Meshを参照すること。
- Gold取得後の再取得防止、Managerの1個カウント、0.75秒後のCLEAR、UI参照。
- Pebble Intactに常設Rigidbody/破片を入れないこと、Fracturedを寿命削除すること。
- Pebble testの金塊は `voxelRock: null` で、trackerから外部露出を受けること。
- 単一FPS Player、Main Camera、AudioListener、照準と、Main Camera上のMiningTool。
- `.meta`/GUID、Model import settings、Prefab/Sceneのserialized references。
- PurePoly確認Sceneと `VoxelRockMVP` の役割分離、および背景用PurePolyインスタンスのIgnore Raycast/Collider無効設定。

## Verification Methods

### 基本

1. Unity EditorでC# compile完了を待ち、Consoleの赤Errorを0にする。
2. 対象Scene/PrefabでMissing Script、Missing Reference、重複、Layer/Tag、Collider/Rigidbody、SerializedFieldを確認する。
3. Play Modeで変更対象と近接する既存ループを確認する。
4. `git diff --check`、`git diff --stat`、`git status --short` で意図した差分だけか確認する。

### プロジェクト固有Verifier

Unityの `-executeMethod` で呼べるpublic static entrypoint:

- `BornToDig.EditorTools.VoxelRockMvpVerifier.VerifyBatch` — VoxelRock生成/採掘/FPS互換。現在Sceneとは前提不一致。
- `BornToDig.EditorTools.GoldNuggetMvpVerifier.VerifyBatch` — Gold Edit Mode。
- `BornToDig.EditorTools.GoldNuggetMvpPlayModeVerifier.VerifyBatch` — Gold Play Mode。
- `BornToDig.EditorTools.DestructiblePebbleVerifier.VerifyAllBatch` — A/B/C Prefab Edit Mode。
- `BornToDig.EditorTools.DestructiblePebblePlayModeVerifier.VerifyRockABatch` — Pebble Play Mode。
- `BornToDig.EditorTools.PebbleRockTestVerifier.VerifyEditModeBatch` — Pebble集合Edit Mode。
- `BornToDig.EditorTools.PebbleRockTestPlayModeVerifier.VerifyPlayModeBatch` — Pebble集合Play Mode。

これらはSceneを開く、Play Modeへ入る、またはAssetを読み込む。実行前にentrypointと現在Scene前提を確認する。Installer/Builder/Generatorは検証ではなくAssetを更新するため、無断で代用しない。

Test Framework packageは存在するが、NUnit用test asmdef/test fileは確認できていない。compile/YAML確認とEditor/Play verifierを区別して報告する。

## Recent Significant Changes

2026-08-18〜19のworking treeで確認した未コミット変更:

- `MiningTool` にDestructiblePebbleへのdamage dispatchを追加。
- `GoldNuggetMVP` に外部露出率bridgeを追加。
- `VoxelRockMVP.unity` からVoxelRockインスタンスを外し、`PebbleRockCluster_Test` とその内部の金塊へ置換。
- `Assets/BornToDig/DestructiblePebbles/` 一式を追加。Runtime、Editor installer/verifier、A/B/C Prefab/Model、test generator/verifierを含む。
- `Assets/PurePoly/Mining_Pack/` をインポートし、独立した `PurePolyMiningPackEnvironmentTest.unity` と生成・プレビュー用Editor builderを追加。

既存AGENTSにはPebble Play検証とVoxel smoke検証のPASS記録があったが、このProject State作成時にはUnity検証を再実行していない。特にVoxel smoke verifierは現在Sceneと前提がずれているため、過去結果を現在結果として扱わない。
