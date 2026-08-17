# BORN TO DIG ボクセル岩MVP

## いちばん簡単な確認方法

1. Unityで `Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP` を開きます。
2. 上部のPlayボタンを押します。
3. 画面中央の「+」を岩に合わせ、左クリックします。
4. 同じ場所を続けてクリックすると、凹みが深くなり、最後は貫通します。
5. 右クリックを押しながらマウスを動かすと視点を変更できます。
6. 右クリック中はW/A/S/Dで移動、Q/Eで上下移動できます。

## シーンを作り直す方法

Unity上部メニューの
`Tools > BORN TO DIG > Create Voxel Rock MVP Scene`
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
