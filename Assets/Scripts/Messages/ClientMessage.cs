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
    public string slot;
}

[Serializable]
public class UnequipItemMessage
{
    public string slot;
}

[Serializable]
public class SwapItemMessage
{
    public string fromSlot;
    public string toSlot;
}

[Serializable]
public class CharacterCreateMessage
{
    public string name;
    public string archetype;
}

[Serializable]
public class CharacterSelectMessage
{
    public string characterId;
}

[Serializable]
public class CharacterDeleteMessage
{
    public string characterId;
}