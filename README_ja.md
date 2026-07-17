# FlowLyrics

[English](README.md) | **日本語**

## ダウンロード

[最新版のFlowLyricsをダウンロード](https://github.com/MoriKouta/FlowLyrics/releases/latest)

Releaseページの **Assets** から `FlowLyrics-vX.X.X-win-x64-portable.zip` をダウンロードしてください。GitHubが自動表示する `Source code (zip)` と `Source code (tar.gz)` はアプリ本体ではありません。

Windows版Spotifyで再生中の曲を検出し、同期歌詞を透明な常時最前面ウィンドウに表示するWindows専用アプリです。

Spotify Web APIやSpotify Developerアカウントは使いません。WindowsのGlobal System Media Transport Controls（SMTC）から、Spotifyアプリが公開している曲名・アーティスト・再生位置を取得します。歌詞はLRCLIBから検索し、取得済みの結果をPC内にキャッシュします。

## 主な機能

- Windows版Spotifyの曲変更・一時停止・シークへ自動追従
- LRCLIBのタイムスタンプ付き同期歌詞（表記揺れを含む複数検索を並列実行）
- 同期歌詞がない場合は通常歌詞を「簡易タイミング」と明示して表示可能
- 背景完全透過、色付き半透明パネルの両方に対応
- 常に手前、1〜12行表示、現在行の位置を上／中央／下から選択
- 長い歌詞の自動改行と、ウィンドウに合わせた自動文字縮小
- 10種類の全体カラープリセット、個別色指定、全体ランダムカラー
- 現在行を太字、前後行を個別の濃さ・サイズで表示
- フォント、透明度、縁取り、影、行間、余白、角丸を変更可能
- 外枠、曲名、再生位置バー、再生ボタンを個別に表示／非表示
- Spotifyの再生／一時停止、前の曲、次の曲をオーバーレイから操作
- ロック時はクリックを背後のアプリへ通す
- ドラッグ移動、4隅のグリップでサイズ変更
- 曲ごとの歌詞タイミング補正
- 手持ちの `.lrc` ファイルを曲へ登録
- 設定画面からローカルLRCフォルダを開き、追加・更新を自動検出
- タスクトレイ常駐、Windows自動起動、グローバルショートカットのON/OFF
- 英語を初期値とする多言語UI
- Spotifyの認証情報・パスワード・Client ID不要

## 対応環境

- Windows 10 1809以降、またはWindows 11
- Windows版Spotifyデスクトップアプリ
- インターネット接続（新しい歌詞を検索するときだけ）
- ソースから実行する場合のみ [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

SpotifyはMicrosoft Store版・Spotify公式配布版の両方を、アプリIDに `spotify` を含むメディアセッションとして検出します。

## 最初の起動

1. ReleaseのAssetsから `FlowLyrics-v1.1.1-win-x64-portable.zip` をダウンロードして展開します。
2. `FlowLyrics.exe` をダブルクリックします。.NETの追加インストールは不要です。
3. Windows版Spotifyで曲を再生します。
4. 歌詞ウィンドウを好きな場所へドラッグし、いずれかの角でサイズを調整します。
5. 三点ボタン、または右クリックで設定を開きます。
6. 配置後、`Ctrl + Alt + L` でロックします。

Windows SmartScreenが表示された場合は、配布元を確認したうえで「詳細情報」から実行できます。本アプリはコード署名証明書を付けていない個人ビルドです。

## 操作

| 操作 | 内容 |
| --- | --- |
| ドラッグ | ウィンドウ移動 |
| 4隅のいずれかをドラッグ | サイズ変更 |
| 右クリック | 設定、歌詞再取得、タイミング調整 |
| 画面下のボタン | 前の曲／再生・一時停止／次の曲 |
| `Ctrl + Alt + L` | ロック／ロック解除 |
| `Ctrl + Alt + K` | 表示／非表示 |
| トレイアイコンをダブルクリック | 表示／非表示 |

ロック中はマウス操作が背後のNuke、ブラウザなどへそのまま通ります。解除はグローバルショートカットかタスクトレイから行います。

## 歌詞が出ない場合

1. Spotifyの曲名がWindowsの音量・メディアパネルに表示されるか確認します。
2. FlowLyricsを終了し、Spotifyを再起動してからFlowLyricsを起動します。
3. 右クリックから「歌詞を再取得」を試します。
4. タイムスタンプ付きLRCがあれば、Settingsの「Local LRC」からフォルダを開いて追加します。

LRCフォルダは `%AppData%\FlowLyrics\lyrics-cache` です。ファイル名を `アーティスト - 曲名.lrc` または `曲名.lrc` にすると、再生中の曲へ自動照合されます。ファイルの追加・更新・削除は起動中でも自動検出します。

## 保存場所

設定と歌詞キャッシュは次に保存されます。

```text
%APPDATA%\FlowLyrics
```

Spotifyのログイン情報は保存しません。

## 制限

- Spotifyスマホアプリや別PCだけで再生している曲には追従しません。
- 排他的フルスクリーンのゲームより前には表示できない場合があります。
- SpotifyまたはWindows側がメディアセッションを公開しない状態では検出できません。
- LRCLIBに同期歌詞がない曲は、手動LRCが必要です。
- 再生操作はSpotifyがWindowsメディアセッションに許可した操作だけ有効になります。

## プライバシー

曲名、アーティスト名、アルバム名、曲尺だけをLRCLIBへ送信します。音声データ、Spotifyアカウント、再生履歴一覧は送信しません。サーバーやデータベースは使用しません。

## 開発構成

- C# / WPF / .NET 10
- `Windows.Media.Control`によるSMTC取得
- LRCLIB REST API
- Win32拡張スタイルによるクリック透過
- 外部NuGetパッケージなし

ライセンスや外部サービスについては `THIRD_PARTY_NOTICES.md` を参照してください。
