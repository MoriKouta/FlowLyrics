# FlowLyrics Codex Local handoff

更新日: 2026-09-20

## 現在の状態

- リポジトリ: `MoriKouta/FlowLyrics`
- 引き継ぎ先ブランチ: `develop/1.3.1`
- アプリケーション実装の基準コミット: `0a2b1f6a5bfaec90ea056e7d02a2f6a64903e29a`
- 基準コミットの件名: `Refine Personal Sync visual workflow`
- 開発版: `1.3.1-dev.10`
- 正式版: `1.3.0`

この文書と `LOCAL_ASSET_MANIFEST.md` は上記アプリケーション基準コミットの後に追加する移行用文書である。そのため、文書を含む最新HEADは固定値を本文へ自己参照させず、次で確認する。

```powershell
git rev-parse --verify HEAD
git status --short --branch
```

`main` へ直接pushしない。開発は `develop/1.3.1` で継続し、正式版、タグ、GitHub Releaseはメンテナーが明示的に承認するまで作成しない。

## 現在の開発目的

Windows向けWPF歌詞オーバーレイ FlowLyrics の `1.3.1` 開発を継続している。現在の中心は次の3点である。

1. Spotify専用だったWindows Media Session処理を、Apple Music、VLC、TIDAL、主要ブラウザなどにも使える汎用実装へする。
2. LRCLIB検索、メタデータ補正、キャッシュ、再生・シーク・音量操作を複数プレイヤー環境で安定させる。
3. 元のLRCLIB／ローカルLRC／キャッシュを変更せず、曲・再生元ごとに歌詞タイミングを補正できる Personal Sync と、文字後方Glowを実用レベルにする。

## 現在実装済みの内容

### Media Session / LRCLIB

- Windows GSMTCのプレイヤー非依存プロバイダー、AUTO選択、Preferred Playerフォールバック、除外リスト。
- プレイヤー名、ソース、raw／正規化後メタデータ、能力を確認できる診断画面。
- Apple Musicのアーティスト欄へ混入したアルバム名の補正。
- YouTubeの公式MV表記、引用符、区切り、日英韓の別名から、タイトル／アーティスト候補を順位付け。
- LRCLIB `/api/search` の共通URI生成、1回だけのURLエンコード、Title＋Artist＋Album → Title＋Artist → Titleの段階検索、ID重複除去、候補スコアリング。
- プレイヤーを跨いで共有できる曲ID、歌詞キャッシュ、手動LRCLIB選択。古いキーの移行も実装済み。
- 非ゼロのタイムライン原点を持つプレイヤーへのシーク、UI Automationフォールバック、アプリ別音量・ミュート。
- plain歌詞を全文連続スクロールし、手動スクロール後は現在位置を基準に自動スクロールへ戻す挙動。

### Personal Sync

- `1.3.1-dev.8` で導入し、`dev.9` と `dev.10` でUIと操作を再設計済み。
- LRCLIB、ローカルLRC、既存キャッシュのタイムスタンプを変更しない非破壊マッピング。
- 再生元ごとのProfileを優先し、同じ曲の全再生元Profileへフォールバックする。歌詞ID／内容IDが異なるProfileは検出するが自動適用しない。
- `+0.1s` は「歌詞を0.1秒遅く表示」の意味。変換は概ね `lyricsTime = playbackTime - offset`。
- 選択した歌詞行を現在再生位置へ合わせる操作、`±0.1秒`／`±0.5秒`ボタン、Undo／Redo、Reset。
- 移動可能な通常ウィンドウ。オーバーレイ横に置ける場合は自動配置する。
- 上から下へ流れる縦タイムライン、同期点、歌詞停止区間、現在位置の可視化。
- 「ここから歌詞を止める」→再開させたい歌詞行を選択→その歌詞が聞こえた瞬間にオレンジボタンを再度押す、という停止・再開操作。
- 停止区間と再開同期点を関連IDで結び、停止終了時刻の編集時に再開同期点も連動。
- 時刻の文字入力欄を廃止し、編集も `±0.1秒`／`±0.5秒`ボタンに統一。
- 開いて何も変更せず閉じた場合はProfileを保存しない。
- プレイヤー操作部にReverseの `R` と同じ16.5px系統のドットフォント `S` ボタン。
- 検索可能な履歴ウィンドウ。サービス、再生アプリ、元タイトル、投稿者、変更点を表示。
- ブラウザがURLをGSMTCへ公開しない場合は、`YouTube · Google Chrome（推定）`のように推定と確定を区別。
- 保存先は `%APPDATA%\FlowLyrics\personal-sync\profiles.json`。一時ファイルからの原子的置換と、破損JSONの退避を実装済み。現行Schemaはv2。

