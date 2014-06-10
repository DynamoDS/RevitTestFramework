SET git=%USERPROFILE%\AppData\Local\GitHub\PORTAB~1\bin\git.exe
%git% checkout master
MSBuild.exe ..\src\RevitTestFramework.sln /p:Configuration=Release