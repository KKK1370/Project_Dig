# BORN TO DIG ボクセル岩MVP

## いちばん簡単な確認方法

1. Unityで `Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP` を開きます。
2. 上部のPlayボタンを押します。
3. 画面中央の「+」を岩に合わせ、左クリックします。
4. 同じ場所を続けてクリックすると、凹みが深くなり、最後は貫通します。
5. マウスで視点変更、W/A/S/Dで移動、Spaceでジャンプできます。
6. Escでカーソルを解除できます。画面をクリックすると操作へ戻ります。

## シーンを作り直す方法

Unity上部メニューの
`Tools > BORN TO DIG > Create Voxel Rock MVP Scene`
を選択してください。

既にあるシーンへFPSプレイヤーとの互換設定だけを適用する場合は、
`Tools > BORN TO DIG > Integrate FPS Player With Voxel Rock Scene`
を選択してください。

## 主なInspector設定

- Voxel Rock / Resolution: `48`
- Voxel Rock / Iso Level: `0.5`
- Player Camera / Mining Distance: `4`
- Player Camera / Mining Radius: `0.2`
- Player Camera / Mining Strength: `0.75`

## モデルについて

- 原本GLB: `Assets/BornToDig/VoxelRock/Source/BORN_TO_DIG_Rock.glb`
- Unityで実際に参照するモデル: `Assets/BornToDig/VoxelRock/Models/BORN_TO_DIG_Rock.fbx`

Unity標準ではGLBを直接モデルとして読み込めないため、同じ形状をFBXへ変換して使用しています。
FBXのRead/Writeと単位倍率は、シーン作成ツールが自動設定します。

## 既存プログラムとの共存

- `VoxelRockMVP`では既存の`MVP_FPS_Player`を使用します。
- カメラ、AudioListener、照準は各1個だけです。
- 旧`FlyCameraController`と旧`ClickableVoxelRock`はこのシーンでは動かしません。
- `SampleScene`とその既存オブジェクトは変更しません。
