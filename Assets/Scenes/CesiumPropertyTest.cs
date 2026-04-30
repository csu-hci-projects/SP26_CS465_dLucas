using CesiumForUnity;
using UnityEngine;

[RequireComponent(typeof(Cesium3DTileset))]
public class TileQualityController : MonoBehaviour
{
    [SerializeField] private float activeSse = 4f;       // high quality in view
    [SerializeField] private float culledSse = 4096f;    // aggressively coarse out-of-view

    private Cesium3DTileset _tileset;

    private void Awake()
    {
        _tileset = GetComponent<Cesium3DTileset>();
        ApplyLodPolicy();
    }

    private void ApplyLodPolicy()
    {
        _tileset.maximumScreenSpaceError = activeSse;
        _tileset.enforceCulledScreenSpaceError = true;
        _tileset.culledScreenSpaceError = culledSse;
    }
}