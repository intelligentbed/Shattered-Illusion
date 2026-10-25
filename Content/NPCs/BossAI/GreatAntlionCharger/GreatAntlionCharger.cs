using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Items.TreasureBags;
using ShatteredIllusion.Common.Players.ParrySystem;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.CameraModifiers;
using ShatteredIllusion.Content.Tiles.Trophies;
using ShatteredIllusion.Content.Items.Placeables.Relics;
using ShatteredIllusion.Content.Particles;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using ShatteredIllusion.Core.Packets;
using System.IO;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger
{
    [AutoloadBossHead] //First real attempt at making boss Ai
                       //ID LIKE TO THANK MERLIN FOR HELPING ME OUT ALSO FOR CLEANING MY DIRTY NASTY CODE A LITTLE
    public class GreatAntlionCharger : ModNPC, IParryable
    {
        private const int MainFrameCount = 4;
        private const int BurrowFrameCount = 8;
        private const int PopFrameCount = 6;
        private const int ParryFrameCount = 4;
        private const int SpitFrameCount = 5;

        int burrowFrameHeight;
        int popFrameHeight;
        int parryFrameHeight;
        int spitFrameHeight;

        private const int BurrowDigTime = 90;
        private const int BurrowPursuitEnd = 185;
        private const int BurrowTelegraphEnd = 215;
        private const int BurrowEnd = 250;

        private const int SpitWindupFrameCount = 3;
        private const int SpitFireFrame = 3;
        private const int SpitRecoverFrame = 4;
        private const float SpitFireTiming = 90f; // must match the Timer == 90f 

        const float MouthForwardOffset = 8f;
        const float MouthSidewaysOffset = 6f;
        const float MouthVerticalOffset = 8f;
        private const float SpriteVisualScale = 0.8f;

        private SlotId rumbleSoundSlot;
        private bool launchRoarPlayed;
        private float Phase2DiveLandingX;
        private float Phase2DiveGroundY;
        private float Phase2DiveLaunchX;
        private float Phase2DiveLaunchY;

        private const int Phase2Flag = 1;
        private const int Phase2TransitioningFlag = 2;

        public bool Phase2
        {
            get => ((int)NPC.ai[2] & Phase2Flag) != 0;
            set => SetPhase2Flags(Phase2Flag, value);
        }

        public bool Phase2Transitioning
        {
            get => ((int)NPC.ai[2] & Phase2TransitioningFlag) != 0;
            set => SetPhase2Flags(Phase2TransitioningFlag, value);
        }

        private void SetPhase2Flags(int bit, bool on)
        {
            int flags = (int)NPC.ai[2];
            NPC.ai[2] = on ? (flags | bit) : (flags & ~bit);
        }

        private int DashStuckTimer;
        private Vector2 spitAimDir;
        private float burrowSurfaceY;
        private const int DeathTicks = 170;

        // he digs in for the first bit of the transition then bursts out here
        private float TransitionBoom => Cinema.Enabled ? 45f : 30f;


        private int DespawnTimer;
        private const int DespawnTime = 300;
        private const float DespawnRange = 2100f;

        private const string BurrowTexturePath =
            "ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/GreatAntlionChargerBurrow";

        private const string PopTexturePath =
            "ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/GreatAntlionChargerPop";

        private const string ParryTexturePath =
            "ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/GreatAntlionChargerParry";

        private const string SpitTexturePath =
            "ShatteredIllusion/Content/NPCs/BossAI/GreatAntlionCharger/GreatAntlionChargerSpit";

        private static readonly SoundStyle Roar1 = new SoundStyle("ShatteredIllusion/Sounds/GreatAntlionSounds/GreatAntlionRoar1");
        private static readonly SoundStyle Roar2 = new SoundStyle("ShatteredIllusion/Sounds/GreatAntlionSounds/GreatAntlionRoar2");
        private static readonly SoundStyle Roar3 = new SoundStyle("ShatteredIllusion/Sounds/GreatAntlionSounds/GreatAntlionRoar3");


        public enum AttackPhase
        {
            WaitingForCutscene,
            Launch,
            Dash,
            Phase2BurrowDive,
            Burrow,
            Spit,
            Cooldown,
            Death
        }
        public AttackPhase CurrentState
        {
            get => (AttackPhase)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float Timer => ref NPC.ai[1];

        public ref float AttackSequenceIndex => ref NPC.ai[3];


        public bool IsParryable { get; private set; }

        private bool IsHidden =>
            CurrentState == AttackPhase.WaitingForCutscene ||
            (CurrentState == AttackPhase.Burrow && Timer > 30f && Timer < BurrowPursuitEnd) ||
            (CurrentState == AttackPhase.Phase2BurrowDive && Timer > 30f && Timer <= 90f);

        private Asset<Texture2D>? burrowTexture;
        private Asset<Texture2D>? popTexture;
        private Asset<Texture2D>? parryTexture;
        private Asset<Texture2D>? spitTexture;

        // we are in the attack loop SO ARE YOU
        private static readonly AttackPhase[] Phase1AttackOrder =
        {
        AttackPhase.Launch,
        AttackPhase.Dash,
        AttackPhase.Spit,
        AttackPhase.Burrow,
        AttackPhase.Dash,
        AttackPhase.Burrow,
        AttackPhase.Spit,
    };

        private static readonly AttackPhase[] Phase2AttackOrder =
        {
        AttackPhase.Phase2BurrowDive,
        AttackPhase.Phase2BurrowDive,
        AttackPhase.Burrow,
        AttackPhase.Dash,
        AttackPhase.Spit,
        AttackPhase.Dash,
        };


        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = MainFrameCount;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailCacheLength[Type] = 12;
            NPCID.Sets.TrailingMode[Type] = 0;
            Cinema.Watch(Type);
        }

        public override void SetDefaults()
        {
            NPC.width = 160;
            NPC.height = 70;
            NPC.damage = 40;
            NPC.defense = 10;
            NPC.lifeMax = 3500;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 100f;

            NPC.boss = true;
            NPC.knockBackResist = 0f;

            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;

            if (ModLoader.TryGetMod("ShatteredMusiMod", out Mod musicMod))
            {
                Music = MusicLoader.GetMusicSlot(musicMod, "Music/GreatAntlionCharger");
                SceneEffectPriority = SceneEffectPriority.BossHigh;
            }
        }
        public void FinishIntro()
        {
            if (CurrentState != AttackPhase.WaitingForCutscene)
                return;

            AttackSequenceIndex = 0;
            CurrentState = Phase1AttackOrder[(int)AttackSequenceIndex];
            Timer = 0;

            NPC.alpha = 0;

            NPC.TargetClosest(false);

            Player target = Main.player[NPC.target];

            if (target.active && !target.dead)
            {
                float distance = Vector2.Distance(NPC.Center, target.Center);

                float launchSpeed = MathHelper.Clamp(
                    distance * 0.10f,
                    32f,
                    64f
                );

                float directionX =
                    target.Center.X > NPC.Center.X ? 1f : -1f;

                NPC.velocity.X = directionX * launchSpeed;
                NPC.velocity.Y = 0f;

                NPC.direction = (int)directionX;
                NPC.spriteDirection = NPC.direction;
            }

            NPC.netUpdate = true;

            AntlionFX.Impact(NPC.Bottom, 1.2f);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            if (Main.masterMode)
            {
                NPC.lifeMax = 5915;
                NPC.damage = 100;
            }
            else if (Main.expertMode)
            {
                NPC.lifeMax = 4550;
                NPC.damage = 80;
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {

            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 5, 10));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AntlionTrophyItem>(), 10));
            npcLoot.Add(ItemDropRule.MasterModeDropOnAllPlayers(ModContent.ItemType<AntlionRelic>()));
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<AntlionTreasureBag>()));
        }

        public override void OnKill()
        {
            if (SoundEngine.TryGetActiveSound(rumbleSoundSlot, out ActiveSound? sound))
                sound?.Stop();

            DownedSystem.SetDownedGreatAntlionCharger();

            ScreenShake(10f, 24);
            AntlionFX.Impact(NPC.Center, 3.2f, false);
            AntlionFX.Embers(NPC.Center, 40, 4f, 14f, new Color(255, 170, 90), 14f);
        }

        public override void Load()
        {
            burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
            popTexture = ModContent.Request<Texture2D>(PopTexturePath);
            parryTexture = ModContent.Request<Texture2D>(ParryTexturePath);
            spitTexture = ModContent.Request<Texture2D>(SpitTexturePath);
        }

        private void EnsureFrameHeightsCached()
        {
            if (burrowFrameHeight == 0 && burrowTexture != null && burrowTexture.IsLoaded)
                burrowFrameHeight = burrowTexture.Value.Height / BurrowFrameCount;

            if (popFrameHeight == 0 && popTexture != null && popTexture.IsLoaded)
                popFrameHeight = popTexture.Value.Height / PopFrameCount;

            if (parryFrameHeight == 0 && parryTexture != null && parryTexture.IsLoaded)
                parryFrameHeight = parryTexture.Value.Height / ParryFrameCount;

            if (spitFrameHeight == 0 && spitTexture != null && spitTexture.IsLoaded)
                spitFrameHeight = spitTexture.Value.Height / SpitFrameCount;
        }

        private float GetGroundAlignedOffset(float frameHeightPx)
        {
            float visualHalfHeight = frameHeightPx / 2f * SpriteVisualScale * NPC.scale;
            float hitboxHalfHeight = NPC.height / 2f;
            return hitboxHalfHeight - visualHalfHeight;
        }
        private const float TimerEqualityTolerance = 0.01f;

        private bool TimerAt(float target) => Math.Abs(Timer - target) < TimerEqualityTolerance;


        private Vector2 SafeNormalize(Vector2 vector)
        {
            if (vector == Vector2.Zero)
                return new Vector2(NPC.spriteDirection, 0f);

            vector.Normalize();
            return vector;
        }

        private static bool IsZero(float value) => Math.Abs(value) < 0.0001f;
        private static int DifficultyValue(int normal, int expert, int master)
        {
            if (Main.masterMode) return master;
            if (Main.expertMode) return expert;
            return normal;
        }

        private static float DifficultyValue(float normal, float expert, float master)
        {
            if (Main.masterMode) return master;
            if (Main.expertMode) return expert;
            return normal;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            DrawDiveArc(spriteBatch);
            DrawBurrowFX(spriteBatch);
            DrawTransitionFX(spriteBatch);

            if (CurrentState == AttackPhase.WaitingForCutscene || NPC.alpha >= 255)
            {
                return false;
            }

            if (burrowTexture == null) burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
            if (popTexture == null) popTexture = ModContent.Request<Texture2D>(PopTexturePath);
            if (parryTexture == null) parryTexture = ModContent.Request<Texture2D>(ParryTexturePath);
            if (spitTexture == null) spitTexture = ModContent.Request<Texture2D>(SpitTexturePath);

            EnsureFrameHeightsCached();

            //red = parryable
            Color tintColor = IsParryable ? new Color(255, 120, 120) : Color.White;

            if (CurrentState == AttackPhase.Death)
                tintColor = Color.Lerp(Color.White, new Color(255, 110, 80), 0.5f + 0.5f * (float)Math.Sin(Timer * (0.5f + Timer / 100f)));
            Color finalDrawColor = NPC.GetAlpha(drawColor).MultiplyRGB(tintColor);

            DrawAura(spriteBatch);
            DrawAfterimages(spriteBatch, screenPos);

            bool drewSpecial =
                TryDrawParryTexture(spriteBatch, screenPos, finalDrawColor) ||
                TryDrawPopTexture(spriteBatch, screenPos, finalDrawColor) ||
                TryDrawSpitTexture(spriteBatch, screenPos, finalDrawColor) ||
                TryDrawBurrowTexture(spriteBatch, screenPos, finalDrawColor);

            if (!drewSpecial)
            {
                DrawMainTexture(spriteBatch, screenPos, finalDrawColor);
                DrawEyeGlow(spriteBatch);
            }

            DrawSpitAim(spriteBatch);
            return false;
        }

        private SpriteEffects CurrentSpriteEffects =>
            NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        private bool TryDrawParryTexture(SpriteBatch spriteBatch, Vector2 screenPos, Color finalDrawColor)
        {
            // Triggered if recently parried/stunned via negative timers in Cooldown.
            bool isParryDrawing = (CurrentState == AttackPhase.Cooldown && Timer < 0f) || CurrentState == AttackPhase.Death;
            if (!isParryDrawing || parryTexture == null || !parryTexture.IsLoaded)
            {
                return false;
            }

            Texture2D texture = parryTexture.Value;
            Rectangle sourceRect = new Rectangle(0, NPC.frame.Y, texture.Width, parryFrameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, parryFrameHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY + GetGroundAlignedOffset(parryFrameHeight));

            spriteBatch.Draw(texture, drawPos, sourceRect, finalDrawColor, NPC.rotation, origin, NPC.scale * SpriteVisualScale, CurrentSpriteEffects, 0f);
            return true;
        }

        private bool TryDrawPopTexture(SpriteBatch spriteBatch, Vector2 screenPos, Color finalDrawColor)
        {
            bool isPhase2PopDrawing = CurrentState == AttackPhase.Phase2BurrowDive && Timer >= 91f && Timer < 111f;
            if (!isPhase2PopDrawing || popTexture == null || !popTexture.IsLoaded)
            {
                return false;
            }

            Texture2D texture = popTexture.Value;
            Rectangle sourceRect = new Rectangle(0, NPC.frame.Y, texture.Width, popFrameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, popFrameHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY + GetGroundAlignedOffset(popFrameHeight));

            spriteBatch.Draw(texture, drawPos, sourceRect, finalDrawColor, NPC.rotation, origin, NPC.scale * SpriteVisualScale, CurrentSpriteEffects, 0f);
            return true;
        }

        private bool TryDrawSpitTexture(SpriteBatch spriteBatch, Vector2 screenPos, Color finalDrawColor)
        {
            bool isSpitDrawing = CurrentState == AttackPhase.Spit;
            if (!isSpitDrawing || spitTexture == null || !spitTexture.IsLoaded)
            {
                return false;
            }

            Texture2D texture = spitTexture.Value;
            Rectangle sourceRect = new Rectangle(0, NPC.frame.Y, texture.Width, spitFrameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, spitFrameHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY + GetGroundAlignedOffset(spitFrameHeight));

            spriteBatch.Draw(texture, drawPos, sourceRect, finalDrawColor, NPC.rotation, origin, NPC.scale * SpriteVisualScale, CurrentSpriteEffects, 0f);
            return true;
        }

        private bool TryDrawBurrowTexture(SpriteBatch spriteBatch, Vector2 screenPos, Color finalDrawColor)
        {
            bool isPhase1Burrowing = CurrentState == AttackPhase.Burrow && Timer <= BurrowDigTime;
            bool isPhase2Burrowing = CurrentState == AttackPhase.Phase2BurrowDive && Timer <= 45f;
            bool isTransitionDig = Phase2Transitioning && Timer <= TransitionBoom - 10f;

            if ((!isPhase1Burrowing && !isPhase2Burrowing && !isTransitionDig) || burrowTexture == null || !burrowTexture.IsLoaded)
            {
                return false;
            }

            Texture2D texture = burrowTexture.Value;
            Rectangle sourceRect = new Rectangle(0, NPC.frame.Y, texture.Width, burrowFrameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, burrowFrameHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY + GetGroundAlignedOffset(burrowFrameHeight));

            spriteBatch.Draw(texture, drawPos, sourceRect, finalDrawColor, NPC.rotation, origin, NPC.scale * SpriteVisualScale, CurrentSpriteEffects, 0f);
            return true;
        }

        private void DrawMainTexture(SpriteBatch spriteBatch, Vector2 screenPos, Color finalDrawColor)
        {
            Texture2D mainTexture = TextureAssets.Npc[NPC.type].Value;

            Vector2 mainOrigin = new Vector2(
                mainTexture.Width / 2f,
                (mainTexture.Height / MainFrameCount) / 2f
            );

            Vector2 mainDrawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY + GetGroundAlignedOffset(mainTexture.Height / (float)MainFrameCount));

            spriteBatch.Draw(
                mainTexture,
                mainDrawPos,
                NPC.frame,
                finalDrawColor,
                NPC.rotation,
                mainOrigin,
                NPC.scale * SpriteVisualScale,
                CurrentSpriteEffects,
                0f
            );
        }

        // glowing ridge while he tunnels, cracks + light column when he's about to come up
        private void DrawBurrowFX(SpriteBatch spriteBatch)
        {
            if (!Cinema.Enabled || CurrentState != AttackPhase.Burrow || IsZero(burrowSurfaceY))
                return;

            Vector2 ground = new Vector2(NPC.Center.X, burrowSurfaceY);
            float flicker = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f);

            if (Timer > BurrowDigTime && Timer < BurrowPursuitEnd)
            {
                float speed = MathHelper.Clamp(Math.Abs(NPC.velocity.X) / 16f, 0f, 1f);

                AntlionFX.Glow(spriteBatch, ground, new Color(235, 175, 100) * (0.5f * flicker), new Vector2(260f + 180f * speed, 50f));
                AntlionFX.Glow(spriteBatch, ground + new Vector2(0f, -6f), new Color(255, 220, 160) * 0.35f, new Vector2(110f + 90f * speed, 20f));
            }
            else if (Timer >= BurrowPursuitEnd && Timer < BurrowTelegraphEnd)
            {
                float warn = (Timer - BurrowPursuitEnd) / (BurrowTelegraphEnd - BurrowPursuitEnd);

                for (int i = 0; i < 6; i++)
                {
                    float side = i % 2 == 0 ? 0f : MathHelper.Pi;
                    float angle = side + (i / 2 - 1) * 0.06f * (side == 0f ? 1f : -1f);
                    float length = (90f + 50f * (i / 2)) * warn * 2.2f;

                    AntlionFX.Beam(spriteBatch, ground, angle, length, MathHelper.Lerp(3f, 8f, warn), new Color(255, 120, 60) * (0.6f * warn * flicker));
                }

                AntlionFX.Glow(spriteBatch, ground + new Vector2(0f, -150f * warn), new Color(255, 170, 90) * (0.5f * warn * flicker), new Vector2(70f + 60f * warn, 120f + 380f * warn));
                AntlionFX.Glow(spriteBatch, ground, new Color(255, 90, 50) * (0.8f * warn * flicker), new Vector2(120f + 160f * warn, 30f + 20f * warn));
            }
            else if (Timer >= BurrowTelegraphEnd && Timer < BurrowTelegraphEnd + 25f)
            {
                float fade = 1f - (Timer - BurrowTelegraphEnd) / 25f;

                AntlionFX.Glow(spriteBatch, ground + new Vector2(0f, -330f * fade), new Color(255, 215, 150) * (0.8f * fade), new Vector2(170f, 700f));
                AntlionFX.Glow(spriteBatch, ground, new Color(255, 190, 120) * (0.8f * fade), new Vector2(420f, 70f));
            }
        }

        // cracks spreading along the ground while he charges, light column when it goes off
        private void DrawTransitionFX(SpriteBatch spriteBatch)
        {
            if (!Cinema.Enabled || !Phase2Transitioning)
                return;

            float boom = TransitionBoom;
            Vector2 ground = new Vector2(NPC.Center.X, burrowSurfaceY);
            float flicker = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            float charge = MathHelper.Clamp(Timer / boom, 0f, 1f);
            float fade = Timer < boom ? 1f : 1f - MathHelper.Clamp((Timer - boom) / 40f, 0f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i % 2 == 0 ? 0f : MathHelper.Pi;
                angle += (i / 2 - 1.5f) * 0.05f * (angle == 0f ? 1f : -1f);
                float length = (180f + 70f * (i / 2)) * charge * 2.4f;

                AntlionFX.Beam(spriteBatch, ground, angle, length, MathHelper.Lerp(3f, 10f, charge), new Color(255, 110, 55) * (0.7f * fade * flicker));
            }

            AntlionFX.Glow(spriteBatch, ground, new Color(255, 90, 50) * (0.8f * fade * flicker), new Vector2(160f + 420f * charge, 40f + 30f * charge));

            if (Timer < boom)
                AntlionFX.Glow(spriteBatch, ground + new Vector2(0f, -150f * charge), new Color(255, 170, 90) * (0.5f * charge * flicker), new Vector2(70f + 60f * charge, 120f + 380f * charge));
            else
            {
                AntlionFX.Glow(spriteBatch, ground + new Vector2(0f, -450f * fade), new Color(255, 215, 160) * (0.9f * fade), new Vector2(240f, 900f));
                AntlionFX.Glow(spriteBatch, NPC.Center, new Color(255, 230, 190) * (0.7f * fade), new Vector2(700f, 700f) * (1.4f - 0.4f * fade));
            }
        }

        private void SpawnRubble(int count)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < count; i++)
            {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    new Vector2(NPC.Center.X + Main.rand.NextFloat(-500f, 500f), NPC.Center.Y - 500f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(2f, 5f)),
                    ModContent.ProjectileType<FallingRubble>(),
                    DifficultyValue(15, 30, 15),
                    2f,
                    Main.myPlayer
                );
            }
        }

        private bool IsAirborneDive => CurrentState == AttackPhase.Phase2BurrowDive && Timer >= 111f;

        // red haze behind him, builds during the phase 2 transition and stays low after
        private void DrawAura(SpriteBatch spriteBatch)
        {
            if (!Cinema.Enabled)
                return;

            float strength;

            if (CurrentState == AttackPhase.Death)
                strength = 0.6f + 1.6f * Timer / DeathTicks;
            else if (Phase2Transitioning)
                strength = Timer < 45f ? Timer / 45f * 1.4f : MathHelper.Lerp(1.4f, 0.45f, MathHelper.Clamp((Timer - 45f) / 55f, 0f, 1f));
            else if (Phase2)
                strength = 0.45f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f);
            else
                return;

            AntlionFX.Glow(spriteBatch, NPC.Center, new Color(255, 80, 50) * (0.35f * strength), new Vector2(460f, 220f));
        }

        private void DrawAfterimages(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (!Cinema.Enabled)
                return;

            bool dashing = CurrentState == AttackPhase.Dash && Timer >= 19f;

            if (!dashing && CurrentState != AttackPhase.Launch && !IsAirborneDive)
                return;

            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = new Vector2(texture.Width / 2f, NPC.frame.Height / 2f);
            Vector2 offset = new Vector2(0f, NPC.gfxOffY + GetGroundAlignedOffset(NPC.frame.Height));

            for (int i = 2; i < NPC.oldPos.Length; i += 2)
            {
                if (NPC.oldPos[i] == Vector2.Zero || Vector2.DistanceSquared(NPC.oldPos[i], NPC.position) < 36f)
                    continue;

                float fade = 1f - i / (float)NPC.oldPos.Length;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos + offset;

                spriteBatch.Draw(texture, drawPos, NPC.frame, new Color(255, 170, 90, 0) * (fade * 0.45f), NPC.rotation, origin, NPC.scale * SpriteVisualScale, CurrentSpriteEffects, 0f);
            }
        }

        private void DrawEyeGlow(SpriteBatch spriteBatch)
        {
            if (!Cinema.Enabled)
                return;

            Vector2 eye = NPC.Center + (new Vector2(NPC.spriteDirection * 88f, -2f) * NPC.scale).RotatedBy(NPC.rotation);

            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f);
            float intensity = Phase2 ? 1.25f : 0.8f;
            float flareSize = 0f;

            if (CurrentState == AttackPhase.Dash)
            {
                float windup = Timer <= 18f ? Timer / 18f : 0f;
                intensity += 0.5f + windup;
                flareSize = Timer <= 18f ? 30f + 130f * windup : 55f;
            }
            else if (Phase2Transitioning)
            {
                float charge = Timer < 45f ? Timer / 45f : MathHelper.Clamp(1f - (Timer - 45f) / 40f, 0f, 1f);
                intensity += 1.2f * charge;
                flareSize = 220f * charge;
            }

            AntlionFX.Glow(spriteBatch, eye, new Color(255, 40, 30) * (0.7f * pulse * intensity), new Vector2(54f, 54f));

            if (flareSize > 1f)
                AntlionFX.Flare(spriteBatch, eye, new Color(255, 130, 100) * (0.8f * pulse), flareSize, Main.GlobalTimeWrappedHourly * 0.6f);
        }

        // same numbers as the real jump so what you see is what you get
        private void DrawDiveArc(SpriteBatch spriteBatch)
        {
            if (CurrentState != AttackPhase.Phase2BurrowDive || Timer < 56f || Timer >= 111f || IsZero(Phase2DiveGroundY) || !ModContent.GetInstance<ShatteredIllusionConfig>().SlamTelegraph)
                return;

            const float arcHeight = 500f;
            const int beads = 30;

            float reveal = MathHelper.Clamp((Timer - 56f) / 20f, 0f, 1f);
            float urgency = MathHelper.Clamp((Timer - 56f) / 55f, 0f, 1f);

            Vector2 start = NPC.Center;
            Vector2 end = new Vector2(Phase2DiveLandingX, Phase2DiveGroundY - NPC.height / 2f);

            for (int i = 0; i <= beads; i++)
            {
                float t = i / (float)beads;

                if (t > reveal)
                    break;

                Vector2 pos = new Vector2(
                    MathHelper.Lerp(start.X, end.X, t),
                    MathHelper.Lerp(start.Y, end.Y, t) - arcHeight * 4f * t * (1f - t)
                );

                float wave = (float)Math.Pow(0.5f + 0.5f * Math.Sin(t * 14f - Main.GlobalTimeWrappedHourly * 9f), 3.0);
                float size = 26f + 30f * wave;

                AntlionFX.Glow(spriteBatch, pos, new Color(255, 150, 70) * ((0.35f + 0.5f * wave) * (0.5f + 0.5f * urgency)), new Vector2(size, size));
            }

            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (6f + 10f * urgency));
            Vector2 landing = new Vector2(end.X, Phase2DiveGroundY);

            AntlionFX.Glow(spriteBatch, landing, new Color(255, 70, 40) * (0.9f * pulse * reveal), new Vector2(380f, 70f));
            AntlionFX.Glow(spriteBatch, landing, new Color(255, 200, 140) * (0.7f * pulse * reveal), new Vector2(190f, 34f));
        }

        private void DrawSpitAim(SpriteBatch spriteBatch)
        {
            if (!Cinema.Enabled || CurrentState != AttackPhase.Spit || Timer > SpitFireTiming || spitAimDir == Vector2.Zero)
                return;

            float progress = Timer / SpitFireTiming;
            Vector2 mouth = NPC.Center + new Vector2(NPC.spriteDirection * (NPC.width / 2f + MouthForwardOffset + MouthSidewaysOffset), MouthVerticalOffset);

            int shotCount = DifficultyValue(1, 3, 5);
            float spread = DifficultyValue(0f, 0.18f, 0.22f);
            float width = MathHelper.Lerp(3f, 12f, progress * progress);

            for (int i = 0; i < shotCount; i++)
            {
                Vector2 dir = shotCount > 1 ? spitAimDir.RotatedBy((i - (shotCount - 1) / 2f) * spread) : spitAimDir;
                float rotation = dir.ToRotation();

                AntlionFX.Beam(spriteBatch, mouth, rotation, 350f, width, new Color(255, 150, 80) * MathHelper.Lerp(0.15f, 0.8f, progress));
                AntlionFX.Beam(spriteBatch, mouth, rotation, 350f, width * 0.35f, new Color(255, 225, 180) * (progress * 0.8f));
            }
        }

        // sand blowing across the screen the whole fight
        private void DoAmbientStorm()
        {
            if (Main.dedServ)
                return;

            AntlionFX.Mood(Phase2 ? 1f : 0.55f);

            if (Main.GameUpdateCount % (Phase2 ? 2 : 3) != 0 || Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) > 2200f)
                return;

            Vector2 pos = Main.screenPosition + new Vector2(Main.rand.NextFloat(-80f, Main.screenWidth * 0.6f), Main.rand.NextFloat(0f, Main.screenHeight));
            Vector2 vel = new Vector2(Main.rand.NextFloat(16f, 26f), Main.rand.NextFloat(-1f, 1.5f));
            Color color = new Color(230, 190, 130) * Main.rand.NextFloat(0.25f, 0.5f);

            AntlionFX.Streak(pos, vel, new Vector2(Main.rand.NextFloat(30f, 70f), Main.rand.NextFloat(2f, 4f)), color, 45);
        }

        private void BackStreaks(int count)
        {
            if (Main.dedServ || Math.Abs(NPC.velocity.X) < 2f)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = NPC.Center + new Vector2(-Math.Sign(NPC.velocity.X) * NPC.width * 0.4f, Main.rand.NextFloat(-NPC.height * 0.5f, NPC.height * 0.5f));
                Vector2 vel = new Vector2(-NPC.velocity.X * Main.rand.NextFloat(0.2f, 0.45f), Main.rand.NextFloat(-0.8f, 0.8f));

                AntlionFX.Streak(pos, vel, new Vector2(Main.rand.NextFloat(34f, 70f), Main.rand.NextFloat(3f, 5f)), new Color(255, 205, 140), 18);
            }
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            if (IsHidden || CurrentState == AttackPhase.Death)
            {
                return false;
            }

            return base.CanHitPlayer(target, ref cooldownSlot);
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            if (IsHidden)
            {
                return false;
            }

            return base.CanBeHitByItem(player, item);
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (IsHidden)
            {
                return false;
            }

            return base.CanBeHitByProjectile(projectile);
        }

        public override void FindFrame(int frameHeight)
        {
            EnsureFrameHeightsCached();

            if (TryFindParryFrame()) return;
            if (TryFindPhase2PopFrame()) return;
            if (TryFindBurrowFrame()) return;

            FindDefaultFrame(frameHeight);
        }

        private bool TryFindParryFrame()
        {
            if (CurrentState == AttackPhase.Death)
            {
                if (parryTexture == null) parryTexture = ModContent.Request<Texture2D>(ParryTexturePath);
                if (parryTexture == null || !parryTexture.IsLoaded)
                {
                    return false;
                }

                // collapses over the first bit then stays down
                NPC.frame.Width = parryTexture.Width();
                NPC.frame.Height = parryFrameHeight;
                NPC.frame.X = 0;
                NPC.frame.Y = Math.Min((int)(Timer / 14f), ParryFrameCount - 1) * parryFrameHeight;
                return true;
            }

            if (CurrentState != AttackPhase.Cooldown || Timer >= 0f)
            {
                return false;
            }

            if (parryTexture == null) parryTexture = ModContent.Request<Texture2D>(ParryTexturePath);
            if (parryTexture == null || !parryTexture.IsLoaded)
            {
                return false;
            }

            float maxStun = Main.masterMode ? 5f : 15f;
            float progress = MathHelper.Clamp(Math.Abs(Timer) / maxStun, 0f, 1f);
            int frame = Math.Min((int)(progress * ParryFrameCount), ParryFrameCount - 1);

            NPC.frame.Width = parryTexture.Width();
            NPC.frame.Height = parryFrameHeight;
            NPC.frame.X = 0;
            NPC.frame.Y = frame * parryFrameHeight;
            return true;
        }

        private bool TryFindPhase2PopFrame()
        {
            bool phase2PopAnimation = CurrentState == AttackPhase.Phase2BurrowDive && Timer >= 91f && Timer < 111f;
            if (!phase2PopAnimation)
            {
                return false;
            }

            if (popTexture == null) popTexture = ModContent.Request<Texture2D>(PopTexturePath);
            if (popTexture == null || !popTexture.IsLoaded)
            {
                return false;
            }

            const float animationDuration = 20f;
            float progress = MathHelper.Clamp((Timer - 91f) / animationDuration, 0f, 1f);
            int frame = Math.Min((int)(progress * PopFrameCount), PopFrameCount - 1);

            NPC.frame.Width = popTexture.Width();
            NPC.frame.Height = popFrameHeight;
            NPC.frame.X = 0;
            NPC.frame.Y = frame * popFrameHeight;
            return true;
        }

        private bool TryFindBurrowFrame()
        {
            bool phase2DiveAnimation = CurrentState == AttackPhase.Phase2BurrowDive && Timer <= 45f;
            bool normalBurrowAnimation = CurrentState == AttackPhase.Burrow && Timer <= BurrowDigTime;
            bool transitionDigAnimation = Phase2Transitioning && Timer <= TransitionBoom - 10f;

            if (!phase2DiveAnimation && !normalBurrowAnimation && !transitionDigAnimation)
            {
                return false;
            }

            if (burrowTexture == null) burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
            if (burrowTexture == null || !burrowTexture.IsLoaded)
            {
                return false;
            }

            float animationTime = transitionDigAnimation ? TransitionBoom - 10f : phase2DiveAnimation ? 45f : BurrowDigTime;
            float animationProgress = MathHelper.Clamp(Timer / animationTime, 0f, 1f);
            int frame = Math.Min((int)(animationProgress * BurrowFrameCount), BurrowFrameCount - 1);

            NPC.frame.Width = burrowTexture.Width();
            NPC.frame.Height = burrowFrameHeight;
            NPC.frame.X = 0;
            NPC.frame.Y = frame * burrowFrameHeight;
            return true;
        }

        private void FindDefaultFrame(int frameHeight)
        {
            Texture2D mainTexture = TextureAssets.Npc[NPC.type].Value;
            NPC.frame.Width = mainTexture.Width;
            NPC.frame.Height = frameHeight;

            if (CurrentState == AttackPhase.Spit && TryFindSpitFrame())
            {
                return;
            }

            if (IsZero(NPC.velocity.X) || CurrentState == AttackPhase.WaitingForCutscene)
            {
                NPC.frame.Y = 0;
                return;
            }

            NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.15f;

            if (NPC.frameCounter >= MainFrameCount)
            {
                NPC.frameCounter = 0f;
                NPC.frame.Y += frameHeight;

                if (NPC.frame.Y >= frameHeight * MainFrameCount)
                {
                    NPC.frame.Y = 0;
                }
            }
        }

        private bool TryFindSpitFrame()
        {
            if (spitTexture == null) spitTexture = ModContent.Request<Texture2D>(SpitTexturePath);
            if (spitTexture == null || !spitTexture.IsLoaded)
            {
                return false;
            }

            int spitFrame;

            if (Timer < SpitFireTiming)
            {
                float windupProgress = MathHelper.Clamp(Timer / SpitFireTiming, 0f, 1f);
                spitFrame = Math.Min((int)(windupProgress * SpitWindupFrameCount), SpitWindupFrameCount - 1);
            }
            else if (Timer < SpitFireTiming + 5f)
            {
                spitFrame = SpitFireFrame;
            }
            else
            {
                spitFrame = SpitRecoverFrame;
            }

            NPC.frame.Width = spitTexture.Width();
            NPC.frame.Height = spitFrameHeight;
            NPC.frame.X = 0;
            NPC.frame.Y = spitFrame * spitFrameHeight;
            return true;
        }

        // Scans downwarda and returns the Y of the nearest solid tile.
        private float GetGroundY(Vector2 checkPosition)
        {
            int startTileX = (int)(checkPosition.X / 16f);
            int startTileY = (int)(checkPosition.Y / 16f);

            if (startTileX < 0 || startTileX >= Main.maxTilesX)
                return checkPosition.Y;

            int endTileY = Math.Min(startTileY + 120, Main.maxTilesY);

            for (int y = Math.Max(startTileY, 0); y < endTileY; y++)
            {
                Tile tile = Main.tile[startTileX, y];

                if (tile != null && tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                {
                    return y * 16f;
                }
            }

            return checkPosition.Y;
        }

        // Returns true if there's at least one player who is active, alive, and within DespawnRange of the boss 
        //coming back to this there is a much better waty to handle this
        private bool AnyPlayerPresentNearby()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];

                if (p.active && !p.dead && Vector2.Distance(p.Center, NPC.Center) < DespawnRange)
                {
                    return true;
                }
            }

            return false;
        }

        public override void AI()
        {
            if (CurrentState != AttackPhase.Launch)
                launchRoarPlayed = false;

            // Despawn check
            if (!AnyPlayerPresentNearby())
            {
                DespawnTimer++;

                if (DespawnTimer >= DespawnTime)
                {
                    if (SoundEngine.TryGetActiveSound(rumbleSoundSlot, out ActiveSound? despawnSound))
                        despawnSound?.Stop();

                    NPC.active = false;
                    NPC.netUpdate = true;
                    return;
                }
            }
            else
            {
                DespawnTimer = 0;
            }

            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            NPC.color = Color.White;
            IsParryable = false;
            NPC.rotation = 0f;
            DoAmbientStorm();

            if (CurrentState == AttackPhase.Death)
            {
                HandleDeath();
                return;
            }

            if (NPC.alpha < 255)
            {
                Lighting.AddLight(NPC.Center, 0.85f, 0.65f, 0.35f);
            }

            // Phase 2 begins at 55% HP
            if (!Phase2 && !Phase2Transitioning && NPC.life <= NPC.lifeMax * 0.55f)
            {
                StartPhase2();
            }

            if (Phase2Transitioning)
            {
                HandlePhase2Transition();
                return;
            }

            if (!target.active || target.dead)
            {
                NPC.velocity.Y += 0.2f;
                return;
            }

            // Lets him step up small ledges while charging
            Collision.StepUp(
                ref NPC.position,
                ref NPC.velocity,
                NPC.width,
                NPC.height,
                ref NPC.stepSpeed,
                ref NPC.gfxOffY
            );


            switch (CurrentState)
            {
                case AttackPhase.WaitingForCutscene:
                    HandleWaitingForCutscene();
                    break;

                case AttackPhase.Launch:
                    HandleLaunch();
                    break;

                case AttackPhase.Dash:
                    HandleDash(target);
                    break;

                case AttackPhase.Burrow:
                    HandleBurrow(target);
                    break;

                case AttackPhase.Spit:
                    HandleSpit(target);
                    break;

                case AttackPhase.Phase2BurrowDive:
                    HandlePhase2BurrowDive(target);
                    break;

                case AttackPhase.Cooldown:
                    HandleCooldown();
                    break;

            }
        }

        private void HandleWaitingForCutscene()
        {
            NPC.velocity = Vector2.Zero;
            NPC.alpha = 255;
        }

        private void HandleLaunch()
        {
            NPC.alpha = 0;
            Timer++;
            NPC.spriteDirection = NPC.direction;
            NPC.noTileCollide = false;

            // Hop over small ledges when velocity suddenly zeroes out
            if (IsZero(NPC.velocity.X) && !IsZero(NPC.oldVelocity.X))
            {
                NPC.velocity.Y = -5.5f;
            }

            if (Timer <= 25f && IsZero(NPC.velocity.Y))
            {
                Vector2 groundPos = new Vector2(
                    NPC.Center.X,
                    NPC.position.Y + NPC.height
                );

                Dust groundDust = Dust.NewDustPerfect(
                    groundPos + new Vector2(
                        Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                        0f
                    ),
                    DustID.SandstormInABottle
                );

                groundDust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                groundDust.velocity = new Vector2(
                    -NPC.direction * Main.rand.NextFloat(2f, 5f),
                    -Main.rand.NextFloat(1f, 3f)
                );
            }

            if (Timer <= 15f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust trailDust = Dust.NewDustPerfect(
                        NPC.Center + Main.rand.NextVector2Circular(
                            NPC.width / 2f,
                            NPC.height / 2f
                        ),
                        DustID.SandstormInABottle
                    );

                    trailDust.scale = Main.rand.NextFloat(2f, 3.2f);
                    trailDust.noGravity = true;
                    trailDust.velocity =
                        -NPC.velocity * 0.15f +
                        Main.rand.NextVector2Circular(1f, 1f);
                }
            }

            if (!launchRoarPlayed && Timer <= 8f)
            {
                SoundEngine.PlaySound(Roar1);
                launchRoarPlayed = true;
            }

            if (TimerAt(1f))
            {
                AntlionFX.Impact(NPC.Bottom, 0.7f);
            }

            if (Timer <= 40f)
                BackStreaks(2);

            // Don't let this idiot launch himself into the depths of Terraria
            NPC.velocity.X *= 0.96f;

            if (Timer >= 60f)
            {
                Timer = 0;
                CurrentState = AttackPhase.Cooldown;
                NPC.noTileCollide = false;
                NPC.netUpdate = true;
            }
        }

        private void HandleDash(Player target)
        {
            NPC.alpha = 0;
            Timer++;
            NPC.spriteDirection = NPC.direction;

            if (Timer <= 18f)
            {
                NPC.velocity.X *= 0.8f;

                IsParryable = true;

                float faceDir = target.Center.X > NPC.Center.X ? 1f : -1f;
                NPC.direction = (int)faceDir;
                NPC.spriteDirection = NPC.direction;

                // Dust gathering visual telegraph
                for (int i = 0; i < 2; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        NPC.Center + Main.rand.NextVector2Circular(NPC.width / 2f, NPC.height / 2f),
                        DustID.SandstormInABottle,
                        new Vector2(-NPC.direction * Main.rand.NextFloat(2f, 5f), -1f)
                    );
                    d.scale = 1.4f;
                    d.noGravity = true;
                }

                if (!Main.dedServ)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 spawnOffset = Main.rand.NextVector2Circular(NPC.width * 0.9f, NPC.height * 0.9f);
                        Vector2 inwardVelocity = -spawnOffset * Main.rand.NextFloat(0.05f, 0.09f);

                        BossParticleSystem.SandStreaks.Create(new ParticleInfo(
                            position: (NPC.Center + spawnOffset).ToNumerics(),
                            velocity: inwardVelocity.ToNumerics(),
                            rotation: 0f,
                            scale: new SystemVector2(26f, 5f),
                            color: new Color(255, 210, 140, 0),
                            duration: 20
                        ));
                    }
                }

                AntlionFX.ZoomHold(NPC.Center, 0.04f * Timer / 18f);

                if (TimerAt(18f))
                {
                    SoundEngine.PlaySound(Roar1, NPC.Center);
                    AntlionFX.Flash(NPC.Center, new Color(255, 120, 90), 0.18f);
                    AntlionFX.Ring(NPC.Center, new Vector2(200f, 200f), new Color(255, 120, 90), 14);
                }

                return;
            }

            //dash
            if (TimerAt(19f))
            {
                DashStuckTimer = 0;
                NPC.noTileCollide = false;

                float directionX = target.Center.X > NPC.Center.X ? 1f : -1f;
                float dashSpeed = DifficultyValue(10f, 19.5f, 21.5f);

                NPC.velocity.X = directionX * dashSpeed;
                NPC.velocity.Y = 0f;

                NPC.direction = (int)directionX;
                NPC.netUpdate = true;
            }

            if (TimerAt(19f))
            {
                AntlionFX.Impact(NPC.Bottom, 0.6f);
            }

            if (Timer <= 45f)
                BackStreaks(2);

            IsParryable = true;

            // STUCK PREVENTION (which works like most the time DAMMIT)
            if (Math.Abs(NPC.velocity.X) < 1f && Math.Abs(NPC.oldVelocity.X) > 3f)
            {
                DashStuckTimer++;

                if (DashStuckTimer == 1)
                {
                    NPC.velocity.Y = -8f;
                }

                if (DashStuckTimer >= 4)
                {
                    NPC.noTileCollide = true;
                    NPC.velocity.X = NPC.direction * 12f;
                    NPC.velocity.Y = -3f;

                    DashStuckTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else
            {
                DashStuckTimer = 0;
            }

            if (NPC.noTileCollide && Math.Abs(NPC.velocity.X) > 4f)
            {
                NPC.noTileCollide = false;
            }

            if (Timer <= 35f)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Center,
                    DustID.Sand,
                    -NPC.velocity * 0.2f
                );

                dust.scale = 1.8f;
                dust.noGravity = true;
            }

            // Deceleration
            NPC.velocity.X *= 0.98f;

            if (Timer >= 63f)
            {
                DashStuckTimer = 0;
                NPC.noTileCollide = false;

                Timer = 0;
                CurrentState = AttackPhase.Cooldown;
                NPC.netUpdate = true;
            }
        }

        private void HandleBurrow(Player target)
        {
            Timer++;

            // Digging in on the surface
            if (Timer <= BurrowDigTime)
            {
                float progress = Timer / BurrowDigTime;
                float smoothProgress = progress * progress * (3f - 2f * progress);

                NPC.alpha = (int)MathHelper.Lerp(0f, 190f, smoothProgress);

                NPC.noTileCollide = true;
                NPC.velocity.X *= 0.92f;
                NPC.velocity.Y = 0f;

                int dustCount = progress < 0.5f ? 3 : 5;

                for (int i = 0; i < dustCount; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        NPC.Bottom + new Vector2(
                            Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                            0f
                        ),
                        DustID.Sand
                    );

                    d.velocity = new Vector2(
                        Main.rand.NextFloat(-3f, 3f),
                        -Main.rand.NextFloat(2f, 5f)
                    );

                    d.scale = Main.rand.NextFloat(1.3f, 2.1f);
                    d.noGravity = false;
                }

                if (Main.rand.NextBool(3))
                {
                    Dust tungstenDust = Dust.NewDustPerfect(
                        NPC.Bottom + new Vector2(
                            Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                            0f
                        ),
                        DustID.Tungsten
                    );

                    tungstenDust.velocity = new Vector2(
                        Main.rand.NextFloat(-2f, 2f),
                        -Main.rand.NextFloat(1f, 3f)
                    );

                    tungstenDust.scale = Main.rand.NextFloat(0.8f, 1.3f);
                    tungstenDust.noGravity = true;
                }

                if (TimerAt(1f))
                {
                    ScreenShake(2f, 5);
                    SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                    AntlionFX.ZoomPunch(NPC.Center, 0.04f);
                }

                if (Timer % 10 == 0)
                    AntlionFX.Ring(NPC.Bottom, new Vector2(150f, 40f), new Color(235, 200, 140), 18);

                if (Timer % 3 == 0)
                {
                    AntlionFX.Streak(
                        NPC.Bottom + new Vector2(Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f), 0f),
                        new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(4f, 9f)),
                        new Vector2(Main.rand.NextFloat(18f, 36f), 3f),
                        new Color(255, 205, 140),
                        18
                    );
                }
            }


            // Underground pursuit
            else if (Timer < BurrowPursuitEnd)
            {
                NPC.alpha = 255;
                NPC.noTileCollide = true;
                float actualGroundY = GetGroundY(target.Center) + 64f;

                float deltaX = target.Center.X - NPC.Center.X;
                float speedX = MathHelper.Clamp(
                    deltaX * 0.08f,
                    -16f,
                    16f
                );

                NPC.velocity.X = speedX;
                NPC.velocity.Y = (actualGroundY - NPC.Center.Y) * 0.2f;

                if (!SoundEngine.TryGetActiveSound(rumbleSoundSlot, out ActiveSound? sound) || !sound!.IsPlaying)
                {
                    rumbleSoundSlot = SoundEngine.PlaySound(
                        new SoundStyle("ShatteredIllusion/Sounds/GreatAntlionSounds/AntlionBurrowing")
                        {
                            IsLooped = true,
                            Volume = 0.4f
                        },
                        NPC.Center
                    );
                }
                else
                {
                    sound!.Position = NPC.Center;
                    sound.Volume = 0.4f;
                }

                Vector2 groundPos = new Vector2(
                    NPC.Center.X,
                    actualGroundY - 64f
                );

                Dust d = Dust.NewDustPerfect(
                    groundPos + new Vector2(
                        Main.rand.NextFloat(-20f, 20f),
                        0f
                    ),
                    DustID.SandstormInABottle
                );

                Dust tungstenDust = Dust.NewDustPerfect(
                    groundPos + new Vector2(
                        Main.rand.NextFloat(-20f, 20f),
                        0f
                    ),
                    DustID.Tungsten
                );

                d.velocity = new Vector2(
                    0f,
                    -Main.rand.NextFloat(2f, 4f)
                );

                d.scale = Main.rand.NextFloat(1.5f, 2.8f);

                burrowSurfaceY = GetGroundY(NPC.Center);
                Vector2 surface = new Vector2(NPC.Center.X, burrowSurfaceY);

                if (Timer % 9 == 0)
                    AntlionFX.Ring(surface, new Vector2(170f, 44f), new Color(235, 200, 140), 20);

                if (Timer % 14 == 0 && Vector2.Distance(target.Center, NPC.Center) < 900f)
                    ScreenShake(1.5f, 8);

                if (Cinema.Enabled)
                {
                    // furrow: sand kicked up in front, falls back down behind him
                    for (int i = 0; i < 2; i++)
                    {
                        Dust furrow = Dust.NewDustPerfect(
                            surface + new Vector2(Main.rand.NextFloat(-50f, 50f), 0f),
                            DustID.Sand,
                            new Vector2(-NPC.velocity.X * Main.rand.NextFloat(0.1f, 0.35f), -Main.rand.NextFloat(3f, 8f))
                        );

                        furrow.scale = Main.rand.NextFloat(1.4f, 2.4f);
                    }
                }

                AntlionFX.Streak(
                    surface + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f),
                    new Vector2(-NPC.velocity.X * 0.35f + Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(3f, 7f)),
                    new Vector2(Main.rand.NextFloat(16f, 32f), 3f),
                    new Color(255, 205, 140),
                    20
                );
            }

            //sandtelegraph
            else if (Timer < BurrowTelegraphEnd)
            {
                if (SoundEngine.TryGetActiveSound(rumbleSoundSlot, out ActiveSound? sound))
                {
                    sound!.Volume *= 0.95f;

                    if (sound.Volume <= 0.05f)
                    {
                        sound.Stop();
                    }
                }

                NPC.velocity = Vector2.Zero;
                NPC.alpha = 255;

                float actualGroundY = GetGroundY(NPC.Center);
                Vector2 telegraphPos = new Vector2(
                    NPC.Center.X,
                    actualGroundY
                );

                for (int i = 0; i < 3; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        telegraphPos + new Vector2(
                            Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                            0f
                        ),
                        DustID.SandstormInABottle
                    );

                    d.velocity = new Vector2(
                        0f,
                        -Main.rand.NextFloat(4f, 8f)
                    );

                    d.scale = Main.rand.NextFloat(2f, 3.5f);
                    d.noGravity = true;
                }

                // pulses get faster the closer he is to coming up
                burrowSurfaceY = telegraphPos.Y;
                float warn = (Timer - BurrowPursuitEnd) / (BurrowTelegraphEnd - BurrowPursuitEnd);
                int beat = (int)MathHelper.Lerp(10f, 3f, warn);

                if (Timer % Math.Max(1, beat) == 0)
                    AntlionFX.Ring(telegraphPos, new Vector2(MathHelper.Lerp(110f, 260f, warn), MathHelper.Lerp(34f, 70f, warn)), new Color(255, 110, 70), 18);

                AntlionFX.ZoomHold(NPC.Center, 0.05f * warn);
            }
            // Erupt upward out of the ground
            else
            {
                if (SoundEngine.TryGetActiveSound(rumbleSoundSlot, out ActiveSound? sound))
                {
                    sound?.Stop();
                }

                if (TimerAt(BurrowTelegraphEnd))
                {
                    ScreenShake(5f, 8);
                    float actualGroundY = GetGroundY(NPC.Center);

                    if (!Main.dedServ)
                    {
                        BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                            position: new Vector2(NPC.Center.X, actualGroundY).ToNumerics(),
                            velocity: SystemVector2.Zero,
                            rotation: 0f,
                            scale: new SystemVector2(220f, 90f),
                            color: new Color(235, 200, 140, 0),
                            duration: 24
                        ));
                    }

                    NPC.Center = new Vector2(
                        NPC.Center.X,
                        actualGroundY - 30f
                    );

                    NPC.velocity = new Vector2(0f, -18f);
                    NPC.alpha = 0;

                    NPC.noTileCollide = true;

                    NPC.netUpdate = true;

                    SoundEngine.PlaySound(Roar2, NPC.Center);


                    AntlionFX.Impact(new Vector2(NPC.Center.X, actualGroundY), 1.6f);
                    burrowSurfaceY = actualGroundY;

                    for (int i = 0; i < 14; i++)
                    {
                        Vector2 sprayDir = (-MathHelper.PiOver2 + Main.rand.NextFloat(-0.6f, 0.6f)).ToRotationVector2();

                        AntlionFX.Streak(
                            new Vector2(NPC.Center.X + Main.rand.NextFloat(-40f, 40f), actualGroundY),
                            sprayDir * Main.rand.NextFloat(10f, 22f),
                            new Vector2(Main.rand.NextFloat(40f, 90f), 4f),
                            new Color(255, 205, 140),
                            22
                        );
                    }

                    for (int i = 0; i < 35; i++)
                    {
                        Vector2 dustVel =
                            Main.rand.NextVector2Circular(9f, 9f) +
                            new Vector2(0f, -5f);

                        Dust d = Dust.NewDustPerfect(
                            NPC.Center,
                            DustID.Sand,
                            dustVel
                        );

                        d.scale = Main.rand.NextFloat(2f, 3.8f);
                    }

                    // amount of rubble based of difficulty
                    int rubbleCount = 6;

                    if (Main.masterMode)
                    {
                        rubbleCount = 12;
                    }
                    else if (Main.expertMode)
                    {
                        rubbleCount = 8;
                    }

                    for (int r = 0; r < rubbleCount; r++)
                    {
                        float offsetX = Main.rand.NextFloat(-500f, 500f); // the spread range of rubble

                        Vector2 spawnPos = new Vector2(
                            NPC.Center.X + offsetX,
                            NPC.Center.Y - 500f
                        );

                        Vector2 velocity = new Vector2(
                            Main.rand.NextFloat(-2f, 2f),
                            Main.rand.NextFloat(2f, 5f)
                        );

                        int fallDamage = DifficultyValue(15, 30, 15);

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                spawnPos,
                                velocity,
                                ModContent.ProjectileType<FallingRubble>(),
                                fallDamage,
                                2f,
                                Main.myPlayer
                            );
                        }
                    }
                }

                // Gravity back on for the actual jump out
                NPC.velocity.Y += 0.35f;
                if (Timer >= BurrowEnd)
                {
                    NPC.noTileCollide = false;
                    Timer = 0;
                    CurrentState = AttackPhase.Cooldown;
                    NPC.netUpdate = true;
                }
            }
        }

        private void HandleSpit(Player target)
        {
            NPC.alpha = 0;
            Timer++;

            NPC.velocity.X *= 0.85f;

            if (TimerAt(1f))
            {
                NPC.velocity = Vector2.Zero;
            }

            float spitDeltaX = target.Center.X - NPC.Center.X;

            if (Math.Abs(spitDeltaX) > 10f)
            {
                int newDirection = spitDeltaX > 0f ? 1 : -1;

                if (newDirection != NPC.spriteDirection)
                {
                    NPC.direction = newDirection;
                    NPC.spriteDirection = newDirection;
                    NPC.netUpdate = true;
                }
            }

            Vector2 mouthPosition = NPC.Center + new Vector2(
                NPC.spriteDirection * (NPC.width / 2f + MouthForwardOffset + MouthSidewaysOffset),
                MouthVerticalOffset
            );

            // telegraph for the spit
            if (Timer <= 90f)
            {
                Vector2 baseDir = SafeNormalize(target.Center - mouthPosition);
                spitAimDir = baseDir;
                AntlionFX.ZoomHold(NPC.Center, 0.03f * Timer / 90f);

                if (!Cinema.Enabled)
                {
                    int shotCount = DifficultyValue(1, 3, 5);
                    float spread = DifficultyValue(0f, 0.18f, 0.22f);

                    if (Timer % 2 == 0)
                    {
                        for (int i = 0; i < shotCount; i++)
                        {
                            Vector2 shotDir = baseDir;

                            if (shotCount > 1)
                            {
                                float offset = (i - (shotCount - 1) / 2f) * spread;
                                shotDir = baseDir.RotatedBy(offset);
                            }

                            for (int d = 1; d <= 10; d++)
                            {
                                Vector2 dustPos = mouthPosition + shotDir * (d * 35f);

                                Dust lineDust = Dust.NewDustPerfect(
                                    dustPos,
                                    DustID.SandstormInABottle,
                                    Vector2.Zero
                                );

                                lineDust.scale = 0.9f;
                                lineDust.noGravity = true;
                            }
                        }
                    }
                }

                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustPerfect(
                        mouthPosition + Main.rand.NextVector2Circular(8f, 8f),
                        DustID.SandstormInABottle,
                        Main.rand.NextVector2Circular(1f, 1f)
                    );

                    dust.scale = Main.rand.NextFloat(1.2f, 2f);
                    dust.noGravity = true;
                }
            }

            //Spit
            if (TimerAt(90f))
            {
                Vector2 predictedPosition = target.Center + target.velocity * 4f;
                Vector2 direction = SafeNormalize(predictedPosition - mouthPosition);

                float aimInaccuracy = Main.rand.NextFloat(-0.05f, 0.05f);
                direction = direction.RotatedBy(aimInaccuracy);

                int shotCount = DifficultyValue(1, 3, 5);
                float spitSpeed = DifficultyValue(12f, 13f, 14f);
                float spread = DifficultyValue(0f, 0.18f, 0.22f);

                for (int i = 0; i < shotCount; i++)
                {
                    Vector2 shotDirection = direction;

                    if (shotCount > 1)
                    {
                        float offset = (i - (shotCount - 1) / 2f) * spread;
                        shotDirection = direction.RotatedBy(offset);
                    }

                    int spitDamage = DifficultyValue(10, 25, 15);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            mouthPosition,
                            shotDirection * spitSpeed,
                            ModContent.ProjectileType<SandBall>(),
                            spitDamage,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.DD2_OgreSpit, mouthPosition);
                ScreenShake(2f, 4);
                AntlionFX.ZoomPunch(NPC.Center, 0.025f);
                AntlionFX.Ring(mouthPosition, new Vector2(120f, 120f), new Color(255, 190, 110), 14);
                AntlionFX.Embers(mouthPosition, 8, 2f, 6f, new Color(255, 190, 110), 8f, direction * 3f);

                for (int i = 0; i < 15; i++)
                {
                    Dust dust = Dust.NewDustPerfect(
                        mouthPosition,
                        DustID.Sand,
                        direction * Main.rand.NextFloat(2f, 5f) +
                        Main.rand.NextVector2Circular(2f, 2f)
                    );

                    dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                }
            }

            if (Timer >= 105f)
            {
                Timer = 0;
                CurrentState = AttackPhase.Cooldown;
                NPC.netUpdate = true;
            }
        }

        private void HandlePhase2BurrowDive(Player target)
        {
            Timer++;

            if (Timer >= 56f)
                AntlionFX.Bars(NPC.Center, 3);

            // BURROW DOWN FOR PHASE 2 
            if (Timer <= 55f)
            {
                float progress = MathHelper.Clamp(Timer / 45f, 0f, 1f);
                float smoothProgress = progress * progress * (3f - 2f * progress);

                NPC.alpha = (int)MathHelper.Lerp(0f, 255f, smoothProgress);
                NPC.noTileCollide = true;

                NPC.velocity.X *= 0.90f;
                NPC.velocity.Y = 0f;

                for (int i = 0; i < 4; i++)
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Bottom + new Vector2(
                            Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                            0f
                        ),
                        DustID.Sand
                    );

                    dust.velocity = new Vector2(
                        Main.rand.NextFloat(-3f, 3f),
                        -Main.rand.NextFloat(2f, 5f)
                    );

                    dust.scale = Main.rand.NextFloat(1.3f, 2.3f);
                }

                if (TimerAt(1f))
                {
                    SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                    AntlionFX.ZoomPunch(NPC.Center, 0.05f);
                    AntlionFX.Ring(NPC.Bottom, new Vector2(220f, 60f), new Color(235, 200, 140), 22);
                }
            }

            // REPOSITIONING FARTHER WAY FOR THE LAUNCH
            else if (Timer <= 75f)
            {
                NPC.alpha = 255;
                NPC.noTileCollide = true;
                NPC.velocity = Vector2.Zero;

                if (TimerAt(56f))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float direction = target.Center.X >= NPC.Center.X
                            ? 1f
                            : -1f;

                        float launchX = target.Center.X - direction * 800f;
                        launchX = MathHelper.Clamp(
                            launchX,
                            200f,
                            Main.maxTilesX * 16f - 200f
                        );

                        // Land BEHIND the player (hopefully)
                        Phase2DiveLandingX =
                            target.Center.X - direction * 180f;

                        Phase2DiveLandingX = MathHelper.Clamp(
                            Phase2DiveLandingX,
                            200f,
                            Main.maxTilesX * 16f - 200f
                        );

                        float launchGroundY = GetGroundY(
                            new Vector2(launchX, target.Center.Y)
                        );

                        Phase2DiveGroundY = GetGroundY(
                            new Vector2(
                                Phase2DiveLandingX,
                                target.Center.Y
                            )
                        );

                        NPC.Center = new Vector2(
                            launchX,
                            launchGroundY - NPC.height / 2f
                        );

                        NPC.direction = (int)direction;
                        NPC.spriteDirection = NPC.direction;

                        NPC.netUpdate = true;

                        SIPackets.SendAntlionDiveState(NPC, Phase2DiveLandingX, Phase2DiveGroundY);
                    }
                }

                if (ModContent.GetInstance<ShatteredIllusionConfig>().SlamTelegraph)
                {
                    if (Timer % 12 == 0 && !IsZero(Phase2DiveGroundY))
                        AntlionFX.Ring(new Vector2(Phase2DiveLandingX, Phase2DiveGroundY), new Vector2(320f, 70f), new Color(255, 90, 60), 22);
                }
                // telegraph for the jump
                else if (Timer % 2 == 0)
                {
                    Vector2 diveOrigin = NPC.Center;
                    Vector2 landingPoint = new Vector2(Phase2DiveLandingX, Phase2DiveGroundY - NPC.height / 2f);

                    const float previewArcHeight = 350f;

                    for (int d = 1; d <= 10; d++)
                    {
                        float t = d / 10f;

                        float x = MathHelper.Lerp(diveOrigin.X, landingPoint.X, t);
                        float y = MathHelper.Lerp(diveOrigin.Y, landingPoint.Y, t)
                                  - previewArcHeight * 4f * t * (1f - t);

                        Vector2 dustPos = new Vector2(x, y);

                        Dust lineDust = Dust.NewDustPerfect(
                            dustPos,
                            DustID.SandstormInABottle,
                            Vector2.Zero
                        );

                        lineDust.scale = 2f;
                        lineDust.noGravity = true;
                    }
                }

                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Center + Main.rand.NextVector2Circular(30f, 15f),
                        DustID.SandstormInABottle,
                        Main.rand.NextVector2Circular(1f, 1f)
                    );

                    dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                    dust.noGravity = true;
                }
            }

            // TELEGRAPH FROM THE LAUNCH
            else if (Timer <= 90f)
            {
                NPC.alpha = 255;
                NPC.noTileCollide = true;
                NPC.velocity = Vector2.Zero;

                for (int i = 0; i < 5; i++)
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Bottom + new Vector2(
                            Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                            0f
                        ),
                        DustID.Sand
                    );

                    dust.velocity = new Vector2(
                        Main.rand.NextFloat(-4f, 4f),
                        -Main.rand.NextFloat(4f, 8f)
                    );

                    dust.scale = Main.rand.NextFloat(1.5f, 3f);
                    dust.noGravity = true;
                }

                AntlionFX.ZoomHold(NPC.Center, 0.07f * Math.Max(0f, Timer - 76f) / 14f);

                if (Timer % 4 == 0)
                    AntlionFX.Ring(NPC.Bottom, new Vector2(200f, 50f), new Color(235, 200, 140), 16);

                if (TimerAt(76f))
                {
                    SoundEngine.PlaySound(
                        Roar3,
                        NPC.Center
                    );
                }
            }


            // POP ANIMATION WINDUP
            else if (Timer < 111f)
            {
                NPC.alpha = 0;
                NPC.noTileCollide = true;
                NPC.velocity = Vector2.Zero;
                AntlionFX.ZoomHold(NPC.Center, 0.07f);

                if (TimerAt(91f))
                {
                    ScreenShake(3f, 6);
                    AntlionFX.Impact(NPC.Bottom, 1.3f);
                    Phase2DiveLaunchX = NPC.Center.X;
                    Phase2DiveLaunchY = NPC.Center.Y;

                    NPC.direction =
                        Phase2DiveLandingX > Phase2DiveLaunchX ? 1 : -1;

                    NPC.spriteDirection = NPC.direction;

                    NPC.netUpdate = true;

                    SoundEngine.PlaySound(
                        Roar3,
                        NPC.Center
                    );
                }
            }


            // ACTUAL PHASE 2 JUMP/ARC 
            else
            {
                NPC.alpha = 0;
                NPC.noTileCollide = true;

                const float airTime = 40f;
                const float arcHeight = 500f;

                if (TimerAt(111f))
                {
                    AntlionFX.Impact(NPC.Center, 1.2f, false);

                    // Launch burst dust thingy
                    for (int i = 0; i < 30; i++)
                    {
                        Dust dust = Dust.NewDustPerfect(
                            NPC.Center,
                            DustID.Sand,
                            Main.rand.NextVector2Circular(8f, 8f)
                        );

                        dust.scale = Main.rand.NextFloat(2f, 3.5f);
                        dust.noGravity = true;
                    }

                    // RUBBLE
                    int rubbleCount = DifficultyValue(6, 8, 12);

                    for (int i = 0; i < rubbleCount; i++)
                    {
                        float rubbleX =
                            target.Center.X +
                            Main.rand.NextFloat(-500f, 500f);

                        Vector2 rubbleSpawn = new Vector2(
                            rubbleX,
                            target.Center.Y - 450f
                        );

                        Vector2 rubbleVelocity = new Vector2(
                            Main.rand.NextFloat(-2f, 2f),
                            Main.rand.NextFloat(2f, 5f)
                        );
                        int rubbleDamage = DifficultyValue(15, 30, 15);
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                rubbleSpawn,
                                rubbleVelocity,
                                ModContent.ProjectileType<FallingRubble>(),
                                rubbleDamage,
                                2f,
                                Main.myPlayer
                            );
                        }
                    }
                }

                if (Timer >= 111f)
                {
                    float rawProgress = (Timer - 111f) / airTime;
                    float progress = MathHelper.Clamp(rawProgress, 0f, 1f);

                    float landingY =
                        Phase2DiveGroundY - NPC.height / 2f;

                    float newX =
                        MathHelper.Lerp(Phase2DiveLaunchX, Phase2DiveLandingX, progress);

                    float newY =
                        MathHelper.Lerp(Phase2DiveLaunchY, landingY, progress)
                        - arcHeight * 4f * progress * (1f - progress);

                    NPC.Center = new Vector2(newX, newY);

                    float dxdt = (Phase2DiveLandingX - Phase2DiveLaunchX) / airTime;
                    float dydt = (landingY - Phase2DiveLaunchY) / airTime - arcHeight * 4f * (1f - 2f * progress) / airTime;

                    NPC.rotation = (float)Math.Atan2(dydt, Math.Abs(dxdt)) * NPC.direction * 0.8f;
                    AntlionFX.ZoomHold(NPC.Center, 0.03f);

                    AntlionFX.Streak(
                        NPC.Center - new Vector2(dxdt, dydt) * 2f + Main.rand.NextVector2Circular(30f, 20f),
                        new Vector2(-dxdt, -dydt) * 0.4f,
                        new Vector2(Main.rand.NextFloat(50f, 90f), 5f),
                        new Color(255, 190, 120),
                        16
                    );

                    // Sand trail while flying
                    if (Main.rand.NextBool(2))
                    {
                        Dust dust = Dust.NewDustPerfect(
                            NPC.Center,
                            DustID.Sand,
                            Main.rand.NextVector2Circular(1.5f, 1.5f)
                        );

                        dust.scale = Main.rand.NextFloat(1.5f, 2.5f);
                        dust.noGravity = true;
                    }

                    // CRASH LANDING
                    if (rawProgress >= 1f)
                    {
                        ScreenShake(7f, 10);
                        NPC.Center = new Vector2(
                            Phase2DiveLandingX,
                            landingY
                        );

                        NPC.velocity = Vector2.Zero;
                        NPC.noTileCollide = false;

                        SoundEngine.PlaySound(
                            SoundID.Item14,
                            NPC.Center
                        );

                        NPC.rotation = 0f;
                        AntlionFX.Impact(NPC.Bottom, 2.2f);

                        for (int i = 0; i < 50; i++)
                        {
                            Vector2 dustVelocity =
                                Main.rand.NextVector2Circular(10f, 7f);

                            dustVelocity.Y -= 4f;

                            Dust dust = Dust.NewDustPerfect(
                                NPC.Bottom,
                                DustID.Sand,
                                dustVelocity
                            );

                            dust.scale = Main.rand.NextFloat(2f, 4f);
                            dust.noGravity = true;
                        }

                        // Start the actual burrow animation.
                        Timer = 0;
                        CurrentState = AttackPhase.Burrow;

                        NPC.alpha = 0;
                        NPC.noTileCollide = true;

                        NPC.netUpdate = true;
                    }
                }
            }
        }

        private void HandleCooldown()
        {
            NPC.alpha = 0;
            Timer++;
            NPC.noTileCollide = false;
            NPC.velocity.X *= 0.88f;

            float cooldownTime = DifficultyValue(64f, 55f, 45f);

            if (Timer >= cooldownTime)
            {
                Timer = 0;

                AttackPhase[] attackOrder = Phase2
                    ? Phase2AttackOrder
                    : Phase1AttackOrder;

                AttackSequenceIndex =
                    (AttackSequenceIndex + 1) % attackOrder.Length;

                CurrentState = attackOrder[(int)AttackSequenceIndex];
                NPC.netUpdate = true;
            }
        }


        // dies in a delay so the animation can play, ai/timer sync it for everyone
        public override bool CheckDead()
        {
            if (CurrentState == AttackPhase.Death)
                return Timer >= DeathTicks;

            CurrentState = AttackPhase.Death;
            Timer = 0;
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            NPC.velocity.X = 0f;
            IsParryable = false;
            NPC.netUpdate = true;

            return false;
        }

        private void HandleDeath()
        {
            Timer++;

            NPC.dontTakeDamage = true;
            NPC.noTileCollide = false;
            NPC.velocity.X *= 0.8f;
            NPC.life = Math.Max(NPC.life, 1);

            const float sandOutStart = 110f;
            float t = Timer / DeathTicks;

            if (TimerAt(1f))
            {
                if (SoundEngine.TryGetActiveSound(rumbleSoundSlot, out ActiveSound? sound))
                    sound?.Stop();

                SoundEngine.PlaySound(Roar2, NPC.Center);
                ScreenShake(8f, 20);
                AntlionFX.Impact(NPC.Center, 1.5f, false);
            }

            if (TimerAt(60f))
                SoundEngine.PlaySound(Roar1, NPC.Center);

            if (TimerAt(sandOutStart))
            {
                SoundEngine.PlaySound(Roar3, NPC.Center);
                ScreenShake(6f, 16);
                AntlionFX.Flash(NPC.Center, new Color(255, 200, 150), 0.4f);
            }

            if (Cinema.DeathCameraEnabled)
            {
                AntlionFX.Bars(NPC.Center, 3);
                AntlionFX.ZoomHold(NPC.Center, 0.16f * MathHelper.Clamp(Timer / 60f, 0f, 1f));
            }

            // shaking harder the closer it gets
            NPC.position.X += Main.rand.NextFloat(-1f, 1f) * (1f + 3f * t);

            if (Timer % 4 == 0)
            {
                Vector2 point = NPC.Center + new Vector2(Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f), Main.rand.NextFloat(-NPC.height / 2f, NPC.height / 2f));

                AntlionFX.Ring(point, new Vector2(90f, 90f), new Color(255, 150, 90), 16);
                AntlionFX.Embers(point, 3, 2f, 6f, new Color(255, 170, 100), 9f, new Vector2(0f, -2f));
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = -0.3f + 0.6f * t }, point);
            }

            if (Timer % 24 == 0 && Timer < sandOutStart)
            {
                ScreenShake(3f + 3f * t, 10);
                AntlionFX.Impact(NPC.Center + Main.rand.NextVector2Circular(60f, 25f), 0.5f + t, false);
            }

            // turns into sand
            if (Timer >= sandOutStart)
            {
                float fade = (Timer - sandOutStart) / (DeathTicks - sandOutStart);
                NPC.alpha = (int)(255f * MathHelper.Clamp(fade, 0f, 1f));

                for (int i = 0; i < 4; i++)
                {
                    Dust d = Dust.NewDustPerfect(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f), Main.rand.NextFloat(-NPC.height / 2f, NPC.height / 2f)),
                        DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-2f, 4f), -Main.rand.NextFloat(2f, 7f))
                    );

                    d.scale = Main.rand.NextFloat(1.6f, 3f);
                    d.noGravity = true;
                }

                AntlionFX.Streak(
                    NPC.Center + Main.rand.NextVector2Circular(NPC.width / 2f, NPC.height / 2f),
                    new Vector2(Main.rand.NextFloat(6f, 16f), -Main.rand.NextFloat(2f, 8f)),
                    new Vector2(Main.rand.NextFloat(40f, 80f), 4f),
                    new Color(255, 205, 140),
                    24
                );
            }

            if (Timer >= DeathTicks && Main.netMode != NetmodeID.MultiplayerClient)
            {
                NPC.dontTakeDamage = false;
                NPC.life = 0;
                NPC.checkDead();
            }
        }

        private void StartPhase2()
        {
            Phase2 = true;
            Phase2Transitioning = true;
            burrowSurfaceY = GetGroundY(NPC.Center);

            Timer = 0;
            AttackSequenceIndex = 0;

            NPC.velocity = Vector2.Zero;
            NPC.noTileCollide = false;
            NPC.alpha = 0;
            NPC.color = Color.White;

            IsParryable = false;

            NPC.netUpdate = true;

            SoundEngine.PlaySound(Roar2, NPC.Center);
            ScreenShake(7f, 18);
            AntlionFX.ZoomPunch(NPC.Center, 0.08f);
            AntlionFX.Flash(NPC.Center, new Color(255, 120, 90), 0.45f);

            for (int i = 0; i < 60; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 6f);
                velocity.Y -= 3f;

                Dust dust = Dust.NewDustPerfect(
                    NPC.Center,
                    DustID.Sand,
                    velocity
                );

                dust.scale = Main.rand.NextFloat(2f, 4f);
                dust.noGravity = true;
            }

            // One big expanding ring to sell the "power up" moment.
            if (!Main.dedServ)
            {
                BossParticleSystem.Shockwaves.Create(new ParticleInfo(
                    position: NPC.Center.ToNumerics(),
                    velocity: SystemVector2.Zero,
                    rotation: 0f,
                    scale: new SystemVector2(340f, 340f),
                    color: new Color(255, 130, 110, 0),
                    duration: 30
                ));

                // Embers thrown outward in every direction alongside the sand.
                for (int i = 0; i < 24; i++)
                {
                    Vector2 emberVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 9f);

                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: NPC.Center.ToNumerics(),
                        velocity: emberVelocity.ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(14f, 14f),
                        color: new Color(255, 160, 90, 0),
                        duration: Main.rand.Next(24, 36)
                    ));
                }
            }
        }

        private void HandlePhase2Transition()
        {
            Timer++;

            bool movie = Cinema.Enabled;
            float boom = TransitionBoom;
            float transitionEnd = movie ? 100f : 60f;

            NPC.velocity.X *= 0.85f;
            NPC.dontTakeDamage = true;

            // digs in, then gets thrown out of the ground on the boom
            if (Timer < boom)
            {
                float digProgress = MathHelper.Clamp(Timer / (boom - 10f), 0f, 1f);

                NPC.velocity.Y = 0f;
                NPC.noTileCollide = true;
                NPC.alpha = (int)MathHelper.Lerp(0f, 255f, digProgress * digProgress * (3f - 2f * digProgress));
            }
            else
            {
                NPC.alpha = 0;

                if (NPC.velocity.Y < 0f)
                {
                    NPC.noTileCollide = true;
                    NPC.velocity.Y += 0.5f;
                }
                else
                {
                    NPC.noTileCollide = false;
                }
            }

            float flashPulse = (float)Math.Sin(Timer * 0.6f) * 0.5f + 0.5f;
            NPC.color = Color.Lerp(Color.White, new Color(255, 90, 90), flashPulse);

            IsParryable = false;


            NPC.position.X += Main.rand.NextFloat(-1.5f, 1.5f);

            if (movie)
            {
                AntlionFX.Bars(NPC.Center, 3);

                if (TimerAt(1f))
                    Cinema.Vignette(0.6f);
            }

            if (movie && Timer < 45f)
            {
                AntlionFX.ZoomHold(NPC.Center, 0.14f * Timer / 45f);

                // sand spiraling in
                for (int i = 0; i < 3; i++)
                {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(200f, 400f);
                    Vector2 swirl = new Vector2(-offset.Y, offset.X).SafeNormalize(Vector2.Zero) * (4f + 6f * Timer / 45f);

                    AntlionFX.Streak(NPC.Center + offset, -offset * 0.05f + swirl, new Vector2(Main.rand.NextFloat(30f, 60f), 4f), new Color(255, 190, 120), 16);
                }

                if (Timer % 2 == 0)
                {
                    AntlionFX.Streak(
                        NPC.Bottom + new Vector2(Main.rand.NextFloat(-160f, 160f), 0f),
                        new Vector2(0f, -Main.rand.NextFloat(4f, 10f)),
                        new Vector2(Main.rand.NextFloat(14f, 26f), 3f),
                        new Color(255, 150, 90),
                        24
                    );
                }

                if (Timer % 10 == 0)
                    AntlionFX.Ring(NPC.Center, new Vector2(140f, 140f) * (0.6f + Timer / 45f), new Color(255, 110, 80), 16);
            }
            else if (movie)
            {
                AntlionFX.ZoomHold(NPC.Center, 0.14f * MathHelper.Clamp(1f - (Timer - 45f) / 40f, 0f, 1f));

                if (TimerAt(52f) || TimerAt(59f))
                    AntlionFX.Ring(NPC.Center, new Vector2(600f, 600f), new Color(255, 150, 100), 30);
            }

            int dustCount = Timer < boom ? 6 : 10;

            for (int i = 0; i < dustCount; i++)
            {
                Vector2 dustPosition = NPC.Bottom + new Vector2(
                    Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f),
                    0f
                );

                Dust dust = Dust.NewDustPerfect(
                    dustPosition,
                    DustID.Sand
                );

                dust.velocity = new Vector2(
                    Main.rand.NextFloat(-4f, 4f),
                    -Main.rand.NextFloat(2f, 6f)
                );

                dust.scale = Main.rand.NextFloat(1.5f, 3f);
                dust.noGravity = true;
            }

            if (Timer % 15 == 0)
            {
                ScreenShake(3f, 8);
            }

            if (TimerAt(boom))
            {
                NPC.Center = new Vector2(NPC.Center.X, burrowSurfaceY - 30f);
                NPC.velocity = new Vector2(0f, -15f);
                NPC.noTileCollide = true;
                SpawnRubble(DifficultyValue(6, 8, 12));
                NPC.netUpdate = true;

                for (int i = 0; i < 14; i++)
                {
                    Vector2 sprayDir = (-MathHelper.PiOver2 + Main.rand.NextFloat(-0.6f, 0.6f)).ToRotationVector2();

                    AntlionFX.Streak(
                        new Vector2(NPC.Center.X + Main.rand.NextFloat(-40f, 40f), burrowSurfaceY),
                        sprayDir * Main.rand.NextFloat(10f, 22f),
                        new Vector2(Main.rand.NextFloat(40f, 90f), 4f),
                        new Color(255, 205, 140),
                        22
                    );
                }

                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                SoundEngine.PlaySound(Roar3, NPC.Center);
                Cinema.Title("Great Antlion Charger", "Enraged", 90);
                ScreenShake(10f, 20);
                AntlionFX.Impact(NPC.Center, 2.6f, false);

                for (int i = 0; i < 45; i++)
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Center,
                        DustID.Sand,
                        Main.rand.NextVector2Circular(7f, 7f)
                    );

                    dust.scale = Main.rand.NextFloat(2f, 3.5f);
                    dust.noGravity = true;
                }
            }

            if (Timer >= transitionEnd)
            {
                Timer = 0;
                AttackSequenceIndex = 0;
                CurrentState = AttackPhase.Burrow;
                Phase2Transitioning = false;
                NPC.dontTakeDamage = false;
                Cinema.Vignette(0f);
                NPC.netUpdate = true;
            }
        }

        public void OnParried(Player player)
        {
            float knockbackDir = NPC.Center.X < player.Center.X ? -1f : 1f;
            NPC.velocity = new Vector2(knockbackDir * 10f, -4f);

            AntlionFX.Impact(NPC.Center, 0.8f, false, AntlionFX.ParryStyle);

            CurrentState = AttackPhase.Cooldown;

            // Master mode gets a 5 tick stun instead of the normal 15 because I HATE YOU.
            Timer = Main.masterMode ? -5f : -15f;

            IsParryable = false;
            NPC.netUpdate = true;

            for (int i = 0; i < 20; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    NPC.Center,
                    DustID.Gold,
                    Main.rand.NextVector2Circular(6f, 6f)
                );

                d.noGravity = true;
            }


            for (int i = 0; i < 14; i++)
            {
                Vector2 sparkVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 8f);

                if (!Main.dedServ)
                {
                    BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                        position: NPC.Center.ToNumerics(),
                        velocity: sparkVelocity.ToNumerics(),
                        rotation: 0f,
                        scale: new SystemVector2(10f, 10f),
                        color: new Color(255, 225, 120, 0),
                        duration: Main.rand.Next(18, 28)
                    ));
                }
            }
        }
        public static void ReceiveDiveState(BinaryReader reader)
        {
            int npcIndex = reader.ReadInt32();
            float landingX = reader.ReadSingle();
            float groundY = reader.ReadSingle();

            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
                return;

            NPC npc = Main.npc[npcIndex];

            if (npc.active && npc.ModNPC is GreatAntlionCharger antlion)
            {
                antlion.Phase2DiveLandingX = landingX;
                antlion.Phase2DiveGroundY = groundY;
            }
        }

        private void ScreenShake(float strength, int frames, float vibration = 5f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            Main.instance.CameraModifiers.Add(
                new PunchCameraModifier(
                    NPC.Center,
                    Main.rand.NextVector2CircularEdge(1f, 1f),
                    strength,
                    vibration,
                    frames,
                    1000f
                )
            );
        }
    }
}