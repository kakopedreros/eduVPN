#
#   eduVPN - VPN for education and research
#
#   Copyright: 2017-2021 The Commons Conservancy eduVPN Programme
#   SPDX-License-Identifier: GPL-3.0+
#

VERSION=1.0.37
TAPWIN_VERSION=9.24.5.1
OPENVPN_VERSION=2.5.1.12

# Default testing configuration and platform
TEST_CFG=Debug
!IF "$(PROCESSOR_ARCHITECTURE)" == "AMD64"
TEST_PLAT=x64
!ELSE
TEST_PLAT=x86
!ENDIF

# Utility default flags
REG_FLAGS=/f
NUGET_FLAGS=-Verbosity quiet
MSBUILD_FLAGS=/m /v:minimal /nologo
CSCRIPT_FLAGS=//Nologo
WIX_EXTENSIONS=-ext WixNetFxExtension -ext WixUtilExtension -ext WixBalExtension
WIX_WIXCOP_FLAGS=-nologo "-set1$(MAKEDIR)\wixcop.xml"
WIX_CANDLE_FLAGS=-nologo \
	-dTAPWin.Version="$(TAPWIN_VERSION)" \
	-dOpenVPN.Version="$(OPENVPN_VERSION)" \
	-dCore.Version="$(VERSION)" \
	-dVersion="$(VERSION)" \
	$(WIX_EXTENSIONS)
WIX_LIGHT_FLAGS=-nologo -dcl:high -spdb -sice:ICE03 -sice:ICE60 -sice:ICE61 -sice:ICE82 $(WIX_EXTENSIONS)
WIX_INSIGNIA_FLAGS=-nologo


######################################################################
# Default target
######################################################################

All :: \
	Setup


######################################################################
# Registration
######################################################################

Register :: \
	NuGetRestore \
	RegisterOpenVPNInteractiveService \
	RegisterShortcuts

Unregister :: \
	UnregisterShortcuts \
	UnregisterOpenVPNInteractiveService

NuGetRestore ::
	bin\nuget.exe restore $(NUGET_FLAGS)


######################################################################
# Setup
######################################################################

Setup :: \
	NuGetRestore \
	SetupBuild \
	SetupMSI \
	SetupExe

"bin\Setup\eduVPN.windows.json.minisig" \
"bin\Setup\LetsConnect.windows.json.minisig" : \
	"bin\Setup\eduVPN.windows.json" \
	"bin\Setup\LetsConnect.windows.json"
	echo Signing $**
	minisign.exe -Sm $**


######################################################################
# Configuration specific rules
######################################################################

CFG=Debug
!INCLUDE "MakefileCfg.mak"

CFG=Release
!INCLUDE "MakefileCfg.mak"


######################################################################
# Platform specific rules
######################################################################

PLAT=x86
!INCLUDE "MakefilePlat.mak"

PLAT=x64
!INCLUDE "MakefilePlat.mak"
