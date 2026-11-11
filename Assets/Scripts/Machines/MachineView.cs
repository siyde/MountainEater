using UnityEngine;

namespace MountainEater.Machines
{
    /// <summary>
    /// GEÇİCİ GÖRÜNÜM: Mini Drill'in ilkel şekillerden kurulmuş modeli (Faz 11'de gerçek model gelecek).
    /// Taban + gövde + dönen matkap ucu. Collider yok: tıklamaları ve kazıyı engellemez.
    /// Mantık içermez; MachineController her karede konum ve durumunu ayarlar.
    /// </summary>
    public sealed class MachineView : MonoBehaviour
    {
        private const float SpinSpeed = 900f; // derece/sn

        private Transform bit;
        private Renderer bodyRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color bodyColor;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int MachineId { get; set; }

        /// <summary>Modeli oluşturur. scale ≈ voxel boyutu.</summary>
        public static MachineView Create(string name, Transform parent, Material bodyMaterial, Material metalMaterial, float scale)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<MachineView>();

            // Yerel uzay: +Y yüzeyden dışarı, matkap ucu -Y yönünde kayaya bakar.
            CreatePart(PrimitiveType.Cylinder, root.transform, metalMaterial,
                new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.1f, 0.9f) * scale);
            GameObject body = CreatePart(PrimitiveType.Cube, root.transform, bodyMaterial,
                new Vector3(0f, 0.55f * scale, 0f), new Vector3(0.6f, 0.7f, 0.6f) * scale);
            CreatePart(PrimitiveType.Cylinder, root.transform, metalMaterial,
                new Vector3(0f, 1.05f * scale, 0f), new Vector3(0.25f, 0.15f, 0.25f) * scale);

            var bitPivot = new GameObject("BitPivot").transform;
            bitPivot.SetParent(root.transform, false);
            bitPivot.localPosition = Vector3.zero;
            CreatePart(PrimitiveType.Cylinder, bitPivot, metalMaterial,
                new Vector3(0f, -0.35f * scale, 0f), new Vector3(0.18f, 0.4f, 0.18f) * scale);
            CreatePart(PrimitiveType.Cube, bitPivot, metalMaterial,
                new Vector3(0f, -0.35f * scale, 0f), new Vector3(0.32f, 0.12f, 0.08f) * scale);

            view.bit = bitPivot;
            view.bodyRenderer = body.GetComponent<Renderer>();
            view.propertyBlock = new MaterialPropertyBlock();
            view.SetBodyColor(bodyMaterial.HasProperty(BaseColorId) ? bodyMaterial.GetColor(BaseColorId) : Color.yellow);
            return view;
        }

        public void SetPose(Vector3 position, Vector3 up)
        {
            Vector3 normal = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
            Vector3 forward = Vector3.Cross(normal, Vector3.right);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.Cross(normal, Vector3.forward);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, normal));
        }

        public void Spin(float deltaTime)
        {
            if (bit != null) bit.Rotate(Vector3.up, SpinSpeed * deltaTime, Space.Self);
        }

        public void SetBodyColor(Color color)
        {
            if (bodyRenderer == null || color == bodyColor) return;
            bodyColor = color;
            propertyBlock.SetColor(BaseColorId, color);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }

        private static GameObject CreatePart(PrimitiveType type, Transform parent, Material material, Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return part;
        }
    }
}
