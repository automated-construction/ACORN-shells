@echo off
SET CHECK5="%AppData%\McNeel\Rhinoceros\5.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\"
SET CHECK6="%AppData%\McNeel\Rhinoceros\6.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\"
SET PYLOCATION5="%AppData%\McNeel\Rhinoceros\5.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib\acorn_shell\"
SET PYLOCATION6="%AppData%\McNeel\Rhinoceros\6.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib\acorn_shell\"
SET GHLOCATION="%AppData%\Grasshopper\Libraries\ACORNShell.ghpy"

echo PLEASE ENSURE RHINO IS NOT RUNNING.
pause

echo.

IF EXIST %CHECK5% (
IF EXIST %PYLOCATION5% (
echo Deleting acorn_shell python library for Rhino 5.
@RD /S /Q %PYLOCATION5%
)

echo Installing acorn_shell python library for Rhino 5.
xcopy ".\acorn_shell" %PYLOCATION5%
)

IF EXIST %CHECK6% (
IF EXIST %PYLOCATION6% (
echo Deleting acorn_shell python library for Rhino 6.
@RD /S /Q %PYLOCATION6%
)

echo Installing acorn_shell python library for Rhino 6.
xcopy ".\acorn_shell" %PYLOCATION6%

IF EXIST %GHLOCATION% (
echo Deleting acorn_shell GH components for Rhino 6.
del %GHLOCATION%
)

echo Installing acorn_shell GH components.
copy ".\acorn_shell_components\ACORNShell.ghpy" %GHLOCATION%
)

echo.

echo INSTALL FINISHED.
pause