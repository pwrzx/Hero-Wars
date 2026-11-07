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
        public StageDefinition(string name, long power, params TowerDefinition[] sequence)
        { title = name; startingPower = power; towers = sequence; }

        // Check every reachable partial clear, including power gained in other rooms.
        // This will also be the acceptance gate for the future on-device generator.
        public void Validate()
        {
            if (startingPower <= 0 || towers == null || towers.Length == 0)
                throw new ArgumentException("A stage needs positive hero power and at least one tower.");
            long entryPower = startingPower;
            var unique = new HashSet<long> { startingPower };
            foreach (TowerDefinition tower in towers)
            {
                if (tower?.enemies == null || tower.enemies.Length == 0 || tower.enemies.Length > 8)
                    throw new ArgumentException("Each tower needs between one and eight rooms.");
                foreach (long enemy in tower.enemies)
                    if (enemy <= 0 || !unique.Add(enemy))
                        throw new ArgumentException("Enemy powers must be positive and distinct within a stage.");
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
                        if (current == tower.enemies[i])
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
    public sealed class RunSave
    {
        public int version = 1;
        public int stageIndex;
        public int[] clearedMasks;
        public bool failed;
    }

    // Unity-independent game rules: presentation never calculates or awards power.
    public sealed class StageRun
    {
        public StageDefinition Definition { get; }
        public long Power { get; private set; }
        public int TowerIndex { get; private set; }
        public bool Failed { get; private set; }
        public bool Complete => TowerIndex == Definition.towers.Length;
        private readonly int[] clearedMasks;

        public StageRun(StageDefinition definition)
        {
            definition.Validate();
            Definition = definition;
            Power = definition.startingPower;
            clearedMasks = new int[definition.towers.Length];
        }

        public bool IsDefeated(int tower, int room) => (clearedMasks[tower] & (1 << room)) != 0;

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
        { stageIndex = stageIndex, clearedMasks = (int[])clearedMasks.Clone(), failed = Failed };

        public static bool TryRestore(StageDefinition definition, RunSave save, out StageRun run)
        {
            run = new StageRun(definition);
            if (save == null || save.version != 1 || save.clearedMasks == null ||
                save.clearedMasks.Length != definition.towers.Length) return false;
            // Replay legal defeats, rather than trusting a saved power value or tower index.
            for (int t = 0; t < definition.towers.Length; t++)
            {
                int mask = save.clearedMasks[t];
                int validBits = (1 << definition.towers[t].enemies.Length) - 1;
                if (mask < 0 || (mask & ~validBits) != 0) return false;
                int remaining = mask;
                while (remaining != 0)
                {
                    bool advanced = false;
                    for (int r = 0; r < definition.towers[t].enemies.Length; r++)
                    {
                        if ((remaining & (1 << r)) == 0 || t != run.TowerIndex ||
                            run.Power <= definition.towers[t].enemies[r]) continue;
                        run.Attack(t, r);
                        remaining &= ~(1 << r);
                        advanced = true;
                    }
                    if (!advanced) return false;
                }
            }
            if (save.failed)
            {
                if (run.Complete) return false;
                bool canFail = false;
                for (int r = 0; r < definition.towers[run.TowerIndex].enemies.Length; r++)
                    canFail |= !run.IsDefeated(run.TowerIndex, r) &&
                               definition.towers[run.TowerIndex].enemies[r] > run.Power;
                if (!canFail) return false;
                run.Failed = true;
            }
            return true;
        }
    }
}
