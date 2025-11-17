Write-Host "Running addlicense hook..."
$files = Get-ChildItem -Path src -Recurse -Include *.cs,*.java,*.xml |
         Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|Resources|bouncycastle|keepassdroid|TwofishCipher|SamsungPass' }
foreach ($f in $files) {
    if ($f.Extension -eq ".xml") {
        # For XML, skip the first line (XML declaration)
        addlicense -f GPLv3.txt --skip=1 $f.FullName 
    } else {
        addlicense -f GPLv3.txt $f.FullName 
    }
}
