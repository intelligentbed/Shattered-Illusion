using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace ShatteredIllusion.Common.Utilities
{
    // Finding things to aim at. All distances are pixels.
    public static class Targeting
    {
        // Closest living player. range <= 0 means unlimited. requireLoS skips players behind tiles.
        public static Player ClosestPlayer(Vector2 from, float range = 0f, bool requireLoS = false)
        {
            Player best = null;
            float bestD = range > 0f ? range * range : float.MaxValue;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead || p.ghost)
                    continue;

                float d = Vector2.DistanceSquared(from, p.Center);
                if (d >= bestD)
                    continue;
                if (requireLoS && !WorldUtils.HasLineOfSight(from, p.Center))
                    continue;

                best = p;
                bestD = d;
            }
            return best;
        }

        // Every living player within range (allocates a list; fine for once-per-attack calls, not per-frame spam).
        public static System.Collections.Generic.List<Player> PlayersInRange(Vector2 from, float range)
        {
            var list = new System.Collections.Generic.List<Player>(4);
            float r2 = range * range;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead && !p.ghost && Vector2.DistanceSquared(from, p.Center) <= r2)
                    list.Add(p);
            }
            return list;
        }

        // Closest enemy NPC for friendly projectiles/minions. Skips friendlies, critters, dummies and immortals.
        public static NPC ClosestEnemy(Vector2 from, float range, bool requireLoS = false, Func<NPC, bool> filter = null)
        {
            NPC best = null;
            float bestD = range * range;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.CanBeChasedBy())
                    continue;

                float d = Vector2.DistanceSquared(from, n.Center);
                if (d >= bestD)
                    continue;
                if (filter != null && !filter(n))
                    continue;
                if (requireLoS && !WorldUtils.HasLineOfSight(from, n.Center))
                    continue;

                best = n;
                bestD = d;
            }
            return best;
        }

        // Prefers the NPC the owner right-click-targeted (minion target), else the closest enemy.
        public static NPC OwnerTargetOrClosest(Player owner, Vector2 from, float range, bool requireLoS = false)
        {
            if (owner.HasMinionAttackTargetNPC)
            {
                NPC n = Main.npc[owner.MinionAttackTargetNPC];
                if (n.CanBeChasedBy() && Vector2.Distance(from, n.Center) <= range * 1.5f)
                    return n;
            }
            return ClosestEnemy(from, range, requireLoS);
        }

        // Keep an NPC's own target fresh, only retargeting when the current one is invalid. Returns the target or null.
        public static Player EnsureTarget(NPC npc, bool faceTarget = false)
        {
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
                npc.TargetClosest(faceTarget);

            Player p = Main.player[npc.target];
            return p.active && !p.dead ? p : null;
        }

        // Steer a projectile toward a target. strength is radians per tick (0.05 lazy, 0.15 aggressive). Keeps speed.
        public static void Home(Projectile proj, Vector2 targetCenter, float strength)
        {
            proj.velocity = MathX.TurnVelocity(proj.velocity, proj.Center.DirTo(targetCenter), strength);
        }

        // Steering that also eases speed toward `speed` (use when a projectile spawns slow and should accelerate into a chase).
        public static void Home(Projectile proj, Vector2 targetCenter, float strength, float speed, float accel = 0.4f)
        {
            float current = proj.velocity.Length();
            float next = Ease.Approach(current, speed, accel);
            Vector2 dir = proj.velocity.SafeNormalize(proj.Center.DirTo(targetCenter));
            proj.velocity = MathX.TurnVelocity(dir * next, proj.Center.DirTo(targetCenter), strength);
        }
    }

    // Multiplayer-safe spawn/sync wrappers. The rule of thumb in this mod: the SERVER (or single player) owns NPCs
    // and hostile projectiles; the OWNING CLIENT owns friendly projectiles.
    public static class Net
    {
        public static bool IsClient => Main.netMode == NetmodeID.MultiplayerClient;
        public static bool IsServer => Main.netMode == NetmodeID.Server;
        public static bool IsSingle => Main.netMode == NetmodeID.SinglePlayer;

        // True on the machine that should run authoritative logic for an NPC/hostile projectile.
        public static bool IsAuthority => Main.netMode != NetmodeID.MultiplayerClient;

        // True on the machine that owns this projectile (its player) or the server for ownerless/hostile ones.
        public static bool IsOwner(Projectile p) => p.owner == Main.myPlayer || (p.owner == 255 && IsAuthority);

        public static void SyncNPC(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        // Authority-only hostile projectile. Returns -1 on clients so callers can ignore the result.
        public static int HostileProjectile(IEntitySource source, Vector2 pos, Vector2 vel, int type, int damage,
            float knockback = 0f, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f)
        {
            if (!IsAuthority)
                return -1;
            return Projectile.NewProjectile(source, pos, vel, type, damage, knockback, Main.myPlayer, ai0, ai1, ai2);
        }

        // Authority-only NPC spawn. Returns Main.maxNPCs when nothing spawned (matches NPC.NewNPC's failure value).
        public static int SpawnNPC(IEntitySource source, Vector2 pos, int type, int start = 0, float ai0 = 0f, float ai1 = 0f)
        {
            if (!IsAuthority)
                return Main.maxNPCs;
            return NPC.NewNPC(source, (int)pos.X, (int)pos.Y, type, start, ai0, ai1);
        }

        // Fire a callback for each fan of projectiles: evenly spread `count` shots across `arc` around `dir`.
        public static void Fan(Vector2 origin, Vector2 dir, int count, float arc, float speed, Action<Vector2, Vector2> spawn)
        {
            if (count <= 0)
                return;
            float baseAngle = dir.ToRotation();
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float a = baseAngle + MathHelper.Lerp(-arc * 0.5f, arc * 0.5f, t);
                spawn(origin, a.ToRotationVector2() * speed);
            }
        }

        // Evenly spaced ring of `count` shots with an optional rotation offset.
        public static void Ring(Vector2 origin, int count, float speed, Action<Vector2, Vector2> spawn, float offset = 0f)
        {
            for (int i = 0; i < count; i++)
                spawn(origin, (offset + MathHelper.TwoPi * i / count).ToRotationVector2() * speed);
        }
    }
}
