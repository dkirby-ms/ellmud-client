using System;

[Serializable]
public class CommandMessage
{
    public string verb;
    public string[] args;
}

[Serializable]
public class EquipItemMessage
{
    public string itemId;
    public string targetSlot;
}

[Serializable]
public class UnequipItemMessage
{
    public string slot;
}

[Serializable]
public class SwapItemMessage
{
    public string itemId;
    public string targetSlot;
}

[Serializable]
public class ToggleFlagMessage
{
    public string flag;
    public bool enabled;
}

[Serializable]
public class CharacterCreateMessage
{
    public string name;
    public string startingZoneSlug;
}