### Color / Glow / Settings

- 文字本体ではなく文字の背面レイヤーへGlowを付与。
- Glowの色、ぼかし量、濃さを設定可能。
- curated preset、random、保存パレット、`.flowpalette` import/exportへGlow設定を含める。
- SettingsのColorタブで `TEXT EFFECTS` と `SURFACE` を別カードへ分離。
- 復元BAMLへ実行時にUIを追加する経路があるため、Glow UIの取り付け失敗を黙って成功扱いしない契約チェックを追加。

## 未完了の内容

- `dev.10` の実機UX確認が未完了。Windows上で実際のプレイヤー、DPI、ウィンドウ配置を使い、下記の再現手順を人間が確認する必要がある。
- ATEEZ「BAD」公式MVを使った「VICTORIAで停止し、間の後に `She's so bad` から再開」の一連操作は、変換ロジックとUI構築テストは通っているが、ユーザーによる最終確認前である。
- Glow表示、Glow設定の実表示、`TEXT EFFECTS`／`SURFACE` の分離も、`dev.10` で修正してCIのUI構築テストは通ったが、ユーザーによる目視確認前である。
- Personal Syncの新規画面は日本語／英語の分岐が中心で、既存アプリが対応する全10言語への完全翻訳は未実施。
- `1.3.1` はまだ正式リリースしていない。次の確認版を作る場合は `1.3.1-dev.11` とする。
- GitHub Actions #28の `dev.10` artifactは14日保持のため既に期限切れ。必要ならworkflowを再実行するか、Windowsローカルで再ビルドする。

## 既知の不具合・制限

現HEADで再現が確定している致命的不具合は記録されていない。ただし次は既知の制限または未検証項目である。

- Windows GSMTCがブラウザのページURLを渡さない場合、YouTube判定はタイトル、投稿者、検索候補などからの推定になる。履歴では必ず「推定」と表示する。
- 排他的フルスクリーンではオーバーレイが最前面にならない場合がある。
- 排他モード音声ではWindowsがアプリ別音量を公開しないため、音量操作を無効化する。
- スマートフォンや別PCだけで再生しているメディアには追従できない。
- Work環境には.NET SDKがなく、今回ローカルでbuild/testは再実行できていない。最後のWindows CI結果を基準にしている。
- 最終CIには `CS4014` が `FlowLyrics/MainWindow.cs` の3箇所、`CS0649` が `FlowLyrics.Interop/NativeMethods.cs` のRECTフィールド4箇所に残る。CIは成功しているが、次回関連箇所を触る際に意図を確認する。
- UIスモークテストはWPFウィンドウの構築、テーマ、主要コントロールの存在を検証するもので、視認性や操作感を完全には保証しない。

## 直近で決定した仕様

- Personal Syncは歌詞を見ながら使える移動可能なウィンドウとし、歌詞オーバーレイをなるべく隠さない位置へ出す。
- 時刻をユーザーに文字入力させない。基本調整もイベント編集も `±0.1秒`／`±0.5秒`で行う。
- 停止／再開は、停止開始、再開歌詞の選択、実際に聞こえた瞬間の確定を一続きの操作にする。
- タイムラインは歌詞の流れと同じ上→下方向にする。
- 「ここから歌詞を止める」を「ここから選択した歌詞へ切り替える」より先に配置する。
- Syncの `S` はReverseの `R` と同じ視覚サイズ・配置にする。
- 履歴一覧には全ProfileをLyricsタブへ直接展開せず、現在曲の概要と履歴ウィンドウへの導線だけを置く。
- 履歴はブラウザ名とサービス名を分け、確定できないYouTube判定は推定と明記する。
- Glowは文字そのものをぼかさず、文字背面の複製レイヤーへ適用する。
- `TEXT EFFECTS` と `SURFACE` は同じカード内の見出し分割ではなく、独立カードにする。
- Personal Syncを開閉しただけでは同期済み状態にしない。

## 重要な設計判断

