using UnityEngine;

public class MonsterListener : MonsterAI
{
    protected override bool DetectPlayer()
    {
        // Listener NEVER detects visually — only via noise (CheckForNoise in base)
        // Jukebox taunt still works
        if (FindJukeboxTaunt() != null) return true;
        return false;
    }
}
