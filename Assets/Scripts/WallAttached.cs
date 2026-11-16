using UnityEngine;

/// <summary>
/// Markeert een object (bv. een poliep-station) dat vast op de darmwand zit.
/// ColonWallMotion verplaatst het object mee met de beweging van de wand op die plek,
/// zodat het niet in de wand verdwijnt of ervoor zweeft.
/// Zet dit op de root van het station (het punt dat in de wand zit).
/// </summary>
public class WallAttached : MonoBehaviour
{
    // wordt ingevuld door ColonWallMotion
    [System.NonSerialized] public int segment = -1;
    [System.NonSerialized] public int vertex = -1;
    [System.NonSerialized] public Vector3 basePosition;
}
