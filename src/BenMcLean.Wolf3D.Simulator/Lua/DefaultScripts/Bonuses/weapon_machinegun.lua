local isNew = not Has("Weapon2")
local hadAmmo = GetValue("bullets") > 0
SetValue("Weapon2", 1)
AddValue("bullets", 6)
if isNew then
	SwitchToWeapon("machinegun")
elseif not hadAmmo then
	-- WL_AGENT.C:GiveAmmo inside GiveWeapon - switch back from knife if ammo was zero
	SwitchBestWeaponForAmmo("bullets")
end
PlaySound(ResolveSound("GETMACHINESND", "D_BONUSSND"))
FlashScreen(0xFFF800)
return true
