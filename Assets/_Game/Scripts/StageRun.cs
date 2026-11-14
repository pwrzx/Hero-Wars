using System;
using System.Collections.Generic;

namespace TowerTrials
{
    [Serializable]
    public sealed class TowerDefinition
    {
        public long[] enemies;
        public TowerDefinition(params long[] powers) { enemies = powers; }
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public string title;
        public long startingPower;
        public TowerDefinition[] towers;
        public bool allowPowerWalls;
        public StageDefinition(string name, long power, params TowerDefinition[] sequence)
        { title = name; startingPower = power; towers = sequence; }

        // Taking the weakest remaining enemy first minimizes the starting power needed.
        public long MinimumStartingPower
        {
            get
            {
                long gained = 0, required = 1;
                foreach (TowerDefinition tower in towers)
                {
                    var sorted = (long[])tower.enemies.Clone();
                    Array.Sort(sorted);
                    foreach (long enemy in sorted)
                    {
                        required = Math.Max(required, checked(enemy - gained + 1));
                        gained = checked(gained + enemy);
                    }
                }
                return required;
            }
        }

        public string Signature
        {
            get
            {
                var text = new System.Text.StringBuilder(startingPower.ToString(System.Globalization.CultureInfo.InvariantCulture));
                text.Append(allowPowerWalls ? ":walls" : ":puzzle");
                foreach (TowerDefinition tower in towers)
                {
                    text.Append('|');
                    foreach (long enemy in tower.enemies) text.Append(enemy.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                }
                return text.ToString();
            }
        }

        // Check every reachable partial clear, including power gained in other rooms.
        // This will also be the acceptance gate for the future on-device generator.
        public void Validate()
        {
            if (startingPower <= 0 || towers == null || towers.Length == 0)
                throw new ArgumentException("A stage needs positive hero power and at least one tower.");
            var unique = new HashSet<long>();
            if (!allowPowerWalls) unique.Add(startingPower);
            foreach (TowerDefinition tower in towers)
            {
                if (tower?.enemies == null || tower.enemies.Length == 0 || tower.enemies.Length > 8)
                    throw new ArgumentException("Each tower needs between one and eight rooms.");
                foreach (long enemy in tower.enemies)
                    if (enemy <= 0 || !unique.Add(enemy))
                        throw new ArgumentException("Enemy powers must be positive and distinct within a stage.");
            }
            long entryPower = allowPowerWalls ? Math.Max(startingPower, MinimumStartingPower) : startingPower;
            foreach (TowerDefinition tower in towers)
            {
                int fullMask = (1 << tower.enemies.Length) - 1;
                var visited = new HashSet<int>();
                var pending = new Stack<int>();
                pending.Push(0);
                while (pending.Count > 0)
                {
                    int mask = pending.Pop();
                    if (!visited.Add(mask)) continue;
                    long current = entryPower;
                    for (int i = 0; i < tower.enemies.Length; i++)
                        if ((mask & (1 << i)) != 0) current = checked(current + tower.enemies[i]);
                    for (int i = 0; i < tower.enemies.Length; i++)
                    {
                        if ((mask & (1 << i)) != 0) continue;
                        if (!allowPowerWalls && current == tower.enemies[i])
                            throw new ArgumentException("Equal-power encounter after power gain.");
                        if (current > tower.enemies[i]) pending.Push(mask | (1 << i));
                    }
                }
                if (!visited.Contains(fullMask)) throw new ArgumentException("Tower has no winning route.");
                foreach (long power in tower.enemies) entryPower = checked(entryPower + power);
            }
        }
    }

    public enum AttackOutcome { Invalid, Defeated, TowerCleared, StageCleared, Lost }

    [Serializable]
    public sealed class RewardedReviveSave
    {
        public int[] clearedMasks;
    }

    [Serializable]
    public sealed class RunSave
    {
        public const int CurrentVersion = 4;
        public int version = CurrentVersion;
        public int stageIndex;
        public int[] clearedMasks;
        public int[] roomOrder;
        public bool failed;
        public long startingPower;
        public string definitionSignature, runId;
        public RewardedReviveSave[] rewardedRevives;
        public static bool SupportsVersion(int value) => value >= 1 && value <= CurrentVersion;
    }

