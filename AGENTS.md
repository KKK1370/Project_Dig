# Project_Dig 開発ガイド

## 最初に読むこと

- このリポジトリで新機能の実装、既存機能の修正、シーンやPrefabの変更を始める前に、必ずこの `AGENTS.md` を読むこと。
- プロジェクト構成、主要シーン、入力方式、採掘方式、重要なPrefabなどを変更した場合は、実装内容と矛盾しないよう必要に応じてこのファイルも更新すること。
- 記述より実際のプロジェクト状態が新しい場合は、推測で進めず、該当シーン・Prefab・スクリプトを確認してこのファイルを更新すること。

## ゲーム概要

- ゲーム名／コンセプトは **BORN TO DIG**。一人称視点で岩をツルハシで掘る採掘ゲーム。
- 現在のMVPシーンは `Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity`。
- `Assets/Scenes/SampleScene.unity` は旧方式を含む別のテストシーンであり、現行MVPと混同しないこと。
- Build Settingsには現時点で `SampleScene` のみが登録されている。MVP確認時は `VoxelRockMVP` を明示的に開くこと。

## Unity・主要パッケージ

- Unity Editor: **6000.5.7f1**
- Universal Render Pipeline (URP): 17.5.0
- Input System: 1.20.0
- Active Input Handling: **Input System Package (New)**（`activeInputHandler: 1`）
- uGUI 2.5.0を導入済みで、同パッケージ内のTextMeshProを利用できる。
- AI Navigation、AI Inference、Timeline、Visual Scripting、Meshy連携もあるが、採掘MVPの中核ではない。

## VoxelRockMVPシーン

主要オブジェクト:

- `Voxel Rock`: `VoxelRock`、`MeshFilter`、`MeshRenderer`、`MeshCollider`、実行時に非表示になる元モデル。
- `MVP_FPS_Player`: `CharacterController`、`FpsCharacterController`、`PickaxeViewModel`、`DwarfVisualSlot`、`CharacterMvpHud`。
- プレイヤーの子: `CameraPivot`、`Main Camera`、`PickaxeViewModel`、`CharacterModelRoot_DropDwarfHereLater`。
- `Main Camera`: `MiningTool`、`Camera`、`AudioListener`。
- `GoldNugget_MVP`: 岩とは独立した金塊Prefabインスタンス。Layerは `Ignore Raycast`、ColliderはTriggerでRigidbodyは持たない。
- `MVP_GameManager`: 金塊取得数、0.75秒後のクリア確定、UI遷移を管理する。
- `MVP_UI`: Screen Space OverlayのCanvas。`ObjectiveText`、`PickupPrompt`、`ClearPanel`以下のクリア文言を持つ。
- その他: `Ground`、`Directional Light`。

有効なCamera、AudioListener、照準は各1個だけにする。`FlyCameraController` と `FpsCharacterController` を同時に有効化せず、`ClickableVoxelRock` を `VoxelRockMVP` に追加しないこと。

## FPSプレイヤー

主要ファイル:

- `Assets/FpsCharacterMVP/Prefabs/MVP_FPS_Player.prefab`
- `Assets/FpsCharacterMVP/Runtime/FpsCharacterController.cs`
- `Assets/FpsCharacterMVP/Runtime/PickaxeViewModel.cs`
- `Assets/FpsCharacterMVP/Runtime/DwarfVisualSlot.cs`
- `Assets/FpsCharacterMVP/Runtime/CharacterMvpHud.cs`
- `Assets/FpsCharacterMVP/Editor/FpsCharacterBuilder.cs`

構成と挙動:

- `CharacterController` ベース。WASD／矢印で移動、マウスで視点、Spaceでジャンプ、左Shiftで走る。
- 起動時にカーソルをロックし、Escで解除、左クリックで再取得する。
- `CameraPivot` は目の高さにあり、既存の `Main Camera` を子として使用する。
- `DwarfVisualSlot` は将来の外見Prefab用で、移動・カメラ・採掘とは分離されている。
- `CharacterMvpHud` はIMGUIで操作説明と照準を描画する。金塊MVP用Canvasとは責務が異なるため、どちらも維持する。
- `FpsCharacterBuilder` は既存Cameraを引き継いでFPSとPrefabを作るEditorツール。既存プレイヤーを不用意に再生成しないこと。

## ツルハシ／採掘処理

主要ファイル:

- `Assets/FpsCharacterMVP/Runtime/PickaxeViewModel.cs`
- `Assets/BornToDig/VoxelRock/Scripts/MiningTool.cs`

仕組み:

