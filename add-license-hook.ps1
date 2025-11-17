param(
    [string[]]$Files
)

Write-Host "Running addlicense hook..."

# If pre-commit passed no files (e.g. manual run), fallback to full scan
if (-not $Files -or $Files.Count -eq 0) {
    Write-Host "No files passed. scanning full tree..."
    $Files = Get-ChildItem -Path src -Recurse -Include *.cs, *.java, *.xml |
             Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|Resources|bouncycastle|keepassdroid|TwofishCipher|SamsungPass' } |
             Select-Object -ExpandProperty FullName
}

foreach ($f in $Files) {
    # Skip known directories
    if ($f -match '\\obj\\|\\bin\\') {
        continue
    }

    # Handle XML separately
    if ($f.ToLower().EndsWith('.xml')) {
        addlicense -f GPLv3.txt --skip=1 $f
    }
    elseif ($f.ToLower().EndsWith('.cs') -or $f.ToLower().EndsWith('.java')) {
        addlicense -f GPLv3.txt $f
    }
}