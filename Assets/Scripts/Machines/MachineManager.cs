using System;
using System.Collections.Generic;
using MountainEater.Economy;
using MountainEater.Mining;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Machines
{
    /// <summary>
    /// Bütün makineleri yöneten merkezi sistem (saf C#, testli). Makine başına fizik veya MonoBehaviour yok:
    /// Tick(dt) tek döngüde her makinenin zamanlayıcısını ilerletir ve vuruşları MiningSystem'e gönderir.
    /// Oyuncu ile makineler aynı MiningSystem'i kullanır → aynı kurallar, çift ödül imkânsız.
    ///
    /// Hedef seçimi: makinenin yerleştirildiği noktadan yakından uzağa (sıralı küre ofsetleri), açıkta kalan
    /// (en az bir yüzü havaya bakan), kazılabilir ve makinenin bölgesindeki ilk voxel. Böylece makine
    /// yüzeyden içeri doğru bir krater açar. Hedef kırılana kadar aynı hedefe vurur (arama tekrarlanmaz).
    /// Çalışma alanında hedef kalmazsa durur (NoTarget) ve MachineStopped yayınlar; taşınınca devam eder.
    /// </summary>
    public sealed class MachineManager
    {
        /// <summary>Bir karede makine başına en fazla vuruş (uzun takılmalarda oyunun donmasını önler).</summary>
        public const int MaxHitsPerTick = 8;

        public const float MinInterval = 0.05f;

        private readonly MiningSystem mining;
        private readonly Dictionary<MachineType, MachineSpec> specs = new Dictionary<MachineType, MachineSpec>();
        private readonly List<Machine> machines = new List<Machine>();
        private int nextId = 1;

        /// <summary>Ortak matkap hasarı (Drill Damage yükseltmesi).</summary>
        public int BaseDamage { get; private set; } = 3;

        /// <summary>Ortak vuruş aralığı, saniye (Drill Speed yükseltmesi).</summary>
        public float BaseInterval { get; private set; } = 3f;

        public IReadOnlyList<Machine> Machines => machines;

        public event Action<Machine> MachinePlaced;
        public event Action<Machine> MachineMoved;
        public event Action<Machine> MachineStopped;

        /// <summary>Her vuruş: makine, sonuç, vurulan voxel.</summary>
        public event Action<Machine, MiningResult, Vector3Int> MachineHit;

        public MachineManager(MiningSystem mining, IEnumerable<MachineSpec> machineSpecs)
        {
            this.mining = mining ?? throw new ArgumentNullException(nameof(mining));
            if (machineSpecs == null) return;
            foreach (MachineSpec spec in machineSpecs) specs[spec.Type] = spec;
        }

        public VoxelWorld World => mining.World;

        public bool HasSpec(MachineType type) => specs.ContainsKey(type);
        public MachineSpec GetSpec(MachineType type) => specs[type];
        public IEnumerable<MachineSpec> Specs => specs.Values;

        public void SetDrillStats(int damage, float intervalSeconds)
        {
            BaseDamage = Math.Max(1, damage);
            BaseInterval = Math.Max(MinInterval, intervalSeconds);
        }

        public int GetDamage(MachineType type) =>
            Math.Max(1, Mathf.RoundToInt(BaseDamage * specs[type].DamageMultiplier));

        public float GetInterval(MachineType type) =>
            Math.Max(MinInterval, BaseInterval * specs[type].IntervalMultiplier);

        public int CountOfType(MachineType type)
        {
            int count = 0;
            foreach (Machine machine in machines) if (machine.Type == type) count++;
            return count;
        }

        public int CountInRegion(byte region)
        {
            int count = 0;
            foreach (Machine machine in machines) if (machine.Region == region) count++;
            return count;
        }

        public Machine Find(int id)
        {
            foreach (Machine machine in machines) if (machine.Id == id) return machine;
            return null;
        }

        /// <summary>Bir sonraki makinenin fiyatı: temel fiyat × büyüme^(sahip olunan sayı), 2 anlamlı basamak.</summary>
        public long GetNextPrice(MachineType type)
        {
            MachineSpec spec = specs[type];
            double price = spec.BasePrice * Math.Pow(spec.PriceGrowth, CountOfType(type));
            if (price >= 9.0e18) return long.MaxValue;
            return (long)Upgrades.UpgradeTable.RoundToSignificant(price, 2);
        }

        // ---------- Yerleştirme ----------

        /// <summary>
        /// Voxel bir makine için uygun mu? Dolu, açıkta (yüzeyde), sahipli ve açık bir bölgede olmalı.
        /// </summary>
        public bool CanPlaceAt(Vector3Int voxel, out string reason)
        {
            VoxelWorld world = mining.World;
            if (!world.IsSolid(voxel.x, voxel.y, voxel.z)) { reason = "Kaya yüzeyi seç"; return false; }

            byte region = world.GetRegion(voxel.x, voxel.y, voxel.z);
            if (region == 0) { reason = "Bu kaya hiçbir bölgeye ait değil"; return false; }
            if (!mining.CanMine(voxel)) { reason = "Bölge kilitli"; return false; }
            if (!IsExposed(voxel)) { reason = "Açıkta bir yüzey seç"; return false; }

            reason = string.Empty;
            return true;
        }

        /// <summary>Para yetiyorsa ve yer uygunsa makineyi satın alıp yerleştirir. Uygun değilse para düşmez.</summary>
        public Machine TryBuyAndPlace(MachineType type, Vector3Int anchor, Vector3 normal, EconomyManager economy,
            out string reason)
        {
            if (!specs.ContainsKey(type)) { reason = "Bilinmeyen makine"; return null; }
            if (!CanPlaceAt(anchor, out reason)) return null;

            long price = GetNextPrice(type);
            if (!economy.TrySpend(price)) { reason = "Yetersiz para"; return null; }

            return Place(type, anchor, normal);
        }

        /// <summary>Ücretsiz yerleştirme (kayıttan yükleme, testler). Yer uygunluğunu çağıran kontrol eder.</summary>
        public Machine Place(MachineType type, Vector3Int anchor, Vector3 normal)
        {
            var machine = new Machine { Id = nextId++, Type = type };
            SetLocation(machine, anchor, normal);
            machines.Add(machine);
            MachinePlaced?.Invoke(machine);
            return machine;
        }

        public bool TryMove(int machineId, Vector3Int anchor, Vector3 normal, out string reason)
        {
            Machine machine = Find(machineId);
            if (machine == null) { reason = "Makine yok"; return false; }
            if (!CanPlaceAt(anchor, out reason)) return false;

            SetLocation(machine, anchor, normal);
            MachineMoved?.Invoke(machine);
            return true;
        }

        private void SetLocation(Machine machine, Vector3Int anchor, Vector3 normal)
        {
            machine.Anchor = anchor;
            machine.Normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            machine.Region = mining.World.GetRegion(anchor.x, anchor.y, anchor.z);
            machine.State = MachineState.Working;
            machine.HasTarget = false;
            machine.Timer = 0f;
        }

        // ---------- Çalışma ----------

        /// <summary>Bütün makineleri dt saniye ilerletir (oyun döngüsü her karede çağırır).</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            for (int i = 0; i < machines.Count; i++)
            {
                Machine machine = machines[i];
                if (machine.State != MachineState.Working) continue;

                float interval = GetInterval(machine.Type);
                machine.Timer += deltaTime;

                int hits = 0;
                while (machine.Timer >= interval && hits < MaxHitsPerTick)
                {
                    machine.Timer -= interval;
                    hits++;
                    if (!HitOnce(machine)) break;
                }

                // Uzun takılmadan sonra birikmiş vuruşları yığma (Faz 10 çevrimdışı simülasyon ayrı çalışacak).
                if (machine.Timer > interval) machine.Timer = interval;
            }
        }

        /// <summary>Bir vuruş yapar. Hedef bulunamazsa makineyi durdurur ve false döner.</summary>
        private bool HitOnce(Machine machine)
        {
            if (!EnsureTarget(machine))
            {
                machine.State = MachineState.NoTarget;
                machine.HasTarget = false;
                machine.Timer = 0f;
                MachineStopped?.Invoke(machine);
                return false;
            }

            Vector3Int target = machine.Target;
            MiningResult result = mining.ApplyHit(target, specs[machine.Type].HitRadius, GetDamage(machine.Type),
                MiningSource.Machine, machine.Region);

            machine.BlocksMined += result.VoxelsBroken;
            machine.MoneyEarned += result.Reward;
            if (!mining.World.IsSolid(target.x, target.y, target.z)) machine.HasTarget = false;

            MachineHit?.Invoke(machine, result, target);
            return true;
        }

        /// <summary>Mevcut hedef hâlâ geçerliyse onu, değilse en yakın uygun voxeli seçer.</summary>
        private bool EnsureTarget(Machine machine)
        {
            if (machine.HasTarget && IsValidTarget(machine, machine.Target)) return true;

            IReadOnlyList<Vector3Int> offsets = SphereOffsets.Get(specs[machine.Type].WorkRadius);
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector3Int candidate = machine.Anchor + offsets[i];
                if (!IsValidTarget(machine, candidate)) continue;

                machine.Target = candidate;
                machine.HasTarget = true;
                return true;
            }

            machine.HasTarget = false;
            return false;
        }

        private bool IsValidTarget(Machine machine, Vector3Int voxel)
        {
            VoxelWorld world = mining.World;
            return world.IsSolid(voxel.x, voxel.y, voxel.z)
                   && world.GetRegion(voxel.x, voxel.y, voxel.z) == machine.Region
                   && mining.CanMine(voxel)
                   && IsExposed(voxel);
        }

        /// <summary>En az bir yüzü havaya bakıyor mu? (Dünyanın altı zemin sayılır, yanlar ve üst hava.)</summary>
        public bool IsExposed(Vector3Int v)
        {
            VoxelWorld world = mining.World;
            return !world.IsSolid(v.x + 1, v.y, v.z) || !world.IsSolid(v.x - 1, v.y, v.z) ||
                   !world.IsSolid(v.x, v.y + 1, v.z) || (v.y > 0 && !world.IsSolid(v.x, v.y - 1, v.z)) ||
                   !world.IsSolid(v.x, v.y, v.z + 1) || !world.IsSolid(v.x, v.y, v.z - 1);
        }
    }
}
