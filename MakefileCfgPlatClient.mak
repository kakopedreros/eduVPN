#
#   eduVPN - VPN for education and research
#
#   Copyright: 2017-2021 The Commons Conservancy eduVPN Programme
#   SPDX-License-Identifier: GPL-3.0+
#

WIX_CANDLE_FLAGS_CFG_PLAT_CLIENT=$(WIX_CANDLE_FLAGS_CFG_PLAT_CLIENT) \
	-dClientTarget="$(CLIENT_TARGET)" \
	-dClientTitle="$(CLIENT_TITLE)" \
	-dClientUpgradeCode="$(CLIENT_UPGRADE_CODE)" \
	-dClientAboutUri="$(CLIENT_ABOUT_URI)" \
	-dClientId="$(CLIENT_ID)" \
	-dIDS_CLIENT_PREFIX="$(IDS_CLIENT_PREFIX)"


######################################################################
# Registration
######################################################################

!IF "$(CFG)" == "$(TEST_CFG)"
!IF "$(PLAT)" == "$(TEST_PLAT)"
RegisterShortcuts :: \
	"$(PROGRAMDATA)\Microsoft\Windows\Start Menu\Programs\$(CLIENT_TITLE) Client.lnk"

UnregisterShortcuts ::
	-if exist "$(PROGRAMDATA)\Microsoft\Windows\Start Menu\Programs\$(CLIENT_TITLE) Client.lnk" del /f /q "$(PROGRAMDATA)\Microsoft\Windows\Start Menu\Programs\$(CLIENT_TITLE) Client.lnk"

"$(PROGRAMDATA)\Microsoft\Windows\Start Menu\Programs\$(CLIENT_TITLE) Client.lnk" : \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET).Client.exe" \
	"bin\$(CFG)\$(PLAT)\eduVPN.Resources.dll"
	cscript.exe "bin\MkLnk.wsf" //Nologo $@ "$(MAKEDIR)\bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET).Client.exe" \
		/F:"$(MAKEDIR)\bin\$(CFG)\$(PLAT)" \
		/LN:"@$(MAKEDIR)\bin\$(CFG)\$(PLAT)\eduVPN.Resources.dll,-$(IDS_CLIENT_PREFIX)1" \
		/C:"@$(MAKEDIR)\bin\$(CFG)\$(PLAT)\eduVPN.Resources.dll,-$(IDS_CLIENT_PREFIX)2"

RegisterOpenVPNInteractiveService :: \
	UnregisterOpenVPNInteractiveServiceSCM \
	"bin\$(CFG)\$(PLAT)" \
	"bin\$(CFG)\$(PLAT)\eduVPN.Resources.dll" \
	"bin\OpenVPN\$(PLAT)\config"
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /ve                   /t REG_SZ /d "$(MAKEDIR)\bin\OpenVPN\$(PLAT)"             $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "exe_path"         /t REG_SZ /d "$(MAKEDIR)\bin\OpenVPN\$(PLAT)\openvpn.exe" $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "config_dir"       /t REG_SZ /d "$(MAKEDIR)\bin\OpenVPN\$(PLAT)\config"      $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "config_ext"       /t REG_SZ /d "conf"                                       $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "log_dir"          /t REG_SZ /d "$(MAKEDIR)\bin\OpenVPN\$(PLAT)\config"      $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "log_append"       /t REG_SZ /d "0"                                          $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "priority"         /t REG_SZ /d "NORMAL_PRIORITY_CLASS"                      $(REG_FLAGS)
	reg.exe add "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "ovpn_admin_group" /t REG_SZ /d "Users"                                      $(REG_FLAGS)
	sc.exe create "OpenVPNServiceInteractive$$$(CLIENT_TARGET)" \
		binpath= "\"$(MAKEDIR)\bin\OpenVPN\$(PLAT)\openvpnserv.exe\" -instance interactive $$$(CLIENT_TARGET)" \
		DisplayName= "@$(MAKEDIR)\bin\$(CFG)\$(PLAT)\eduVPN.Resources.dll,-$(IDS_CLIENT_PREFIX)5" \
		type= own \
		start= auto \
		depend= "Dhcp"
	sc.exe description "OpenVPNServiceInteractive$$$(CLIENT_TARGET)" "@$(MAKEDIR)\bin\$(CFG)\$(PLAT)\eduVPN.Resources.dll,-$(IDS_CLIENT_PREFIX)6"
	net.exe start "OpenVPNServiceInteractive$$$(CLIENT_TARGET)"