- 編集可能なGitソースが唯一の開発元。EXE、portable ZIP、`bin`、`obj`、`publish`、復元済みバイナリからソースを再構築しない。
- `FlowLyrics.g.resources` に元のWPF BAMLがあり、一部UIは読み込み後にコードで注入する。`FlowLyrics.SettingsWindow.xaml` だけを変更しても実行時UIへ反映されない場合があるため、`FlowLyrics/SettingsWindow.cs` の実行時構築経路とテストも確認する。
- Personal SyncはLRC本文を変更せず、再生時刻を歌詞時刻へ写像する純粋関数として `PersonalSyncMapper` に隔離する。
- Profile解決順は「同じ曲＋同じ歌詞＋同じ再生元」→「同じ曲＋同じ歌詞＋全再生元」。異なる歌詞用Profileは自動適用しない。
- 再生元の安定IDとユーザー表示用のProviderラベルは分離する。推定Providerを確定情報として保存・表示しない。
- Personal SyncのJSONはLRCLIBキャッシュと分離し、原子的に保存する。
- コアの正規化、選択、時間変換はUIから分離し、Windows依存部分を増やしすぎない。
- CIはWindowsと.NET 10を正とする。確認版はself-contained、single-file、win-x64、trimなし。

## 関連する主要ファイル

| ファイル | 役割 |
| --- | --- |
| `FlowLyrics.csproj` | WPFアプリのビルド設定、.NET 10、Windows Desktop参照、埋め込みリソース |
| `.github/workflows/development-build.yml` | `develop/1.3.1` のtest、win-x64 publish、artifact作成 |
| `FlowLyrics/MainWindow.cs` | 再生状態、歌詞表示、Personal Sync適用、`S`ボタン、Glow描画 |
| `FlowLyrics/PersonalSyncWindow.cs` | 移動可能なPersonal Sync本体、歌詞選択、停止／再開、縦タイムライン、編集 |
| `FlowLyrics/PersonalSyncManagerWindow.cs` | 履歴検索、Profile一覧、詳細表示 |
| `FlowLyrics/PersonalSyncUiTheme.cs` | Personal Syncウィンドウ共通テーマ、ボタン、リスト、スクロールバー |
| `FlowLyrics.Models/PersonalSyncModels.cs` | Profile、Anchor、Hold、Source、Schema v2 |
| `FlowLyrics.Core/PersonalSyncMapper.cs` | 再生時刻→歌詞時刻の純粋変換 |
| `FlowLyrics.Services/PersonalSyncIdentity.cs` | 曲、再生元、歌詞の安定ID作成 |
| `FlowLyrics.Services/PersonalSyncSourceClassifier.cs` | YouTube／Browser media／アプリの表示分類と推定フラグ |
| `FlowLyrics.Services/PersonalSyncStore.cs` | Profile解決、保存、破損退避、優先順位 |
| `FlowLyrics/SettingsWindow.cs` | Glow、Color、履歴導線、Media Session UIの実行時構築 |
| `FlowLyrics.SettingsWindow.xaml` | Settingsの編集可能なXAML表現。ただし復元BAML経路にも注意 |
| `FlowLyrics.Models/AppSettings.cs` | Glowを含む設定値と正規化 |
| `FlowLyrics.Models/ColorPalettes.cs` | Glowを含む標準プリセット |
| `FlowLyrics.Controls/OutlinedText.cs` | 歌詞文字の描画 |
| `FlowLyrics.Services/LyricsService.cs` | LRCLIB取得、候補検索、フォールバック、HTTP診断 |
| `FlowLyrics.Core/ProviderMetadataRepair.cs` | Apple Music／YouTubeのメタデータ補正候補 |
| `FlowLyrics.Services/MediaSessionService.cs` | セッション列挙、選択、フォールバック |
| `FlowLyrics.Services/WindowsMediaSessionProvider.cs` | Windows GSMTC実装 |
| `FlowLyrics.Tests/CoreBehaviorTests.cs` | 48件の回帰テストとWPF UI構築スモークテスト |
| `CHANGELOG.md` | `1.3.1-dev.1`〜`dev.10`の変更記録 |
| `DEVELOPMENT.md` | バージョン方針、BAML制約、ビルド方針 |

## 再現・実機確認手順

### Personal Syncの停止／再開