    // Unity-independent game rules: presentation never calculates or awards power.
    public sealed class StageRun
    {
        public StageDefinition Definition { get; }
        public long Power { get; private set; }
        public long StartingPower { get; }
        public string RunId { get; private set; }
        public int TowerIndex { get; private set; }
        public bool Failed { get; private set; }
        public bool Complete => TowerIndex == Definition.towers.Length;
        private readonly int[] clearedMasks;
        private readonly int[] roomOrder, towerOffsets;
        private readonly List<RewardedReviveSave> rewardedRevives = new List<RewardedReviveSave>();

        public int DefeatedCount
        {
            get
            {
                int count = 0;
                foreach (int savedMask in clearedMasks)
                    for (int mask = savedMask; mask != 0; mask &= mask - 1) count++;
                return count;
            }
        }

        public bool PowerBlocked
        {
            get
            {
                if (Complete) return false;
                for (int room = 0; room < Definition.towers[TowerIndex].enemies.Length; room++)
                    if (!IsDefeated(TowerIndex, room) && Power > Definition.towers[TowerIndex].enemies[room]) return false;
                return true;
            }
        }

        public StageRun(StageDefinition definition, long? startingPower = null) : this(definition, startingPower, true) { }

        private StageRun(StageDefinition definition, long? startingPower, bool shuffle)
        {
            definition.Validate();
            Definition = definition;
            StartingPower = startingPower ?? definition.startingPower;
            if (StartingPower <= 0) throw new ArgumentException("Hero power must be positive.");
            long maximum = StartingPower;
            foreach (TowerDefinition tower in definition.towers)
                foreach (long enemy in tower.enemies) maximum = checked(maximum + enemy);
            Power = StartingPower;
            var id = Guid.NewGuid();
            RunId = id.ToString("N");
            clearedMasks = new int[definition.towers.Length];
            towerOffsets = new int[definition.towers.Length];
            int roomCount = 0;
            for (int t = 0; t < towerOffsets.Length; t++)
            {
                towerOffsets[t] = roomCount;
                roomCount += definition.towers[t].enemies.Length;
            }
            roomOrder = new int[roomCount];
            var random = shuffle ? new Random(id.GetHashCode()) : null;
            for (int t = 0; t < towerOffsets.Length; t++)
            {
                int offset = towerOffsets[t];
                // Inside-out Fisher–Yates, independently within each tower.
                for (int floor = 0; floor < definition.towers[t].enemies.Length; floor++)
                {
                    int slot = random?.Next(floor + 1) ?? floor;
                    roomOrder[offset + floor] = roomOrder[offset + slot];
                    roomOrder[offset + slot] = floor;
                }
            }
        }

        public int RoomAtFloor(int tower, int originalFloor) => roomOrder[towerOffsets[tower] + originalFloor];

        public bool IsDefeated(int tower, int room) => (clearedMasks[tower] & (1 << room)) != 0;

        // A rewarded continue reverses only the defeat, never an enemy or power transaction.
        public bool TryRevive()
        {
            if (!Failed || Complete) return false;
            Failed = false;
            return true;
        }

        public bool TryReviveWithPowerBoost()
        {
            if (!Failed || Complete) return false;
            long boosted;
            try
            {
                boosted = checked(Power + Power / 5 + (Power % 5 == 0 ? 0 : 1));
                long maximum = boosted;
                for (int t = 0; t < Definition.towers.Length; t++)
                    for (int r = 0; r < Definition.towers[t].enemies.Length; r++)
                        if (!IsDefeated(t, r)) maximum = checked(maximum + Definition.towers[t].enemies[r]);
            }
            catch (OverflowException) { return false; }
            rewardedRevives.Add(new RewardedReviveSave { clearedMasks = (int[])clearedMasks.Clone() });
            Power = boosted;
            Failed = false;
            return true;
        }

        public AttackOutcome Attack(int tower, int room)
        {
            if (Failed || Complete || tower != TowerIndex || room < 0 ||
                room >= Definition.towers[tower].enemies.Length || IsDefeated(tower, room))
                return AttackOutcome.Invalid;
            long enemy = Definition.towers[tower].enemies[room];
            if (Power <= enemy)
            {
                Failed = true;
                return AttackOutcome.Lost;
            }
            Power = checked(Power + enemy);
            clearedMasks[tower] |= 1 << room;
            if (clearedMasks[tower] != (1 << Definition.towers[tower].enemies.Length) - 1)
                return AttackOutcome.Defeated;
            TowerIndex++;
            return Complete ? AttackOutcome.StageCleared : AttackOutcome.TowerCleared;
        }

