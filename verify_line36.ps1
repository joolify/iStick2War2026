$p = 'C:\repos\iStick2War2026\Assets\Scripts\Enemies\Paratrooper_V2\ParatrooperWeaponSystem_V2.cs'
$lines = [System.IO.File]::ReadAllLines($p)
for ($i = 32; $i -le 40; $i++) {
    $ln = $i + 1
    $s = $lines[$i]
    $lead = 0
    while ($lead -lt $s.Length -and $s[$lead] -eq [char]32) { $lead++ }
    Write-Host ("{0,4}: lead={1,2} |{2}" -f $ln, $lead, $s)
}
