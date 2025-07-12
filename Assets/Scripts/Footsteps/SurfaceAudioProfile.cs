using UnityEngine;
using UnityEngine.Audio;

//public enum SurfaceType
//{
//    Default,
//    Wood,
//    Metal,
//    Grass,
//    Concrete
//}

[RequireComponent(typeof(Collider))]
public class SurfaceAudioProfile : MonoBehaviour
{
    //[Header("Surface Settings")]
    //public SurfaceType surfaceType = SurfaceType.Default;
    [Header("Audio")]
    public AudioResource RandomContainer;
}
