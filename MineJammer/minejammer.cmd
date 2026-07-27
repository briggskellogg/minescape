@echo off
setlocal
set "MJ_ROOT=%~dp0"
set "MJ_PY=%~dp0..\.tools\python-3.13.14\python.exe"
if not exist "%MJ_PY%" (
  echo Pinned MineScape Python is missing: "%MJ_PY%" 1>&2
  exit /b 2
)
"%MJ_PY%" -c "import sys; root=sys.argv.pop(1); sys.path.insert(0, root); from minejammer.cli import main; raise SystemExit(main(sys.argv[1:]))" "%MJ_ROOT%." %*
exit /b %ERRORLEVEL%
