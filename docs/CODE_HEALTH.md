# Code health audit

監査日: 2026-09-23。開始点: `30e8cd2cc67bdbc328499daf56ea16825347fd56`。
これはこのrevisionで確認した記録であり、将来のコードにそのまま当てはめない。

## 現在の構造と良い点

114個の追跡C#ファイル、project/CI設定、テスト、4つの大きなclassの主要経路と関連Core/Services/Controlsを横断して監査した。全経路の実プレイヤー試験やセキュリティ保証ではない。

- Coreにmetadata normalization、Recording Identity、Personal Syncの時刻計算、Stop After Trackの判定がある。純粋な入力でテストできる。
- Windows Media Sessionはproviderと選択/stabilization serviceに分離済み。LyricsServiceはWindow/controlを直接参照していない。
- raw/display/matching metadataを分け、誤った自動歌詞選択を拒否するテストがある。
- 永続保存先は歌詞cache、manual選択、Local LRC preference、Sync profileに分かれ、原子的なファイル置換とロックを使用。用途が違うため、同じstoreへ統合しない。
- Glow overlay、CurrentTrackHeader、PlayerControlVisuals、PersonalSyncUiThemeは再利用境界がある。
- GitHub development buildはWindows上で全テストを実行してからportableを生成する。

## 大きいファイルと変更リスク

| 開始時のファイル | 行数 | 実際に集中している仕事 / 判断 |
|---|---:|---|
| MainWindow.cs | 3886 | media更新、歌詞取得/描画、Sync lifecycle、settings preview、volume、seek、click-through、layout。BAML connectorも含む。P1: UIの外観以外の変更理由が多い |
| SettingsWindow.cs | 3445 | runtime BAML補完、翻訳、style生成、preview、player選択、profile/LRC操作。P1: 非同期のsource/profile更新と大量のUI構築が混在 |
| PersonalSyncWindow.cs | 1358 | editor構築、選択/drag/hold/undo、track変更とsave queue。P1: 編集状態と保存lifecycleを跨ぐ変更が複雑 |
| LyricsService.cs | 1268 | 検索順序、採用/cache検証、Local LRC/manual優先度に加えHTTP retry/backoff/request cache。P1: 独立して変更される通信責務が混在 |
| CoreBehaviorTests.cs | 876 | 共通の既存動作テスト。production責務ではない。新しい通信/保存テストは個別のファイルへ追加 |
| CandidateSearchWindow.cs | 689 | 検索進捗とUI。現在の課題はまず上記4classを優先 |
| SystemVolumeService.cs | 589 | COM audioとdispose。長さにはinterop定義も含み、長いことだけでは分割しない |

巨大methodの例: LyricsService.SearchCandidatesAsync（local function込み約249行）、SettingsWindow.ApplySoftSettingsTheme（約169行）、MainWindow.InitializePersonalSyncUi（約128行）、PersonalSyncWindow constructor（約230行）。生成connector、XAML文字列、UI compositionを業務ロジックの複雑さと混同しない。

## 優先度付き所見

