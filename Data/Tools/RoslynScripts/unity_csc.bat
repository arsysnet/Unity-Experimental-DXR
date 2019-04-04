@ECHO OFF
set APPLICATION_CONTENTS=%~dp0..\..
"%APPLICATION_CONTENTS%\Tools\Roslyn\csc" /shared %*
exit /b %ERRORLEVEL%