- `PickaxeViewModel` は一人称ツルハシの見た目とスイングアニメーションを担当する。
- 左クリック長押し、またはGamepadのRight Triggerで一定間隔ごとにスイングする。
- 表示は `Handle`、`Metal Head`、`Left Tip` からなる簡易モデルで、採掘判定自体は持たない。
- 採掘判定は `Main Camera` の `MiningTool` が担当する。
- 画面中央からRaycastし、命中Colliderが対象 `VoxelRock.RockCollider` の場合だけ `VoxelRock.Mine()` を呼ぶ。
- カーソル再ロックのクリックで誤採掘しないよう、既存シーンではカーソルロック状態のゲートを使う。
- 現在のシーン値は概ね Distance `15.186258`、Radius `0.585`、Strength `1.528`。READMEやBuilderの初期値へ不用意に戻さないこと。
- `MiningSkillProgression` が `MiningTool` と `PickaxeViewModel` の間隔を同期して変更する場合がある。

## VoxelRockの仕組みと主要スクリプト

主要ファイル:

- `Assets/BornToDig/VoxelRock/Scripts/VoxelRock.cs`
- `Assets/BornToDig/VoxelRock/Scripts/VoxelGrid.cs`
- `Assets/BornToDig/VoxelRock/Scripts/VoxelMeshVoxelizer.cs`
- `Assets/BornToDig/VoxelRock/Scripts/MarchingCubes.cs`
- `Assets/BornToDig/VoxelRock/Scripts/MiningTool.cs`
- `Assets/BornToDig/VoxelRock/Models/BORN_TO_DIG_Rock.fbx`
- `Assets/BornToDig/VoxelRock/Materials/BORN_TO_DIG_Rock.mat`
- `Assets/BornToDig/VoxelRock/Source/BORN_TO_DIG_Rock.glb`

処理の流れ:

1. `VoxelRock.Initialize()` がRead/Write可能な元岩メッシュを岩ローカル空間へ変換する。
2. `VoxelMeshVoxelizer.FillFromMesh()` が閉じたメッシュを走査し、`VoxelGrid` の密度を内部 `1`、空間 `0` として埋める。
3. `MarchingCubes.Generate()` が密度グリッドからランタイムMeshを生成する。
4. 同じMeshを `MeshFilter` と `MeshCollider` に設定する。
5. `VoxelRock.Mine()` が `VoxelGrid.CarveSphereAmount()` でワールド空間の球範囲を減算する。
6. 密度が変わったときだけMeshとColliderを再生成し、`DensityRemoved` イベントを通知する。

現行設定:

- Grid Resolution: `48`
- Iso Level: `0.5`
- Bounds Padding: `0.06`
- `Voxel Rock` Scale: `5, 5, 5`
- 元モデルは初期化後に非表示になる。別GameObjectまで削除する仕組みではない。

`VoxelGrid` はトリリニア密度サンプル、勾配、球状密度減算を持つ。`VoxelRock.SampleDensityWorld()` と `IsSolidAtWorldPoint()` はこの既存密度をワールド座標から読むための小さな公開APIで、金塊の露出判定に使用する。別のVoxelシステムを作らないこと。

## Input System方式

- `Assets/InputSystem_Actions.inputactions` に `Player` と `UI` のAction Mapがあり、Move、Look、Attack、Interact、Crouch、Jump、Previous、Next、Sprint等を定義している。
- ただし主要ランタイムコードは `PlayerInput` や生成ラッパーではなく、`Keyboard.current`、`Mouse.current`、`Gamepad.current` を直接読む。
- 新機能は `FpsCharacterController`、`PickaxeViewModel`、`MiningTool`、`MiningSkillProgression` の方式に合わせる。
- 新機能だけ旧 `UnityEngine.Input` を混在させない。Action Asset中心への全面移行はMVP範囲を超えるため、別途明示的な承認が必要。
- 条件付きLegacy Inputコードが残っていても、現在のProject SettingsではNew Input System側が使用される。

## 採掘スキルMVP

主要ファイルは `Assets/MiningSkillMVP/Runtime/MiningSkillProgression.cs`。

- Sceneロード後にBootstrapが自動生成するため、Scene上に重複追加しない。
- 現行 `VoxelRock.DensityRemoved` と旧 `ClickableVoxelRock.VoxelsRemoved` の両方に対応する。
- 採掘量からSkill Pointを加算する。
- Tabでスキル画面を開閉し、表示中はFPS入力を止める。
- Skill Pointで採掘速度を強化し、採掘とツルハシの間隔を同時に短縮する。
- UIはIMGUIで描画される。

## 重要なPrefab・Scene・Editorツール