| ID / risk | 問題・根拠 | 利用者への影響 / 方針 |
|---|---|---|
| P0 / Critical | 初期監査時点では再現確認した即時の重大障害なし | 「潜在不具合なし」の保証ではない |
| H1 / P1 High（抽出済み） | LyricsServiceがHTTP/cache gate/backoffと歌詞採用を所有。GetJsonWithRetryAsync/Core、ClearTrackCacheAsyncに通信詳細が漏れていた | LrclibClientへ通信責務と状態を移動。LyricsServiceは曲keyとURLを渡し、通信cacheの内部を直接操作しない。採用基準と検索順序は維持 |
| H2 / P1 High | MainWindowは現在曲・歌詞・Sync・Settings・描画の進行管理を集中所有 | 同じfieldを多数のcallbackが更新。完全分割はせず、次回はSync lifecycleをテストで固定してから分離 |
| H3 / P1 High（再現済みbug、修正） | PersonalSyncStore.Upsertだけが失敗時にmemoryを無効化、Delete/DeleteForContextは同じ対処なし。LyricsOverrideStoreのSet/Removeにもmemory先行変更あり | 書込先をロックした6ケース中5件で不一致を再現。各storeのSaveAsyncへ失敗時の無効化を集約し、次回操作でdiskの確定状態を再読込。失敗した選択や削除が別の保存へ混ざる問題を防止 |
| H4 / P1 High | MainWindow.AdjustCurrentTrackOffset/ResetTrackOffset、SettingsWindow.RefreshPersonalSyncProfilesにはawait後のtrack/operation世代確認が一様ではない | 古い処理が次曲UIへ反映される可能性。未再現の構造リスク。UI移行のcharacterizationを伴う別作業へ |
| H5 / P1 High | MainWindow.ExitApplicationとClosedでtimer/event/taskの片付け責務が分散。匿名store/watcher購読、Cancel後の完了待ちが一様ではない | 終了と保存・更新の競合をレビューしにくい。終了テストを先に追加してからlifetimeをまとめる。今回は終了仕様を変えない |
| M1 / P2 Medium | LyricsServiceのcache→LrclibRecord変換がvalidationと表示用で重複 | field追加時のずれ。現在のmappingは等価。次のcache変更時に共通化を検討 |
| M2 / P2 Medium | MainWindowに旧Sync Popupの構築、編集profile、undo/redoが残り、通常Sは別Windowを開く | 古いUIと新UIの境界が読みづらい。reflectionテストも旧fieldsを使用。安易に削除しない |
| M3 / P2 Medium | CreateProfileButton/NudgePersonalSyncProfile、LyricsService.RequestPathは通常ソース呼出が見当たらない | dead候補。BAML/reflection/互換性まで証明していないので削除しない。旧settings/cache移行は現役互換処理 |
| M4 / P2 Medium | Settingsのpalette import/export、起動時settings load、LRC列挙に同期disk処理。WPF継続側のLRC parse/tree走査もある | 大量データ/遅いdiskでUI停止の可能性。計測と入力規模を先に確認。UIAは既にbackground、GetResultはcompleted guardを持つ箇所と区別 |
| M5 / P2 Medium | request gate辞書とAppLoggerのpath lock辞書がprocess中に増える。TrimRequestCacheは期限切れのみ除去 | 長時間の大量検索/多数保存先でメモリ増加余地。今回の抽出では上限やgate削除の意味を変えない |
| M6 / P2 Medium | UiFontなどWPF専用helperがServicesにある。Models/Coreは相互参照する | フォルダは別assemblyではない。Window直接参照の循環は見つからない。移動だけの変更やDIは不要 |
| M7 / P2 Medium | Main/Settings/Syncの色・時刻表示・card等には同じ値も残るが、用途やactive状態が異なる | 全style統合はしない。PlayerControlVisuals等の既存共有点を使う。30/.72/4等を一律置換しない |
| M8 / P2 Medium | 複数のcatchが空、Settings保存/Sync nudgeの失敗が利用者に見えない。反対にoptional UIA/logはbest-effortが妥当 | すべてthrowにしない。ユーザー編集の保存失敗と診断失敗を別に扱う改善が必要 |
| L1 / P3 Low | num/flag変数、冗長な型名、改行・brace差、recoveredコードの不要代入 | 機能と無関係なrename/format sweepは行わない |

provider固有処理はMetadataRepair/UIAに隔離され、曲名を使った新しいハードコードは追加しない。technical headingの英語固定は仕様であり未翻訳bugとは数えない。操作文言には翻訳辞書と10言語テストがあるが、全メッセージの言語品質を保証するものではない。

## 状態の持ち主

