# dev.11 playback and technical UI validation

2026-09-25。開始revision: `c38874a52316e8c7f0637b9e712bb46b38afc13f`。
ローカルの実機ログ・画像はGit管理外の `.local/scratch/` に保持する。

## Timeline wrap

- 修正前の入力テストで179.7→0.2秒が180秒に、150→20秒が150.32秒に保持されることを再現。大きな後戻りをすべて1.8秒待つ経路が原因。
- 同じsession/曲identity、確定済みmetadata、更新されたtimeline timestamp（取得時から1.5秒以内）、5秒以上の不連続を条件に即採用する。前後とも再生中で終端→先頭ならWrap、それ以外の大きな不連続はSeekとしてrevisionを進める。
- 端の判定範囲は曲長の2%、0.5〜3秒に制限する。直近観測から3秒以内、explicit seek待機中ではないこともWrapの条件。Repeat設定の変更自体ではrevisionを進めない。
- 小さな後戻り、古いtimestamp、証拠が不足した補正には従来の安定化を残す。GSMTCは外部seekの意図を明示しないため、大きく新鮮な不連続から推定する制限がある。
- SnapshotのrevisionでMainWindowが表示だけを再開する。古いscroll callbackを無効化し、lyrics/lookup/Sync/global offset/anchors/holdsを再取得・削除しない。
- テスト: serviceの境界・seek・anti-jitter・設定変更13件、実WPFでsynced/plain表示と保存内容維持2件。修正前の9ケース中4件が失敗、修正後15件成功。
- 実Spotify/GSMTC: 約253.9秒の同一曲Repeatでprovider=0.115秒、stable=0.115秒、Wrap revision=1を同じ取得で確認。外部150→20秒seekは約77msでstable=20.050秒。検証後に元のPaused/Track/20.260秒へ復帰。Spotify以外の実プレイヤーは未検証。
- UIとSync維持はWPF自動試験、実Spotifyの位置反映はprovider/serviceの実機試験として区別する。
- checkpoint検証: 全293テスト成功、失敗/skip 0。Release build成功。再コンパイル時の既存警告6件は増加なし、最後の増分buildは警告0件、error 0。

公式APIのtimestamp意味は [Microsoft LastUpdatedTime](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties.lastupdatedtime) で確認。

## 検索・設定のtechnical UI

- SearchのTITLE/ARTIST/ALBUM/KEYWORD、ID入力、操作、候補評価とMATCH/DIFF/SCOREを固定英語＋Flow Dotsへ統一。曲名・歌手・歌詞本文は通常font、説明・エラーは選択言語を維持。候補カードの細い枠とcompact badge/actionを実行時BAMLへ適用。
- AUTO/CACHE/MANUAL/BEST MATCH/LOCAL LRCをMainとSettingsで固定表記。Sync/履歴/PreviewのCloseと数値、Diagnosticsのtechnical dumpもfontを整合。
- Settingsの常設Cancelを除去。Closeとタイトルバーの×は有効なlive draftを確定。Hold編集の取り消しや確認dialogは維持。BAMLの接続後にCloseの表記を設定し、埋め込み属性の上書きを避ける。
- 10言語のWPF画面、ID取得・Preview・Use、Closeでの保存を回帰検証。日本語の検索/Settingsを実desktopのnative captureで目視確認。実ネットワークへの投稿や歌詞選択の品質変更は行わない。
- 全294テスト成功、失敗/skip 0。Release build成功、既存警告6件、新規警告/errorなし。

## Shuffle・transport初期値

- GSMTCのIsShuffleEnabled / nullable IsShuffleActive / TryChangeShuffleActiveAsyncを使用。provider→session→snapshotへ保持し、Coordinatorは観測状態のみを表示の根拠とする。未対応/不明/対象session変更は送信しない。command拒否時も表示を変更しない。アプリ名での分岐・独自playlist操作は追加しない。
- Shuffle→Previous→Play/Pause→Next→Repeatの順。共通PlayerControlVisualsで7×7 dotの大きさ・間隔・17.1 DIPの外寸を統一。Repeatは向かい合う矢印と中央1、Shuffleは交差矢印。既存utility群とは間隔を維持し、狭幅では既存の段階的非表示へ統合。
- 新規/欠落property時はShuffle/Repeat/Reverse/Sync非表示、Volumeは従来どおり。明示済みtrue/falseはsettings load/clone/normalize/saveで維持。schema17に破壊的migrationを追加せず、JSON既定値で互換を保つ。起動時にRepeat/Shuffle変更要求を送らない。
- focused 41テスト成功。4通りのRepeat/Shuffle capability、unknown state、acceptedだが未観測、reject、external state、session/capability変更、JSON旧版と明示値、live preview/Close保存、10言語を含む。
- WPF native captureでON/OFF/List/Trackと配置を確認。96/120/144 DPIはoffscreen render、実desktop captureはこのPCのDPI。216～760 DIPの8幅でbutton交差なし。別モニターへのDPI切替と他プレイヤーの実機動作は未検証。

## 追加のSpotify実機確認

- Shuffle要求accepted=Trueの直後は観測値Falseを維持。その後GSMTCのTrueを検出し、外部APIでFalseへ戻した際も追従。
- 実MainWindow＋実Spotify timeline＋独立した検証用lyrics/SyncでRepeatを確認。0.036秒で先頭行、0:00、scroll=0へ同時更新。lyrics/lookup/cancellation/cache revision/profile参照を維持し、保存profileの内容も一致。実ユーザーの歌詞やprofileは使用しない。
- 同じ構成の150→20秒外部seekは約80msで表示へ反映（20.016秒、行7、0:20）。
- Repeat Listの自然な次曲遷移はserviceで別identityかつStable、TimelineChange=None（0.379秒）。同一曲Wrap扱いにならないことを確認。
- 全probe終了後、元の曲・Paused・Track repeat・Shuffle=False・20.260秒への復帰を別の読み取りで確認。ログ/probe/画像はGit管理外の`.local/`のみ。

最終検証: 全302テスト成功、failed/skipped 0（開始時278）。Release build成功。既存の再コンパイル警告CS4014×2、CS0649×4は増加なし、最後の増分buildはwarning/errorとも0。diff reviewと`git diff --check`を実施。

API根拠: [Microsoft Shuffle command](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trychangeshuffleactiveasync?view=winrt-28000)、[nullable Shuffle state](https://learn.microsoft.com/ja-jp/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionplaybackinfo.isshuffleactive?view=winrt-28000)。
