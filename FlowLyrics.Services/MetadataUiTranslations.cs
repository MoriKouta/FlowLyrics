using System;
using System.Collections.Generic;

namespace FlowLyrics.Services;

internal static class MetadataUiTranslations
{
	private static readonly string[] Languages = ["ja-JP", "zh-CN", "zh-TW", "ko-KR", "es-ES", "fr-FR", "de-DE", "pt-BR", "ru-RU"];
	// English keys are the en-US text, in the same fallback scheme as LocalizationService.
	private static readonly IReadOnlyDictionary<string, string[]> Values = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		["Adjust lyric timing"] = ["歌詞タイミングを調整", "调整歌词时间", "調整歌詞時間", "가사 타이밍 조정", "Ajustar sincronización", "Ajuster le timing", "Text-Timing anpassen", "Ajustar sincronização", "Настроить синхронизацию"],
		["Use Local LRC for this track"] = ["この曲でLocal LRCを使う", "此歌曲使用本地 LRC", "此歌曲使用本機 LRC", "이 곡에 로컬 LRC 사용", "Usar LRC local en esta canción", "Utiliser un LRC local pour ce titre", "Lokale LRC für diesen Titel nutzen", "Usar LRC local nesta faixa", "Использовать локальный LRC для трека"],
		["Local LRC files"] = ["Local LRCファイル", "本地 LRC 文件", "本機 LRC 檔案", "로컬 LRC 파일", "Archivos LRC locales", "Fichiers LRC locaux", "Lokale LRC-Dateien", "Arquivos LRC locais", "Локальные файлы LRC"],
		["Load timestamped lyrics to adjust timing."] = ["時刻付き歌詞を読み込むと調整できます。", "加载带时间戳的歌词以调整时间。", "載入有時間戳的歌詞以調整時間。", "타임스탬프 가사를 불러오면 조정할 수 있습니다.", "Carga letras sincronizadas para ajustar el tiempo.", "Chargez des paroles horodatées pour régler le timing.", "Zum Anpassen Text mit Zeitstempeln laden.", "Carregue letras com marcação de tempo para ajustar.", "Загрузите текст с метками времени для настройки."],
		["Adjust timing after choosing the right lyrics. Original lyrics stay unchanged."] = ["正しい歌詞を選んでからタイミングを調整します。元の歌詞は変わりません。", "选好歌词后调整时间，原歌词不会改变。", "選好歌詞後調整時間，原歌詞不會改變。", "올바른 가사를 선택한 후 조정하세요. 원본은 유지됩니다.", "Elige la letra correcta y ajusta su tiempo. El original se conserva.", "Choisissez les bonnes paroles, puis ajustez le timing. L’original est conservé.", "Erst den richtigen Text wählen, dann das Timing anpassen. Das Original bleibt erhalten.", "Escolha a letra certa e ajuste o tempo. O original é mantido.", "Выберите правильный текст и настройте время. Оригинал сохраняется."],
		["ON · Use a matching local file when available. Otherwise use LRCLIB."] = ["ON · 対応するローカルファイルを優先。ない場合はLRCLIBを使います。", "ON · 优先使用匹配的本地文件，否则使用 LRCLIB。", "ON · 優先使用相符的本機檔案，否則使用 LRCLIB。", "ON · 일치하는 로컬 파일을 우선 사용하고 없으면 LRCLIB를 사용합니다.", "ON · Prioriza el archivo local. Si no existe, usa LRCLIB.", "ON · Priorité au fichier local. Sinon, utilisation de LRCLIB.", "ON · Passende lokale Datei bevorzugen, sonst LRCLIB.", "ON · Prioriza o arquivo local; caso contrário, usa LRCLIB.", "ON · Сначала локальный файл, иначе LRCLIB."],
		["OFF · Use LRCLIB or its cache. Local files are kept."] = ["OFF · LRCLIBまたはそのキャッシュを使用。ローカルファイルは残ります。", "OFF · 使用 LRCLIB 或缓存，保留本地文件。", "OFF · 使用 LRCLIB 或快取，保留本機檔案。", "OFF · LRCLIB 또는 캐시를 사용합니다. 로컬 파일은 유지됩니다.", "OFF · Usa LRCLIB o su caché. Conserva los archivos locales.", "OFF · LRCLIB ou son cache. Les fichiers locaux sont conservés.", "OFF · LRCLIB oder Cache nutzen. Lokale Dateien bleiben erhalten.", "OFF · Usa LRCLIB ou cache. Os arquivos locais são mantidos.", "OFF · LRCLIB или кэш. Локальные файлы сохраняются."],
		["Lyrics source and details"] = ["歌詞の取得元と詳細", "歌词来源与详情", "歌詞來源與詳細資訊", "가사 출처 및 상세 정보", "Origen y detalles de la letra", "Source et détails des paroles", "Textquelle und Details", "Origem e detalhes da letra", "Источник и сведения о тексте"],
		["Sync history"] = ["同期履歴", "同步历史", "同步紀錄", "동기화 기록", "Historial de sincronización", "Historique de synchronisation", "Sync-Verlauf", "Histórico de sincronização", "История синхронизации"],
		["No timing adjustments for these lyrics"] = ["この歌詞はタイミング未調整です", "此歌词尚未调整时间", "此歌詞尚未調整時間", "이 가사의 타이밍은 아직 조정되지 않았습니다", "Sin ajustes de tiempo para esta letra", "Aucun ajustement pour ces paroles", "Keine Timing-Anpassung für diesen Text", "Sem ajustes para esta letra", "Для этого текста время не настроено"],
		["Timing saved for different lyrics · not applied"] = ["別の歌詞用の調整があります（未適用）", "已有其他歌词的调整，未应用", "已有其他歌詞的調整，未套用", "다른 가사의 조정이 있어 적용하지 않았습니다", "Ajustes de otra letra · sin aplicar", "Réglage pour d’autres paroles · non appliqué", "Timing für anderen Text · nicht angewendet", "Ajuste de outra letra · não aplicado", "Настройка для другого текста · не применена"],
		["Global offset"] = ["全体オフセット", "整体偏移", "整體偏移", "전체 오프셋", "Desfase global", "Décalage global", "Globaler Versatz", "Deslocamento geral", "Общий сдвиг"],
		["Timing editor"] = ["タイミング編集", "时间编辑", "時間編輯", "타이밍 편집", "Editor de tiempos", "Éditeur de synchronisation", "Timing-Editor", "Editor de tempo", "Редактор времени"],
		["Adjustment points"] = ["調整ポイント", "调整点", "調整點", "조정 지점", "Puntos de ajuste", "Points de réglage", "Anpassungspunkte", "Pontos de ajuste", "Точки настройки"],
		["Align to now"] = ["現在に合わせる", "对齐当前位置", "對齊目前位置", "현재 위치에 맞추기", "Alinear al momento actual", "Aligner sur maintenant", "An aktuelle Position anpassen", "Alinhar ao momento atual", "Совместить с текущим моментом"],
		["Resume here"] = ["ここで再開", "从这里恢复", "從這裡繼續", "여기서 재개", "Reanudar aquí", "Reprendre ici", "Hier fortsetzen", "Retomar aqui", "Продолжить здесь"],
		["Start lyric hold"] = ["空白区間を開始", "开始暂停歌词", "開始暫停歌詞", "가사 일시 정지 시작", "Iniciar pausa de letra", "Commencer une pause des paroles", "Liedtext anhalten", "Iniciar pausa da letra", "Приостановить текст"],
		["Hold in progress. Choose the lyric to resume."] = ["空白区間を設定中。再開する歌詞を選んでください。", "正在暂停。请选择恢复的歌词。", "正在暫停。請選擇繼續的歌詞。", "일시 정지 중입니다. 재개할 가사를 선택하세요.", "Pausa en curso. Elige la letra para reanudar.", "Pause en cours. Choisissez les paroles de reprise.", "Pause aktiv. Text zum Fortsetzen auswählen.", "Pausa em andamento. Escolha a letra para retomar.", "Пауза. Выберите строку для продолжения."],
		["More"] = ["その他", "更多", "更多", "더 보기", "Más", "Plus", "Mehr", "Mais", "Ещё"],
		["Now"] = ["現在", "当前", "目前", "현재", "Ahora", "Maintenant", "Jetzt", "Agora", "Сейчас"],
		["Drag to the current position to align"] = ["現在位置へドラッグして合わせる", "拖到当前位置以对齐", "拖曳到目前位置以對齊", "현재 위치로 드래그하여 맞추기", "Arrastra a la posición actual para alinear", "Faites glisser sur la position actuelle pour aligner", "Zum Ausrichten auf die aktuelle Position ziehen", "Arraste para a posição atual para alinhar", "Перетащите на текущую позицию"],
		["Seek to this lyric"] = ["この歌詞の位置へ移動", "跳转到此歌词", "跳轉至此歌詞", "이 가사 위치로 이동", "Ir a esta letra", "Aller à ces paroles", "Zu dieser Textzeile springen", "Ir para esta letra", "Перейти к этой строке"],
		["Changes are saved when you close."] = ["変更は閉じるときに保存されます。", "关闭时保存更改。", "關閉時儲存變更。", "닫을 때 변경 사항이 저장됩니다.", "Los cambios se guardan al cerrar.", "Les modifications sont enregistrées à la fermeture.", "Änderungen werden beim Schließen gespeichert.", "As alterações são salvas ao fechar.", "Изменения сохраняются при закрытии."],
		["Lyric hold"] = ["歌詞停止", "歌词暂停", "歌詞暫停", "가사 일시 정지", "Pausa de letra", "Pause des paroles", "Textpause", "Pausa da letra", "Пауза текста"],
		["Use selected lyric"] = ["選択した歌詞に合わせる", "使用所选歌词", "使用所選歌詞", "선택한 가사 사용", "Usar letra seleccionada", "Utiliser les paroles sélectionnées", "Ausgewählten Text verwenden", "Usar letra selecionada", "Использовать выбранную строку"],
		["Select a lyric"] = ["歌詞を選択", "选择歌词", "選擇歌詞", "가사 선택", "Seleccionar letra", "Sélectionner des paroles", "Text auswählen", "Selecionar letra", "Выбрать строку"],
		["Not set"] = ["未設定", "未设置", "未設定", "설정 안 됨", "Sin configurar", "Non défini", "Nicht festgelegt", "Não definido", "Не задано"],
		["Stop using"] = ["使用解除", "停止使用", "停止使用", "사용 해제", "Dejar de usar", "Ne plus utiliser", "Nicht mehr verwenden", "Parar de usar", "Отключить"],
		["Use"] = ["使用", "使用", "使用", "사용", "Usar", "Utiliser", "Verwenden", "Usar", "Использовать"],
		["Choose"] = ["選択", "选择", "選擇", "선택", "Elegir", "Choisir", "Auswählen", "Escolher", "Выбрать"],
		["Glow"] = ["グロー", "辉光", "輝光", "글로우", "Resplandor", "Lueur", "Leuchten", "Brilho", "Свечение"],
		["Glow Blur"] = ["グローのぼかし", "辉光模糊", "輝光模糊", "글로우 흐림", "Desenfoque del resplandor", "Flou de la lueur", "Leuchtunschärfe", "Desfoque do brilho", "Размытие свечения"],
		["Glow Opacity"] = ["グローの濃さ", "辉光不透明度", "輝光不透明度", "글로우 불투명도", "Opacidad del resplandor", "Opacité de la lueur", "Leuchtdeckkraft", "Opacidade do brilho", "Непрозрачность свечения"],
		["TEXT EFFECTS"] = ["文字の効果", "文字效果", "文字效果", "텍스트 효과", "EFECTOS DE TEXTO", "EFFETS DE TEXTE", "TEXTEFFEKTE", "EFEITOS DE TEXTO", "ЭФФЕКТЫ ТЕКСТА"],
		["SURFACE"] = ["背景とウィンドウ", "背景与窗口", "背景與視窗", "배경 및 창", "FONDO Y VENTANA", "FOND ET FENÊTRE", "HINTERGRUND UND FENSTER", "FUNDO E JANELA", "ФОН И ОКНО"],
		["LRCLIB ID"] = ["LRCLIB ID", "LRCLIB ID", "LRCLIB ID", "LRCLIB ID", "ID de LRCLIB", "ID LRCLIB", "LRCLIB-ID", "ID do LRCLIB", "ID LRCLIB"],
		["Load ID"] = ["IDから読み込む", "按 ID 加载", "依 ID 載入", "ID로 불러오기", "Cargar ID", "Charger l’ID", "ID laden", "Carregar ID", "Загрузить по ID"],
		["Enter a positive LRCLIB ID."] = ["LRCLIB IDを正の整数で入力してください。", "请输入正整数 LRCLIB ID。", "請輸入正整數 LRCLIB ID。", "양의 정수 LRCLIB ID를 입력하세요.", "Introduce un ID de LRCLIB entero positivo.", "Saisissez un ID LRCLIB entier positif.", "Geben Sie eine positive ganze LRCLIB-ID ein.", "Digite um ID do LRCLIB inteiro positivo.", "Введите положительный целочисленный ID LRCLIB."],
		["Loading LRCLIB ID…"] = ["LRCLIB IDを読み込み中…", "正在加载 LRCLIB ID…", "正在載入 LRCLIB ID…", "LRCLIB ID 불러오는 중…", "Cargando ID de LRCLIB…", "Chargement de l’ID LRCLIB…", "LRCLIB-ID wird geladen…", "Carregando ID do LRCLIB…", "Загрузка по ID LRCLIB…"],
		["Review this record and preview the lyrics before using it."] = ["曲情報と歌詞のプレビューを確認してから適用してください。", "使用前请核对歌曲信息并预览歌词。", "使用前請核對歌曲資訊並預覽歌詞。", "곡 정보와 가사 미리보기를 확인한 후 적용하세요.", "Revisa el registro y la vista previa de la letra antes de usarlo.", "Vérifiez les informations et l’aperçu des paroles avant utilisation.", "Prüfen Sie den Eintrag und die Textvorschau vor dem Übernehmen.", "Confira os dados e a prévia da letra antes de usar.", "Проверьте запись и предпросмотр текста перед применением."],
		["LRCLIB search results may remain cached after a new submission. If you know the LRCLIB ID, load it directly."] = [
			"LRCLIBの検索結果はサーバー側キャッシュにより、新規登録後しばらく反映されない場合があります。LRCLIB IDが分かる場合はIDから直接読み込めます。",
			"新提交后，LRCLIB 搜索结果可能仍被缓存。如果知道 LRCLIB ID，可直接按 ID 加载。",
			"新提交後，LRCLIB 搜尋結果可能仍被快取。如果知道 LRCLIB ID，可直接依 ID 載入。",
			"새 가사를 등록한 후에도 LRCLIB 검색 결과가 캐시되어 있을 수 있습니다. LRCLIB ID를 알면 직접 불러올 수 있습니다.",
			"Los resultados de LRCLIB pueden seguir en caché tras un nuevo envío. Si conoces el ID de LRCLIB, cárgalo directamente.",
			"Les résultats LRCLIB peuvent rester en cache après un nouvel envoi. Si vous connaissez l’ID LRCLIB, chargez-le directement.",
			"LRCLIB-Suchergebnisse können nach einem neuen Beitrag im Cache bleiben. Wenn Sie die LRCLIB-ID kennen, laden Sie sie direkt.",
			"Os resultados do LRCLIB podem continuar em cache após um novo envio. Se souber o ID do LRCLIB, carregue-o diretamente.",
			"После новой публикации результаты поиска LRCLIB могут оставаться в кэше. Если известен ID LRCLIB, загрузите запись напрямую."],
		["SPOTIFY WINDOW STATE"] = ["SPOTIFYウィンドウ状態", "SPOTIFY 窗口状态", "SPOTIFY 視窗狀態", "SPOTIFY 창 상태", "ESTADO DE VENTANA SPOTIFY", "ÉTAT DE LA FENÊTRE SPOTIFY", "SPOTIFY-FENSTERSTATUS", "ESTADO DA JANELA SPOTIFY", "СОСТОЯНИЕ ОКНА SPOTIFY"],
		["DISPLAY TITLE"] = ["表示曲名", "显示曲名", "顯示曲名", "표시 곡명", "TÍTULO MOSTRADO", "TITRE AFFICHÉ", "ANGEZEIGTER TITEL", "TÍTULO EXIBIDO", "ОТОБРАЖАЕМОЕ НАЗВАНИЕ"],
		["TITLE ALIASES"] = ["曲名の別名", "曲名别名", "曲名別名", "곡명 별칭", "ALIAS DEL TÍTULO", "ALIAS DU TITRE", "TITELALIASSE", "NOMES ALTERNATIVOS", "ВАРИАНТЫ НАЗВАНИЯ"],
		["ARTIST SEARCH CANDIDATES"] = ["アーティスト検索候補", "艺人搜索候选", "藝人搜尋候選", "아티스트 검색 후보", "ARTISTAS PARA BÚSQUEDA", "ARTISTES POUR LA RECHERCHE", "KÜNSTLER-SUCHKANDIDATEN", "ARTISTAS PARA PESQUISA", "ИСПОЛНИТЕЛИ ДЛЯ ПОИСКА"],
		["EDITION SIGNATURE"] = ["版の識別情報", "版本标识", "版本識別", "버전 식별 정보", "IDENTIDAD DE EDICIÓN", "IDENTITÉ DE VERSION", "VERSIONSKENNUNG", "IDENTIDADE DA VERSÃO", "ПРИЗНАКИ ВЕРСИИ"],
		["RELEASE CONTEXT"] = ["リリースの補足情報", "发行背景", "發行背景", "발매 부가 정보", "CONTEXTO DE LANZAMIENTO", "CONTEXTE DE SORTIE", "VERÖFFENTLICHUNGSKONTEXT", "CONTEXTO DO LANÇAMENTO", "КОНТЕКСТ РЕЛИЗА"],
		["METADATA INFERENCE"] = ["メタデータの解釈根拠", "元数据推断依据", "中繼資料推斷依據", "메타데이터 해석 근거", "INTERPRETACIÓN DE METADATOS", "INTERPRÉTATION DES MÉTADONNÉES", "METADATENINTERPRETATION", "INTERPRETAÇÃO DOS METADADOS", "ИНТЕРПРЕТАЦИЯ МЕТАДАННЫХ"]
	};

	public static bool TryGet(string language, string key, out string value)
	{
		int index = Array.FindIndex(Languages, item => string.Equals(language, item, StringComparison.OrdinalIgnoreCase));
		if (index >= 0 && Values.TryGetValue(key, out string[]? translations)) { value = translations[index]; return true; }
		value = key;
		return false;
	}
}
