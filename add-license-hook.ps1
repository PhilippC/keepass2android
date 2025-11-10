Write-Host "Running addlicense hook..."
$files = Get-ChildItem -Path src -Recurse -Include *.cs,*.java,*.xml |
         Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|Resources' }
foreach ($f in $files) {
    addlicense $f.FullName -c 'Philipp Crocoll' -y
}
