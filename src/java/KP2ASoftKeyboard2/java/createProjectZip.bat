rmdir projectzip  /s /q
mkdir projectzip
del project.zip
xcopy bin projectzip\bin\ /E
xcopy res projectzip\res\ /E
rmdir projectzip\bin\res\crunch  /s /q
cd projectzip
"c:\Program Files\7-Zip\7z.exe" a -tzip project.zip bin
"c:\Program Files\7-Zip\7z.exe" a -tzip project.zip res
cd ..
xcopy projectzip\project.zip project.zip