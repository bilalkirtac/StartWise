@echo off
rem Packs StartWise into dist\StartWise-<version>.mcpb (Claude Desktop extension). No exe is built: the sources are compiled in memory.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0packaging\pack.ps1"
