using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public class VulkanIndexBuffer : VulkanDeviceBuffer<ushort>
{
    public VulkanIndexBuffer(Vk vk,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        ushort[] indices) : base(vk, device, commandPool, indices,
            BufferUsageFlags.BufferUsageIndexBufferBit) { }

    public override void Bind(in CommandBuffer commandBuffer) => this.Vk.CmdBindIndexBuffer(commandBuffer, this.Buffer, 0, IndexType.Uint16);
}
