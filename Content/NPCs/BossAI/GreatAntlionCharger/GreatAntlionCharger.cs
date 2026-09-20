using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
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
            Cooldown
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
            Main.npcFrameCount[NPC.type] = MainFrameCount;

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
            Color finalDrawColor = NPC.GetAlpha(drawColor).MultiplyRGB(tintColor);

            if (TryDrawParryTexture(spriteBatch, screenPos, finalDrawColor)) return false;
            if (TryDrawPopTexture(spriteBatch, screenPos, finalDrawColor)) return false;
            if (TryDrawSpitTexture(spriteBatch, screenPos, finalDrawColor)) return false;
            if (TryDrawBurrowTexture(spriteBatch, screenPos, finalDrawColor)) return false;

            DrawMainTexture(spriteBatch, screenPos, finalDrawColor);
            return false;
        }

        private SpriteEffects CurrentSpriteEffects =>
            NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        private bool TryDrawParryTexture(SpriteBatch spriteBatch, Vector2 screenPos, Color finalDrawColor)
        {
            // Triggered if recently parried/stunned via negative timers in Cooldown.
            bool isParryDrawing = CurrentState == AttackPhase.Cooldown && Timer < 0f;
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

            if ((!isPhase1Burrowing && !isPhase2Burrowing) || burrowTexture == null || !burrowTexture.IsLoaded)
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

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            if (IsHidden)
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

            if (!phase2DiveAnimation && !normalBurrowAnimation)
            {
                return false;
            }

            if (burrowTexture == null) burrowTexture = ModContent.Request<Texture2D>(BurrowTexturePath);
            if (burrowTexture == null || !burrowTexture.IsLoaded)
            {
                return false;
            }

            float animationTime = phase2DiveAnimation ? 45f : BurrowDigTime;
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

            for (int y = startTileY; y < startTileY + 120; y++)
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

            if (Timer <= 8f)
            {
                SoundEngine.PlaySound(Roar1);
            }

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

                if (TimerAt(18f))
                {
                    SoundEngine.PlaySound(Roar1, NPC.Center);
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

                // telegraph for the jump
                if (Timer % 2 == 0)
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

                if (TimerAt(91f))
                {
                    ScreenShake(3f, 6);
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


        private void StartPhase2()
        {
            Phase2 = true;
            Phase2Transitioning = true;

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

            NPC.velocity.X *= 0.85f;
            NPC.velocity.Y = 0f;

            NPC.noTileCollide = false;
            NPC.alpha = 0;

            float flashPulse = (float)Math.Sin(Timer * 0.6f) * 0.5f + 0.5f;
            NPC.color = Color.Lerp(Color.White, new Color(255, 90, 90), flashPulse);

            IsParryable = false;


            NPC.position.X += Main.rand.NextFloat(-1.5f, 1.5f);

            int dustCount = Timer < 30f ? 6 : 10;

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

            if (TimerAt(30f))
            {
                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                ScreenShake(6f, 14);

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

            if (Timer >= 60f)
            {
                Timer = 0;
                AttackSequenceIndex = 0;
                CurrentState = AttackPhase.Burrow;
                Phase2Transitioning = false;
                NPC.netUpdate = true;
            }
        }

        public void OnParried(Player player)
        {
            float knockbackDir = NPC.Center.X < player.Center.X ? -1f : 1f;
            NPC.velocity = new Vector2(knockbackDir * 10f, -4f);

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