| 状態 | 正とする持ち主 | UI側のコピー/役割 |
|---|---|---|
| 選択session/Stable・Pending・NoSession | MediaSessionService（元観測はprovider） | MainWindow snapshotは描画用。古いtaskは世代/identityで拒否 |
| lyrics結果 | LyricsServiceがlookup結果を生成 | MainWindowは採用した結果と描画cursorを保持 |
| HTTP response cache / retry / backoff | LrclibClient（今回抽出） | LyricsServiceが所有/Dispose。lyrics identityの安全性を保証するcacheとは別 |
| disk lyrics cache/manual/LRC設定 | 各専用store | request cacheやhot cacheで永続ユーザー選択を置き換えない |
| Sync profile | PersonalSyncStoreの保存済みprofile | editorはcloneの編集中draft、Mainは現在適用するpreview。重複をなくすために同一objectへしない |
| Repeat | providerからのAutoRepeatMode観測 | coordinatorは観測値を公開、buttonは描画だけ |
| Stop After Track | PlaybackCommandCoordinator/StopAfterTrackReservation | 永続設定ではなく一度だけの予約 |
| control visibility/settings | AppSettingsとSettingsService | SettingsWindowは編集draft、MainのpreviewはCancelで元へ戻す |

## 今回の計画（実装前）

1. 永続的な短いAGENTSルール、repository skill、この監査記録をdocs checkpointにする。
2. H3をdisk書込失敗で再現。再現したものだけ回帰テスト付きの独立fixで直す。
3. H1をLrclibClientへ抽出。HTTP handler注入を維持し、通信のheader/timeout/retry/404/cancel、cache/bypass/track単位無効化を変更前テストで固定する。
4. 各code checkpointでfocused test→全test→Release build→diff review→commit/push。

採用基準、検索順序、MatcherVersion=8、cache key、保存format/schema、UI/font/layout、audio操作、versionは今回のrefactorでは変更しない。

## Baseline / static analysis

- .NET SDK 10.0.401、net10.0-windows10.0.19041.0。
- 指定のRelease build成功、compiler warning 6件: MainWindow CS4014×2（DispatcherOperationを待たない箇所、retry task）、NativeMethods.Rect CS0649×4（interop用field）。error 0。
- 全261テスト成功、失敗0、skip 0。
- EnableNETAnalyzers=true / AnalysisLevel=latest / AnalysisMode未指定 / TreatWarningsAsErrors=false / EnforceCodeStyleInBuild=false / Nullable=annotations。専用.editorconfig/ruleset/global.jsonなし。今回warning-as-errorやformatは導入しない。
- analyzer警告が0でもasync寿命やUIの競合が安全とは限らない。まず既存警告を種類ごとに扱い、次に限定範囲でnullable/analysis ruleを試す。SDK固定はCI運用も含む別判断とする。

## 実施結果

- H3: 修正前の失敗注入は6件中5件失敗。修正後は6件成功、全267件成功（失敗0、skip 0）。Release build成功。コンパイル時の既存警告6件は増加なし、直後の増分buildは警告0件。
- H1: LrclibTransportTestsに11ケースを先に追加し、既存LrclibRefreshTestsの6ケースと合わせて抽出前後とも17件成功。公開API経由でheader、応答cacheの独立性、同時要求の共有、待機中/通信中cancel、429再試行、network/JSON/timeoutの分類、Disposeを保護。既存テストで404、明示refresh、track単位無効化を保護。
- 最終の指定コマンド: 全278件成功（baseline 261、追加17）、失敗0、skip 0。Release build成功、error 0。ソース再コンパイル時の警告はbaselineと同じ6件、最後の増分build出力は警告0件。diff確認済み。
- 30/45秒timeout値、retry delay、cache expiryと削除方針は差分で維持を確認。実時間で30秒待つ試験、実LRCLIBの障害/制限、実プレイヤー操作は行っていない。通信テストはfake HTTP handlerを使用。

## 利用者向けの変更説明

| 問題 | なぜ問題か | 今回 | 効果 |
|---|---|---|---|
| 通信と歌詞選択を同じclassが担当 | 通信の修正で歌詞選択を誤って変えやすい | LrclibClientが通信・再試行・応答cacheを担当 | 通信だけを変更・検証する場所が明確になる |
| 保存失敗後も削除や選択変更がmemoryに残る | 保存できなかった操作が、後の別操作で保存される | 各storeの共通保存出口で未保存状態を無効化 | 保存失敗後も確定済みデータへ戻れる |
| 開発が続くと判断基準が会話履歴へ散る | 次のAIが全面改修や不用意な削除をしやすい | 短いAGENTS原則とcode-health手順・監査記録を追加 | 同じ順序で監査・テスト・小さなcheckpointを繰り返せる |

