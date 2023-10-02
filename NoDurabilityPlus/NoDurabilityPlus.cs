using HarmonyLib;
using HMLLibrary;
using System;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

//This mod is an extension/fork of the existing AlmostNoDurability published by Aidanamite at https://www.raftmodding.com/mods/almost-no-durability.

public class NoDurabilityPlus : Mod
{
    static JsonModInfo modInfo;

    //Adds compatibility for Augmented Equipment
    static Dictionary<string, EquipSlotType> EquipSlotDictionary = new Dictionary<string, EquipSlotType>
    {
        ["None"] = (EquipSlotType)0,
        ["Feet"] = (EquipSlotType)1,
        ["Head"] = (EquipSlotType)2,
        ["Light"] = (EquipSlotType)3,
        ["Chest"] = (EquipSlotType)4,
        ["Backpack"] = (EquipSlotType)6,
        ["ZiplineTool"] = (EquipSlotType)7,
        ["OxygenBottle"] = (EquipSlotType)8,
        ["Belt"] = (EquipSlotType)9,
        ["SwimFeet"] = (EquipSlotType)10,
        ["Glove"] = (EquipSlotType)11

    };

    static Dictionary<string, string> EquipNameSlotDictionary = new Dictionary<string, string>
    {
        {"0","None"},
        {"1","Feet"},
        {"2","Head"},
        {"3","Light"},
        {"4","Chest"},
        {"6","Backpack"},
        {"7","ZiplineTool"},
        {"8","OxygenBottle"},
        {"9","Belt"},
        {"10","SwimFeet"},
        {"11","Glove"}
    };

  public static EquipSlotType Get(string equipmentType) {
    return EquipSlotDictionary[equipmentType];
  }

  public static string GetName(string equipmentname) {
    return EquipNameSlotDictionary[equipmentname];
  }





    static Dictionary<int, int> counterCache = new Dictionary<int, int>();
    static Dictionary<string, int> _slotModifiers;
    static Dictionary<string, int> SlotModifiers
    {
        get
        {
            if(_slotModifiers == null || _slotModifiers.Count == 0)
            {
                _slotModifiers = initializeSlotModifiers();
            }
            return _slotModifiers;
        }
    }
    static bool useExperimental = false;
    static string[] itemBlacklist = { "bucket" };  //Item names more readable.  Maybe refactor into IDs later.
    private const string HARMONY_ID = "us.barpidone.raftmods.nodurabilityplus";

    //If following changed, modify the json entry as well
    private const string KEY_WIDE_ANTI_DUPE = "WideAntiDuplication"; 
    private const string KEY_HOTSLOT = "PlayerInventory"; 

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

    public static void setExperimentalMode(bool value)
    {
        useExperimental = value;
        Log("Experimental mode: " + value);
    }

    public static bool slotShouldBeAffected(Slot slot) {
        return slot.itemInstance.settings_equipment != null 
            && slot.itemInstance.settings_equipment.EquipType != EquipSlotType.None;
    }

    public static Dictionary<string,int> initializeSlotModifiers()
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
            if (Harmony.HasAnyPatches("com.bahamut.augmentedequipment"))
            {
                Debug.Log("[NoDurabilityPlus]: AugmentedEquipment is installed! Enabling compatibility mode.");
                string[] AugmentedEquipmentSlotTypes = new string[3] {"Light", "SwimFeet", "Glove"};
                foreach(string slotName in AugmentedEquipmentSlotTypes)
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
            if (!Harmony.HasAnyPatches("com.bahamut.augmentedequipment"))
            {
                Debug.LogException(e);
            }
        }
        return ret;
    }

    public static bool isBlacklisted(Slot instance)
    {
        if(useExperimental)
            return (instance.itemInstance.BaseItemMaxUses <= 1);

        string itemName = instance.itemInstance.settings_Inventory.DisplayName.ToLower();
        for (int i = 0; i < itemBlacklist.Length; ++i)
        {
            if (itemBlacklist[i] == itemName)
                return true;
        }
        return false;
    }

    public static bool isBlacklisted(PlayerInventory instance)
    {
        if(useExperimental)
            return (instance.GetSelectedHotbarItem().BaseItemMaxUses <= 1);

        string itemName = instance.GetSelectedHotbarItem().settings_Inventory.DisplayName.ToLower();
        for (int i = 0; i < itemBlacklist.Length; ++i)
        {
            if (itemBlacklist[i] == itemName)
                return true;
        }
        return false;
    }

    public static bool ModifyLoss(Slot instance)
    {
        string itemName = instance.itemInstance.settings_Inventory.DisplayName.ToLower();
        string slotName = instance.itemInstance.settings_equipment.EquipType.ToString();
        if(EquipNameSlotDictionary.ContainsKey(slotName))
        {
            slotName = GetName(slotName);
        }
        if(slotName == "Belt")
        {
            return true;
        }
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
        Log("loaded [v"+ modInfo.version +"].");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(HARMONY_ID);
        Destroy(gameObject);
        Log("unloaded.");
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
        ExtraSettingsAPI_SetCheckboxState(KEY_WIDE_ANTI_DUPE, useExperimental);
    }

    public void ExtraSettingsAPI_SettingsClose()
    {
        ES_API_SetValues();
    }

    public void ExtraSettingsAPI_SetCheckboxState(string SettingName, bool value)
    {
        if(ExtraSettingsAPI_Loaded)
            ExtraSettingsAPI_Traverse.Method("setCheckboxState", new object[] { this, SettingName, value }).GetValue<bool>();
    }

    public bool ExtraSettingsAPI_GetCheckboxState(string SettingName)
    {
        if(ExtraSettingsAPI_Loaded)
            return ExtraSettingsAPI_Traverse.Method("getCheckboxState", new object[] { this, SettingName }).GetValue<bool>();
        return false;
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
       setExperimentalMode(ExtraSettingsAPI_GetCheckboxState(KEY_WIDE_ANTI_DUPE));
    }

    public bool ExtraSettingsAPI_HandleSettingVisible(string SettingName) 
    {
        if (Harmony.HasAnyPatches("com.bahamut.augmentedequipment"))
        {
            return true;
        }
        else
        {
            return false;
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
            if (slotShouldBeAffected(__instance) && !isBlacklisted(__instance)){
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
            if (durabilityStacksToRemove > 0 && !isBlacklisted(__instance) && ModifyLoss(__instance)){
                durabilityStacksToRemove = 0;
            }
        }
    }
}