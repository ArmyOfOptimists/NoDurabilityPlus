using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

//This mod is an extension/fork of the existing AlmostNoDurability published by Aidanamite at https://www.raftmodding.com/mods/almost-no-durability.

public class NoDurabilityPlus : Mod
{
    static JsonModInfo modInfo;
    static Dictionary<int, int> counterCache = new Dictionary<int, int>();
    static Dictionary<string, int> _slotModifiers;
    static Dictionary<string, int> SlotModifiers
    {
        get
        {
            if(_slotModifiers == null || _slotModifiers.Count == 0)
            {
                _slotModifiers = loadSlotModifiers();
            }
            return _slotModifiers;
        }
    }

    static string KEY_HOTSLOT = "PlayerInventory"; //If changed, modify the json entry as well
    static string HARMONY_ID = "us.barpidone.raftmods.nodurabilityplus";

    Harmony harmony;    

    public static new void Log(object message)
    {
        Debug.Log("[" + modInfo.name + "]: " + message.ToString());
    }

    public static void ErrorLog(object message)
    {
        Debug.LogError("[" + modInfo.name + "]: " + message.ToString());
    }

    public static void setModifier(string type, int value) 
    {
        SlotModifiers[type] = value;
        Log(string.Format( "Type: {0}, Modifier: {1}", type, SlotModifiers[type]));
    }

    public static bool slotShouldBeAffected(Slot slot) {
        return slot.itemInstance.settings_equipment != null 
            && slot.itemInstance.settings_equipment.EquipType != EquipSlotType.None;
    }

    public static Dictionary<string,int> loadSlotModifiers()
    {
        Dictionary<string, int> ret = new Dictionary<string, int>();
        try
        {
            foreach (int slotIndex in Enum.GetValues(typeof(EquipSlotType)))
            {
                string slotName = ((EquipSlotType)slotIndex).ToString();
                if (slotName != "None" && slotName != "Backpack") // Backpack has no durability so no need to include it.
                {
                    ret[slotName] = 0;
                    Debug.Log("Added '" + slotName + "' to modifier list.");
                }
            }
            ret["PlayerInventory"] = 0;
            Debug.Log("Added '" + KEY_HOTSLOT + "' to modifier list.");
        }
        catch (Exception e)
        {
            Log("Exception: " + e.Message);
            Debug.LogException(e);
        }
        return ret;
    }

    /**
     * returns true if durability loss has to be prevented
     */
    public static bool ModifyLoss(Slot instance)
    {
        string slotName = instance.itemInstance.settings_equipment.EquipType.ToString();
        int modifier = SlotModifiers[slotName];
        if (modifier > 0)
            return doCache(instance, modifier);
        else
            return true;
    }

    public static bool ModifyLoss(PlayerInventory instance)
    {
        int modifier = SlotModifiers[KEY_HOTSLOT];
        if (modifier > 0)
            return doCache(instance, modifier);
        else

            return true;
    }

    public static bool doCache(object instance, int modifier)
    {
        int hash = instance.GetHashCode();

        if (counterCache.ContainsKey(hash))
        {
            counterCache[hash] = counterCache[hash] + 1;
        }
        else
        {
            counterCache.Add(hash, 1);
        }

        if (counterCache[hash] < modifier)
        {
            return true;
        }
        else
        {
            counterCache[hash] = 0;
            return false;
        }
    }

    public void Start()
    {
        modInfo = modlistEntry.jsonmodinfo;   
        harmony = new Harmony(HARMONY_ID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Log("loaded :) [v"+ modInfo.version +"]");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(HARMONY_ID);
        Destroy(gameObject);
        Log("unloaded :(");
    }

    /*******************************
    *   EXTRA-SETTINGS-API STUFF   *
    ********************************/
    static HarmonyLib.Traverse ExtraSettingsAPI_Traverse;
    static bool ExtraSettingsAPI_Loaded = false;

    public void ExtraSettingsAPI_Load()
    {
        ES_API_SetValues();
    }    

    public void ExtraSettingsAPI_SettingsOpen()
    {
        foreach (var sm in SlotModifiers)
        {
            ExtraSettingsAPI_SetComboboxSelectedIndex(sm.Key, sm.Value);            
        }
    }

    public void ExtraSettingsAPI_SettingsClose()
    {
        ES_API_SetValues();
    }

    public void ExtraSettingsAPI_SetComboboxSelectedIndex(string SettingName, int value)
    {
        if (ExtraSettingsAPI_Loaded)
            ExtraSettingsAPI_Traverse.Method("setComboboxSelectedIndex", new object[] { this, SettingName, value }).GetValue<int>();
    }

    public int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName)
    {
        if (ExtraSettingsAPI_Loaded)
            return ExtraSettingsAPI_Traverse.Method("getComboboxSelectedIndex", new object[] { this, SettingName }).GetValue<int>();
        return -1;
    }

    public void ES_API_SetValues()
    {
        List<string> keys = new List<string>(SlotModifiers.Keys);
        foreach(string k in keys)
        {
            setModifier(k, ExtraSettingsAPI_GetComboboxSelectedIndex(k));
        }
    }

    /********************
    *   HARMONY STUFF   *
    *********************/
    [HarmonyPatch(typeof(Slot), "IncrementUses")]
    public class HarmonyPatch_Slot_IncrementUses
    {
        [HarmonyPrefix]
        static void Prefix(ref Slot __instance, ref int amountOfUsesToAdd)
        {
            if (slotShouldBeAffected(__instance)){
                if (amountOfUsesToAdd < 0 && ModifyLoss(__instance)){
                    amountOfUsesToAdd = 0;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerInventory), "RemoveDurabillityFromHotSlot")]
    public class HarmonyPatch_PlayerInventory_RemoveDurabillityFromHotSlot
    {
        [HarmonyPrefix]
        static void Prefix(ref PlayerInventory __instance, ref int durabilityStacksToRemove)
        {
            if (durabilityStacksToRemove > 0 && ModifyLoss(__instance)){
                durabilityStacksToRemove = 0;
            }
        }
    }
}