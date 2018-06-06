@echo off

REM Build the two projects containing the binaries
msbuild SyncPro.UI\SyncPro.UI.csproj /property:Configuration=Debug
msbuild SyncProLogViewer\SyncProLogViewer.csproj  /property:Configuration=Debug

REM Sign the binaries
"%ProgramFiles(x86)%\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"  sign /sha1 a2cecb61c24b3a8b76680b1bb587bb59ac42896c /tr http://timestamp.digicert.com /td sha256 /fd sha256 SyncProLogViewer\bin\Debug\SyncProLogViewer.exe
"%ProgramFiles(x86)%\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"  sign /sha1 a2cecb61c24b3a8b76680b1bb587bb59ac42896c /tr http://timestamp.digicert.com /td sha256 /fd sha256 SyncPro.UI\bin\Debug\SyncPro.UI.exe

REM Build the MSI
msbuild SyncPro.Setup\SyncPro.Setup.wixproj /property:Configuration=Debug

REM Sign the MSI
"%ProgramFiles(x86)%\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"  sign /sha1 a2cecb61c24b3a8b76680b1bb587bb59ac42896c /tr http://timestamp.digicert.com /td sha256 /fd sha256 "SyncPro.Setup\bin\Debug\SyncPro Setup.msi"