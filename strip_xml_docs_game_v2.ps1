$root = 'C:\repos\iStick2War2026\Assets\Scripts\Game_V2'
$files = Get-ChildItem -Path $root -Recurse -Filter '*.cs' -File

foreach ($f in $files) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        $m = [regex]::Match($line, '^(\s*)///\s*(.*)$')
        if (-not $m.Success) {
            $out.Add($line)
            continue
        }
        $indent = $m.Groups[1].Value
        $rest = $m.Groups[2].Value
        while ($rest -match '<[^>]+>') {
            $rest = $rest -replace '<[^>]+>', ''
        }
        $rest = $rest -replace '&lt;', '<' -replace '&gt;', '>' -replace '&amp;', '&'
        $rest = $rest.Trim()
        if ($rest.Length -eq 0) {
            continue
        }
        $out.Add($indent + '// ' + $rest)
    }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($f.FullName, $out.ToArray(), $utf8)
}

Write-Host "Processed $($files.Count) files."
