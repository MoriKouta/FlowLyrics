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
