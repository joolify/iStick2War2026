$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = 'C:\repos\iStick2War2026\Assets\Scripts\Game_V2'

function Repair-File([string] $path, [hashtable] $replacements) {
    $t = [System.IO.File]::ReadAllText($path)
    foreach ($k in $replacements.Keys) {
        $t = $t.Replace($k, $replacements[$k])
    }
    [System.IO.File]::WriteAllText($path, $t, $utf8)
}

# --- WaveManager_V2 ---
Repair-File (Join-Path $root 'WaveManager_V2.cs') @{
    '// Raised when  applies a positive amount (enemy fire, etc.).' = '// Raised when ApplyBunkerDamage applies a positive amount (enemy fire, etc.).'
    '// Unscaled seconds since this run entered ; -1 if not InWave.' = '// Unscaled seconds since this run entered WaveLoopState_V2.InWave; -1 if not InWave.'
    '// Last scaling snapshot for the wave that entered  (still valid in Shop until the next wave starts).' = '// Last scaling snapshot for the wave that entered WaveLoopState_V2.InWave (still valid in Shop until the next wave starts).'
    '// Call from  after Play: shows Wave N for , then fades out.' = '// Call from MainMenu_V2 after Play: shows Wave N for _topBarWaveTextVisibleSeconds, then fades out.'
    '// Shows the top-bar wave label (hold + fade) using .' = '// Shows the top-bar wave label (hold + fade) using CurrentWaveNumber.'
    '// Forwarded to  (bomb vs other bunker hits).' = '// telemetrySource: forwarded to WaveRunTelemetry_V2 (bomb vs other bunker hits).'
    '// If  is 0, cover is breached' = '// If BunkerHealth is 0, cover is breached'
}

# --- MainMenu_V2 (mojibake dash variants) ---
$m = Join-Path $root 'MainMenu_V2.cs'
$mt = [System.IO.File]::ReadAllText($m)
$mt = $mt.Replace('// True when the menu is active and  has not run this session', '// True when the menu is active and HandlePlay has not run this session')
$mt = $mt.Replace('// (automation / tests: use with  Preparing, not only ).', '// (automation / tests: use with WaveManager_V2 Preparing, not only Time.timeScale).')
$mt = $mt.Replace('// text and block world-space  colliders. Add a UI  on the', '// text and block world-space MainMenuNavButton_V2 colliders. Add a UI Button on the')
$mt = $mt.Replace('// same GameObject so clicks invoke  / settings.', '// same GameObject so clicks invoke HandlePlay / settings.')
$mt = $mt.Replace('// Called from UI Button or  (world Collider2D).', '// Called from UI Button or MainMenuNavButton_V2 (world Collider2D).')
$mt = $mt.Replace('// Call after  when returning from game over. If this component sits on an', '// Call after SceneManager.LoadScene when returning from game over. If this component sits on an')
$mt = $mt.Replace('// inactive GameObject,  never ran', '// inactive GameObject, Awake never ran')
# fix corrupted em dash sequences if present
$mt = $mt -replace '\uFFFD\?\?', '-'
$mt = $mt -replace ' \?\? ', ' - '
[System.IO.File]::WriteAllText($m, $mt, $utf8)

# --- MainMenuNavButton_V2 ---
$mn = Join-Path $root 'MainMenuNavButton_V2.cs'
$mnt = [System.IO.File]::ReadAllText($mn)
$mnt = $mnt.Replace('// across  ', '// across SceneManager.LoadScene ')
$mnt = $mnt -replace 'across  .', 'across SceneManager.LoadScene —'
[System.IO.File]::WriteAllText($mn, $mnt, $utf8)

# --- EnemySpawner_V2 ---
Repair-File (Join-Path $root 'EnemySpawner_V2.cs') @{
    '// 1-based wave index from  for debug logs; 0 if unset.' = '// 1-based wave index from WaveManager_V2.CurrentWaveNumber for debug logs; 0 if unset.'
    '// One-line spawner state for GameError / telemetry (call before  if possible).' = '// One-line spawner state for GameError / telemetry (call before StopWave if possible).'
    '// Forces horizontal spawn to match  so bombplanes always' = '// Forces horizontal spawn to match EnemySpawner invert flags so bombplanes always'
}

# --- Bombplane / BloodHitVfx ---
$bombplaneV2 = Join-Path (Join-Path (Split-Path $root -Parent) 'Enemies\BombPlane_V2') 'Bombplane_V2.cs'
Repair-File $bombplaneV2 @{
    '// Prefer  from spawners so direction matches spawn side.' = '// Prefer BeginBombRun(bool fromLeft) from spawners so direction matches spawn side.'
}
Repair-File (Join-Path $root 'BloodHitVfx_V2.cs') @{
    '// Aligns local +X with  in the XY plane (2D shooter convention).' = '// Aligns local +X with dir in the XY plane (2D shooter convention).'
}

# --- WaveRunTelemetry_V2 ---
$wrt = Join-Path $root 'WaveRunTelemetry_V2.cs'
$wt = [System.IO.File]::ReadAllText($wrt)
$wt = $wt.Replace('// Seconds since this wave entered InWave (matches  basis).', '// Seconds since this wave entered InWave (matches TelemetryEvent.waveDurationSec basis).')
$wt = $wt.Replace('// Clamped fraction used for  on each event in this file.', '// Clamped fraction used for TelemetryEvent.bunkerCriticalLow on each event in this file.')
$wt = $wt.Replace('// Clamped ratio threshold for  (Inspector echo).', '// Clamped ratio threshold for TelemetryEvent.bunkerPressureTimeSec (Inspector echo).')
$wt = $wt.Replace('// Subset of  from bomb explosions ().', '// Subset of damageTakenBunker from bomb explosions (BombProjectile_V2).')
$wt = $wt.Replace('// True when  snapshot is <= 0 (cover breached).', '// True when bunkerHp snapshot is <= 0 (cover breached).')
$wt = $wt.Replace('// True when hero is dead at snapshot (see  / ).', '// True when hero is dead at snapshot (see run_end / session_quit).')
$wt = $wt.Replace('// Non-empty only on wave_cleared / run_end (abort): JSON from  via JsonUtility.', '// Non-empty only on wave_cleared / run_end (abort): JSON from TelemetryWaveScaling via JsonUtility.')
$wt = $wt.Replace('// Called from  when bunker loses HP.', '// Called from WaveManager_V2 when bunker loses HP.')
$wt = $wt.Replace('// Called from  with a coarse damage source.', '// Called from WaveManager_V2.ApplyBunkerDamage with a coarse damage source.')
$wt = $wt.Replace('// Called from  after a successful shop spend.', '// Called from WaveManager_V2 after a successful shop spend.')
$wt = $wt.Replace('// Call from  for feel-KPI first-kill timing.', '// Call from WaveManager_V2.ReportEnemyKilled for feel-KPI first-kill timing.')
[System.IO.File]::WriteAllText($wrt, $wt, $utf8)

Write-Host 'Repair pass done.'
