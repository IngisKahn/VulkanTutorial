using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public readonly struct Vertex
{
    public readonly Vector2D<float> Position;
    public readonly Vector3D<float> Color;
    public readonly Vector2D<float> TexCoord;

    public Vertex(Vector2D<float> position, Vector3D<float> color, Vector2D<float> texCoord)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
    }
    public static VertexInputBindingDescription BindingDescription
    {
        get
        {
            unsafe
            {
                return new(0, (uint)sizeof(Vertex), VertexInputRate.Vertex);
            }
        }
    }
    public static VertexInputAttributeDescription[] AttributeDescriptions =>
        new VertexInputAttributeDescription[]
            {
                new(0, 0, Format.R32G32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Position))),
                new(1, 0, Format.R32G32B32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Color))),
                new(2, 0, Format.R32G32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(TexCoord)))
            };
}
