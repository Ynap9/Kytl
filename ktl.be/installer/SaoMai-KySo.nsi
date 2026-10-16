Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

!define APP_ID        "SaoMai-KySo"
!define APP_NAME      "Sao Mai - Ký số"
!define APP_VERSION   "1.3.0"
!define APP_PUBLISHER "Sao Mai Digital Solutions"
!define APP_EXE       "SaoMai-KySo.exe"

!define LEGACY_EXE        "KTL.exe"
!define LEGACY_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\KTL"

!define PUBLISH_DIR "..\..\publish"
!define DRIVERS_DIR "drivers"

!define WEBVIEW2_EXE      "MicrosoftEdgeWebView2Setup.exe"
!define BIT4ID_EXE        "bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe"
!define TOKEN_MANAGER_EXE "VGCAv1_Installer_1.0.21.0402.exe"

!define WEBVIEW2_CLIENT "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"

!if /FileExists "${PUBLISH_DIR}\${APP_EXE}"
!else
  !error "${PUBLISH_DIR}\${APP_EXE} not found - run build-desktop.ps1 before compiling."
!endif

Name        "${APP_NAME} ${APP_VERSION}"
OutFile     "installer-output\${APP_ID}-Setup.exe"
InstallDir  "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${UNINST_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP_PUBLISHER} - ${APP_NAME} ${APP_VERSION}"

VIProductVersion "1.3.0.0"
VIAddVersionKey "ProductName"     "${APP_NAME}"
VIAddVersionKey "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey "FileVersion"     "${APP_VERSION}.0"
VIAddVersionKey "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "LegalCopyright"  "${APP_PUBLISHER}"

Var Detected
Var DepFailed
Var DriverInstalled

!define MUI_ABORTWARNING
!define MUI_ICON   "..\ktl.be.desktop\Assets\app.ico"
!define MUI_UNICON "..\ktl.be.desktop\Assets\app.ico"
!define MUI_WELCOMEPAGE_TEXT "${APP_NAME} generates documents in bulk, adds red seals and handwritten signatures, and digitally signs PDF files.$\r$\n$\r$\nSetup will also install any missing components: Microsoft Edge WebView2, the bit4id token driver and VGCA TokenManager.$\r$\n$\r$\nClick Next to continue."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Run ${APP_NAME} now"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function DetectWebView2
  StrCpy $Detected "0"
  SetRegView 64
  ReadRegStr $R0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_CLIENT}" "pv"
  ${If} $R0 == ""
    ReadRegStr $R0 HKCU "SOFTWARE\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_CLIENT}" "pv"
  ${EndIf}
  ${If} $R0 != ""
  ${AndIf} $R0 != "0.0.0.0"
    StrCpy $Detected "1"
  ${EndIf}
FunctionEnd

Function DetectBit4id
  StrCpy $Detected "0"
  SetRegView 32
  EnumRegKey $R0 HKLM "SOFTWARE\bit4id" 0
  ${If} $R0 != ""
    StrCpy $Detected "1"
  ${EndIf}
  ${If} $Detected != "1"
    ${If} ${FileExists} "$PROGRAMFILES32\Bit4Id\Universal MW\uninst.exe"
      StrCpy $Detected "1"
    ${EndIf}
  ${EndIf}
  SetRegView 64
FunctionEnd

Function DetectTokenManager
  StrCpy $Detected "0"
  SetRegView 64
  StrCpy $R1 0
  ${Do}
    EnumRegKey $R0 HKLM "SOFTWARE\Microsoft\Cryptography\Defaults\Provider" $R1
    ${If} $R0 == ""
      ${Break}
    ${EndIf}
    StrCpy $R2 $R0 6
    ${If} $R2 == "eToken"
      StrCpy $Detected "1"
      ${Break}
    ${EndIf}
    IntOp $R1 $R1 + 1
  ${Loop}
  ${If} $Detected != "1"
    ${If} ${FileExists} "$WINDIR\System32\eTOKCSP.dll"
      StrCpy $Detected "1"
    ${EndIf}
  ${EndIf}
FunctionEnd

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${APP_NAME} requires 64-bit Windows."
    Abort
  ${EndIf}
  SetShellVarContext all
  StrCpy $DepFailed ""
  StrCpy $DriverInstalled "0"
FunctionEnd