        public RunSave Save(int stageIndex) => new RunSave
        { stageIndex = stageIndex, clearedMasks = (int[])clearedMasks.Clone(), roomOrder = (int[])roomOrder.Clone(), failed = Failed,
            startingPower = StartingPower, definitionSignature = Definition.Signature, runId = RunId,
            rewardedRevives = rewardedRevives.ConvertAll(value => new RewardedReviveSave { clearedMasks = (int[])value.clearedMasks.Clone() }).ToArray() };

        public static bool TryRestore(StageDefinition definition, RunSave save, out StageRun run)
        {
            run = null;
            if (save == null || !RunSave.SupportsVersion(save.version) || save.clearedMasks == null ||
                save.clearedMasks.Length != definition.towers.Length) return false;
            if (save.version == 1 && definition.allowPowerWalls) return false;
            if (save.version >= 2)
            {
                if (save.startingPower <= 0 || save.definitionSignature != definition.Signature ||
                    string.IsNullOrEmpty(save.runId)) return false;
            }
            try { run = new StageRun(definition, save.version == 1 ? definition.startingPower : save.startingPower, false); }
            catch (OverflowException) { return false; }
            if (save.version >= 2) run.RunId = save.runId;
            if (save.version >= 4 && !run.RestoreRoomOrder(save.roomOrder)) return false;
            if (save.version >= 3)
            {
                if (save.rewardedRevives == null) return false;
                foreach (RewardedReviveSave reward in save.rewardedRevives)
                    if (reward == null || !ReplayToMasks(run, reward.clearedMasks) ||
                        !ReplayDefeat(run) || !run.TryReviveWithPowerBoost()) return false;
            }
            // Replay the exact cleared-room checkpoint before each reward, then remaining kills.
            if (!ReplayToMasks(run, save.clearedMasks)) return false;
            if (save.failed && !ReplayDefeat(run)) return false;
            return true;
        }

        private bool RestoreRoomOrder(int[] saved)
        {
            if (saved == null || saved.Length != roomOrder.Length) return false;
            for (int t = 0; t < towerOffsets.Length; t++)
            {
                int seen = 0, count = Definition.towers[t].enemies.Length;
                for (int floor = 0; floor < count; floor++)
                {
                    int room = saved[towerOffsets[t] + floor];
                    if (room < 0 || room >= count || (seen & (1 << room)) != 0) return false;
                    seen |= 1 << room;
                }
            }
            Array.Copy(saved, roomOrder, saved.Length);
            return true;
        }

        private static bool ReplayToMasks(StageRun run, int[] masks)
        {
            if (masks == null || masks.Length != run.Definition.towers.Length) return false;
            for (int t = 0; t < run.Definition.towers.Length; t++)
            {
                int mask = masks[t];
                int validBits = (1 << run.Definition.towers[t].enemies.Length) - 1;
                if (mask < 0 || (mask & ~validBits) != 0) return false;
                if ((mask & run.clearedMasks[t]) != run.clearedMasks[t]) return false;
                int remaining = mask & ~run.clearedMasks[t];
                while (remaining != 0)
                {
                    bool advanced = false;
                    for (int r = 0; r < run.Definition.towers[t].enemies.Length; r++)
                    {
                        if ((remaining & (1 << r)) == 0 || t != run.TowerIndex ||
                            run.Power <= run.Definition.towers[t].enemies[r]) continue;
                        run.Attack(t, r);
                        remaining &= ~(1 << r);
                        advanced = true;
                    }
                    if (!advanced) return false;
                }
            }
            return true;
        }

        private static bool ReplayDefeat(StageRun run)
        {
            if (run.Complete || run.Failed) return false;
            for (int r = 0; r < run.Definition.towers[run.TowerIndex].enemies.Length; r++)
                if (!run.IsDefeated(run.TowerIndex, r) && run.Definition.towers[run.TowerIndex].enemies[r] >= run.Power)
                    return run.Attack(run.TowerIndex, r) == AttackOutcome.Lost;
            return false;
        }
    }
}
