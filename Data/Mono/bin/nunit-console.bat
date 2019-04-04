@echo off
"%~dp0cli.bat" %MONO_OPTIONS% "%~dp0..\lib\mono\1.0\nunit-console.exe" %1 %2 %3 %4 %5 %6 %7 %8 %9
exit \b %ERRORLEVEL%