## Code Health Summary / 次の小さい単位

- **Largest files:** MainWindow 3886行、SettingsWindow 3445行、PersonalSyncWindow 1358行は維持。LyricsServiceは1268→1013行、抽出したLrclibClientは294行。行数の減少自体を品質評価には使わない。
- **New responsibilities:** 新しい製品機能は追加しない。code-health skillが継続監査手順を定義。
- **Extracted responsibilities:** HTTP lifecycle、retry/backoff、response cache/track associationをLrclibClientへ。既存のHttpMessageHandler注入を維持し、追加interfaceやframeworkなし。
- **Duplicated logic found:** cache record復元（M1）、旧Sync UI（M2）、一部style（M7）。意味の違うcache/storeは統合しない。保存失敗時の対処は各storeの保存出口へ集約。
- **Dead code found:** 確実に削除できると証明したものはなし。M3の候補、BAML connector、旧settings migrationは残す。
- **New technical debt:** 抽出に伴いログ値の改行等を整える小さなprivate helperが両classに存在する。業務判定のコピーは増やしていない。ログ仕様変更時の整合確認対象とし、汎用frameworkは追加しない。HTTP gate/cacheの長期増加（M5）は既存から継承。
- **Analyzer warnings:** 既存CS4014×2、CS0649×4。新規警告なし。警告抑制や全体formatは行わない。

| 次の対象 | 最小の分離候補 / 先に固定する動作 |
|---|---|
| MainWindow（最優先） | Personal Syncの現在曲・preview・適用結果の進行管理。PersonalSyncTransitionTests等を基礎に、保存中の曲変更と終了を再現してから抽出。描画全体やBAMLを一度に移さない |
| SettingsWindow | profile/source一覧の非同期更新。RuntimeSettingsTests/PlayerVisibilityTestsに加え、閉じた画面・古い選択への遅延結果を拒否する試験を先に用意 |
| PersonalSyncWindow | 編集draftとsave queueの寿命。PersonalSyncEditorV2Tests/TransitionTestsを基礎に、drag/hold/undo中の切替とcloseを固定。UI compositionだけを細切れにしない |
| LyricsService | 次のcache機能変更時にcache→record変換の一元化。検索段階の249行methodは優先順・早期終了のcharacterization後に検討 |

UIイベントの寿命、await後の曲世代チェック（H4/H5）、大きな入力でのUI停止（M4）は今回の完了範囲ではない。現時点の構造リスクであり、実機で再現・解消を確認した不具合としては報告しない。

## 継続運用

大きな機能群の後は `$code-health` を実行し、この記録を現在コードで更新する。仕組みはAGENTS（必須の短い原則）、skill（監査/検証/報告手順）、回帰テスト、既存Windows CI。根拠のないhealth scoreや行数上限ゲートは置かない。

OpenAI一次資料: [AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md)、[skills](https://learn.chatgpt.com/docs/build-skills)、[役割分担](https://learn.chatgpt.com/docs/customization/overview)。

## 2026-09-25 scoped follow-up

Repeat不具合に対して、timeline不連続の判定・revisionはMediaSessionService、表示の再開はMainWindowへ配置。LyricsServiceや保存処理に再生cycle状態を追加しない。新しいcontroller/frameworkは不要と判断。古いscroll callbackにはpresentation revisionを付け、現行cycleへ書き戻さない。詳細と検証は [dev.11 playback/UI記録](DEV11_PLAYBACK_UI_VALIDATION.md) を参照。Window全体の責務集中（H2/H4/H5）を解消したとは扱わない。

Shuffleは既存のprovider/service/coordinator境界へ追加し、MainWindowには配置と観測表示のみを追加。Repeat/Shuffleのdot生成をPlayerControlVisualsへ集約した。Searchのtechnical fontは実行時templateで設定し、metadata本文を分離。Settingsの不要なCancel経路はユーザー指定で廃止したが、設定draftの元値は残す。これは保守性だけのrefactorではなく、明示された機能/UI変更である。既存6警告は抑制せず維持。BAML依存とWindowの大きさは既存課題として継続し、無関係な全体分割は行わない。
