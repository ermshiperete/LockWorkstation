@echo off
%windir%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe LockWorkStationService.exe
sc start LockWorkStationService