using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core.V3;
using ParticleLibrary.Core.V3.Particles;
using Terraria;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    // One instance buffer per draw layer. Every Shape3D marked Instanced in that layer is written into it
    // and drawn with a single instanced call per texture, using ParticleLibrary's own shader.
    internal sealed class InstancedShapeBuffer : GeometryBuffer<VertexPositionTexture, ParticleInstance>
    {
        private struct Run
        {
            public Texture2D Texture;
            public bool Additive;
            public int Start;
            public int Count;
        }

        private readonly ThreeDLayer layer;
        private readonly ParticleInstance[] data;
        private readonly List<Run> runs = new();

        public InstancedShapeBuffer(ThreeDLayer layer, int maxInstances) : base(maxInstances)
        {
            this.layer = layer;

            VertexPositionTexture[] vertices =
            [
                new(new(-0.5f, -0.5f, 0f), Vector2.Zero),
                new(new(-0.5f, 0.5f, 0f), Vector2.UnitY),
                new(new(0.5f, -0.5f, 0f), Vector2.UnitX),
                new(new(0.5f, 0.5f, 0f), Vector2.One)
            ];

            short[] indices = [0, 2, 3, 0, 3, 1];

            var instanceElements = new VertexElement[3];
            instanceElements[0] = new VertexElement(sizeof(float) * 0, VertexElementFormat.Vector4, VertexElementUsage.Normal, 0);
            instanceElements[1] = new VertexElement(sizeof(float) * 4, VertexElementFormat.Vector2, VertexElementUsage.Normal, 1);
            instanceElements[2] = new VertexElement(sizeof(float) * 6, VertexElementFormat.Color, VertexElementUsage.Color, 0);

            data = new ParticleInstance[GetMaxInstances()];
            Initialize(vertices, indices, instanceElements);
        }

        public override void Render()
        {
            if (data.Length == 0 || Main.gameMenu)
                return;

            List<IThreeDDrawable> list = ThreeDRenderer.Bucket(layer);

            if (list.Count == 0)
                return;

            View3 view = View3.World;
            Rectangle bounds = ThreeDRenderer.Bounds;
            int used = 0;

            runs.Clear();

            foreach (IThreeDDrawable d in list)
            {
                if (d is not Shape3D { UsingInstancing: true } shape || !shape.Alive)
                    continue;

                int written = shape.WriteInstances(data, used, view, bounds, layer);

                if (written == 0)
                    continue;

                int last = runs.Count - 1;

                if (last >= 0 && runs[last].Texture == shape.Texture && runs[last].Additive == shape.Additive)
                {
                    Run run = runs[last];
                    run.Count += written;
                    runs[last] = run;
                }
                else
                {
                    runs.Add(new Run { Texture = shape.Texture, Additive = shape.Additive, Start = used, Count = written });
                }

                used += written;
            }

            GetBuffers(out VertexBufferBinding[] bindings, out IndexBuffer indexBuffer);

            if (used == 0 || bindings == null)
                return;

            SetData(data);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            VertexBuffer instanceBuffer = bindings[1].VertexBuffer;

            device.RasterizerState = Main.LocalPlayer.gravDir == -1 ? RasterizerState.CullClockwise : RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            device.Indices = indexBuffer;

            foreach (Run run in runs)
            {
                bindings[1] = new VertexBufferBinding(instanceBuffer, run.Start, 1);
                device.SetVertexBuffers(bindings);
                device.BlendState = run.Additive ? BlendState.Additive : BlendState.AlphaBlend;
                device.Textures[0] = run.Texture;

                ParticleManagerV3.InstancedParticleEffect.Apply();
                device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 4, 0, 2, run.Count);
            }

            bindings[1] = new VertexBufferBinding(instanceBuffer, 0, 1);
        }
    }
}
