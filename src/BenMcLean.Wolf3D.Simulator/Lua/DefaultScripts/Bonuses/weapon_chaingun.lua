local isNew = not Has("Weapon3")
local hadAmmo = GetValue("bullets") > 0
SetValue("Weapon3", 1)
AddValue("bullets", 6)
if isNew then
	SwitchToWeapon("chaingun")
elseif not hadAmmo then
	-- WL_AGENT.C:GiveAmmo inside GiveWeapon - switch back from knife if ammo was zero
	SwitchBestWeaponForAmmo("bullets")
end
SetValue("FaceGrinTics", 4)
PlaySound(ResolveSound("GETGATLINGSND", "D_BONUSSND"))
FlashScreen(0xFFF800)
return true