1. Windows 10 1809以降またはWindows 11で `develop/1.3.1` をbuildする。
2. Chrome等で ATEEZ「BAD」公式MVを再生し、同期歌詞を表示する。
3. プレイヤー部のドット文字 `S` を押す。ウィンドウが移動可能で、歌詞オーバーレイを避けた位置へ出ることを確認する。
4. `VICTORIA` の歌詞で「ここから歌詞を止める」を押す。プレビューがその歌詞で固定されることを確認する。
5. 右側の歌詞一覧で再開させたい `She's so bad` の行を選ぶ。
6. 実際に `She's so bad` が聞こえた瞬間にオレンジの再開ボタンを押す。
7. 停止区間と再開同期点が一度に作成され、縦タイムラインと編集一覧に見えることを確認する。
8. 停止終了を `±0.1秒`／`±0.5秒`で編集し、対応する再開同期点も連動することを確認する。
9. 閉じて同じ曲を再生し直し、同じ再生元でProfileが再適用されることを確認する。
10. 別の歌詞IDを選び、以前のProfileが自動適用されず「別の歌詞用」として検出だけされることを確認する。

### 未操作時の保存

1. Personal Sync未設定の曲で `S` を押す。
2. 何も変更せず閉じる。
3. `S` が同期済み表示にならず、履歴にも空Profileが増えないことを確認する。

### Glow / Settings

1. Settings > Colorを開く。
2. `TEXT EFFECTS` と `SURFACE` が別カードであることを確認する。
3. Glow色、Glow Blur、Glow Opacityが表示されることを確認する。
4. Glow BlurとOpacityを上げ、文字本体ではなく背面だけが発光することを確認する。
5. preset、random、保存パレット、export/import後にもGlow値が維持されることを確認する。

### 履歴と再生元

1. 同じ曲をSpotify、ブラウザ内YouTube、ブラウザ内の別メディアで再生してProfileを作る。
2. Lyrics > Personal Syncから履歴ウィンドウを開く。
3. 検索、曲選択、変更点の詳細表示を確認する。
4. `YouTube · Google Chrome（推定）` と `Browser media · Google Chrome` が適切に区別されることを確認する。

## Build / Test方法

前提はWindows x64と.NET 10 SDKである。リポジトリルートで実行する。

```powershell
dotnet --info
dotnet restore .\FlowLyrics.csproj
dotnet build .\FlowLyrics.csproj -c Release
dotnet test .\FlowLyrics.Tests\FlowLyrics.Tests.csproj -c Release --logger "console;verbosity=normal"
```

自己完結portable build:

```powershell
$publish = Join-Path $PWD 'publish'
dotnet publish .\FlowLyrics.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o $publish `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -p:DebugSymbols=false `
  -p:DebugType=None
```

最後に確認済みのCIは [GitHub Actions #28](https://github.com/MoriKouta/FlowLyrics/actions/runs/32651775757)。基準コミット `0a2b1f6a5bfaec90ea056e7d02a2f6a64903e29a` をWindowsでcheckoutし、48/48テスト、publish、artifact uploadが成功した。artifact自体は期限切れである。

## 次に着手すべき内容

1. Codex Localでリポジトリをcloneし、必ず `develop/1.3.1` をcheckoutして `git pull --ff-only` する。
2. .NET 10 SDKを確認し、上記のbuild/testをローカルで実行する。
3. 新機能追加より先に、上記3系統の実機確認を行う。特にATEEZ「BAD」の停止／再開、Glowの実表示、Settingsカード分離を確認する。
4. 問題があれば、再現条件、スクリーンショット、`%APPDATA%\FlowLyrics\logs`、`personal-sync\profiles.json` の該当Profileを個人情報を除いて記録する。
5. 修正時は関連範囲を限定し、`FlowLyrics.Tests/CoreBehaviorTests.cs` に回帰テストを追加する。
6. 次の確認版を作る場合のみ、`BuildInfo.cs`、`Properties/AssemblyInfo.cs`、Settings表示、README、CHANGELOG、workflow artifact名を `1.3.1-dev.11` へ揃える。
7. `develop/1.3.1` へpushしてWindows Actionsを通す。`main`、tag、Releaseは触らない。

## ローカル素材

GitHubへ追加していないWork内の画像、仕様Markdown、過去build artifactは `docs/LOCAL_ASSET_MANIFEST.md` を参照する。いずれもソースのbuild/testに必須ではなく、バイナリや画像はGitへ追加しない。
