# BORN TO DIG / Project_Dig — Codex 開発ルール

## Project Identity

- Project name: **BORN TO DIG**
- Unity project: **Project_Dig**
- 一人称視点の採掘ゲーム。既存の採掘ループと参照関係を維持し、依頼された範囲だけを変更する。

## 作業開始時の確認順序

1. この `AGENTS.md` を読む。
2. `docs/CODEX_PROJECT_STATE.md` が存在する場合は読む。
3. 作業に該当する `.agents/skills/` のSkillを読む。
4. Gitの現在ブランチと既存差分を確認し、ユーザーの未コミット変更を自分の変更と区別する。
5. Project Stateを入口に、依頼に直接関係するScene、Prefab、コード、設定だけを確認する。

Project Stateや過去の記録より実際のファイルが新しい場合は、推測で仕様を確定しない。関係ファイルを確認し、確認済み情報と推測を区別する。

## 情報管理の役割

- `AGENTS.md`: 長期的に守るルール、禁止事項、コーディング方針、作業プロセス。
- `docs/CODEX_PROJECT_STATE.md`: 現在の構造、主要ファイル、MVP状態、既知の問題、現時点の設計判断。
- `.agents/skills/`: Unity機能追加、デバッグ、検証など、繰り返し使う作業手順。
- Codex Memories: デバッグで得た再利用可能な知見、プロジェクト固有の癖、過去に失敗した方法、繰り返し役立つパターン。

同じ情報を複数層へ大量に重複させない。変わりやすいScene値、ブランチ、検証結果、ファイル一覧を `AGENTS.md` に蓄積しない。

## 変更範囲と既存仕様

- 関係ファイルと影響範囲を確認してから変更する。
- 既存仕様を推測で変更しない。不明点は不明と記録する。
- ユーザーから依頼されていない大規模リファクタリング、別システムへの置換、ついで実装を行わない。
- 変更範囲を最小限にし、既存の正常なゲームプレイを維持する。
- 失敗した変更や未検証事項を隠さない。未検証のものを「確認済み」と報告しない。

## Unity Assetと参照の安全

- 既存Scene、Prefab、Material、Model、Texture、Audio、Import設定、ProjectSettingsを不用意に変更しない。
- Unityの `.meta` とGUIDを尊重する。Assetの移動・Rename・削除時は参照破壊を確認する。
- Blender、FBX、GLBなどのソースアセットを明示的な依頼なしに破壊的編集しない。
- Scene/Prefabの再生成ツールは既存調整を上書きし得る。対象と差分を確認せず実行しない。
- Scene/Prefab変更後はMissing Script、Missing Reference、重複コンポーネント、Layer、Tag、Collider、Rigidbody、SerializedFieldを確認する。
- RuntimeコードとEditor専用コードを分離し、Editor APIをRuntimeへ混入させない。

## 壊してはいけない主要境界

- 現行Voxel採掘は `MiningTool` → `VoxelRock.Mine()` → `VoxelGrid` → `MarchingCubes` → Mesh/MeshCollider更新という責務分担を維持する。
- `MiningTool` は中央Rayによる採掘入口であり、VoxelRockとDestructiblePebbleの両方へ到達できる。既存対象を壊す一方的な置換をしない。
- 現行MVPではFPS Player、Main Camera、AudioListener、照準を重複させない。`FlyCameraController` とFPS制御を同時に有効化しない。
- `Assets/Scenes/SampleScene.unity` の旧クリック式Voxelと、`Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity` の現行開発系を混同しない。
- DestructiblePebbleは通常時にIntactだけを配置し、Rigidbody付きFracturedを破壊時だけ生成して寿命削除する方針を維持する。
- お宝の取得、Manager、UI、CLEARフローを、露出判定方式の変更だけを理由に重複実装しない。
- 新しい入力は既存のNew Input System方式に合わせる。MVP全体の入力設計移行は明示的な依頼なしに行わない。

詳細な現在構成、Sceneの実体、数値、重要ファイルは `docs/CODEX_PROJECT_STATE.md` を参照する。

## Unity C# 方針

- 既存コードのnamespace、命名、`SerializeField`、Inspector構成を優先する。
- null安全性を考慮する。ただし原因を調べずnullチェックだけで症状を隠さない。
- `Update` 内の不要な重処理、不必要なGC Alloc、Find系APIの乱用を避ける。
- イベント購読は解除まで含め、重複購読や破棄済み参照を残さない。
- MVP段階では、過剰な抽象化より単純で堅牢な実装を優先する。
- 公開API追加は必要最小限にし、既存の責務を保つ。

## 検証と完了条件

- 可能な範囲で実装後にC#コンパイル、Unity Console、対象Scene/Prefab参照、Play Mode挙動、Git差分を確認する。
- Console Errorを残した状態で完了扱いにしない。実行環境の制約で確認できない場合は、未検証項目と理由を明記する。
- ゲーム機能を変更した場合は、対象機能だけでなく近接する既存ループへの回帰も確認する。
- Scene/Prefab/ゲームコードを変更していない作業では、Unityに不要な再保存をさせない。
- 完了前に `git diff` と未追跡ファイルを確認し、意図しないScene、Prefab、Import設定、ProjectSettings、バイナリ、自動生成物がないことを確認する。
- ユーザーの依頼なしにcommit、push、branch切替を行わない。

## Project State更新ルール

作業終了前に `docs/CODEX_PROJECT_STATE.md` を確認する。次の変更で内容が古くなった場合だけ、現在状態を短く正確に更新する。

- 新システムまたは新しい依存関係を追加した。
- システム構造、重要な設計判断、主要なデータフローを変更した。
- 重要なバグを修正し、再利用可能な制約や既知問題が変わった。
- 主要Sceneまたは主要Prefabを追加・置換・役割変更した。
- 検証方法や利用可能なテスト環境が変わった。

typo、小さな数値変更、一時デバッグ、構造に影響しない微細変更では更新不要。巨大な時系列ログにせず、古い状態や解決済み問題を整理する。

## Memoryへ残す判断

デバッグで得た再利用可能な知見、繰り返し起きる問題、失敗した方法、Project_Dig固有の注意点はCodex Memories向きである。現在のScene構成や一時的なブランチ状態はProject Stateへ置く。Memory更新権限や利用可否が不明な場合は、勝手にユーザー設定を変更せず最終報告で説明する。
