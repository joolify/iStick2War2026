using System;
using System.Collections.Generic;
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

        private const string PrefSuppressOnboarding = "iStick2War.WaveTelemetryAnalyzer.SuppressOnboardingBunkerFlags";
        private const string PrefOnboardingMaxWave = "iStick2War.WaveTelemetryAnalyzer.OnboardingMaxWaveInclusive";

        /// <summary>Swedish v1 heuristic legend shown at top of report and in UI before first analysis.</summary>
        private static readonly string AnalyzerIntroSv =
            "Rapport (v1-heuristik)\n" +
            "För varje rad wave_cleared / run_end skrivs en rad med nyckeltal + valfria flaggor:\n\n" +
            "Flagga\tUngefär\n" +
            "OBJECTIVE_INACTIVE\tenemiesKilled > 0 och damageTakenBunker == 0 och bunker vid InWave-start ≥ root tryck-tröskel (ratio); undviker falsk signal när vågen redan börjar under tryck-tröskeln utan ny bunker-skada\n" +
            "PRELOAD_FAIL\twaveDurationSec > 3 och (bunkerStart01 < 0.2 med bunker-skada > 0, eller bunkerHpWaveStart ≤ 0)\n" +
            "LOW_COVER_STALL\tbunkerHpWaveStart > 0: bunkerStart01 < 0.6, bunker-skada 0, pressureScore > 0.8, waveDurationSec > 6 (carry-in-tryck utan ny bunker-skada; ej post-breach)\n" +
            "COLLAPSE_CHAIN\tföregående wave_cleared/run_end: minRatio < 0.2 eller breach; denna rad: wave+1, bunkerStart01 < 0.3, pressureScore > 0.7\n" +
            "HIGH_CARRY_IN_PRESSURE\tsom PRELOAD inte gäller: bunkerStart01 ≥ 0.2, bunker-skada, stor skillnad press total vs efter första träff\n" +
            "NEAR_BREACH\twave_cleared, minBunkerHpRatio < 0.15, inte bunkerBreached (tur/panik, inte sweet spot)\n" +
            "OVERLOAD_FAIL\trun_end och (heroDead eller bunkerBreached)\n" +
            "NEXT_WAVE_OVERLOAD\twave_cleared: nästa wave_cleared/run_end-rad i filen är run_end för wave+1 med game over (heroDead eller bunkerBreached)\n" +
            "SWEET_SPOT_HINT\tgrov \"bra fight\"-heuristik för wave_cleared (skada + min ratio-intervall, ingen död/breach)\n" +
            "TRIVIAL_BUNKER\tingen bunker-skada och minBunkerHpRatio ≥ 0.99\n" +
            "PRESSURE_DOMINATED_DURATION\ttryck-tid följer nästan hela våglängden\n\n" +
            "Dessutom visas ungefärlig HP-ratio-lutning från första/sista bunkerHpSamples-punkt (slope≈…/s).\n\n" +
            "Flaggorna är tolkningshjälp, inte balans-sanning — justera trösklar i editor-skriptet när ni har mer data.\n\n" +
            "Inställning «Undertryck onboarding-flaggor»: döljer OBJECTIVE_INACTIVE, TRIVIAL_BUNKER och SWEET_SPOT_HINT för valt vågnummer och lägre " +
            "(så tidiga vågor inte feltolkas som balans-/retention-signaler).";

        private string _jsonPath = "";
        private Vector2 _scroll;
        private string _report = "";
        private bool _showIntroFoldout = true;
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
            if (GUILayout.Button("Analyze", GUILayout.Height(28)))
            {
                RunAnalysis();
            }

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
            sb.AppendLine("---");
            sb.AppendLine($"File: {_jsonPath}");
            sb.AppendLine($"bunkerPressureHpRatioThresholdUsed (root): {pressureThr:0.###} (reference; row flags use fixed heuristics v1)");
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

            var waveEndRows = new List<TelemetryEventDto>(root.events.Length);
            foreach (TelemetryEventDto e in root.events)
            {
                if (e == null || string.IsNullOrEmpty(e.kind))
                {
                    continue;
                }

                if (e.kind == "wave_cleared" || e.kind == "run_end")
                {
                    waveEndRows.Add(e);
                }
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

                string stressDeltaText = hasPrevWaveStress
                    ? $"{ev.waveStressScore - prevWaveStressScore:0.##}"
                    : "—";

                sb.AppendLine(
                    $"[{ev.kind}] wave={ev.wave} dur={ev.waveDurationSec:0.##}s " +
                    $"kills={ev.enemiesKilled} bunkerDmg={ev.damageTakenBunker} " +
                    $"stress={ev.waveStressScore:0.#} press={ev.bunkerPressureTimeSec:0.##}s " +
                    $"pressPostHit={ev.bunkerPressureTimeAfterFirstDamageSec:0.##}s " +
                    $"minRatio={ev.minBunkerHpRatioThisWave:0.###} slope≈{slope:0.####}/s");
                sb.AppendLine(
                    $"  scores: pressureScore={pressureScore:0.###} combatScore01={combatScore01:0.###} survivalScore01={survivalScore01:0.###} controlLossScore={controlLossScore:0.###}");
                sb.AppendLine(
                    $"  pacing: stressDelta={stressDeltaText} bunkerStart01={bunkerStart01:0.###} heroStart01={heroStart01:0.###} startHpBuffer01={startHpBuffer01:0.###}");
                sb.AppendLine(
                    $"  pacing: survivalMarginV1={survivalMarginV1:0.###} survivalMarginMin01={survivalMarginMin01:0.###} pressureDeficitAvg01={pressureDeficitAvg01:0.###} pressureContinuity01={pressureContinuity01:0.###}");
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

            _report = sb.ToString();
            _lastMessageType = MessageType.Info;
            _lastMessage = "Analysis complete.";
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
            bool isWaveEnd = ev.kind == "wave_cleared" || ev.kind == "run_end";

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
            if ((ev.kind == "wave_cleared" || ev.kind == "run_end") &&
                ev.waveDurationSec > 3f &&
                (preloadLowCoverWithHits || preloadNoCoverAtStart))
            {
                flags.Add("PRELOAD_FAIL");
            }

            if ((ev.kind == "wave_cleared" || ev.kind == "run_end") &&
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

            if (ev.kind == "run_end" && (ev.heroDead || ev.bunkerBreached))
            {
                flags.Add("OVERLOAD_FAIL");
            }

            if (ev.kind == "wave_cleared" &&
                nextWaveEndRowInFileOrder != null &&
                nextWaveEndRowInFileOrder.kind == "run_end" &&
                nextWaveEndRowInFileOrder.wave == ev.wave + 1 &&
                (nextWaveEndRowInFileOrder.heroDead || nextWaveEndRowInFileOrder.bunkerBreached))
            {
                flags.Add("NEXT_WAVE_OVERLOAD");
            }

            if (!skipOnboardingWaveHeuristics &&
                ev.kind == "wave_cleared" &&
                !ev.heroDead &&
                !ev.bunkerBreached &&
                ev.damageTakenBunker >= 30 &&
                ev.minBunkerHpRatioThisWave >= 0.28f &&
                ev.minBunkerHpRatioThisWave <= 0.92f)
            {
                flags.Add("SWEET_SPOT_HINT");
            }

            if (!skipOnboardingWaveHeuristics &&
                ev.kind == "wave_cleared" &&
                !ev.bunkerBreached &&
                ev.minBunkerHpRatioThisWave >= 0f &&
                ev.minBunkerHpRatioThisWave < 0.15f)
            {
                flags.Add("NEAR_BREACH");
            }

            if (!skipOnboardingWaveHeuristics &&
                (ev.kind == "wave_cleared" || ev.kind == "run_end") &&
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
        private sealed class TelemetryFileRootDto
        {
            public string _comment;
            public TelemetryGlossaryDto glossary;
            public float bunkerCriticalHpFractionUsed;
            public float bunkerHpSampleIntervalSecUsed;
            public int bunkerHpSamplesMaxPerWaveUsed;
            public float bunkerPressureHpRatioThresholdUsed;
            public TelemetryEventDto[] events;
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
            public float timeInBunkerSec;
            public int shotsFired;
            public int rayHits;
            public int rayMisses;
            public int projectileLaunches;
            public int reloads;
            public int shopPurchasesPrior;
            public int shopCurrencySpentPrior;
            public int heroHpWaveStart;
            public int bunkerHpWaveStart;
            public int currencyWaveStart;
            public string weaponType;
            public string autoHeroTestProfile;
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
        }
    }
}
