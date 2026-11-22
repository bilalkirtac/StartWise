@echo off
rem Packs StartWise into dist\StartWise-<version>.mcpb (Claude Desktop extension). Run build.cmd first.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0packaging\pack.ps1"
