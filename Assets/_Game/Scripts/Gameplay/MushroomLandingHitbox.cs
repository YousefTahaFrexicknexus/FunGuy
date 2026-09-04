using UnityEngine;

public class MushroomLandingHitbox : MonoBehaviour
{
    public LandingQuality landingQuality;
    public bool IsActive = false;

    void OnEnable()
    {
        IsActive = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            IsActive = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            IsActive = false;
        }
    }
}
