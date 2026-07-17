# FlowLyrics v1.1.1

[English](#english) | [日本語](#日本語)

## English

FlowLyrics v1.1.1 improves localization, local LRC handling, playback controls, font selection, and general usability.

### Changes

- Added a multilingual interface with English as the default language
- Added Japanese, Chinese, Korean, Spanish, French, German, Portuguese, and Russian UI options
- Added local LRC folder management in Settings
- Automatically detects added, updated, and removed `.lrc` files while the app is running
- Improved local LRC matching using `Artist - Song Title.lrc` and `Song Title.lrc` file names
- Improved playback-control layout and skip-button appearance
- Removed the behavior that opened Settings by double-clicking the overlay
- Improved font selection so font names are previewed using their corresponding fonts
- Improved lyric search and matching for title variations, featured artists, live versions, remasters, and alternate editions
- Added approximate timing for plain lyrics when synchronized lyrics are unavailable
- Added configurable lyric-line count and current-line position
- Added individual visibility controls for the frame, song title, progress bar, and playback controls
- Improved automatic wrapping and font-size adjustment
- Fixed minor UI and stability issues

### Download

Download `FlowLyrics-v1.1.1-win-x64-portable.zip` from **Assets** below.

The automatically generated `Source code (zip)` and `Source code (tar.gz)` files are not the Windows application.

### Updating from an Earlier Version

1. Close FlowLyrics completely from the system tray.
2. Download and extract the new portable ZIP.
3. Run the new `FlowLyrics.exe`.

Settings and cached lyrics are stored in `%APPDATA%\FlowLyrics`, so they should remain available when replacing the application folder.

Windows SmartScreen may appear because this build is not code-signed. Verify that the file was downloaded from this repository before running it.

---

## 日本語

FlowLyrics v1.1.1では、多言語対応、ローカルLRC管理、再生操作、フォント選択、全体的な操作性を改善しました。

### 変更内容

- 英語を初期値とする多言語UIを追加
- 日本語、中国語、韓国語、スペイン語、フランス語、ドイツ語、ポルトガル語、ロシア語に対応
- SettingsからローカルLRCフォルダを管理できる機能を追加
- 起動中の `.lrc` ファイル追加・更新・削除を自動検出
- `アーティスト - 曲名.lrc` および `曲名.lrc` によるローカルLRC照合を改善
- 再生コントロールの配置と前後スキップボタンの表示を改善
- オーバーレイのダブルクリックで設定画面が開く動作を廃止
- フォント一覧を各フォントの表示で確認できるよう改善
- feat.表記、ライブ、リマスター、別バージョンなどを含む歌詞検索・候補判定を改善
- 同期歌詞がない場合に通常歌詞を簡易タイミングで表示する機能を追加
- 歌詞の表示行数と現在行の位置を設定可能に変更
- 外枠、曲名、再生位置バー、再生ボタンの個別表示設定を追加
- 長い歌詞の自動改行と文字サイズ自動調整を改善
- UIおよび安定性に関する軽微な不具合を修正

### ダウンロード

下の **Assets** から `FlowLyrics-v1.1.1-win-x64-portable.zip` をダウンロードしてください。

GitHubが自動表示する `Source code (zip)` と `Source code (tar.gz)` はアプリ本体ではありません。

### 旧バージョンからの更新方法

1. タスクトレイからFlowLyricsを完全に終了します。
2. 新しいポータブルZIPをダウンロードして展開します。
3. 新しい `FlowLyrics.exe` を起動します。

設定と歌詞キャッシュは `%APPDATA%\FlowLyrics` に保存されるため、アプリフォルダを置き換えても基本的に引き継がれます。

本ビルドはコード署名されていないため、Windows SmartScreenが表示される場合があります。このリポジトリからダウンロードしたファイルであることを確認してから実行してください。
