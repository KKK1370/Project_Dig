# BORN TO DIG / Project_Dig — Codex 開発ルール

## Project Identity

- Project name: **BORN TO DIG**
- Unity project: **Project_Dig**
- プロジェクトルートは `Assets/`、`Packages/`、`ProjectSettings/` を含むディレクトリとする。

## 作業開始時の情報取得順序

1. この `AGENTS.md` を全文読む。
2. `docs/CODEX_PROJECT_STATE.md` が存在する場合は読む。
3. 依頼に合う `.agents/skills/` 配下のSkillを読む。
4. 依頼に直接関係するScene、Prefab、スクリプト、設定だけを確認する。

`CODEX_PROJECT_STATE.md` より実際のファイルが新しい、または両者が矛盾する場合は、推測で進めず実体を確認する。確認後、必要な場合だけProject Stateを現在状態へ更新する。

## 4層の知識管理

- **Codex Memories**: デバッグで得た再利用可能な知見、プロジェクト固有の癖、失敗した方法、繰り返す問題、有益な開発パターン。
- **AGENTS.md**: 必ず守る安定ルール、禁止事項、コーディング方針、作業プロセス。
- **.agents/skills/**: 実装、デバッグ、検証など繰り返し使う具体的な手順。
- **docs/CODEX_PROJECT_STATE.md**: 現在のMVP、システム構造、主要ファイル、Scene、Prefab、既知の問題、現在有効な設計判断。

同じ情報を複数層へ大量に重複させない。Memoryが利用できない場合はユーザー設定を勝手に変更せず、必要な有効化設定を最終報告する。

## 変更の基本ルール

- 関係ファイルと依存関係を確認してから変更する。
- 確認できない既存仕様を推測で変更しない。不明点は不明と明示する。
- ユーザーから依頼されていない大規模リファクタリング、機能追加、性能改善を行わない。
- 明示的な依頼なしに、汎用インベントリ、セーブ、複数お宝対応、Scene遷移など現在のMVP外の仕組みを追加しない。
- 変更範囲を最小限にし、既存のUnity C#スタイルと設計を優先する。
- 動作中の採掘パイプラインを不用意に書き直さない。特に `VoxelRock`、`VoxelGrid`、`VoxelMeshVoxelizer`、`MarchingCubes`、`MiningTool` の責務を維持し、必要最小限の公開APIまたは追加コンポーネントで拡張する。
- 失敗した変更や未解決のエラーを隠さない。
- 勝手にcommit、push、branch切替をしない。

## Unity Assetの安全性

- 既存Scene、Prefab、Material、Texture、Model、Animation、Audioを不用意に変更しない。
- SceneまたはPrefabの変更が必要な場合は、既存の参照、Override、調整値を先に確認する。
- `.meta` とGUIDを尊重する。Assetの移動、Rename、削除では参照破壊を確認する。
- Blender、FBX、GLBなどのソースアセットを明示的な依頼なしに破壊的編集しない。
- Builder、Installer、互換設定ツールは既存SceneやPrefabを再生成・上書きし得る。対象と差分を確認せず実行しない。
- Camera、AudioListener、照準、`MiningTool`、プレイヤー、Bootstrap生成物を重複させない。
- 新しいColliderが採掘Rayを遮らないか確認する。Layer、Tag、Trigger、Rigidbodyの既存意図を維持する。
- SceneやPrefabを変更した場合はMissing Script、Missing Reference、SerializedField、Prefab Overrideを確認する。

## コーディング方針

- Unity C#の既存スタイル、namespace、命名、`[SerializeField]` の使い方を優先する。
- null安全性とUnity Objectのライフサイクルを考慮する。
- `Update` 内の不要な重処理、不必要なGC Alloc、`Find` 系APIの乱用を避ける。
- Editor専用コードはRuntimeコードから分離し、必要に応じて `Editor/` または `#if UNITY_EDITOR` を使用する。
- 現在の入力方式を関連コードで確認し、新機能だけ旧 `UnityEngine.Input` を混在させない。入力方式の全面移行は明示的な依頼なしに行わない。
- 過剰設計を避け、MVP段階では単純で堅牢な実装を優先する。
- 既存の互換経路や旧方式を削除する場合は、参照されていないことと削除依頼の範囲を確認する。

## 検証と完了条件

- 変更内容に比例した検証を行う。利用可能な方法は `docs/CODEX_PROJECT_STATE.md` と `born-to-dig-validation` Skillで確認する。
- C# compile errorとUnity Consoleの赤エラーを残した状態で完了扱いにしない。
- 採掘関連変更では、FPS入力、ツルハシ、岩初期生成、反復採掘、穴の深化、MeshCollider更新、既存の宝取得ループへの回帰を確認する。
- UI、Scene、Prefab変更では、Missing Script、参照切れ、重複Camera/AudioListener/照準、Layer、Tag、Collider、Rigidbody、SerializedFieldを確認する。
- Unityを実行できない場合は、静的確認だけを実行したことと未検証項目を明記する。未実行の検証を「確認済み」と書かない。
- 作業終了前に `git diff` と `git status` を確認し、意図しないScene、Prefab、Import設定、ProjectSettings、バイナリ、自動生成ファイルの変更がないことを確認する。

## CODEX_PROJECT_STATE 更新ルール

次の変更を行った場合、作業終了前に `docs/CODEX_PROJECT_STATE.md` を確認する。

- 新システムの追加
- 主要システム構造の変更
- 重要なバグ修正
- Sceneまたは主要Prefabの追加
- 重要な設計判断の変更
- 新しい主要依存関係の追加

内容が古くなった場合だけ更新する。typo修正、小さな数値変更、一時デバッグ、微細な変更では更新不要。Project Stateを変更履歴の羅列にせず、次のCodexセッションが現在地と調査開始点を短時間で把握できる状態を保つ。古い履歴はRecent Significant Changesから整理する。

## Codex Memoriesへ残す判断

- 再利用できる原因・対策、プロジェクト固有の落とし穴、繰り返し役立つ調査手順だけを候補にする。
- 現在のScene構成、主要ファイル、MVP状態、既知の問題はProject Stateへ書く。
- 絶対ルール、禁止事項、作業プロセスはAGENTS.mdへ書く。
- 単発の作業ログ、容易に再取得できる値、コードの大量転載をMemoryへ保存しない。