UnregisterOpenVPNInteractiveService :: \
	UnregisterOpenVPNInteractiveServiceSCM
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /ve                   $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "exe_path"         $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "config_dir"       $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "config_ext"       $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "log_dir"          $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "log_append"       $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "priority"         $(REG_FLAGS) > NUL 2>&1
	-reg.exe delete "HKLM\Software\OpenVPN$$$(CLIENT_TARGET)" /v "ovpn_admin_group" $(REG_FLAGS) > NUL 2>&1

UnregisterOpenVPNInteractiveServiceSCM ::
	-net.exe stop "OpenVPNServiceInteractive$$$(CLIENT_TARGET)"  > NUL 2>&1
	-sc.exe delete "OpenVPNServiceInteractive$$$(CLIENT_TARGET)" > NUL 2>&1
!ENDIF
!ENDIF


######################################################################
# Setup
######################################################################

!IF "$(CFG)" == "Release"
SetupMSI :: \
	"bin\Setup\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET).msi"
!ENDIF


######################################################################
# Building
######################################################################

"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET).Client.exe" ::
	bin\nuget.exe restore $(NUGET_FLAGS)
	msbuild.exe "eduVPN.sln" /p:Configuration="$(CFG)" /p:Platform="$(PLAT)" $(MSBUILD_FLAGS)

Clean ::
	-msbuild.exe "eduVPN.sln" /t:Clean /p:Configuration="$(CFG)" /p:Platform="$(PLAT)" $(MSBUILD_FLAGS)

"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client.wixobj" : \
	"eduVPNClient.wxs" \
	"bin\$(CFG)\$(PLAT)\$(VC142REDIST_MSM)"
	"$(WIX)bin\wixcop.exe" $(WIX_WIXCOP_FLAGS) "eduVPNClient.wxs"
	"$(WIX)bin\candle.exe" $(WIX_CANDLE_FLAGS_CFG_PLAT_CLIENT) -dVC150RedistMSM="$(VC142REDIST_MSM)" -out $@ "eduVPNClient.wxs"

Clean ::
	-if exist "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client.wixobj" del /f /q "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client.wixobj"

"bin\Setup\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET).msi" : \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_ar.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_de.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_fr.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_nl.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_sl.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_tr.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_uk.mst" \
	"bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_en-US.msi"
	copy /y "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_en-US.msi" "$(@:"=).tmp" > NUL
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_ar.mst" 1025 /L
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_de.mst" 1025 /L
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_fr.mst" 1036 /L
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_nl.mst" 1043 /L
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_sl.mst" 1060 /L
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_tr.mst" 1055 /L
	cscript.exe $(CSCRIPT_FLAGS) "bin\MSI.wsf" //Job:AddStorage "$(@:"=).tmp" "bin\$(CFG)\$(PLAT)\$(CLIENT_TARGET)Client_$(VERSION)_$(SETUP_TARGET)_uk.mst" 1058 /L
!IFDEF MANIFESTCERTIFICATETHUMBPRINT
	signtool.exe sign /sha1 "$(MANIFESTCERTIFICATETHUMBPRINT)" /fd sha256 /tr "$(MANIFESTTIMESTAMPRFC3161URL)" /td sha256 /d "$(CLIENT_TITLE) Client" /q "$(@:"=).tmp"
!ENDIF
	attrib.exe +r "$(@:"=).tmp"
	if exist $@ attrib.exe -r $@
	move /y "$(@:"=).tmp" $@ > NUL


######################################################################
# Locale-specific rules
######################################################################

LANG=ar
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=en-US
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=de
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=fr
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=nl
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=sl
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=tr
!INCLUDE "MakefileCfgPlatClientLang.mak"

LANG=uk
!INCLUDE "MakefileCfgPlatClientLang.mak"
