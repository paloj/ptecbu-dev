@echo off
REM Script to start a Windows Backup to a specified target with the hostname

REM Check if the backup target parameter is provided
if "%~1"=="" (
    echo Usage: %0 ^<BackupTarget^>
    echo Example: %0 \\NAS\backup\
    exit /b 1
)

REM Get the hostname of the computer
for /f %%i in ('hostname') do set Hostname=%%i

REM Set the backup target with the hostname
set BackupTarget=%~1\%Hostname%

REM Check if the backup target folder exists, and if not, create it
if not exist "%BackupTarget%" (
    mkdir "%BackupTarget%"
    echo Created backup folder: %BackupTarget%
)

REM Start the backup
wbadmin start backup -backuptarget:%BackupTarget% -include:C: -allcritical -quiet

REM Check the exit code to detect errors (optional)
if %errorlevel% neq 0 (
    echo Backup failed with error code %errorlevel%.
    REM Add error handling or logging here
) else (
    echo Backup completed successfully.
    REM Write the current date and time to lastsystemimage.txt
    for /f "delims=" %%a in ('wmic OS Get localdatetime ^| find "."') do set datetime=%%a
    set datetime=%datetime:~0,4%-%datetime:~4,2%-%datetime:~6,2% %datetime:~8,2%:%datetime:~10,2%:%datetime:~12,2%
    echo %datetime% > lastsystemimage.txt
)
