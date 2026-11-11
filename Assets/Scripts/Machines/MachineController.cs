using System.Collections.Generic;
using MountainEater.Mining;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Machines
{
    /// <summary>
    /// Makine sisteminin sahnedeki tarafı:
    /// - Her karede MachineManager.Tick(dt) (bütün makineler tek döngüde).
    /// - Her makine için bir MachineView (görünüm) oluşturur ve günceller: matkap, hedefine doğru kayar ve
    ///   çalışırken ucu döner; durduğunda gövde kırmızıya döner.
    /// - Vuruşlarda hafif efekt (MiningEffects, kare başına bütçeli).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MachineController : MonoBehaviour
    {
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material metalMaterial;

        [Header("Görünüm")]
        [SerializeField] private Color workingColor = new Color(1.00f, 0.78f, 0.15f);
        [SerializeField] private Color stoppedColor = new Color(0.85f, 0.20f, 0.18f);
        [SerializeField] private Color selectedColor = new Color(1.00f, 0.95f, 0.65f);
        [SerializeField, Min(0.1f)] private float followSharpness = 6f;

        [Tooltip("Bir karede en fazla kaç makine vuruş efekti gösterilsin (yüzlerce makinede performans).")]
        [SerializeField, Min(0)] private int maxEffectsPerFrame = 6;

        private readonly Dictionary<int, MachineView> views = new Dictionary<int, MachineView>();
        private MachineManager machines;
        private ChunkManager chunkManager;
        private MiningEffects effects;
        private float voxelSize = 1f;
        private int effectsThisFrame;

        public MachineManager Machines => machines;

        /// <summary>Seçili makine (Machines paneli); -1 = yok.</summary>
        public int SelectedMachineId { get; set; } = -1;

        public Material BodyMaterial => bodyMaterial;
        public Material MetalMaterial => metalMaterial;

        public void Initialize(MachineManager machineManager, ChunkManager chunks, MiningEffects miningEffects)
        {
            machines = machineManager;
            chunkManager = chunks;
            effects = miningEffects;
            voxelSize = chunks.VoxelSize;
            EnsureMaterials();

            machines.MachinePlaced += OnMachinePlaced;
            machines.MachineMoved += OnMachineMoved;
            machines.MachineHit += OnMachineHit;

            foreach (Machine machine in machines.Machines) OnMachinePlaced(machine);
        }

        /// <summary>Yerleştirme önizlemesi için aynı modeli üretir.</summary>
        public MachineView CreateView(string viewName)
        {
            EnsureMaterials();
            return MachineView.Create(viewName, transform, bodyMaterial, metalMaterial, voxelSize);
        }

        /// <summary>Makinenin şu anki dünya konumu (kamera odaklama, HUD yazıları).</summary>
        public Vector3 GetMachineWorldPosition(Machine machine)
        {
            return views.TryGetValue(machine.Id, out MachineView view)
                ? view.transform.position
                : GetRestPosition(machine, machine.Anchor);
        }

        private void Update()
        {
            if (machines == null) return;

            effectsThisFrame = 0;
            float dt = Time.deltaTime;
            machines.Tick(dt);

            float follow = 1f - Mathf.Exp(-followSharpness * dt);
            foreach (Machine machine in machines.Machines)
            {
                if (!views.TryGetValue(machine.Id, out MachineView view)) continue;

                Vector3Int focusVoxel = machine.HasTarget ? machine.Target : machine.Anchor;
                Vector3 targetPosition = GetRestPosition(machine, focusVoxel);
                view.SetPose(Vector3.Lerp(view.transform.position, targetPosition, follow), machine.Normal);

                bool working = machine.State == MachineState.Working;
                if (working) view.Spin(dt);
                view.SetBodyColor(!working ? stoppedColor : machine.Id == SelectedMachineId ? selectedColor : workingColor);
            }
        }

        /// <summary>Matkap tabanı hedef voxelin yüzeyine oturur (normal yönünde yarım voxel dışarıda).</summary>
        private Vector3 GetRestPosition(Machine machine, Vector3Int voxel)
        {
            return chunkManager.VoxelCenterToWorld(voxel) + machine.Normal * (voxelSize * 0.5f);
        }

        private void OnMachinePlaced(Machine machine)
        {
            if (views.ContainsKey(machine.Id)) return;
            MachineView view = CreateView($"Machine #{machine.Id} ({machine.Type})");
            view.MachineId = machine.Id;
            view.SetPose(GetRestPosition(machine, machine.Anchor), machine.Normal);
            views[machine.Id] = view;
        }

        private void OnMachineMoved(Machine machine)
        {
            if (views.TryGetValue(machine.Id, out MachineView view))
            {
                view.SetPose(GetRestPosition(machine, machine.Anchor), machine.Normal); // yeni yere ışınla
            }
        }

        private void OnMachineHit(Machine machine, MiningResult result, Vector3Int target)
        {
            if (effects == null || effectsThisFrame >= maxEffectsPerFrame || !result.HitAnything) return;
            effectsThisFrame++;

            Vector3 point = chunkManager.VoxelCenterToWorld(target) + machine.Normal * (voxelSize * 0.5f);
            effects.PlayMachineHit(point, machine.Normal, result);
        }

        private void EnsureMaterials()
        {
            if (bodyMaterial == null) bodyMaterial = CreateLitMaterial("MachineBody", new Color(1f, 0.78f, 0.15f), 0.3f);
            if (metalMaterial == null) metalMaterial = CreateLitMaterial("MachineMetal", new Color(0.32f, 0.34f, 0.37f), 0.6f);
        }

        private static Material CreateLitMaterial(string materialName, Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private void OnDestroy()
        {
            if (machines == null) return;
            machines.MachinePlaced -= OnMachinePlaced;
            machines.MachineMoved -= OnMachineMoved;
            machines.MachineHit -= OnMachineHit;
        }
    }
}
