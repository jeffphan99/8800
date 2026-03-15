public enum PlayerRole
{
    None = 0,
    Silverback = 1,
    Neurochimp = 2,
    WrenchMonkey = 3
}

public static class PlayerRoleHelper
{
    public static string DisplayName(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Silverback: return "Silverback";
            case PlayerRole.Neurochimp: return "Neurochimp";
            case PlayerRole.WrenchMonkey: return "Wrench Monkey";
            default: return "Unassigned";
        }
    }

    public static int SkinIndex(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Silverback: return 0;
            case PlayerRole.Neurochimp: return 1;
            case PlayerRole.WrenchMonkey: return 2;
            default: return -1;
        }
    }
}