- 現行MVP Scene: `Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity`
- 旧／別テストScene: `Assets/Scenes/SampleScene.unity`
- FPS Prefab: `Assets/FpsCharacterMVP/Prefabs/MVP_FPS_Player.prefab`
- 金塊Prefab: `Assets/BornToDig/GoldNuggetMVP/Prefabs/GoldNugget_MVP.prefab`
- 金塊Model: `Assets/BornToDig/GoldNuggetMVP/Models/GoldNugget_MVP.fbx`（`お宝.blend` から書き出したもの）
- 金塊Material: `Assets/BornToDig/GoldNuggetMVP/Materials/GoldNugget_MVP.mat`
- 日本語UI Font: `Assets/BornToDig/GoldNuggetMVP/Fonts/NotoSansJP-VF.ttf`
- TextMeshPro基本リソース: `Assets/TextMesh Pro`
- `VoxelRockMvpSceneBuilder.cs`: VoxelRockテストシーンを生成する。編集済みシーンを確認せず作り直さない。
- `VoxelRockFpsCompatibility.cs`: FPSを統合し、Camera、AudioListener、照準の重複を防ぐ。
- `VoxelRockMvpVerifier.cs`: 岩生成、Collider更新、反復採掘、貫通、FPS互換性のスモークテスト。
- `FpsCharacterBuilder.cs`: FPSとPrefabを生成するEditorツール。
- `GoldNuggetMvpInstaller.cs`: 金塊PrefabとMVP用Scene構成を再生成するEditorツール。既存調整を上書きし得るため、通常作業で不用意に再実行しない。
- `GoldNuggetMvpVerifier.cs`: 金塊の初期埋没、露出、中央注視、取得、UI、CLEARと既存FPS／採掘の有効性を検証する。

## 旧方式・補助コード

- `Assets/Scripts/ClickableVoxelRock.cs` は32³ bool配列と露出面Cube Meshによる旧岩。現行MVPでは使用しない。
- `Assets/Scripts/FlyCameraController.cs` は旧テスト用。現行MVPでは無効化し、FPSと併用しない。
- `Assets/ai.meshy` はMeshy連携Editor拡張。
- `Assets/TutorialInfo` と `Readme.asset` はUnityテンプレート由来。

## 現在完成している機能

- FPS移動、視点、ジャンプ、スプリント、カーソル制御。
- 一人称ツルハシ表示と連続スイング。
- 画面中央RaycastによるVoxelRock採掘。
- 岩モデルからの密度グリッド生成とMarching Cubes Mesh生成。
- 球状密度削除による凹み、穴、貫通。
- 採掘後のMeshCollider同期更新。
- 操作説明と照準HUD。
- 採掘量、Skill Point、採掘速度アップの採掘スキルMVP。
- FPSとVoxelRockの統合、単一Camera／AudioListener／照準。
- 採掘ループを確認するEditorスモークテスト。
- 岩内部の金塊1個を掘り出し、取得して `MVP CLEAR` になる最小ゲームループ。
- 14点のVoxel密度サンプルによる金塊露出判定、近距離・中央注視・Eキーによる取得判定。
- TextMeshProによる探索、取得プロンプト、取得数、クリア表示。

## 現在の金塊MVP実装

既存採掘を維持した次の最小ループは実装済み。

`岩を掘る → 岩内部の金塊を発見する → 十分に露出させる → 近距離で見てEキーで取得する → MVP CLEAR`

実装内容:

- `GoldNugget_MVP` は岩内部の独立GameObjectで、ワールド位置は概ね `(0.74, 1.69, -3.43)`。開始時はVoxel密度上で完全に埋没している。
- `GoldNuggetMVP.cs` が開始時と `VoxelRock.DensityRemoved` 通知時にCollider周囲14方向の密度を確認し、7点以上（50%）が空間になれば露出済みとして固定する。
- 露出後、Main Cameraから2.75m以内の中央Raycastが金塊Triggerへ当たると `E 金塊を拾う` を表示する。
- 取得はNew Input Systemの `Keyboard.current.eKey`（GamepadはbuttonWest）で行い、取得後はRendererとColliderを無効化して再取得を防ぐ。
- `MVPGameManager.cs` が取得数を1にし、0.75秒後にCLEARを表示する。Scene変更やアプリ終了は行わない。
- `MVPUI.cs` が `金塊を探す 0 / 1`、取得プロンプト、`金塊を入手！ 1 / 1`、`MVP CLEAR`、`金塊を発見しました！` を表示する。
- インベントリ、複数お宝、セーブ、Scene遷移は追加していない。

## 変更時の必須ルール

- **正常に動いている既存の採掘システムを不用意に書き直さないこと。**
- `VoxelRock`、`VoxelGrid`、`VoxelMeshVoxelizer`、`MarchingCubes`、`MiningTool` の責務と流れを維持し、必要最小限の公開APIや追加コンポーネントで拡張すること。
- **大規模リファクタリングをしないこと。** MVPに不要な抽象化、汎用インベントリ、セーブ、複数お宝、Scene遷移を追加しない。
- Scene Builder再実行で調整値や追加物が失われる可能性がある。既存 `VoxelRockMVP` を確認せず再生成しない。
- FPS、Camera、AudioListener、照準、MiningTool、採掘スキルBootstrapを重複生成しない。
- 新しいColliderが採掘Rayを遮らないか確認し、必要ならLayerまたはTriggerで岩を掘れなくしない。
- 新しい入力はNew Input System直接参照方式に合わせる。
- SceneやPrefab変更後はMissing Script、参照切れ、重複、Consoleの赤エラーを確認する。
- 採掘関連変更後はFPS操作、ツルハシ、岩初期生成、反復採掘、穴の深化、Collider更新を再確認する。
