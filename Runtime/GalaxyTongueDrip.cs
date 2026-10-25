using UnityEngine;

namespace GalaxyToon
{
    /// <summary>舌を出した瞬間(シェイプキー TongueOut が半分を超えたとき)に1回、舌先に液体が溜まってふくらみ、粘りながらゆっくり垂れ落ちる(GalaxyTears を舌先で使う)。</summary>
    [AddComponentMenu("GalaxyToon/Galaxy Tongue Drip")]
    [RequireComponent(typeof(GalaxyTears))]
    public sealed class GalaxyTongueDrip : MonoBehaviour
    {
        [Tooltip("舌を出すシェイプキーの名前。")] public string tongueShape = "TongueOut";
        [Tooltip("この値(0〜100)を超えたら垂れ始める。")] public float threshold = 50f;
        [Tooltip("舌を出したままで、まだ液体が舌先に残っているなら、この秒数ごとにもう一度垂らす(0=最初の1回だけ)。")] public float repeatInterval = 4f;

        private GalaxyTears tears;
        private int shapeIndex = -1;

        private void Awake()
        {
            tears = GetComponent<GalaxyTears>();
            if (tears.face != null && tears.face.sharedMesh != null) shapeIndex = tears.face.sharedMesh.GetBlendShapeIndex(tongueShape);
        }

        private bool wasOut;
        private float nextTime; // 次に垂らす時刻

        private void Update()
        {
            if (shapeIndex < 0) return;
            bool isOut = tears.face.GetBlendShapeWeight(shapeIndex) > threshold;
            // 舌を出した瞬間に1回。その後は、舌を出したままで液体がまだ舌先に残っていれば、間隔ごとにもう一度
            if (isOut && (!wasOut || (repeatInterval > 0f && Time.time >= nextTime && tears.HasLiquid)))
            {
                tears.Cry();
                nextTime = Time.time + repeatInterval;
            }
            wasOut = isOut;
        }
    }
}
