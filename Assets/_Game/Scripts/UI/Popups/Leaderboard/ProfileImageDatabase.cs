using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Leaderboard/Profile Image Database")]
public class ProfileImageDatabase : ScriptableObject
{
    public List<Sprite> profiles;

    public Sprite GetSprite(int index)
    {
        if(index < 0 || index >= profiles.Count)
        {
            return profiles[0];
        }

        return profiles[index];
    }
}