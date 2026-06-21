@echo off
setlocal

set "ROOT=%~dp0"
set "LATEST_ROOT=%ROOT%artifacts\latest"
set "INTERACTIVE=1"
pushd "%ROOT%" >nul

if /I "%~1"=="debug" set "INTERACTIVE=0" & goto :build_debug
if /I "%~1"=="release" set "INTERACTIVE=0" & goto :build_release
if /I "%~1"=="both" set "INTERACTIVE=0" & goto :build_both
if /I "%~1"=="webview" set "INTERACTIVE=0" & goto :build_webview
if /I "%~1"=="all" set "INTERACTIVE=0" & goto :build_all
if /I "%~1"=="clean" set "INTERACTIVE=0" & goto :clean_outputs

:menu
cls
echo.
echo Notepad Pro Build Launcher
echo -------------------------
echo 1. Debug
echo 2. Release
echo 3. Debug + Release
echo 4. Webview
echo 5. Full (Debug + Release + Webview)
echo 6. Clean outputs only
echo -------------------------
echo 0. Exit
echo.
set /p choice=Choose an option [0-6]: 
set "choice=%choice: =%"

if not defined choice (
	echo No option entered.
	goto :after_action
)

if "%choice%"=="1" goto :build_debug
if "%choice%"=="2" goto :build_release
if "%choice%"=="3" goto :build_both
if "%choice%"=="4" goto :build_webview
if "%choice%"=="5" goto :build_all
if "%choice%"=="6" goto :clean_outputs
if "%choice%"=="0" goto :exit_launcher

echo Invalid choice.
goto :after_action

:build_debug
echo.
echo Cleaning and building Debug...
call :build_config Debug
if errorlevel 1 goto :finish_error
goto :finish_ok

:build_release
echo.
echo Cleaning and building Release...
call :build_config Release
if errorlevel 1 goto :finish_error
goto :finish_ok

:build_both
echo.
echo Cleaning and building Debug...
call :build_config Debug
if errorlevel 1 goto :finish_error

echo.
echo Cleaning and building Release...
call :build_config Release
if errorlevel 1 goto :finish_error
goto :finish_ok

:build_webview
echo.
echo Building Webview...
pushd "webview" >nul
call npm run build
set "BUILD_RESULT=%errorlevel%"
popd >nul
if not "%BUILD_RESULT%"=="0" goto :finish_error
goto :finish_ok

:build_all
echo.
echo Cleaning and building Debug...
call :build_config Debug
if errorlevel 1 goto :finish_error

echo.
echo Cleaning and building Release...
call :build_config Release
if errorlevel 1 goto :finish_error

echo.
echo Building Webview...
pushd "webview" >nul
call npm run build
set "BUILD_RESULT=%errorlevel%"
popd >nul
if not "%BUILD_RESULT%"=="0" goto :finish_error
goto :finish_ok

:clean_outputs
echo.
echo Cleaning Debug/Release outputs...
call :clean_config_output Debug
if errorlevel 1 goto :finish_error
call :clean_config_output Release
if errorlevel 1 goto :finish_error

if exist "%LATEST_ROOT%" (
	rmdir /s /q "%LATEST_ROOT%"
)

echo Output folders cleaned.
goto :finish_ok

:build_config
set "CFG=%~1"
call :clean_config_output "%CFG%"
if errorlevel 1 exit /b 1

dotnet build "Notepad Pro.sln" -c "%CFG%"
if errorlevel 1 exit /b 1

call :refresh_latest_output "%CFG%"
if errorlevel 1 exit /b 1
exit /b 0

:clean_config_output
set "CFG=%~1"
set "OUT_DIR=NotepadPro\bin\%CFG%\net9.0-windows"

dotnet clean "Notepad Pro.sln" -c "%CFG%"
if errorlevel 1 exit /b 1

if exist "%OUT_DIR%" (
	rmdir /s /q "%OUT_DIR%"
)

exit /b 0

:refresh_latest_output
set "CFG=%~1"
set "SRC_DIR=NotepadPro\bin\%CFG%\net9.0-windows"
set "DST_DIR=%LATEST_ROOT%\%CFG%"

if not exist "%SRC_DIR%" exit /b 1

if exist "%DST_DIR%" (
	rmdir /s /q "%DST_DIR%"
)

mkdir "%DST_DIR%" >nul 2>&1
robocopy "%SRC_DIR%" "%DST_DIR%" /E /NFL /NDL /NJH /NJS /NP >nul
set "ROBO_RESULT=%errorlevel%"
if %ROBO_RESULT% GEQ 8 exit /b 1

exit /b 0

:finish_ok
echo.
echo Build completed successfully.
if exist "%LATEST_ROOT%" (
	echo Fresh binaries mirrored to: "%LATEST_ROOT%"
)
if "%INTERACTIVE%"=="1" goto :after_action
popd >nul
exit /b 0

:finish_error
echo.
echo Build failed.
if "%INTERACTIVE%"=="1" goto :after_action
popd >nul
exit /b 1

:after_action
echo.
pause
goto :menu

:exit_launcher
echo.
echo Exiting launcher.
popd >nul
exit /b 0
