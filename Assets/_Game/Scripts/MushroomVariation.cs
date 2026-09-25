using System.Collections.Generic;
using UnityEngine;

public class MushroomVariation : MonoBehaviour
{
    [SerializeField] List<Material> materials;
    [SerializeField] List<MeshRenderer> meshRenderers;

    void OnEnable()
    {
        AssignRandomMaterial();
        SetRotation();
    }

    void AssignRandomMaterial()
    {
        int randomIndex = Random.Range(0, materials.Count);

        foreach(MeshRenderer renderer in meshRenderers)
        {
            renderer.material = materials[randomIndex];
        }
    }

    void SetRotation()
    {
        int randomYAxisValue = Random.Range(0, 360);
        this.transform.eulerAngles = new Vector3(0, randomYAxisValue, 0);
    }
}