// Уничтожает граф анимаций KayKit вместе с моделью.
using UnityEngine;
using UnityEngine.Playables;

namespace Hockey
{
    public class KayKitGraphOwner : MonoBehaviour
    {
        public PlayableGraph Graph;
        void OnDestroy() { if (Graph.IsValid()) Graph.Destroy(); }
    }
}
