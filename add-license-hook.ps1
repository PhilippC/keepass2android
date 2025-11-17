Write-Host "Running addlicense hook..."
$files = Get-ChildItem -Path src -Recurse -Include *.cs,*.java,*.xml |
         Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|Resources|bouncycastle|keepassdroid|TwofishCipher|SamsungPass' }
foreach ($f in $files) {
    addlicense -f GPLv3.txt $f.FullName -c 'Philipp Crocoll' -y
}
