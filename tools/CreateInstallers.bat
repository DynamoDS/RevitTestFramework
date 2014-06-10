SET git=%USERPROFILE%\AppData\Local\GitHub\PORTAB~1\bin\git.exe
SET msbuild=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe

REM Checkout and build RTF for Revit 2014
%git% checkout master
%msbuild% ..\src\RevitTestFramework.sln /t:rebuild /p:platform="Any CPU" /p:Configuration=Release

REM Checkout and build RTF for Revit 2015
%git% checkout Revit2015
%msbuild% ..\src\RevitTestFramework.sln /t:rebuild /p:platform="Any CPU" /p:Configuration=Release


