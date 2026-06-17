if GetValue("bullets") < GetMax("bullets") then
	local hadAmmo = GetValue("bullets") > 0
	AddValue("bullets", 8)
	PlaySound(ResolveSound("GETAMMOSND", "D_BONUSSND"))
	FlashScreen(0xFFF800)
	-- WL_AGENT.C:GiveAmmo - if ammo was zero (knife forced), switch back to chosen weapon
	if not hadAmmo then SwitchBestWeaponForAmmo("bullets") end
	return true
end
return false
