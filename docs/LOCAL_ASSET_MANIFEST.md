# FlowLyrics local asset manifest

更新日: 2026-09-20

この一覧は、Workの一時ワークスペースには存在したが、GitHubリポジトリには追加していない素材を記録する。EXR、画像、音声、portable ZIP、EXEなどの大きなバイナリはGitへ追加しない。

結論として、現在のソースをbuild/testするために必須のローカル素材はない。以下は過去の不具合記録、見た目の参考、指示書、生成物である。

## UI参考画像

元のWork上の場所は、リポジトリから見て `../upload/` だった。Codex Localへ自動では移らないため、ピクセル単位の比較が必要なら別途コピーする。

| 元ファイル名 | サイズ | SHA-256 | 内容 | 必須性 |
| --- | ---: | --- | --- | --- |
| `734a7102-25ac-49ca-a9a4-f316b1c4ed0c.png` | 38,274 bytes | `5da4654ba2e260782a71866b2600cee0e311e75957c25a5be2262184f8bb9a22` | 白文字の背面に柔らかい発光があるGlow参考 | 目視比較用、build不要 |
| `89745aaf-cb56-4181-9c6f-fec30a93a266.png` | 58,794 bytes | `449c6354b7f53e08b124ecf4a30a5991b19642a5625685673812265f742ab3fc` | `dev.8`頃の小型「歌詞タイミング」Popup。歌詞へ被る・操作が分かりにくい問題の記録 | 履歴用、build不要 |
| `8c030dd8-b1cb-4710-928b-11fbd503fb0b.png` | 126,161 bytes | `4b6370d5ee84a86fd7002fef0d8e1f1a971a29d59a91d2e389e16272c2711ea8` | FlowLyricsオーバーレイと縦型Volume Popup。位置・DPI・操作部の見た目確認 | 目視比較用、build不要 |
| `8c306b49-e61e-4ca7-a6b0-ef501fd4e550.png` | 83,513 bytes | `f73dea5e42dd86ae7fbb890f6606fe75fc4b67dc7790fc0ff5ebd6c30bf3c18d` | Settingsの再生ソース／除外メディアソース画面 | 目視比較用、build不要 |
| `eb171512-ff15-4f1d-b05c-b2fc1c1abde0.png` | 201,613 bytes | `09eaa2c5d27cb72171d9a258eaa77e533eeabbcd8f667b966dcc83f703eff53a` | ブラウザ／YouTube系の混在言語歌詞とVolume Popupの表示記録 | 目視比較用、build不要 |

直近のPersonal SyncレビューでWork会話へ添付された4枚のスクリーンショットは、現在のチェックアウト内には独立ファイルとして存在しない。要求内容は `CODEX_HANDOFF.md` の「直近で決定した仕様」と再現手順へ文章で移した。元画像を使った比較が必要な場合だけ、Work会話から手動で保存する。

## 仕様Markdown

2組とも同内容の重複ファイルがある。すでに実装へ反映された過去指示で、現在のbuildには不要である。

| 元ファイル名 | サイズ | SHA-256 | 内容 |
| --- | ---: | --- | --- |
| `貼り付けたマークダウン（1）(2).md` | 23,291 bytes | `bb5a3642c32b1683e51b9a04916772b6b4607cba36b6dee59dd77a924104be23` | LRCLIB検索修正とWindowsマルチプレイヤー対応の実装指示 |
| `貼り付けたマークダウン（1）(3).md` | 23,291 bytes | `bb5a3642c32b1683e51b9a04916772b6b4607cba36b6dee59dd77a924104be23` | 上記の重複 |
| `貼り付けたマークダウン（2）.md` | 21,208 bytes | `738aa97a016dcabe85944a419efd0d598ee8262de113f52f0f6fae65a925455a` | Volume Popup、Reverse Colors、Media Session診断、Preferred Player、Core分離などの追加指示 |
| `貼り付けたマークダウン（2）(1).md` | 21,208 bytes | `738aa97a016dcabe85944a419efd0d598ee8262de113f52f0f6fae65a925455a` | 上記の重複 |

## 過去の生成物

Work一時領域には `1.3.1-dev.1`〜`dev.9` のportable ZIP、Actionsから取得した二重ZIP、展開済みEXEが残っていた。これらは再現可能な生成物であり、開発元として使用せず、Gitへ追加しない。

確認できた主な名前:

- `FlowLyrics-v1.3.1-dev.1-actions-artifact.zip`
- `dev131-artifact/FlowLyrics-v1.3.1-dev.1-win-x64-portable.zip`
- `dist/FlowLyrics-v1.3.1-dev.2-win-x64-portable.zip`
- `dist/FlowLyrics-v1.3.1-dev.2-win-x64-portable-actions.zip`
- `dist-dev3/FlowLyrics-v1.3.1-dev.3-win-x64-portable.zip`
- `dist-dev3/FlowLyrics-v1.3.1-dev.3-win-x64-portable-actions.zip`
- `FlowLyrics-v1.3.1-dev.4-win-x64-portable.zip`
- `FlowLyrics-v1.3.1-dev.4-win-x64-portable-artifact.zip`
- `FlowLyrics-v1.3.1-dev.5-actions-artifact.zip`
- `dev5-artifact-9245366726/FlowLyrics-v1.3.1-dev.5-win-x64-portable.zip`
- `dev9-artifact/FlowLyrics-v1.3.1-dev.9-win-x64-portable.zip`
- `dev9-artifact/actions-artifact.zip`

`dev.10` のGitHub Actions artifact（artifact ID `9496404890`）は作成時に78MB弱で正常だったが、14日保持のため期限切れである。現在のWorkワークスペースにも `dev.10` ZIP本体は残っていない。必要なら `develop/1.3.1` でworkflowを再実行して再生成する。

## Gitへ追加しないもの

- `*.zip`
- `*.exe`
- `bin/`
- `obj/`
- `publish/`
- EXR、音声、動画
- 上記の参考PNG

現在の `.gitignore` はZIP、EXE、主要build出力を除外している。将来ローカル素材用フォルダを作る場合も、誤って追跡されていないことを `git status --short --untracked-files=all` で確認する。
