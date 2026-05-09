$path = 'C:\repos\iStick2War2026\Assets\Scripts\Enemies\Paratrooper_V2\ParatrooperWeaponSystem_V2.cs'
$arr = [System.IO.File]::ReadAllLines($path)

function Get-LeadingSpaceCount([string] $s) {
    $n = 0
    while ($n -lt $s.Length -and $s[$n] -eq [char]32) {
        $n++
    }
    return $n
}

for ($i = 0; $i -lt $arr.Length; $i++) {
    $ln = $i + 1
    if ($ln -lt 36 -or $ln -gt 1332) {
        continue
    }

    $s = $arr[$i]
    $lead = Get-LeadingSpaceCount $s
    if ($lead -eq 0) {
        continue
    }

    $dedented = [Math]::Max(0, $lead - 4)
    $arr[$i] = (' ' * $dedented) + $s.Substring($lead)
}

[System.IO.File]::WriteAllLines($path, $arr)
