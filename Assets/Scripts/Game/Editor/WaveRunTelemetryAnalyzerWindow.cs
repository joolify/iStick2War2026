using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace iStick2War_V2.Editor
{
    /// <summary>
    /// Offline analysis of <see cref="WaveRunTelemetry_V2"/> JSON logs (JsonUtility format). Heuristics are v1 flags only.
    /// </summary>
    public sealed class WaveRunTelemetryAnalyzerWindow : EditorWindow
    {
        /// <summary>End state of prior wave_cleared/run_end for COLLAPSE_CHAIN (file order).</summary>
        private readonly struct WaveChainPrev
        {
            public readonly bool Has;
            public readonly int Wave;
            public readonly float MinBunkerHpRatioThisWave;
            public readonly bool BunkerBreached;

            public WaveChainPrev(int wave, float minBunkerHpRatioThisWave, bool bunkerBreached)
            {
                Has = true;
                Wave = wave;
                MinBunkerHpRatioThisWave = minBunkerHpRatioThisWave;
                BunkerBreached = bunkerBreached;
            }
        }

        private static string NormalizeKind(string kind)
        {
            return string.IsNullOrEmpty(kind)
                ? string.Empty
                : kind.Trim().ToLowerInvariant();
        }

        private static bool IsWaveEndKind(string kind)
        {
            string k = NormalizeKind(kind);
            return k == "wave_cleared" || k == "run_end";
        }

        private const string PrefSuppressOnboarding = "iStick2War.WaveTelemetryAnalyzer.SuppressOnboardingBunkerFlags";
        private const string PrefOnboardingMaxWave = "iStick2War.WaveTelemetryAnalyzer.OnboardingMaxWaveInclusive";
        private const string PrefBatchFolder = "iStick2War.WaveTelemetryAnalyzer.BatchFolder";
        private const string PrefBatchRecursive = "iStick2War.WaveTelemetryAnalyzer.BatchRecursive";
        private const string PrefBatchWaveRunFilenameOnly = "iStick2War.WaveTelemetryAnalyzer.BatchWaveRunFilenameOnly";
        private const string PrefBatchSceneProfileSubstr = "iStick2War.WaveTelemetryAnalyzer.BatchSceneProfileSubstr";
        private const string PrefBatchAutoHeroSubstr = "iStick2War.WaveTelemetryAnalyzer.BatchAutoHeroSubstr";
        private const string PrefBatchMaxAgeHours = "iStick2War.WaveTelemetryAnalyzer.BatchMaxAgeHours";

        /// <summary>Min total bunker damage before BOMB_DOMINATED_BUNKER (avoid trivial rows).</summary>
        private const int BombDominatedBunkerMinTotalDamage = 20;

        /// <summary>Bomb bunker subset must exceed this fraction of total bunker damage (strict &gt;).</summary>
        private const float BombDominatedBunkerRatioThreshold = 0.9f;

        /// <summary>Swedish v1 heuristic legend shown at top of report and in UI before first analysis.</summary>
        private static readonly string AnalyzerIntroSv =
            "Rapport (v1-heuristik)\n" +
            "För varje rad wave_cleared / run_end skrivs en rad med nyckeltal + valfria flaggor:\n\n" +
            "Flagga\tUngefär\n" +
            "OBJECTIVE_INACTIVE\tenemiesKilled > 0 och damageTakenBunker == 0 och bunker vid InWave-start ≥ root tryck-tröskel (ratio); undviker falsk signal när vågen redan börjar under tryck-tröskeln utan ny bunker-skada\n" +
            "AUTOHERO_AIRCRAFT_LAST_INWAVE\twave_cleared/run_end: autoHeroInWaveAnyHadTarget och autoHeroInWaveLastTargetKindWhenHadTarget var aircraft (bot hade flygmål under InWave; ofta tillsammans med OBJECTIVE_INACTIVE när kills inte ger bunker-skada i telemetrin)\n" +
            "AUTOHERO_PARATROOPER_LAST_INWAVE\twave_cleared/run_end: autoHeroInWaveAnyHadTarget och senaste måltyp var paratrooper (autoHeroInWaveLastTargetParatrooperStateWhenHadTarget kan vara t.ex. Shoot/Land)\n" +
            "PRELOAD_FAIL\twaveDurationSec > 3 och (bunkerStart01 < 0.2 med bunker-skada > 0, eller bunkerHpWaveStart ≤ 0)\n" +
            "LOW_COVER_STALL\tbunkerHpWaveStart > 0: bunkerStart01 < 0.6, bunker-skada 0, pressureScore > 0.8, waveDurationSec > 6 (carry-in-tryck utan ny bunker-skada; ej post-breach)\n" +
            "COLLAPSE_CHAIN\tföregående wave_cleared/run_end: minRatio < 0.2 eller breach; denna rad: wave+1, bunkerStart01 < 0.3, pressureScore > 0.7\n" +
            "HIGH_CARRY_IN_PRESSURE\tsom PRELOAD inte gäller: bunkerStart01 ≥ 0.2, bunker-skada, stor skillnad press total vs efter första träff\n" +
            "BOMB_DOMINATED_BUNKER\twave_cleared/run_end: damageTakenBunkerFromBombs / max(1, damageTakenBunker) > 0,9 och damageTakenBunker ≥ 20 (nästan all bunker-skada från bomber; kräver telemetri-fältet)\n" +
            "NEAR_BREACH\twave_cleared, minBunkerHpRatio < 0.15, inte bunkerBreached (tur/panik, inte sweet spot)\n" +
            "OVERLOAD_FAIL\trun_end och (heroDead eller bunkerBreached)\n" +
            "GAME_ERROR_FAIL\trun_end med endReason som börjar på game_error (watchdog/soft-lock guard)\n" +
            "GAME_WON_END\trun_end med endReason game_won (session avslutad med vinst)\n" +
            "NEXT_WAVE_OVERLOAD\twave_cleared: nästa wave_cleared/run_end-rad i filen är run_end för wave+1 med game over (heroDead eller bunkerBreached)\n" +
            "SWEET_SPOT_HINT\tgrov \"bra fight\"-heuristik för wave_cleared (skada + min ratio-intervall, ingen död/breach)\n" +
            "TRIVIAL_BUNKER\tingen bunker-skada och minBunkerHpRatio ≥ 0.99\n" +
            "PRESSURE_DOMINATED_DURATION\ttryck-tid följer nästan hela våglängden\n\n" +
            "Dessutom visas ungefärlig HP-ratio-lutning från första/sista bunkerHpSamples-punkt (slope≈…/s).\n\n" +
            "JSON: root.unityLogs[] (om den finns) listar Unity Error/Exception/Assert med samma sessionId, wave, vapen, HP, " +
            "position, trackedLivingParatroopers m.m. (batch-TSV har kolumnen unityLogRows). " +
            "root.feelSession = första kill/damage/död/köp + offline feel-KPI:er i batch-TSV (feel_* kolumner).\n\n" +
            "Flaggorna är tolkningshjälp, inte balans-sanning — justera trösklar i editor-skriptet när ni har mer data.\n\n" +
            "Inställning «Undertryck onboarding-flaggor»: döljer OBJECTIVE_INACTIVE, TRIVIAL_BUNKER och SWEET_SPOT_HINT för valt vågnummer och lägre " +
            "(så tidiga vågor inte feltolkas som balans-/retention-signaler).";

        /// <summary>Field guide for JSON + batch columns (Swedish); appended to Analyze/Batch reports and optional UI foldout.</summary>
        private static readonly string AnalyzerTelemetryReferenceSv =
            "--- Telemetri & JSON (vad filen betyder) ---\n" +
            "Källa: «WaveRunTelemetry_V2» skriver en JSON-fil per körning (Unity JsonUtility). Rotobjektet har bl.a. " +
            "«_comment», «glossary», eko-fält för bunker-/HP-sampling, «events[]», valfritt «unityLogs[]», " +
            "valfritt «feelSession», valfritt «poolStats». Alla tider i «realtimeSinceStartup» är sekunder sedan Unity startade när raden skrevs.\n" +
            "Rot: «bunkerCriticalHpFractionUsed», «bunkerHpSampleIntervalSecUsed», «bunkerHpSamplesMaxPerWaveUsed», " +
            "«bunkerPressureHpRatioThresholdUsed» = sparade trösklar/samplingsparametrar vid persist (samma som används i analysens tryck-mått). " +
            "«glossary» i JSON-filen är auktoritativ ordlista för fältnamn om den skiljer sig från denna rapport.\n\n" +
            "## events[] — rader (kind)\n" +
            "• «session_begin»: körning startad; ekonomi/HP/vapen speglar snapshot vid skrivtillfälle. «waveScalingJson» tom.\n" +
            "• «wave_cleared»: vågen som nyss avslutats (lämnar InWave → Shop). «wave» = den avslutade vågen. " +
            "«waveDurationSec» = tid i InWave för den vågen. «enemiesKilled» = kills den vågen. " +
            "Skada/heal/skott m.m. är ackumulatorer för just den InWave-perioden.\n" +
            "• «run_end»: GameOver / GameError / GameWon m.m. «endReason» förklarar utfallet (t.ex. «hero_death», " +
            "«game_error:…», «game_won»). Om GameOver skedde under InWave fylls abort-vågens bunkerkurva som vid wave_cleared.\n" +
            "• «session_quit»: editor/app avslutad utan GameOver (tom «waveScalingJson»).\n\n" +
            "## events[] — viktiga fält (samma namn på alla rader; tolkning beror på kind)\n" +
            "«sessionId»: hex utan bindestreck, samma session som filnamn-suffix.\n" +
            "«wave»: 1-baserat vågnummer vid skrivtid (session_begin = aktuell våg; wave_cleared = nyss klar våg).\n" +
            "«heroHp» / «heroMaxHp», «bunkerHp» / «bunkerMaxHp», «currency»: snapshot vid raden.\n" +
            "«weapon» / «weaponType»: vapen vid snapshot.\n" +
            "«damageTakenHero» / «healingHero»: under aktuell InWave (nollställs nästa InWave).\n" +
            "«damageTakenBunker»: bunker-skada under InWave (via NotifyBunkerDamageTaken).\n" +
            "«damageTakenBunkerFromBombs»: delmängd av ovan från BombProjectile_V2 (samma InWave-reset).\n" +
            "«timeInBunkerSec»: hero i bunkerzon under InWave (oskalad tid).\n" +
            "«shotsFired», «rayHits» / «rayMisses», «projectileLaunches», «reloads»: combat under InWave.\n" +
            "«shopPurchasesPrior» / «shopCurrencySpentPrior» / «shopOffersBoughtPrior[]»: på «wave_cleared» = " +
            "köp i shop-intermissionen före den vågen som just cleared.\n" +
            "«heroHpWaveStart» / «bunkerHpWaveStart» / «currencyWaveStart»: vid InWave-start för den vågen " +
            "(session_begin fyller samma för första radens konsistens).\n" +
            "«autoHeroTestProfile» / «sceneProfileId»: bot/scenprofil om satta. " +
            "«autoHeroInWaveAnyHadTarget» / «autoHeroInWaveLastTargetKindWhenHadTarget» / " +
            "«autoHeroInWaveLastTargetParatrooperStateWhenHadTarget»: InWave-aggregat (se JSON-glossary).\n" +
            "«bunkerBreached»: bunkerHp==0 vid snapshot. «bunkerCriticalLow»: låg men icke-noll bunker enligt rot-tröskel.\n" +
            "«waveStressScore», «bunkerPressureTimeSec», «bunkerPressureTimeAfterFirstDamageSec», " +
            "«minBunkerHpRatioThisWave», «bunkerHpSamples[]»: se rotfältens eko och JSON-glossary.\n" +
            "«waveScalingJson»: JsonUtility-sträng med skalning för wave_cleared / relevant run_end (tom annars).\n\n" +
            "## unityLogs[] (rot)\n" +
            "Varje rad = Unity Error/Exception/Assert (ev. Warning om aktiverat) + gameplay-snapshot " +
            "(wave, state, HP, ammo, scen, spawner-rad, …). «fingerprint» + «repeatCount» slår ihop identiska " +
            "på varandra följande fel. Samma «sessionId» som events[].\n\n" +
            "## feelSession (rot)\n" +
            "Milstolpar i Time.realtimeSinceStartup + sekunder sedan session_begin (fält …SecSinceSessionBegin, −1 om ej hänt). " +
            "Första köpet: valuta/erbjudande + hero HP/max/vapen för power-delta-jämförelse mot senare wave_cleared.\n\n" +
            "## Feel-KPI:er i denna rapport (offline, från events + feelSession)\n" +
            "• «first90s» = ja om sekunder sedan session_begin ligger i [0, 90] för milstolpen.\n" +
            "• «bunkerTimeFrac01» = summa(timeInBunkerSec)/summa(waveDurationSec) över wave_cleared " +
            "(lägre ≈ mindre tid i bunker; grov proxy för «skjuta vs gömma»).\n" +
            "• «combatProxyPerHeroDamageTaken» = (summa rayHits+projectileLaunches+enemiesKilled)/max(1, summa damageTakenHero) " +
            "— kan spegla skicklighet, vapen eller fiendetal; inte «roligt per se».\n" +
            "• «overwhelmedInWaveSecSum» = total InWave-tid för vågor där damageTakenHero/waveDurationSec ≥ 10 " +
            "(konstant FeelOverwhelmedHeroDps i editor-skriptet) — heuristik, kalibrera mot hur det känns i spel.\n" +
            "• «heroMaxHpDeltaAfterFirstShop» = första wave_cleared efter första köpets tid: heroMaxHp minus max vid köp.\n" +
            "• «survSecAfterFirstShop» = realtime till första död efter köp, annars till sista run_end/session_quit.\n\n" +
            "## Batch-TSV (kolumner, filordning)\n" +
            "fil: filnamn. sessionId. sceneProfileId / autoHero från session_begin-liknande meta.\n" +
            "failWave: sista run_end:s våg om fail (ej game_won), annars —. maxWaveCleared: högsta wave på wave_cleared.\n" +
            "sessionSec: sista minus första events[].realtimeSinceStartup (grovt sessionslängd).\n" +
            "flags: heuristiska flaggor unika för körningen (;-separerade). unityLogRows: antal unityLogs-rader.\n" +
            "bombDominatedWaveRows: antal wave_cleared/run_end-rader där heuristiken BOMB_DOMINATED_BUNKER satts.\n" +
            "feel_firstKillSec … feel_firstShopSec: från feelSession (−1/blankt om saknas). " +
            "feel_bunkerTimeFrac01 … feel_survSecAfterFirstShop: härledda som ovan.\n\n" +
            "## Tolkning (viktigt)\n" +
            "Siffror här är stöd för speltest och design, inte automatisk «bra/dålig»-dom. Vid motsägelse: lita på känsla i build + " +
            "reproducerbar kontext (JSON + unityLogs), inte på en enskild proxy.\n";

        private string _jsonPath = "";
        private string _batchFolderPath = "";
        private bool _batchRecursive;
        private bool _batchWaveRunFilenameOnly;
        private string _batchSceneProfileSubstr = "";
        private string _batchAutoHeroSubstr = "";
        private int _batchMaxAgeHours;
        private bool _showBatchOptionsFoldout = true;
        private Vector2 _scroll;
        private string _report = "";
        private bool _showIntroFoldout = true;
        private bool _showTelemetryRefFoldout = true;
        /// <summary>When true, skip OBJECTIVE_INACTIVE, TRIVIAL_BUNKER, and SWEET_SPOT_HINT for early waves (onboarding).</summary>
        private bool _suppressOnboardingBunkerFlags;
        private int _onboardingMaxWaveInclusive;
        private MessageType _lastMessageType = MessageType.Info;
        private string _lastMessage = "";

        [MenuItem("Tools/iStick2War/Analyze wave telemetry JSON…")]
        private static void Open()
        {
            GetWindow<WaveRunTelemetryAnalyzerWindow>(true, "Wave telemetry analyzer", true);
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_jsonPath))
            {
                _jsonPath = Path.Combine(
                    Application.persistentDataPath,
                    "iStick2WarTelemetry");
            }

            _suppressOnboardingBunkerFlags = EditorPrefs.GetBool(PrefSuppressOnboarding, true);
            _onboardingMaxWaveInclusive = Mathf.Max(0, EditorPrefs.GetInt(PrefOnboardingMaxWave, 2));

            _batchFolderPath = EditorPrefs.GetString(PrefBatchFolder, "");
            if (string.IsNullOrEmpty(_batchFolderPath))
            {
                _batchFolderPath = Path.Combine(Application.persistentDataPath, "iStick2WarTelemetry");
            }

            _batchRecursive = EditorPrefs.GetBool(PrefBatchRecursive, false);
            _batchWaveRunFilenameOnly = EditorPrefs.GetBool(PrefBatchWaveRunFilenameOnly, false);
            _batchSceneProfileSubstr = EditorPrefs.GetString(PrefBatchSceneProfileSubstr, "");
            _batchAutoHeroSubstr = EditorPrefs.GetString(PrefBatchAutoHeroSubstr, "");
            _batchMaxAgeHours = Mathf.Max(0, EditorPrefs.GetInt(PrefBatchMaxAgeHours, 0));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Reads one session JSON (same shape as WaveRunTelemetry_V2 writes).", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);

            if (string.IsNullOrEmpty(_report))
            {
                _showIntroFoldout = EditorGUILayout.Foldout(_showIntroFoldout, "Rapport (v1-heuristik) — beskrivning", true);
                if (_showIntroFoldout)
                {
                    EditorGUILayout.HelpBox(AnalyzerIntroSv, MessageType.None);
                }

                _showTelemetryRefFoldout = EditorGUILayout.Foldout(
                    _showTelemetryRefFoldout,
                    "Telemetri & JSON — fältguide (samma som i rapporten)",
                    true);
                if (_showTelemetryRefFoldout)
                {
                    EditorGUILayout.HelpBox(AnalyzerTelemetryReferenceSv, MessageType.None);
                }

                EditorGUILayout.Space(4);
            }

            EditorGUILayout.BeginHorizontal();
            _jsonPath = EditorGUILayout.TextField("JSON file", _jsonPath);
            if (GUILayout.Button("Browse…", GUILayout.Width(80)))
            {
                string p = EditorUtility.OpenFilePanel("Wave telemetry JSON", Path.GetDirectoryName(_jsonPath) ?? "", "json");
                if (!string.IsNullOrEmpty(p))
                {
                    _jsonPath = p;
                }
            }

            if (GUILayout.Button("Open folder", GUILayout.Width(90)))
            {
                string dir = Directory.Exists(_jsonPath) ? _jsonPath : Path.GetDirectoryName(_jsonPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    EditorUtility.RevealInFinder(dir);
                }
                else
                {
                    EditorUtility.RevealInFinder(Application.persistentDataPath);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Batch — många JSON (samma heuristik som «Analyze»)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _batchFolderPath = EditorGUILayout.TextField("Telemetry folder", _batchFolderPath);
            if (GUILayout.Button("Browse…", GUILayout.Width(80)))
            {
                string start = Directory.Exists(_batchFolderPath)
                    ? _batchFolderPath
                    : Application.persistentDataPath;
                string d = EditorUtility.OpenFolderPanel("Wave telemetry folder (batch)", start, "");
                if (!string.IsNullOrEmpty(d))
                {
                    _batchFolderPath = d;
                    EditorPrefs.SetString(PrefBatchFolder, _batchFolderPath);
                }
            }

            if (GUILayout.Button("Summarize folder", GUILayout.Width(120)))
            {
                RunBatchSummary();
            }

            if (GUILayout.Button("Batch: Spara rapportfil", GUILayout.Width(150)))
            {
                SaveBatchReportToBatchFolder();
            }

            if (GUILayout.Button("Kopiera rapport", GUILayout.Width(100)) && !string.IsNullOrEmpty(_report))
            {
                EditorGUIUtility.systemCopyBuffer = _report;
                _lastMessageType = MessageType.Info;
                _lastMessage = "Rapport kopierad till urklipp.";
            }

            EditorGUILayout.EndHorizontal();

            _showBatchOptionsFoldout = EditorGUILayout.Foldout(_showBatchOptionsFoldout, "Batch — filter & alternativ", true);
            if (_showBatchOptionsFoldout)
            {
                EditorGUI.BeginChangeCheck();
                _batchRecursive = EditorGUILayout.ToggleLeft(
                    "Inkludera undermappar (rekursivt)",
                    _batchRecursive);
                _batchWaveRunFilenameOnly = EditorGUILayout.ToggleLeft(
                    "Bara filer som börjar med wave_run_ (stödjer *.json i mappen)",
                    _batchWaveRunFilenameOnly);
                _batchSceneProfileSubstr = EditorGUILayout.TextField(
                    "Scene profile innehåller (tomt = alla)",
                    _batchSceneProfileSubstr);
                _batchAutoHeroSubstr = EditorGUILayout.TextField(
                    "AutoHero-profil innehåller (tomt = alla)",
                    _batchAutoHeroSubstr);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Max filålder (h)");
                _batchMaxAgeHours = Mathf.Max(0, EditorGUILayout.IntField(_batchMaxAgeHours, GUILayout.Width(64)));
                EditorGUILayout.LabelField("0 = ingen gräns (alla filer)", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(PrefBatchRecursive, _batchRecursive);
                    EditorPrefs.SetBool(PrefBatchWaveRunFilenameOnly, _batchWaveRunFilenameOnly);
                    EditorPrefs.SetString(PrefBatchSceneProfileSubstr, _batchSceneProfileSubstr ?? "");
                    EditorPrefs.SetString(PrefBatchAutoHeroSubstr, _batchAutoHeroSubstr ?? "");
                    EditorPrefs.SetInt(PrefBatchMaxAgeHours, _batchMaxAgeHours);
                }

                EditorGUILayout.HelpBox(
                    "Filter: substring match (skiftlägesokänslig) mot session_begin (fallback: första rad med värde). " +
                    "«Max filålder»: endast filer ändrade inom senaste N timmarna (UTC).",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Samma v1-flaggor som enkelfil. Öppna foldout för rekursiv sökning, wave_run_-filter, profilfilter, filålder, per-fil-tabell, histogram, rad-% flaggor.",
                    MessageType.None);
            }

            if (!string.IsNullOrEmpty(_lastMessage))
            {
                EditorGUILayout.HelpBox(_lastMessage, _lastMessageType);
            }

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _suppressOnboardingBunkerFlags = EditorGUILayout.ToggleLeft(
                "Undertryck onboarding-flaggor (OBJECTIVE_INACTIVE, TRIVIAL_BUNKER, SWEET_SPOT_HINT) för våg ≤",
                _suppressOnboardingBunkerFlags);
            EditorGUI.BeginDisabledGroup(!_suppressOnboardingBunkerFlags);
            _onboardingMaxWaveInclusive = Mathf.Max(0, EditorGUILayout.IntField(_onboardingMaxWaveInclusive, GUILayout.Width(44)));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PrefSuppressOnboarding, _suppressOnboardingBunkerFlags);
                EditorPrefs.SetInt(PrefOnboardingMaxWave, _onboardingMaxWaveInclusive);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analyze", GUILayout.Height(28)))
            {
                RunAnalysis();
            }
            if (GUILayout.Button("Analyze: Spara rapportfil", GUILayout.Height(28), GUILayout.Width(170)))
            {
                SaveSingleAnalysisReportToJsonFolder();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunAnalysis()
        {
            _lastMessage = "";
            _report = "";

            if (string.IsNullOrWhiteSpace(_jsonPath) || !File.Exists(_jsonPath))
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "File not found. Pick a .json file under persistentDataPath/iStick2WarTelemetry or browse.";
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(_jsonPath);
            }
            catch (Exception ex)
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "Read failed: " + ex.Message;
                return;
            }

            TelemetryFileRootDto root;
            try
            {
                root = JsonUtility.FromJson<TelemetryFileRootDto>(text);
            }
            catch (Exception ex)
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "JsonUtility parse failed: " + ex.Message;
                return;
            }

            if (root == null || root.events == null || root.events.Length == 0)
            {
                _lastMessageType = MessageType.Warning;
                _lastMessage = "No events[] in file (or empty).";
                return;
            }

            float pressureThr = root.bunkerPressureHpRatioThresholdUsed > 0.001f
                ? root.bunkerPressureHpRatioThresholdUsed
                : 0.8f;

            var sb = new StringBuilder(4096);
            sb.AppendLine(AnalyzerIntroSv);
            sb.AppendLine();
            sb.AppendLine(AnalyzerTelemetryReferenceSv);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"File: {_jsonPath}");
            sb.AppendLine($"bunkerPressureHpRatioThresholdUsed (root): {pressureThr:0.###} (reference; row flags use fixed heuristics v1)");
            int unityLogRows = root.unityLogs != null ? root.unityLogs.Length : 0;
            sb.AppendLine($"unityLogs[] (Unity errors/exceptions snapshot): {unityLogRows} rad(er)");
            if (root.poolStats != null)
            {
                sb.AppendLine(
                    $"poolStats (root): prefabs={root.poolStats.prefabTypeCount}, inactive={root.poolStats.totalInactiveCount}, " +
                    $"created={root.poolStats.totalCreatedCount}, reused={root.poolStats.totalReusedCount}, despawn={root.poolStats.totalDespawnCount}");
            }
            AppendBaselineKpiSingleFileReport(sb, root);
            AppendFeelKpisSingleFileReport(sb, root);
            int onboardMax = Mathf.Max(0, _onboardingMaxWaveInclusive);
            sb.AppendLine(
                "Onboarding-filter: " +
                (_suppressOnboardingBunkerFlags
                    ? $"aktiv — OBJECTIVE_INACTIVE, TRIVIAL_BUNKER & SWEET_SPOT_HINT undertrycks för wave ≤ {onboardMax}"
                    : "av — alla flaggor enligt heuristik v1"));
            sb.AppendLine();
            sb.AppendLine("--- Per-row heuristics (v1) ---");
            sb.AppendLine(
                "v1-skåror (0–1, offline, grovt normaliserade): " +
                "pressureScore ≈ bunkerPressureTimeSec / waveDurationSec; " +
                "combatScore ≈ kills/tid + skott+tungeldon/tid + träffratio (ingen bunker-skada); " +
                "survivalScore ≈ bunker-skada/tid + skada på hjälte/maxHP + (1−minBunkerHpRatio) när ratio giltig; " +
                "controlLossScore = 1 vid heroDead/bunkerBreached, annars (1−minRatio) och |slope| mot referens. " +
                "Justera vikter i editor-skriptet vid behov.");
            sb.AppendLine(
                "Pacing (v1): stressDelta = waveStressScore − föregående wave_cleared/run_end i filordning (första: —). " +
                "bunkerStart01 / heroStart01 = HP vid InWave-start / respektive max (samma rad). startHpBuffer01 = medel av de två. " +
                "survivalMarginV1 = startHpBuffer01 − pressureScore; survivalMarginMin01 = min(bunkerStart01, heroStart01) − pressureScore " +
                "(fångar t.ex. bunker redan låg medan hjälten är full).");
            sb.AppendLine(
                "pressureDeficitAvg01 = tidsvägt medel av (1 − bunkerHpRatio) från bunkerHpSamples (trapezoid mellan prover); " +
                "kompletterar pressureScore (tid under tröskel) med \"hur långt under full cover-kurvan\" under hela vågen.");
            sb.AppendLine(
                "pressureContinuity01 = andel bunkerHpSamples där bunkerHpRatio < root.bunkerPressureHpRatioThresholdUsed " +
                "(0–1; högt ≈ tryck under större delen av samplad tid, jämför med pressureScore).");
            sb.AppendLine();

            bool hasPrevWaveStress = false;
            float prevWaveStressScore = 0f;
            WaveChainPrev chainPrev = default;
            int overloadFailCount = 0;
            int pressureDominatedCount = 0;
            int bombDominatedBunkerRowCount = 0;
            int objectiveInactiveCount = 0;
            int rowsWithWaveEnd = 0;
            float pressureScoreSum = 0f;
            float pressureContinuitySum = 0f;
            float survivalMarginMinSum = 0f;

            var waveEndRows = new List<TelemetryEventDto>(root.events.Length);
            foreach (TelemetryEventDto e in root.events)
            {
                if (e == null || string.IsNullOrEmpty(e.kind))
                {
                    continue;
                }

                if (IsWaveEndKind(e.kind))
                {
                    waveEndRows.Add(e);
                }
            }

            if (waveEndRows.Count == 0)
            {
                var byKind = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (TelemetryEventDto e in root.events)
                {
                    if (e == null)
                    {
                        continue;
                    }

                    string nk = NormalizeKind(e.kind);
                    if (string.IsNullOrEmpty(nk))
                    {
                        nk = "(empty)";
                    }

                    if (!byKind.TryGetValue(nk, out int c))
                    {
                        c = 0;
                    }

                    byKind[nk] = c + 1;
                }

                sb.AppendLine("NOTE: Inga wave_cleared/run_end-rader hittades efter kind-normalisering.");
                sb.AppendLine("Kinds i filen (normaliserade):");
                foreach (KeyValuePair<string, int> kv in BatchSortedPairs(byKind))
                {
                    sb.AppendLine($"  {kv.Key}: {kv.Value}");
                }
                sb.AppendLine();
            }

            for (int wi = 0; wi < waveEndRows.Count; wi++)
            {
                TelemetryEventDto ev = waveEndRows[wi];
                TelemetryEventDto nextWaveEndRow =
                    wi + 1 < waveEndRows.Count ? waveEndRows[wi + 1] : null;

                List<string> flags = EvaluateFlags(
                    ev,
                    _suppressOnboardingBunkerFlags,
                    onboardMax,
                    pressureThr,
                    chainPrev,
                    nextWaveEndRow);
                float slope = EstimateBunkerHpSlopePerSec(ev);
                float pressureScore = ComputePressureScore01(ev);
                float combatScore01 = ComputeCombatScore01(ev);
                float survivalScore01 = ComputeSurvivalScore01(ev);
                float controlLossScore = ComputeControlLossScore01(ev, slope);
                float bunkerStart01 = ComputeBunkerStartHpRatio01(ev);
                float heroStart01 = ComputeHeroStartHpRatio01(ev);
                float startHpBuffer01 = ComputeStartHpBuffer01(ev);
                float survivalMarginV1 = startHpBuffer01 - pressureScore;
                float survivalMarginMin01 = Mathf.Min(bunkerStart01, heroStart01) - pressureScore;
                float pressureDeficitAvg01 = ComputePressureDeficitAvg01(ev);
                float pressureContinuity01 = ComputePressureContinuity01(ev, pressureThr);
                rowsWithWaveEnd++;
                pressureScoreSum += pressureScore;
                pressureContinuitySum += pressureContinuity01;
                survivalMarginMinSum += survivalMarginMin01;
                if (flags.Contains("OVERLOAD_FAIL"))
                {
                    overloadFailCount++;
                }
                if (flags.Contains("PRESSURE_DOMINATED_DURATION"))
                {
                    pressureDominatedCount++;
                }
                if (flags.Contains("BOMB_DOMINATED_BUNKER"))
                {
                    bombDominatedBunkerRowCount++;
                }
                if (flags.Contains("OBJECTIVE_INACTIVE"))
                {
                    objectiveInactiveCount++;
                }

                string stressDeltaText = hasPrevWaveStress
                    ? $"{ev.waveStressScore - prevWaveStressScore:0.##}"
                    : "—";

                sb.AppendLine(
                    $"[{ev.kind}] wave={ev.wave} dur={ev.waveDurationSec:0.##}s " +
                    $"kills={ev.enemiesKilled} bunkerDmg={ev.damageTakenBunker} bombBunkerDmg={ev.damageTakenBunkerFromBombs} " +
                    $"stress={ev.waveStressScore:0.#} press={ev.bunkerPressureTimeSec:0.##}s " +
                    $"pressPostHit={ev.bunkerPressureTimeAfterFirstDamageSec:0.##}s " +
                    $"minRatio={ev.minBunkerHpRatioThisWave:0.###} slope≈{slope:0.####}/s");
                sb.AppendLine(
                    $"  scores: pressureScore={pressureScore:0.###} combatScore01={combatScore01:0.###} survivalScore01={survivalScore01:0.###} controlLossScore={controlLossScore:0.###}");
                sb.AppendLine(
                    $"  pacing: stressDelta={stressDeltaText} bunkerStart01={bunkerStart01:0.###} heroStart01={heroStart01:0.###} startHpBuffer01={startHpBuffer01:0.###}");
                sb.AppendLine(
                    $"  pacing: survivalMarginV1={survivalMarginV1:0.###} survivalMarginMin01={survivalMarginMin01:0.###} pressureDeficitAvg01={pressureDeficitAvg01:0.###} pressureContinuity01={pressureContinuity01:0.###}");
                if (ev.shopOffersBoughtPrior != null && ev.shopOffersBoughtPrior.Length > 0)
                {
                    sb.AppendLine($"  shopOffersBoughtPrior: {string.Join(", ", ev.shopOffersBoughtPrior)}");
                }
                if (!string.IsNullOrEmpty(ev.waveScalingJson))
                {
                    sb.AppendLine("  waveScalingJson: " + Truncate(ev.waveScalingJson, 120));
                }

                sb.AppendLine(flags.Count > 0 ? "  → " + string.Join(", ", flags) : "  → (no flags)");
                sb.AppendLine();

                prevWaveStressScore = ev.waveStressScore;
                hasPrevWaveStress = true;
                chainPrev = new WaveChainPrev(ev.wave, ev.minBunkerHpRatioThisWave, ev.bunkerBreached);
            }

            if (rowsWithWaveEnd > 0)
            {
                float avgPressureScore = pressureScoreSum / rowsWithWaveEnd;
                float avgPressureContinuity = pressureContinuitySum / rowsWithWaveEnd;
                float avgSurvivalMarginMin = survivalMarginMinSum / rowsWithWaveEnd;
                sb.AppendLine("--- Auto-comment (v1) ---");
                string dominant =
                    overloadFailCount > 0 &&
                    (pressureDominatedCount > 0 || (avgPressureScore > 0.85f && avgPressureContinuity > 0.85f))
                        ? "Dominant failure mode: sustained bunker pressure (constant cover erosion over wave duration)."
                    : overloadFailCount > 0
                        ? "Dominant failure mode: acute overload collapse (terminal fail without continuous-pressure signature)."
                    : objectiveInactiveCount >= Mathf.Max(1, rowsWithWaveEnd / 2)
                        ? "Dominant mode: objective under-pressure not engaged (low bunker threat realized)."
                    : avgSurvivalMarginMin > 0.25f
                        ? "Dominant mode: comfortable buffer (likely under-tuned for this profile)."
                    : "Dominant mode: mixed signals (no single failure pattern dominates this file).";
                sb.AppendLine(dominant);
                sb.AppendLine(
                    $"Context: rows={rowsWithWaveEnd}, OVERLOAD_FAIL={overloadFailCount}, " +
                    $"PRESSURE_DOMINATED_DURATION={pressureDominatedCount}, BOMB_DOMINATED_BUNKER={bombDominatedBunkerRowCount}, " +
                    $"OBJECTIVE_INACTIVE={objectiveInactiveCount}, " +
                    $"avgPressureScore={avgPressureScore:0.###}, avgPressureContinuity={avgPressureContinuity:0.###}, " +
                    $"avgSurvivalMarginMin01={avgSurvivalMarginMin:0.###}");
                sb.AppendLine();
            }

            if (root.unityLogs != null && root.unityLogs.Length > 0)
            {
                sb.AppendLine("--- Unity console / errors (root.unityLogs[], senaste först i filen = högst index) ---");
                int show = Mathf.Min(root.unityLogs.Length, 25);
                int start = Mathf.Max(0, root.unityLogs.Length - show);
                for (int i = root.unityLogs.Length - 1; i >= start; i--)
                {
                    TelemetryUnityLogRowDto u = root.unityLogs[i];
                    if (u == null)
                    {
                        continue;
                    }

                    sb.AppendLine(
                        $"[{u.logType}] wave={u.wave} {u.waveLoopState} repeat={u.repeatCount} " +
                        $"hero={u.heroHp}/{u.heroMaxHp} ammo={u.heroAmmoInMag}/{u.heroAmmoMagMax}+{u.heroReserveAmmo} " +
                        $"{Truncate(u.weapon ?? "", 22)} trackedPara={u.trackedLivingParatroopers} fp={u.fingerprint}");
                    sb.AppendLine(
                        $"  ctx: timeScale={u.timeScale:0.###} inWaveUnscaledSec={u.inWaveUnscaledSec:0.###} " +
                        $"dmgH={u.damageTakenHeroThisWave} dmgBnk={u.damageTakenBunkerThisWave} bombBnk={u.damageTakenBunkerFromBombsThisWave} shots={u.shotsFiredThisWave} " +
                        $"bunkPressUnscaled={u.bunkerPressureTimeUnscaledThisWave:0.##}");
                    sb.AppendLine("  scene: " + Truncate(u.activeScenePathOrName ?? "", 100));
                    sb.AppendLine("  scaling: " + Truncate(u.scalingSnapshotShort ?? "", 200));
                    sb.AppendLine("  spawner: " + Truncate(u.spawnerDiagnosticsLine ?? "", 220));
                    sb.AppendLine("  msg: " + Truncate(u.messageTruncated ?? "", 220));
                    if (!string.IsNullOrEmpty(u.stackTraceTruncated))
                    {
                        sb.AppendLine("  stack: " + Truncate(u.stackTraceTruncated, 260));
                    }
                }

                sb.AppendLine();
            }

            _report = sb.ToString();
            _lastMessageType = MessageType.Info;
            _lastMessage = "Analysis complete.";
        }

        private void RunBatchSummary()
        {
            _lastMessage = "";
            _report = "";

            if (string.IsNullOrWhiteSpace(_batchFolderPath) || !Directory.Exists(_batchFolderPath))
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "Batch-mapp finns inte. Välj mapp med wave_run_*.json.";
                return;
            }

            var search = _batchRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(_batchFolderPath, "*.json", search);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int batchRuns = 0;
            long totalUnityLogRows = 0;
            int runsWithUnityLogs = 0;
            int readOrParseFailed = 0;
            int emptyEvents = 0;
            var noWaveEndFiles = new List<string>(8);
            int skippedByFilter = 0;
            var skipReasonCount = new Dictionary<string, int>(StringComparer.Ordinal);
            int runsWithRunEnd = 0;
            int runsWithoutRunEnd = 0;
            int runsWon = 0;
            var failWaves = new List<int>(files.Length);
            var runEndReasons = new Dictionary<string, int>(StringComparer.Ordinal);
            var failWaveHistogram = new Dictionary<int, int>();
            var flagRunCount = new Dictionary<string, int>(StringComparer.Ordinal);
            var flagRowCount = new Dictionary<string, int>(StringComparer.Ordinal);
            int totalWaveEndRows = 0;
            int runsWithSpawnStarvation = 0;
            int totalSpawnStarvedRows = 0;
            int totalSpawnerRecoveries = 0;
            int totalSpawnerFailedAttempts = 0;
            var minRatioWave2 = new List<float>(files.Length);
            var minRatioByWave = new Dictionary<int, List<float>>();
            for (int w = 1; w <= 12; w++)
            {
                minRatioByWave[w] = new List<float>(8);
            }

            var perFileLines = new List<string>(Math.Max(8, files.Length));
            var sceneProfilesSeen = new HashSet<string>(StringComparer.Ordinal);
            var autoHeroSeen = new HashSet<string>(StringComparer.Ordinal);
            int onboardMax = Mathf.Max(0, _onboardingMaxWaveInclusive);
            string sceneFilter = (_batchSceneProfileSubstr ?? "").Trim();
            string heroFilter = (_batchAutoHeroSubstr ?? "").Trim();
            double? maxAge = _batchMaxAgeHours > 0 ? _batchMaxAgeHours : (double?)null;

            foreach (string path in files)
            {
                string fn = Path.GetFileName(path);
                if (_batchWaveRunFilenameOnly &&
                    !fn.StartsWith("wave_run_", StringComparison.OrdinalIgnoreCase))
                {
                    skippedByFilter++;
                    BatchIncrement(skipReasonCount, "filnamn (ej wave_run_)");
                    continue;
                }

                if (maxAge.HasValue)
                {
                    double hours = (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalHours;
                    if (hours > maxAge.Value)
                    {
                        skippedByFilter++;
                        BatchIncrement(skipReasonCount, "filålder");
                        continue;
                    }
                }

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch
                {
                    readOrParseFailed++;
                    continue;
                }

                TelemetryFileRootDto root;
                try
                {
                    root = JsonUtility.FromJson<TelemetryFileRootDto>(text);
                }
                catch
                {
                    readOrParseFailed++;
                    continue;
                }

                if (root == null || root.events == null || root.events.Length == 0)
                {
                    emptyEvents++;
                    noWaveEndFiles.Add(fn + " (events[] empty)");
                    continue;
                }

                TryGetSessionMeta(root.events, out string sessionId, out string sceneProfileId, out string autoHeroProfile);
                if (!string.IsNullOrEmpty(sceneProfileId))
                {
                    sceneProfilesSeen.Add(sceneProfileId);
                }

                if (!string.IsNullOrEmpty(autoHeroProfile))
                {
                    autoHeroSeen.Add(autoHeroProfile);
                }

                if (!string.IsNullOrEmpty(sceneFilter) &&
                    (string.IsNullOrEmpty(sceneProfileId) ||
                     sceneProfileId.IndexOf(sceneFilter, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    skippedByFilter++;
                    BatchIncrement(skipReasonCount, "scene profile");
                    continue;
                }

                if (!string.IsNullOrEmpty(heroFilter) &&
                    (string.IsNullOrEmpty(autoHeroProfile) ||
                     autoHeroProfile.IndexOf(heroFilter, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    skippedByFilter++;
                    BatchIncrement(skipReasonCount, "AutoHero");
                    continue;
                }

                float pressureThr = root.bunkerPressureHpRatioThresholdUsed > 0.001f
                    ? root.bunkerPressureHpRatioThresholdUsed
                    : 0.8f;

                var waveEndRows = new List<TelemetryEventDto>(root.events.Length);
                foreach (TelemetryEventDto e in root.events)
                {
                    if (e == null || string.IsNullOrEmpty(e.kind))
                    {
                        continue;
                    }

                    if (IsWaveEndKind(e.kind))
                    {
                        waveEndRows.Add(e);
                    }
                }

                if (waveEndRows.Count == 0)
                {
                    emptyEvents++;
                    int sessionQuitRows = 0;
                    int sessionBeginRows = 0;
                    for (int i = 0; i < root.events.Length; i++)
                    {
                        TelemetryEventDto ev = root.events[i];
                        string k = NormalizeKind(ev != null ? ev.kind : null);
                        if (k == "session_quit")
                        {
                            sessionQuitRows++;
                        }
                        else if (k == "session_begin")
                        {
                            sessionBeginRows++;
                        }
                    }

                    noWaveEndFiles.Add(
                        $"{fn} (no wave_end rows; session_begin={sessionBeginRows}, session_quit={sessionQuitRows})");
                    continue;
                }

                var flagsThisRun = new HashSet<string>(StringComparer.Ordinal);
                int bombDominatedWaveRowsThisRun = 0;
                int spawnStarvedRowsThisRun = 0;
                int spawnerRecoveriesThisRun = 0;
                int spawnerFailedAttemptsThisRun = 0;
                string lastSpawnerExitReasonThisRun = "";
                string lastSpawnerAbortReasonThisRun = "";
                WaveChainPrev chainPrev = default;
                int maxWaveCleared = 0;
                for (int wi = 0; wi < waveEndRows.Count; wi++)
                {
                    TelemetryEventDto ev = waveEndRows[wi];
                    TelemetryEventDto nextWaveEndRow =
                        wi + 1 < waveEndRows.Count ? waveEndRows[wi + 1] : null;
                    List<string> flags = EvaluateFlags(
                        ev,
                        _suppressOnboardingBunkerFlags,
                        onboardMax,
                        pressureThr,
                        chainPrev,
                        nextWaveEndRow);
                    foreach (string f in flags)
                    {
                        flagsThisRun.Add(f);
                    }

                    totalWaveEndRows++;
                    if (ev.spawnerSpawnStarved)
                    {
                        spawnStarvedRowsThisRun++;
                        totalSpawnStarvedRows++;
                    }

                    spawnerRecoveriesThisRun += Mathf.Max(0, ev.spawnerRecoveryCount);
                    spawnerFailedAttemptsThisRun += Mathf.Max(0, ev.spawnerFailedSpawnAttempts);
                    if (!string.IsNullOrWhiteSpace(ev.spawnerSpawnRoutineExitReason))
                    {
                        lastSpawnerExitReasonThisRun = ev.spawnerSpawnRoutineExitReason;
                    }

                    if (!string.IsNullOrWhiteSpace(ev.spawnerLastSpawnAbortReason))
                    {
                        lastSpawnerAbortReasonThisRun = ev.spawnerLastSpawnAbortReason;
                    }

                    foreach (string f in flags)
                    {
                        BatchIncrement(flagRowCount, f);
                    }

                    if (flags.Contains("BOMB_DOMINATED_BUNKER"))
                    {
                        bombDominatedWaveRowsThisRun++;
                    }

                    if (ev.kind == "wave_cleared")
                    {
                        maxWaveCleared = Mathf.Max(maxWaveCleared, ev.wave);
                        if (ev.wave == 2 &&
                            ev.minBunkerHpRatioThisWave >= 0f &&
                            ev.minBunkerHpRatioThisWave <= 1f)
                        {
                            minRatioWave2.Add(ev.minBunkerHpRatioThisWave);
                        }

                        if (ev.wave >= 1 &&
                            ev.wave <= 12 &&
                            ev.minBunkerHpRatioThisWave >= 0f &&
                            ev.minBunkerHpRatioThisWave <= 1f &&
                            minRatioByWave.TryGetValue(ev.wave, out List<float> bucket))
                        {
                            bucket.Add(ev.minBunkerHpRatioThisWave);
                        }
                    }

                    chainPrev = new WaveChainPrev(ev.wave, ev.minBunkerHpRatioThisWave, ev.bunkerBreached);
                }

                TelemetryEventDto lastRunEnd = null;
                for (int i = waveEndRows.Count - 1; i >= 0; i--)
                {
                    if (NormalizeKind(waveEndRows[i].kind) == "run_end")
                    {
                        lastRunEnd = waveEndRows[i];
                        break;
                    }
                }

                int failWave = -1;
                if (lastRunEnd != null)
                {
                    runsWithRunEnd++;
                    string er = string.IsNullOrEmpty(lastRunEnd.endReason) ? "(tom)" : lastRunEnd.endReason;
                    BatchIncrement(runEndReasons, er);
                    bool isWin = er.Equals("game_won", StringComparison.OrdinalIgnoreCase);
                    if (isWin)
                    {
                        runsWon++;
                    }
                    else
                    {
                        failWave = lastRunEnd.wave;
                        failWaves.Add(failWave);
                        BatchIncrement(failWaveHistogram, failWave);
                    }
                }
                else
                {
                    runsWithoutRunEnd++;
                }

                foreach (string f in flagsThisRun)
                {
                    BatchIncrement(flagRunCount, f);
                }

                double sessionDur = 0.0;
                if (root.events.Length >= 2)
                {
                    TelemetryEventDto a = root.events[0];
                    TelemetryEventDto b = root.events[root.events.Length - 1];
                    if (a != null && b != null)
                    {
                        sessionDur = b.realtimeSinceStartup - a.realtimeSinceStartup;
                    }
                }

                string flagsJoined = flagsThisRun.Count > 0 ? string.Join(";", flagsThisRun) : "(inga)";
                string deathText = failWave >= 0 ? failWave.ToString(CultureInfo.InvariantCulture) : "—";
                int unityRowsThisFile = root.unityLogs != null ? root.unityLogs.Length : 0;
                if (spawnStarvedRowsThisRun > 0)
                {
                    runsWithSpawnStarvation++;
                }

                totalSpawnerRecoveries += spawnerRecoveriesThisRun;
                totalSpawnerFailedAttempts += spawnerFailedAttemptsThisRun;
                if (unityRowsThisFile > 0)
                {
                    runsWithUnityLogs++;
                    totalUnityLogRows += unityRowsThisFile;
                }

                ComputeFeelWaveAggregates(
                    root.events,
                    out float feelBunkerFrac,
                    out float feelCombatPerHeroDmg,
                    out float feelOverwSec,
                    out float feelTotalDur);
                int feelMaxHpDelta = ComputeHeroMaxHpDeltaAfterFirstShop(root.events, root.feelSession);
                float feelSurvAfterShop = ComputeSurvivalRealtimeAfterFirstShop(root.events, root.feelSession);
                string FeelF(float v) =>
                    v >= 0f ? v.ToString("0.###", CultureInfo.InvariantCulture) : "";
                TelemetryFeelSessionSummaryDto fs = root.feelSession;
                string feelTsv =
                    $"\t{(fs != null ? FeelF(fs.firstKillSecSinceSessionBegin) : "")}" +
                    $"\t{(fs != null ? FeelF(fs.firstHeroDamageSecSinceSessionBegin) : "")}" +
                    $"\t{(fs != null ? FeelF(fs.firstHeroDeathSecSinceSessionBegin) : "")}" +
                    $"\t{(fs != null ? FeelF(fs.firstShopPurchaseSecSinceSessionBegin) : "")}" +
                    $"\t{feelBunkerFrac.ToString("0.###", CultureInfo.InvariantCulture)}" +
                    $"\t{feelCombatPerHeroDmg.ToString("0.###", CultureInfo.InvariantCulture)}" +
                    $"\t{feelOverwSec.ToString("0.##", CultureInfo.InvariantCulture)}" +
                    $"\t{feelMaxHpDelta.ToString(CultureInfo.InvariantCulture)}" +
                    $"\t{(feelSurvAfterShop >= 0f ? feelSurvAfterShop.ToString("0.##", CultureInfo.InvariantCulture) : "")}";

                string tsvLine =
                    $"{fn}\t{sessionId}\t{sceneProfileId}\t{autoHeroProfile}\t{deathText}\t{maxWaveCleared}\t{sessionDur:0.#}\t{flagsJoined}\t{unityRowsThisFile}" +
                    $"\t{spawnStarvedRowsThisRun}\t{spawnerRecoveriesThisRun}\t{spawnerFailedAttemptsThisRun}\t{lastSpawnerExitReasonThisRun}\t{lastSpawnerAbortReasonThisRun}" +
                    $"{feelTsv}" +
                    $"\t{bombDominatedWaveRowsThisRun}";
                perFileLines.Add(tsvLine);

                batchRuns++;
            }

            EditorPrefs.SetString(PrefBatchFolder, _batchFolderPath);

            var sb = new StringBuilder(8192);
            sb.AppendLine("BATCH — wave telemetry (v1-heuristik, offline)");
            sb.AppendLine();
            sb.AppendLine(AnalyzerTelemetryReferenceSv);
            sb.AppendLine();
            sb.AppendLine($"Mapp: {_batchFolderPath}");
            sb.AppendLine($"Sökning: {(_batchRecursive ? "rekursiv" : "endast vald mapp")}");
            sb.AppendLine($"Filmatch: *.json{(_batchWaveRunFilenameOnly ? "; namn måste börja med wave_run_" : "")}");
            if (_batchMaxAgeHours > 0)
            {
                sb.AppendLine($"Max filålder: senaste {_batchMaxAgeHours} h (UTC ändringstid)");
            }

            if (!string.IsNullOrEmpty(sceneFilter))
            {
                sb.AppendLine($"Filter scene profile (substring): «{sceneFilter}»");
            }

            if (!string.IsNullOrEmpty(heroFilter))
            {
                sb.AppendLine($"Filter AutoHero (substring): «{heroFilter}»");
            }

            sb.AppendLine(
                "Onboarding-filter: " +
                (_suppressOnboardingBunkerFlags
                    ? $"aktiv för våg ≤ {onboardMax} (samma som enkelfils «Analyze»)"
                    : "av"));
            sb.AppendLine();
            sb.AppendLine($"JSON-filer matchade (*.json): {files.Length}");
            sb.AppendLine(
                $"Unity console-rader (root.unityLogs[]): {totalUnityLogRows} totalt i {runsWithUnityLogs} körningar (minst en rad)");
            sb.AppendLine($"Hoppade över (filter): {skippedByFilter}");
            if (skippedByFilter > 0 && skipReasonCount.Count > 0)
            {
                foreach (KeyValuePair<string, int> kv in BatchSortedPairs(skipReasonCount))
                {
                    sb.AppendLine($"  • {kv.Key}: {kv.Value}");
                }
            }

            sb.AppendLine($"Lyckade körningar (parse OK, events, efter filter): {batchRuns}");
            sb.AppendLine($"Läs/parse-fel: {readOrParseFailed}");
            sb.AppendLine($"Tom events[] / inga wave-rader: {emptyEvents}");
            if (noWaveEndFiles.Count > 0)
            {
                sb.AppendLine("  Exkluderade filer (inga wave_cleared/run_end):");
                for (int i = 0; i < noWaveEndFiles.Count; i++)
                {
                    sb.AppendLine("  - " + noWaveEndFiles[i]);
                }
            }
            sb.AppendLine();

            if (sceneProfilesSeen.Count > 0)
            {
                var sp = new List<string>(sceneProfilesSeen);
                sp.Sort(StringComparer.OrdinalIgnoreCase);
                sb.AppendLine("--- sceneProfileId i batch (unika) ---");
                sb.AppendLine(string.Join(", ", sp));
                sb.AppendLine();
            }

            if (autoHeroSeen.Count > 0)
            {
                var ah = new List<string>(autoHeroSeen);
                ah.Sort(StringComparer.OrdinalIgnoreCase);
                sb.AppendLine("--- autoHeroTestProfile i batch (unika) ---");
                sb.AppendLine(string.Join(", ", ah));
                sb.AppendLine();
            }

            if (batchRuns == 0)
            {
                sb.AppendLine("Ingen giltig data — inget att sammanfatta.");
                _report = sb.ToString();
                _lastMessageType = MessageType.Warning;
                _lastMessage = "Batch: 0 runs.";
                return;
            }

            sb.AppendLine("--- Run end (sista run_end i filen) ---");
            sb.AppendLine($"Körningar med run_end: {runsWithRunEnd} ({100f * runsWithRunEnd / batchRuns:0.#} %)");
            sb.AppendLine($"Körningar utan run_end: {runsWithoutRunEnd} ({100f * runsWithoutRunEnd / batchRuns:0.#} %)");
            sb.AppendLine($"Körningar med game_won: {runsWon} ({100f * runsWon / batchRuns:0.#} %)");
            if (failWaves.Count > 0)
            {
                float meanDw = MeanInt(failWaves);
                float medDw = MedianInt(failWaves);
                float sdDw = StdDevInt(failWaves);
                int minDw = MinInt(failWaves);
                int maxDw = MaxInt(failWaves);
                sb.AppendLine(
                    $"Fail-våg (run_end.wave, exkl. game_won): medel {meanDw:0.##}, median {medDw:0.##}, std.avvik. {sdDw:0.##}, min {minDw}, max {maxDw}");
                sb.AppendLine();
                sb.AppendLine("Histogram (antal körningar per fail-våg):");
                foreach (KeyValuePair<int, int> kv in BatchSortedIntKeys(failWaveHistogram))
                {
                    float pct = 100f * kv.Value / failWaves.Count;
                    sb.AppendLine($"  våg {kv.Key}: {kv.Value} ({pct:0.#} % av run_end-körningar)");
                }
            }
            else
            {
                sb.AppendLine("Ingen fail run_end i batch (antingen bara session_quit eller enbart game_won).");
            }

            if (runEndReasons.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- endReason (sista run_end) ---");
                int totalEr = 0;
                foreach (int c in runEndReasons.Values)
                {
                    totalEr += c;
                }

                foreach (KeyValuePair<string, int> kv in BatchSortedPairs(runEndReasons))
                {
                    sb.AppendLine($"  {kv.Key}: {kv.Value} ({100f * kv.Value / Mathf.Max(1, totalEr):0.#} %)");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("--- Spawn starvation (nya spawner-fält) ---");
            sb.AppendLine(
                $"Körningar med minst en starvation-rad: {runsWithSpawnStarvation}/{batchRuns} ({100f * runsWithSpawnStarvation / Mathf.Max(1, batchRuns):0.#} %)");
            sb.AppendLine($"Starvation-rader totalt (wave_cleared/run_end): {totalSpawnStarvedRows}/{totalWaveEndRows}");
            sb.AppendLine($"Summa recovery-försök (spawnerRecoveryCount): {totalSpawnerRecoveries}");
            sb.AppendLine($"Summa misslyckade spawn-försök (spawnerFailedSpawnAttempts): {totalSpawnerFailedAttempts}");

            sb.AppendLine();
            sb.AppendLine("--- minBunkerHpRatioThisWave på wave_cleared wave=2 ---");
            if (minRatioWave2.Count > 0)
            {
                sb.AppendLine($"Antal körningar med wave=2-rad: {minRatioWave2.Count}");
                sb.AppendLine($"Snitt: {MeanFloat(minRatioWave2):0.###}, median: {MedianFloat(minRatioWave2):0.###}");
            }
            else
            {
                sb.AppendLine("Ingen wave_cleared wave=2 i batch.");
            }

            sb.AppendLine();
            sb.AppendLine("--- minBunkerHpRatioThisWave (wave_cleared) per våg 1–8 — snitt / median / n ---");
            for (int w = 1; w <= 8; w++)
            {
                if (!minRatioByWave.TryGetValue(w, out List<float> bucket) || bucket.Count == 0)
                {
                    continue;
                }

                sb.AppendLine(
                    $"  wave {w}: n={bucket.Count}, snitt={MeanFloat(bucket):0.###}, median={MedianFloat(bucket):0.###}");
            }

            sb.AppendLine();
            sb.AppendLine("--- Flaggor: andel körningar (minst en gång per session) ---");
            foreach (string flag in BatchSortedKeys(flagRunCount))
            {
                int n = flagRunCount[flag];
                sb.AppendLine($"{flag}: {100f * n / batchRuns:0.#} % ({n}/{batchRuns})");
            }

            sb.AppendLine();
            sb.AppendLine("--- Weekly telemetry gates (retention v1) ---");
            int runsWithDeath = failWaves.Count;
            float avgFailWave = runsWithDeath > 0 ? MeanInt(failWaves) : 0f;
            float gameWonRate = 100f * runsWon / Mathf.Max(1, batchRuns);
            float shopConversionRunRate = 0f;
            if (perFileLines.Count > 0)
            {
                int withFirstShop = 0;
                for (int i = 0; i < perFileLines.Count; i++)
                {
                    // TSV: feel_firstShopSec is column index 17 (0-based).
                    string[] cols = perFileLines[i].Split('\t');
                    if (cols.Length > 17 && !string.IsNullOrWhiteSpace(cols[17]))
                    {
                        withFirstShop++;
                    }
                }

                shopConversionRunRate = 100f * withFirstShop / perFileLines.Count;
            }

            bool gateFailWave = runsWithDeath == 0 || avgFailWave >= 6f;
            bool gateShop = shopConversionRunRate >= 65f;
            bool gateWin = gameWonRate >= 20f;
            sb.AppendLine(
                $"Gate_A_avgFailWave>=6: {(gateFailWave ? "PASS" : "FAIL")} (avgFailWave={avgFailWave:0.##}, runsWithDeath={runsWithDeath})");
            sb.AppendLine(
                $"Gate_B_shopConversionRuns>=65%: {(gateShop ? "PASS" : "FAIL")} (shopConversionRuns={shopConversionRunRate:0.##}%)");
            sb.AppendLine(
                $"Gate_C_gameWonRate>=20%: {(gateWin ? "PASS" : "FAIL")} (gameWonRate={gameWonRate:0.##}%)");

            if (totalWaveEndRows > 0 && flagRowCount.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Flaggor: andel wave_cleared/run_end-rader (aggregerat över alla körningar) ---");
                foreach (string flag in BatchSortedKeys(flagRowCount))
                {
                    int n = flagRowCount[flag];
                    sb.AppendLine($"{flag}: {100f * n / totalWaveEndRows:0.#} % ({n}/{totalWaveEndRows} rader)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("--- Per fil (TSV — klistra i Excel) ---");
            sb.AppendLine("Kolumnbeskrivning: se avsnittet «Batch-TSV» i ordlistan överst i denna batch-rapport.");
            sb.AppendLine(
                "fil\tsessionId\tsceneProfileId\tautoHero\tfailWave\tmaxWaveCleared\tsessionSec\tflags\tunityLogRows\t" +
                "spawnStarvedRows\tspawnRecoveries\tspawnFailedAttempts\tspawnExitReasonLast\tspawnAbortReasonLast\t" +
                "feel_firstKillSec\tfeel_firstDmgSec\tfeel_firstDeathSec\tfeel_firstShopSec\t" +
                "feel_bunkerTimeFrac01\tfeel_combatPerHeroDmgTaken\tfeel_overwhelmedInWaveSecSum\t" +
                "feel_heroMaxHpDeltaAfterFirstShop\tfeel_survSecAfterFirstShop\tbombDominatedWaveRows");
            foreach (string line in perFileLines)
            {
                sb.AppendLine(line);
            }

            _report = sb.ToString();
            _lastMessageType = MessageType.Info;
            _lastMessage = $"Batch klar: {batchRuns} körningar, {files.Length} json-filer, {skippedByFilter} hoppade (filter).";
        }

        private void SaveBatchReportToBatchFolder()
        {
            if (string.IsNullOrWhiteSpace(_batchFolderPath) || !Directory.Exists(_batchFolderPath))
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "Batch-mapp finns inte. Välj mapp först.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_report))
            {
                _lastMessageType = MessageType.Warning;
                _lastMessage = "Ingen rapport att spara ännu. Kör «Summarize folder» först.";
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"batch_summary_{stamp}.txt";
            string path = Path.Combine(_batchFolderPath, fileName);
            try
            {
                File.WriteAllText(path, _report, Encoding.UTF8);
                _lastMessageType = MessageType.Info;
                _lastMessage = $"Batchrapport sparad: {path}";
            }
            catch (Exception ex)
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "Kunde inte spara batchrapport: " + ex.Message;
            }
        }

        private void SaveSingleAnalysisReportToJsonFolder()
        {
            if (string.IsNullOrWhiteSpace(_report))
            {
                _lastMessageType = MessageType.Warning;
                _lastMessage = "Ingen rapport att spara ännu. Kör «Analyze» först.";
                return;
            }

            string dir = "";
            if (!string.IsNullOrWhiteSpace(_jsonPath))
            {
                if (Directory.Exists(_jsonPath))
                {
                    dir = _jsonPath;
                }
                else
                {
                    dir = Path.GetDirectoryName(_jsonPath) ?? "";
                }
            }

            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "Kunde inte avgöra mål-mapp från JSON file. Välj en giltig JSON först.";
                return;
            }

            string stem = "wave_analysis";
            if (!string.IsNullOrWhiteSpace(_jsonPath) && File.Exists(_jsonPath))
            {
                stem = Path.GetFileNameWithoutExtension(_jsonPath);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{stem}_analysis_{stamp}.txt";
            string path = Path.Combine(dir, fileName);
            try
            {
                File.WriteAllText(path, _report, Encoding.UTF8);
                _lastMessageType = MessageType.Info;
                _lastMessage = $"Analysrapport sparad: {path}";
            }
            catch (Exception ex)
            {
                _lastMessageType = MessageType.Error;
                _lastMessage = "Kunde inte spara analysrapport: " + ex.Message;
            }
        }

        private static void TryGetSessionMeta(
            TelemetryEventDto[] events,
            out string sessionId,
            out string sceneProfileId,
            out string autoHeroProfile)
        {
            sessionId = "";
            sceneProfileId = "";
            autoHeroProfile = "";
            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                TelemetryEventDto e = events[i];
                if (e == null || e.kind != "session_begin")
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(e.sessionId))
                {
                    sessionId = e.sessionId;
                }

                if (!string.IsNullOrEmpty(e.sceneProfileId))
                {
                    sceneProfileId = e.sceneProfileId;
                }

                if (!string.IsNullOrEmpty(e.autoHeroTestProfile))
                {
                    autoHeroProfile = e.autoHeroTestProfile;
                }

                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                TelemetryEventDto e = events[i];
                if (e == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(e.sessionId))
                {
                    sessionId = e.sessionId;
                }

                if (string.IsNullOrEmpty(sceneProfileId) && !string.IsNullOrEmpty(e.sceneProfileId))
                {
                    sceneProfileId = e.sceneProfileId;
                }

                if (string.IsNullOrEmpty(autoHeroProfile) && !string.IsNullOrEmpty(e.autoHeroTestProfile))
                {
                    autoHeroProfile = e.autoHeroTestProfile;
                }
            }
        }

        private static void BatchIncrement(Dictionary<string, int> dict, string key)
        {
            if (!dict.TryGetValue(key, out int c))
            {
                c = 0;
            }

            dict[key] = c + 1;
        }

        private static void BatchIncrement(Dictionary<int, int> dict, int key)
        {
            if (!dict.TryGetValue(key, out int c))
            {
                c = 0;
            }

            dict[key] = c + 1;
        }

        private static List<KeyValuePair<string, int>> BatchSortedPairs(Dictionary<string, int> dict)
        {
            var list = new List<KeyValuePair<string, int>>(dict);
            list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static List<string> BatchSortedKeys(Dictionary<string, int> dict)
        {
            var keys = new List<string>(dict.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static List<KeyValuePair<int, int>> BatchSortedIntKeys(Dictionary<int, int> dict)
        {
            var list = new List<KeyValuePair<int, int>>(dict);
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list;
        }

        private static float StdDevInt(List<int> values)
        {
            if (values == null || values.Count < 2)
            {
                return 0f;
            }

            float m = MeanInt(values);
            double s2 = 0.0;
            for (int i = 0; i < values.Count; i++)
            {
                double d = values[i] - m;
                s2 += d * d;
            }

            return (float)Math.Sqrt(s2 / values.Count);
        }

        private static int MinInt(List<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }

            int m = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                m = Mathf.Min(m, values[i]);
            }

            return m;
        }

        private static int MaxInt(List<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }

            int m = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                m = Mathf.Max(m, values[i]);
            }

            return m;
        }

        private static float MedianFloat(List<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return float.NaN;
            }

            var copy = new List<float>(values);
            copy.Sort();
            int n = copy.Count;
            int m = n / 2;
            if ((n & 1) == 1)
            {
                return copy[m];
            }

            return 0.5f * (copy[m - 1] + copy[m]);
        }

        private static float MeanInt(List<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return float.NaN;
            }

            double s = 0;
            for (int i = 0; i < values.Count; i++)
            {
                s += values[i];
            }

            return (float)(s / values.Count);
        }

        private static float MedianInt(List<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return float.NaN;
            }

            var copy = new List<int>(values);
            copy.Sort();
            int n = copy.Count;
            int m = n / 2;
            if ((n & 1) == 1)
            {
                return copy[m];
            }

            return 0.5f * (copy[m - 1] + copy[m]);
        }

        private static float MeanFloat(List<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return float.NaN;
            }

            double s = 0;
            for (int i = 0; i < values.Count; i++)
            {
                s += values[i];
            }

            return (float)(s / values.Count);
        }

        private static float ComputeBunkerStartHpRatio01(TelemetryEventDto ev)
        {
            return Mathf.Clamp01((float)ev.bunkerHpWaveStart / Mathf.Max(1, ev.bunkerMaxHp));
        }

        private static float ComputeHeroStartHpRatio01(TelemetryEventDto ev)
        {
            return Mathf.Clamp01((float)ev.heroHpWaveStart / Mathf.Max(1, ev.heroMaxHp));
        }

        /// <summary>Mean of bunker/hero start ratios (same row max HP as proxy).</summary>
        private static float ComputeStartHpBuffer01(TelemetryEventDto ev)
        {
            return 0.5f * (ComputeBunkerStartHpRatio01(ev) + ComputeHeroStartHpRatio01(ev));
        }

        /// <summary>Time-weighted mean (1 − bunkerHpRatio) from samples; complements threshold-based pressureScore.</summary>
        private static float ComputePressureDeficitAvg01(TelemetryEventDto ev)
        {
            BunkerHpSampleDto[] s = ev.bunkerHpSamples;
            if (s == null || s.Length < 2)
            {
                return 0f;
            }

            float sumDeficitDt = 0f;
            for (int i = 1; i < s.Length; i++)
            {
                float t0 = s[i - 1].waveTimeSecSinceInWaveRealtime;
                float t1 = s[i].waveTimeSecSinceInWaveRealtime;
                float dt = t1 - t0;
                if (dt <= 0f)
                {
                    continue;
                }

                float r0 = Mathf.Clamp01(s[i - 1].bunkerHpRatio);
                float r1 = Mathf.Clamp01(s[i].bunkerHpRatio);
                float avgDeficit = 0.5f * ((1f - r0) + (1f - r1));
                sumDeficitDt += avgDeficit * dt;
            }

            float dur = Mathf.Max(0.05f, ev.waveDurationSec);
            return Mathf.Clamp01(sumDeficitDt / dur);
        }

        /// <summary>Share of bunkerHpSamples strictly below pressure ratio threshold (discrete continuity proxy).</summary>
        private static float ComputePressureContinuity01(TelemetryEventDto ev, float bunkerPressureHpRatioThreshold)
        {
            BunkerHpSampleDto[] s = ev.bunkerHpSamples;
            if (s == null || s.Length == 0)
            {
                return 0f;
            }

            float thr = Mathf.Clamp01(bunkerPressureHpRatioThreshold);
            int below = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (Mathf.Clamp01(s[i].bunkerHpRatio) < thr)
                {
                    below++;
                }
            }

            return (float)below / s.Length;
        }

        /// <summary>Sustained bunker pressure vs wave length (0–1).</summary>
        private static float ComputePressureScore01(TelemetryEventDto ev)
        {
            float dur = ev.waveDurationSec;
            if (dur < 0.05f)
            {
                return 0f;
            }

            return Mathf.Clamp01(ev.bunkerPressureTimeSec / dur);
        }

        /// <summary>Weapon / enemy interaction only (no bunker damage).</summary>
        private static float ComputeCombatScore01(TelemetryEventDto ev)
        {
            float dur = Mathf.Max(0.05f, ev.waveDurationSec);
            float killsNorm = Mathf.Clamp01(ev.enemiesKilled / (dur * 0.22f));
            int attacks = Mathf.Max(0, ev.shotsFired + ev.projectileLaunches);
            float attacksNorm = Mathf.Clamp01(attacks / (dur * 3.5f));
            int hitWindow = ev.rayHits + ev.rayMisses;
            float acc = hitWindow > 0 ? (float)ev.rayHits / hitWindow : 0.45f;
            return Mathf.Clamp01(0.42f * killsNorm + 0.35f * attacksNorm + 0.23f * acc);
        }

        /// <summary>Incoming threat to hero + bunker objective (damage rates and lowest cover depth).</summary>
        private static float ComputeSurvivalScore01(TelemetryEventDto ev)
        {
            float dur = Mathf.Max(0.05f, ev.waveDurationSec);
            float bunkerDpsNorm = Mathf.Clamp01(ev.damageTakenBunker / (dur * 18f));
            int hMax = Mathf.Max(1, ev.heroMaxHp);
            float heroDamagePool01 = Mathf.Clamp01((float)ev.damageTakenHero / hMax);
            float minR = ev.minBunkerHpRatioThisWave;
            float coverDepth01 = minR >= 0f && minR <= 1.0001f ? Mathf.Clamp01(1f - minR) : 0f;
            return Mathf.Clamp01(0.38f * bunkerDpsNorm + 0.37f * heroDamagePool01 + 0.25f * coverDepth01);
        }

        /// <summary>Fail / collapse: terminal state → 1; else depth from minRatio + bunker ratio slope (0–1).</summary>
        private static float ComputeControlLossScore01(TelemetryEventDto ev, float slopePerSec)
        {
            if (ev.heroDead || ev.bunkerBreached)
            {
                return 1f;
            }

            float minR = ev.minBunkerHpRatioThisWave;
            if (minR < 0f || minR > 1.0001f)
            {
                minR = 1f;
            }

            float depth = Mathf.Clamp01(1f - minR);
            float slopeNorm = Mathf.Clamp01(Mathf.Abs(slopePerSec) / 0.1f);
            return Mathf.Clamp01(depth * 0.62f + slopeNorm * 0.38f);
        }

        /// <summary>Hero damage taken per second above this counts the whole wave duration toward "overwhelmed" time.</summary>
        private const float FeelOverwhelmedHeroDps = 10f;

        private static string FeelFirst90sLabel(float secSinceSessionBegin)
        {
            if (secSinceSessionBegin < 0f)
            {
                return "—";
            }

            return secSinceSessionBegin <= 90f ? "ja" : "nej";
        }

        private static void ComputeFeelWaveAggregates(
            TelemetryEventDto[] events,
            out float bunkerTimeFrac01,
            out float combatProxyPerHeroDamageTaken,
            out float overwhelmedInWaveSecSum,
            out float totalWaveClearedInWaveSec)
        {
            bunkerTimeFrac01 = 0f;
            combatProxyPerHeroDamageTaken = 0f;
            overwhelmedInWaveSecSum = 0f;
            totalWaveClearedInWaveSec = 0f;
            if (events == null || events.Length == 0)
            {
                return;
            }

            double bunkerSum = 0.0;
            double durSum = 0.0;
            double combatSum = 0.0;
            int heroDmgSum = 0;
            double overwhelmDur = 0.0;
            for (int i = 0; i < events.Length; i++)
            {
                TelemetryEventDto e = events[i];
                if (e == null || NormalizeKind(e.kind) != "wave_cleared")
                {
                    continue;
                }

                float dur = Mathf.Max(0f, e.waveDurationSec);
                if (dur < 0.0001f)
                {
                    continue;
                }

                durSum += dur;
                bunkerSum += Mathf.Max(0f, e.timeInBunkerSec);
                int combatProxy = Mathf.Max(0, e.rayHits + e.projectileLaunches + e.enemiesKilled);
                combatSum += combatProxy;
                heroDmgSum += Mathf.Max(0, e.damageTakenHero);
                float dps = e.damageTakenHero / dur;
                if (dps >= FeelOverwhelmedHeroDps)
                {
                    overwhelmDur += dur;
                }
            }

            totalWaveClearedInWaveSec = (float)durSum;
            if (durSum > 0.0001)
            {
                bunkerTimeFrac01 = Mathf.Clamp01((float)(bunkerSum / durSum));
            }

            combatProxyPerHeroDamageTaken = heroDmgSum > 0 ? (float)(combatSum / heroDmgSum) : (float)combatSum;
            overwhelmedInWaveSecSum = (float)overwhelmDur;
        }

        private static int ComputeHeroMaxHpDeltaAfterFirstShop(
            TelemetryEventDto[] events,
            TelemetryFeelSessionSummaryDto feel)
        {
            if (feel == null ||
                feel.firstShopPurchaseRealtimeSinceStartup <= 0f ||
                feel.firstPurchaseHeroMaxHp <= 0)
            {
                return 0;
            }

            float shopRt = feel.firstShopPurchaseRealtimeSinceStartup;
            if (events == null)
            {
                return 0;
            }

            for (int i = 0; i < events.Length; i++)
            {
                TelemetryEventDto e = events[i];
                if (e == null || NormalizeKind(e.kind) != "wave_cleared")
                {
                    continue;
                }

                if (e.realtimeSinceStartup > shopRt && e.heroMaxHp > 0)
                {
                    return e.heroMaxHp - feel.firstPurchaseHeroMaxHp;
                }
            }

            return 0;
        }

        private static float ComputeSurvivalRealtimeAfterFirstShop(
            TelemetryEventDto[] events,
            TelemetryFeelSessionSummaryDto feel)
        {
            if (feel == null || feel.firstShopPurchaseRealtimeSinceStartup <= 0f || events == null)
            {
                return -1f;
            }

            float shopRt = feel.firstShopPurchaseRealtimeSinceStartup;
            float endRt = -1f;
            for (int i = events.Length - 1; i >= 0; i--)
            {
                TelemetryEventDto e = events[i];
                if (e == null)
                {
                    continue;
                }

                string k = NormalizeKind(e.kind);
                if (k == "run_end" || k == "session_quit")
                {
                    endRt = e.realtimeSinceStartup;
                    break;
                }
            }

            if (endRt < shopRt)
            {
                return -1f;
            }

            if (feel.firstHeroDeathRealtimeSinceStartup > shopRt)
            {
                return feel.firstHeroDeathRealtimeSinceStartup - shopRt;
            }

            return endRt - shopRt;
        }

        private static void AppendBaselineKpiSingleFileReport(StringBuilder sb, TelemetryFileRootDto root)
        {
            TelemetryEventDto[] events = root != null ? root.events : null;
            if (events == null || events.Length == 0)
            {
                return;
            }

            int maxWaveCleared = 0;
            int deathWave = -1;
            bool hasGameWon = false;
            int wavesWithShopPrior = 0;
            int totalWaveClearedRows = 0;
            bool firstShopPurchaseSeen =
                root.feelSession != null &&
                root.feelSession.firstShopPurchaseSecSinceSessionBegin >= 0f;

            float firstRt = events[0] != null ? events[0].realtimeSinceStartup : 0f;
            float lastRt = firstRt;
            for (int i = 0; i < events.Length; i++)
            {
                TelemetryEventDto e = events[i];
                if (e == null)
                {
                    continue;
                }

                lastRt = e.realtimeSinceStartup;
                string kind = NormalizeKind(e.kind);
                if (kind == "wave_cleared")
                {
                    totalWaveClearedRows++;
                    maxWaveCleared = Mathf.Max(maxWaveCleared, e.wave);
                    if (e.shopPurchasesPrior > 0)
                    {
                        wavesWithShopPrior++;
                    }
                }
                else if (kind == "run_end")
                {
                    bool isWin = !string.IsNullOrEmpty(e.endReason) &&
                                 e.endReason.Equals("game_won", StringComparison.OrdinalIgnoreCase);
                    if (isWin)
                    {
                        hasGameWon = true;
                    }
                    else
                    {
                        deathWave = e.wave;
                    }
                }
            }

            float runLengthSec = Mathf.Max(0f, lastRt - firstRt);
            float shopConversion01 = totalWaveClearedRows > 0
                ? (float)wavesWithShopPrior / totalWaveClearedRows
                : 0f;

            sb.AppendLine("--- Baseline KPIs (single run) ---");
            sb.AppendLine($"wave_reach_maxCleared={maxWaveCleared}");
            sb.AppendLine($"run_length_sec={runLengthSec:0.##}");
            sb.AppendLine(
                $"death_point_wave={(deathWave > 0 ? deathWave.ToString(CultureInfo.InvariantCulture) : (hasGameWon ? "game_won" : "none"))}");
            sb.AppendLine(
                $"shop_conversion_waveCleared01={shopConversion01:0.###} " +
                $"(wavesWithShopPrior={wavesWithShopPrior}/{Mathf.Max(1, totalWaveClearedRows)})");
            sb.AppendLine($"shop_conversion_firstPurchaseSeen={(firstShopPurchaseSeen ? "yes" : "no")}");
            sb.AppendLine();
        }

        private static void AppendFeelKpisSingleFileReport(StringBuilder sb, TelemetryFileRootDto root)
        {
            sb.AppendLine("--- Feel KPIs (offline proxies) ---");
            sb.AppendLine(
                "OBS: feel-värden är heuristiska proxies (skicklighet, vapen, RNG, botprofil) — använd för att välja körningar " +
                "att granska, inte som automatisk «bra/dålig»-dom. Se även ordlistan ovan (Feel-KPI / unityLogs / events).");
            TelemetryFeelSessionSummaryDto f = root.feelSession;
            if (f == null)
            {
                sb.AppendLine("(ingen feelSession — spela in med senaste WaveRunTelemetry_V2 / JSON)");
                sb.AppendLine();
                return;
            }

            sb.AppendLine(
                "Milstolpar (sekunder sedan session_begin, -1 = ej inträffat). Fönster «första 90s»: ja om 0–90s.");
            sb.AppendLine(
                $"  firstKillSec={f.firstKillSecSinceSessionBegin:0.###} first90s={FeelFirst90sLabel(f.firstKillSecSinceSessionBegin)}");
            sb.AppendLine(
                $"  firstDamageSec={f.firstHeroDamageSecSinceSessionBegin:0.###} first90s={FeelFirst90sLabel(f.firstHeroDamageSecSinceSessionBegin)}");
            sb.AppendLine(
                $"  firstDeathSec={f.firstHeroDeathSecSinceSessionBegin:0.###} first90s={FeelFirst90sLabel(f.firstHeroDeathSecSinceSessionBegin)}");
            sb.AppendLine(
                $"  firstShopPurchaseSec={f.firstShopPurchaseSecSinceSessionBegin:0.###} first90s={FeelFirst90sLabel(f.firstShopPurchaseSecSinceSessionBegin)}");
            sb.AppendLine(
                $"  firstShop: spent={f.firstShopPurchaseCurrencySpent} offer={f.firstShopOfferKind} " +
                $"heroAtPurchase={f.firstPurchaseHeroHp}/{f.firstPurchaseHeroMaxHp} weapon={f.firstPurchaseWeaponType}");
            ComputeFeelWaveAggregates(
                root.events,
                out float bunkerFrac,
                out float combatPerDmg,
                out float overwSec,
                out float totalDur);
            sb.AppendLine(
                $"Agency / stress (aggregerat över wave_cleared): " +
                $"bunkerTimeFrac01={bunkerFrac:0.###} (lägre ≈ mindre gömma i bunker), " +
                $"combatProxyPerHeroDamageTaken={combatPerDmg:0.###} (hits+launches+kills per HP skada), " +
                $"overwhelmedInWaveSecSum={overwSec:0.##} (vågar där damageTakenHero/waveDurationSec≥{FeelOverwhelmedHeroDps.ToString("0.#", CultureInfo.InvariantCulture)} DPS), " +
                $"totalInWaveSecCleared={totalDur:0.##}");
            int maxHpDelta = ComputeHeroMaxHpDeltaAfterFirstShop(root.events, f);
            float survAfterShop = ComputeSurvivalRealtimeAfterFirstShop(root.events, f);
            sb.AppendLine(
                $"Shop dopamine (proxy): heroMaxHpDelta efter första köp (första wave_cleared efter shop-tid)={maxHpDelta}, " +
                $"survivalRealtimeAfterFirstShop_sec={(survAfterShop >= 0f ? survAfterShop.ToString("0.##", CultureInfo.InvariantCulture) : "—")}");
            sb.AppendLine();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
            {
                return s;
            }

            return s.Substring(0, max) + "…";
        }

        private static List<string> EvaluateFlags(
            TelemetryEventDto ev,
            bool suppressOnboardingBunkerFlags,
            int onboardingMaxWaveInclusive,
            float bunkerPressureHpRatioThresholdUsed,
            WaveChainPrev chainPrev,
            TelemetryEventDto nextWaveEndRowInFileOrder)
        {
            var flags = new List<string>();
            string kind = NormalizeKind(ev.kind);
            bool isWaveEnd = kind == "wave_cleared" || kind == "run_end";

            if (!isWaveEnd)
            {
                return flags;
            }

            bool skipOnboardingWaveHeuristics =
                suppressOnboardingBunkerFlags && ev.wave <= onboardingMaxWaveInclusive;

            float bunkerStart01 = ComputeBunkerStartHpRatio01(ev);
            float thr = Mathf.Clamp(bunkerPressureHpRatioThresholdUsed, 0.05f, 0.99f);
            float pressureScoreRow = ComputePressureScore01(ev);

            if (!skipOnboardingWaveHeuristics &&
                ev.enemiesKilled > 0 &&
                ev.damageTakenBunker == 0 &&
                bunkerStart01 >= thr)
            {
                flags.Add("OBJECTIVE_INACTIVE");
            }

            if ((kind == "wave_cleared" || kind == "run_end") &&
                ev.autoHeroInWaveAnyHadTarget &&
                !string.IsNullOrEmpty(ev.autoHeroInWaveLastTargetKindWhenHadTarget) &&
                string.Equals(
                    ev.autoHeroInWaveLastTargetKindWhenHadTarget.Trim(),
                    "aircraft",
                    StringComparison.OrdinalIgnoreCase))
            {
                flags.Add("AUTOHERO_AIRCRAFT_LAST_INWAVE");
            }

            if ((kind == "wave_cleared" || kind == "run_end") &&
                ev.autoHeroInWaveAnyHadTarget &&
                !string.IsNullOrEmpty(ev.autoHeroInWaveLastTargetKindWhenHadTarget) &&
                string.Equals(
                    ev.autoHeroInWaveLastTargetKindWhenHadTarget.Trim(),
                    "paratrooper",
                    StringComparison.OrdinalIgnoreCase))
            {
                flags.Add("AUTOHERO_PARATROOPER_LAST_INWAVE");
            }

            if (chainPrev.Has &&
                chainPrev.Wave == ev.wave - 1 &&
                (ev.kind == "wave_cleared" || ev.kind == "run_end"))
            {
                bool prevBadCollapse =
                    chainPrev.BunkerBreached ||
                    (chainPrev.MinBunkerHpRatioThisWave >= 0f &&
                     chainPrev.MinBunkerHpRatioThisWave < 0.2f);
                if (prevBadCollapse && bunkerStart01 < 0.3f && pressureScoreRow > 0.7f)
                {
                    flags.Add("COLLAPSE_CHAIN");
                }
            }

            bool preloadLowCoverWithHits =
                bunkerStart01 < 0.2f && ev.damageTakenBunker > 0;
            bool preloadNoCoverAtStart = ev.bunkerHpWaveStart <= 0;
            if ((kind == "wave_cleared" || kind == "run_end") &&
                ev.waveDurationSec > 3f &&
                (preloadLowCoverWithHits || preloadNoCoverAtStart))
            {
                flags.Add("PRELOAD_FAIL");
            }

            if ((kind == "wave_cleared" || kind == "run_end") &&
                ev.bunkerHpWaveStart > 0 &&
                ev.damageTakenBunker == 0 &&
                bunkerStart01 < 0.6f &&
                ev.waveDurationSec > 6f &&
                pressureScoreRow > 0.8f)
            {
                flags.Add("LOW_COVER_STALL");
            }

            if (bunkerStart01 >= 0.2f &&
                ev.damageTakenBunker > 0 &&
                ev.bunkerPressureTimeSec >= 5f &&
                (ev.bunkerPressureTimeSec - ev.bunkerPressureTimeAfterFirstDamageSec) >= 3f)
            {
                flags.Add("HIGH_CARRY_IN_PRESSURE");
            }

            if (kind == "run_end" && (ev.heroDead || ev.bunkerBreached))
            {
                flags.Add("OVERLOAD_FAIL");
            }

            if (kind == "run_end" &&
                !string.IsNullOrEmpty(ev.endReason) &&
                ev.endReason.StartsWith("game_error", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add("GAME_ERROR_FAIL");
            }

            if (kind == "run_end" &&
                !string.IsNullOrEmpty(ev.endReason) &&
                ev.endReason.Equals("game_won", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add("GAME_WON_END");
            }

            if (kind == "wave_cleared" &&
                nextWaveEndRowInFileOrder != null &&
                NormalizeKind(nextWaveEndRowInFileOrder.kind) == "run_end" &&
                nextWaveEndRowInFileOrder.wave == ev.wave + 1 &&
                (nextWaveEndRowInFileOrder.heroDead || nextWaveEndRowInFileOrder.bunkerBreached))
            {
                flags.Add("NEXT_WAVE_OVERLOAD");
            }

            if (!skipOnboardingWaveHeuristics &&
                kind == "wave_cleared" &&
                !ev.heroDead &&
                !ev.bunkerBreached &&
                ev.damageTakenBunker >= 30 &&
                ev.minBunkerHpRatioThisWave >= 0.28f &&
                ev.minBunkerHpRatioThisWave <= 0.92f)
            {
                flags.Add("SWEET_SPOT_HINT");
            }

            if (!skipOnboardingWaveHeuristics &&
                kind == "wave_cleared" &&
                !ev.bunkerBreached &&
                ev.minBunkerHpRatioThisWave >= 0f &&
                ev.minBunkerHpRatioThisWave < 0.15f)
            {
                flags.Add("NEAR_BREACH");
            }

            if (!skipOnboardingWaveHeuristics &&
                (kind == "wave_cleared" || kind == "run_end") &&
                ev.damageTakenBunker == 0 &&
                ev.minBunkerHpRatioThisWave >= 0.99f)
            {
                flags.Add("TRIVIAL_BUNKER");
            }

            // Reference threshold only in report header; optional extra flag:
            if (ev.bunkerPressureTimeSec > ev.waveDurationSec * 0.92f && ev.waveDurationSec > 2f && ev.damageTakenBunker > 0)
            {
                flags.Add("PRESSURE_DOMINATED_DURATION");
            }

            int bunkerDmgTotal = ev.damageTakenBunker;
            int bunkerDmgBombs = ev.damageTakenBunkerFromBombs;
            if ((kind == "wave_cleared" || kind == "run_end") &&
                bunkerDmgTotal >= BombDominatedBunkerMinTotalDamage &&
                bunkerDmgBombs > 0 &&
                bunkerDmgBombs > bunkerDmgTotal * BombDominatedBunkerRatioThreshold)
            {
                flags.Add("BOMB_DOMINATED_BUNKER");
            }

            return flags;
        }

        /// <summary>Linear slope of bunkerHpRatio over sampled wave time (rough collapse speed).</summary>
        private static float EstimateBunkerHpSlopePerSec(TelemetryEventDto ev)
        {
            BunkerHpSampleDto[] s = ev.bunkerHpSamples;
            if (s == null || s.Length < 2)
            {
                return 0f;
            }

            float t0 = s[0].waveTimeSecSinceInWaveRealtime;
            float t1 = s[s.Length - 1].waveTimeSecSinceInWaveRealtime;
            float dt = t1 - t0;
            if (dt < 0.05f)
            {
                return 0f;
            }

            float r0 = s[0].bunkerHpRatio;
            float r1 = s[s.Length - 1].bunkerHpRatio;
            return (r1 - r0) / dt;
        }

        [Serializable]
        private sealed class BunkerHpSampleDto
        {
            public float waveTimeSecSinceInWaveRealtime;
            public int bunkerHp;
            public int bunkerMaxHp;
            public float bunkerHpRatio;
        }

        [Serializable]
        private sealed class TelemetryGlossaryEntryDto
        {
            public string property;
            public string meaning;
        }

        [Serializable]
        private sealed class TelemetryGlossaryDto
        {
            public string title;
            public TelemetryGlossaryEntryDto[] entries;
        }

        [Serializable]
        private sealed class TelemetryUnityLogRowDto
        {
            public string sessionId;
            public string utcIso8601;
            public float realtimeSinceStartup;
            public string unityEditorOrPlayerVersion;
            public string logType;
            public string messageTruncated;
            public string stackTraceTruncated;
            public string fingerprint;
            public int repeatCount;
            public int wave;
            public string waveLoopState;
            public int heroHp;
            public int heroMaxHp;
            public string weapon;
            public string weaponType;
            public string autoHeroTestProfile;
            public string sceneProfileId;
            public float heroPosX;
            public float heroPosY;
            public int heroAmmoInMag;
            public int heroAmmoMagMax;
            public int heroReserveAmmo;
            public int bunkerHp;
            public int bunkerMaxHp;
            public int currency;
            public int enemiesKilledThisWave;
            public int trackedLivingParatroopers;
            public float timeUnscaled;
            public float timeSinceLevelLoad;
            public float timeScale;
            public int frameCount;
            public string activeScenePathOrName;
            public int loadedSceneCount;
            public bool isEditor;
            public string platform;
            public string internetReachability;
            public long managedHeapBytes;
            public float inWaveUnscaledSec;
            public int damageTakenHeroThisWave;
            public int damageTakenBunkerThisWave;
            public int damageTakenBunkerFromBombsThisWave;
            public int shotsFiredThisWave;
            public float bunkerPressureTimeUnscaledThisWave;
            public string scalingSnapshotShort;
            public string spawnerDiagnosticsLine;
        }

        [Serializable]
        private sealed class TelemetryFeelSessionSummaryDto
        {
            public float sessionBeginRealtimeSinceStartup;
            public float firstKillRealtimeSinceStartup;
            public float firstHeroDamageRealtimeSinceStartup;
            public float firstHeroDeathRealtimeSinceStartup;
            public float firstShopPurchaseRealtimeSinceStartup;
            public float firstKillSecSinceSessionBegin;
            public float firstHeroDamageSecSinceSessionBegin;
            public float firstHeroDeathSecSinceSessionBegin;
            public float firstShopPurchaseSecSinceSessionBegin;
            public int firstShopPurchaseCurrencySpent;
            public string firstShopOfferKind;
            public int firstPurchaseHeroHp;
            public int firstPurchaseHeroMaxHp;
            public string firstPurchaseWeaponType;
        }

        private sealed class TelemetryFileRootDto
        {
            public string _comment;
            public TelemetryGlossaryDto glossary;
            public float bunkerCriticalHpFractionUsed;
            public float bunkerHpSampleIntervalSecUsed;
            public int bunkerHpSamplesMaxPerWaveUsed;
            public float bunkerPressureHpRatioThresholdUsed;
            public TelemetryEventDto[] events;
            public TelemetryUnityLogRowDto[] unityLogs;
            public TelemetryFeelSessionSummaryDto feelSession;
            public TelemetryPoolStatsDto poolStats;
        }

        [Serializable]
        private sealed class TelemetryPoolStatsDto
        {
            public int prefabTypeCount;
            public int totalInactiveCount;
            public int totalCreatedCount;
            public int totalReusedCount;
            public int totalDespawnCount;
            public TelemetryPoolPrefabStatsDto[] prefabs;
        }

        [Serializable]
        private sealed class TelemetryPoolPrefabStatsDto
        {
            public string prefabName;
            public int inactiveCount;
            public int createdCount;
            public int reusedCount;
            public int despawnCount;
        }

        [Serializable]
        private sealed class TelemetryEventDto
        {
            public string kind;
            public string sessionId;
            public float realtimeSinceStartup;
            public int wave;
            public float waveDurationSec;
            public int heroHp;
            public int heroMaxHp;
            public int bunkerHp;
            public int bunkerMaxHp;
            public int currency;
            public int enemiesKilled;
            public string weapon;
            public string endReason;
            public int damageTakenHero;
            public int healingHero;
            public int damageTakenBunker;
            public int damageTakenBunkerFromBombs;
            public float timeInBunkerSec;
            public int shotsFired;
            public int rayHits;
            public int rayMisses;
            public int projectileLaunches;
            public int reloads;
            public int shopPurchasesPrior;
            public int shopCurrencySpentPrior;
            public string[] shopOffersBoughtPrior;
            public int heroHpWaveStart;
            public int bunkerHpWaveStart;
            public int currencyWaveStart;
            public string weaponType;
            public string autoHeroTestProfile;
            public bool autoHeroInWaveAnyHadTarget;
            public string autoHeroInWaveLastTargetKindWhenHadTarget;
            public string autoHeroInWaveLastTargetParatrooperStateWhenHadTarget;
            public string sceneProfileId;
            public bool bunkerBreached;
            public bool bunkerCriticalLow;
            public bool heroDead;
            public float waveStressScore;
            public float bunkerPressureTimeSec;
            public float bunkerPressureTimeAfterFirstDamageSec;
            public float minBunkerHpRatioThisWave;
            public BunkerHpSampleDto[] bunkerHpSamples;
            public string waveScalingJson;
            public int spawnerTargetSpawn;
            public int spawnerSpawned;
            public int spawnerPendingDrops;
            public bool spawnerSpawnStarved;
            public string spawnerSpawnRoutineExitReason;
            public string spawnerLastSpawnAbortReason;
            public int spawnerFailedSpawnAttempts;
            public int spawnerRecoveryCount;
        }
    }
}
