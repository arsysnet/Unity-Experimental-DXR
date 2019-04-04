@echo off
"%~dp0cli.bat" %MONO_OPTIONS% "%~dp0..\lib\mono\2.0\wsdl.exe" %1 %2 %3 %4 %5 %6 %7 %8 %9
exit \b %ERRORLEVEL%