Section "-${APP_NAME} application"
  SetDetailsPrint both

  SetRegView 64
  ReadRegStr $1 HKLM "${LEGACY_UNINST_KEY}" "InstallLocation"
  ${If} $1 != ""
  ${AndIf} ${FileExists} "$1\Uninstall.exe"
    DetailPrint "Removing the previous KTL installation..."
    nsExec::Exec 'taskkill /F /IM "${LEGACY_EXE}"'
    Pop $0
    ExecWait '"$1\Uninstall.exe" /S _?=$1'
    RMDir /r "$1"
  ${EndIf}

  DetailPrint "Closing ${APP_EXE} if it is running..."
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0

  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut  "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  CreateDirectory "$INSTDIR\Log"
  nsExec::ExecToLog 'icacls "$INSTDIR\Log" /grant *S-1-5-32-545:(OI)(CI)M'
  Pop $0
  DetailPrint "Granting write access to the Log folder: code $0"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Logs.lnk" "$INSTDIR\Log"

  SetRegView 64
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME} ${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "-USB token and digital signing components"
  SetDetailsPrint both

  DetailPrint "Checking Microsoft Edge WebView2 Runtime..."
  Call DetectWebView2
  ${If} $Detected == "1"
    DetailPrint "  already installed - skipped."
  ${Else}
    ${If} ${FileExists} "$INSTDIR\${DRIVERS_DIR}\${WEBVIEW2_EXE}"
      DetailPrint "  not found - installing..."
      ExecWait '"$INSTDIR\${DRIVERS_DIR}\${WEBVIEW2_EXE}" /silent /install' $0
      DetailPrint "  installer returned code $0"
      Call DetectWebView2
      ${If} $Detected != "1"
        StrCpy $DepFailed "$DepFailed$\n   - Microsoft Edge WebView2 Runtime (code $0)"
      ${EndIf}
    ${Else}
      StrCpy $DepFailed "$DepFailed$\n   - Microsoft Edge WebView2 Runtime (installer missing)"
    ${EndIf}
  ${EndIf}

  DetailPrint "Checking bit4id token driver (Universal MW)..."
  Call DetectBit4id
  ${If} $Detected == "1"
    DetailPrint "  already installed - skipped."
  ${Else}
    ${If} ${FileExists} "$INSTDIR\${DRIVERS_DIR}\${BIT4ID_EXE}"
      DetailPrint "  not found - installing (this may take a few minutes)..."
      ExecWait '"$INSTDIR\${DRIVERS_DIR}\${BIT4ID_EXE}" /S' $0
      DetailPrint "  installer returned code $0"
      Call DetectBit4id
      ${If} $Detected == "1"
        StrCpy $DriverInstalled "1"
      ${Else}
        StrCpy $DepFailed "$DepFailed$\n   - Bit4id Universal MW (code $0)"
      ${EndIf}
    ${Else}
      StrCpy $DepFailed "$DepFailed$\n   - Bit4id Universal MW (installer missing)"
    ${EndIf}
  ${EndIf}

  DetailPrint "Checking legacy token driver (VGCA TokenManager)..."
  Call DetectTokenManager
  ${If} $Detected == "1"
    DetailPrint "  already installed - skipped."
  ${Else}
    ${If} ${FileExists} "$INSTDIR\${DRIVERS_DIR}\${TOKEN_MANAGER_EXE}"
      DetailPrint "  not found - installing..."
      ExecWait '"$INSTDIR\${DRIVERS_DIR}\${TOKEN_MANAGER_EXE}" /S' $0
      DetailPrint "  installer returned code $0"
      Call DetectTokenManager
      ${If} $Detected == "1"
        StrCpy $DriverInstalled "1"
      ${Else}
        StrCpy $DepFailed "$DepFailed$\n   - VGCA TokenManager (code $0)"
      ${EndIf}
    ${Else}
      StrCpy $DepFailed "$DepFailed$\n   - VGCA TokenManager (installer missing)"
    ${EndIf}
  ${EndIf}

  ${If} $DepFailed != ""
    DetailPrint "WARNING: some components could not be installed."
    MessageBox MB_ICONEXCLAMATION "The following components could not be installed:$\n$DepFailed$\n$\n\
${APP_NAME} will still run, but digital signing may not detect certificates on your USB token."
  ${EndIf}

  ${If} $DriverInstalled == "1"
    SetRebootFlag true
  ${EndIf}
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  nsExec::Exec 'taskkill /F /IM "${APP_EXE}"'
  Pop $0

  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Logs.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"

  SetRegView 64
  DeleteRegKey HKLM "${UNINST_KEY}"
SectionEnd
