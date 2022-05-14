using Silk.NET.Vulkan;

namespace VulkanTutorial.DepthBuffering;

public class VulkanIndexBuffer : VulkanDeviceBuffer<uint>
{
    public VulkanIndexBuffer(Vk vk,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        uint[] indices) : base(vk, device, commandPool, indices,
            BufferUsageFlags.BufferUsageIndexBufferBit) { }

    public override void Bind(in CommandBuffer commandBuffer) => this.Vk.CmdBindIndexBuffer(commandBuffer, this.Buffer, 0, IndexType.Uint32);
}
