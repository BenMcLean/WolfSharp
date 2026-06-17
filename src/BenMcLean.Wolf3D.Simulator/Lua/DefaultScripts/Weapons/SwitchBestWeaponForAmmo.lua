-- WL_AGENT.C:GiveAmmo/GiveGas/GiveMissile weapon-switch equivalent.
-- Finds the highest-numbered owned weapon that uses the given ammo type and equips it.
-- Call this only after confirming the switch condition (e.g. weapon was knife, or ammo was zero).
-- Does nothing if no owned weapon uses this ammo type.
local ammoType = ...
local best = -1
local bestName = nil
for i = 0, GetMaxWeaponNumber() do
	if GetWeaponAmmoType(i) == ammoType and Has("Weapon" .. i) then
		if i > best then
			best = i
			bestName = GetWeaponName(i)
		end
	end
end
if bestName ~= nil then SwitchToWeapon(bestName) end
