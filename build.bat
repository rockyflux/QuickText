@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

rem UTF-8 so the Chinese menu renders in modern terminals (Windows Terminal / recent cmd).
chcp 65001 >nul 2>&1

if not "%~1"=="" (
  set "OPT=%~1"
  goto :dispatch
)

:menu
echo.
echo  ========================================
echo   QuickText  编译 / 打包
echo  ----------------------------------------
echo   [1] 编译 Debug
echo   [2] 编译 Release
echo   [3] 单元测试
echo   [4] 窗口冒烟 ^(--smoke^)
echo   [5] 打包绿色版 ^(自包含，免装运行时^)
echo   [6] 打包精简版 ^(需已装 .NET 10 桌面运行时^)
echo   [7] 打包全部 + zip 到 dist\
echo   [8] 完整检查 ^(测试 + Release + 冒烟^)
echo   [0] 退出
echo  ========================================
echo.
set "OPT="
set /p "OPT=请输入序号后回车: "
if "!OPT!"=="" goto :menu

:dispatch
if "!OPT!"=="0" goto :eof
if "!OPT!"=="1" goto :build_debug
if "!OPT!"=="2" goto :build_release
if "!OPT!"=="3" goto :test
if "!OPT!"=="4" goto :smoke
if "!OPT!"=="5" goto :publish_sc
if "!OPT!"=="6" goto :publish_fd
if "!OPT!"=="7" goto :publish_all
if "!OPT!"=="8" goto :full_gate
echo.
echo  [!] 无效序号: !OPT!
if not "%~1"=="" exit /b 1
goto :menu

:build_debug
echo.
call :stop_running
echo  ==^> 编译 Debug ...
dotnet build QuickText.sln -c Debug --nologo
goto :done

:build_release
echo.
call :stop_running
echo  ==^> 编译 Release ...
dotnet build src/QuickText.App -c Release --nologo
goto :done

:test
echo.
echo  ==^> 单元测试 ...
dotnet test tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj -c Release --nologo
goto :done

:smoke
echo.
call :stop_running
echo  ==^> 窗口冒烟 ...
call :run_smoke
goto :done

:publish_sc
echo.
call :stop_running
echo  ==^> 打包绿色版 ^(win-x64 自包含^) ...
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64 --nologo
if errorlevel 1 goto :done
echo.
echo  输出: %cd%\src\QuickText.App\bin\Release\publish\QuickText.exe
goto :done

:publish_fd
echo.
call :stop_running
echo  ==^> 打包精简版 ^(需 .NET 10^) ...
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64-needs-dotnet10 --nologo
if errorlevel 1 goto :done
echo.
echo  输出: %cd%\src\QuickText.App\bin\Release\publish-needs-dotnet10\QuickText.exe
goto :done

:publish_all
echo.
call :stop_running
call :resolve_ver
echo  ==^> 打包全部 ^(版本 !VER!^) ...
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64 --nologo
if errorlevel 1 goto :done
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64-needs-dotnet10 --nologo
if errorlevel 1 goto :done

if not exist "dist" mkdir dist
set "ZIP_SC=dist\QuickText-!VER!-win-x64.zip"
set "ZIP_FD=dist\QuickText-!VER!-win-x64-needs-dotnet10.zip"
del /f /q "!ZIP_SC!" 2>nul
del /f /q "!ZIP_FD!" 2>nul

powershell -NoProfile -Command ^
  "Compress-Archive -Path 'src\QuickText.App\bin\Release\publish\QuickText.exe' -DestinationPath '!ZIP_SC!' -Force; ^
   if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; ^
   Compress-Archive -Path 'src\QuickText.App\bin\Release\publish-needs-dotnet10\QuickText.exe' -DestinationPath '!ZIP_FD!' -Force"
if errorlevel 1 goto :done

echo.
echo  已写入:
echo    !ZIP_SC!
echo    !ZIP_FD!
echo    src\QuickText.App\bin\Release\publish\QuickText.exe
echo    src\QuickText.App\bin\Release\publish-needs-dotnet10\QuickText.exe
goto :done

:full_gate
echo.
call :stop_running
echo  ==^> 完整检查: 测试 ...
dotnet test tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj -c Release --nologo
if errorlevel 1 goto :done
echo.
echo  ==^> 完整检查: Release 编译 ...
dotnet build src/QuickText.App -c Release --nologo
if errorlevel 1 goto :done
echo.
echo  ==^> 完整检查: 冒烟 ...
call :run_smoke
goto :done

:stop_running
rem A live QuickText.exe locks bin\...\QuickText.exe and MSBuild fails with MSB3027 on copy.
tasklist /FI "IMAGENAME eq QuickText.exe" 2>nul | find /I "QuickText.exe" >nul
if errorlevel 1 exit /b 0
echo  ==^> 检测到 QuickText 正在运行，正在结束进程 ...
taskkill /IM QuickText.exe /F >nul 2>&1
rem Brief pause so the OS can release the file handle before the next copy.
timeout /t 1 /nobreak >nul 2>&1
exit /b 0

:run_smoke
rem Prefer the framework build used by CI; fall back to any Release exe if the TFM folder moves.
set "EXE=src\QuickText.App\bin\Release\net10.0-windows\QuickText.exe"
if not exist "!EXE!" (
  set "EXE="
  for /f "delims=" %%F in ('dir /s /b "src\QuickText.App\bin\Release\QuickText.exe" 2^>nul') do (
    set "EXE=%%F"
    goto :smoke_have_exe
  )
)
:smoke_have_exe
if not defined EXE (
  echo  [!] 未找到 Release 的 QuickText.exe，先执行 [2] 编译 Release
  exit /b 1
)
if not exist "!EXE!" (
  echo  [!] 未找到 Release 的 QuickText.exe，先执行 [2] 编译 Release
  exit /b 1
)
set "MARKER=%TEMP%\quicktext-smoke.txt"
del /f /q "%MARKER%" 2>nul
echo  运行: !EXE! --smoke
"!EXE!" --smoke
set "SMOKE_ERR=!ERRORLEVEL!"
set "OK="
if exist "%MARKER%" (
  set /p OK=<"%MARKER%"
)
if /i not "!OK!"=="OK" (
  echo  [!] 冒烟失败 ^(exit=!SMOKE_ERR! marker=!OK!^)
  if exist "%MARKER%" type "%MARKER%"
  exit /b 1
)
if not "!SMOKE_ERR!"=="0" if not "!SMOKE_ERR!"=="" (
  echo  [!] 冒烟进程退出码非 0: !SMOKE_ERR!
  exit /b 1
)
echo  冒烟通过
exit /b 0

:resolve_ver
set "VER="
for /f "delims=" %%V in ('git describe --tags --abbrev=0 2^>nul') do set "VER=%%V"
if not defined VER set "VER=dev"
rem strip leading v from tag for a cleaner zip name if present — keep as-is to match CI tag style
exit /b 0

:done
set "ERR=!ERRORLEVEL!"
echo.
if not "!ERR!"=="0" (
  echo  [x] 失败，退出码 !ERR!
) else (
  echo  [ok] 完成
)
if not "%~1"=="" exit /b !ERR!
echo.
pause
goto :menu
