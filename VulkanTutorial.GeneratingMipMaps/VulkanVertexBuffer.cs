using Silk.NET.Vulkan;

namespace VulkanTutorial.DepthBuffering;

public sealed class VulkanVertexBuffer : VulkanDeviceBuffer<Vertex>
{
    public VulkanVertexBuffer(
        Vk vk,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        Vertex[] vertices) :
        base(
            vk,
            device, commandPool,
            vertices,
            BufferUsageFlags.BufferUsageVertexBufferBit) { }

    public override void Bind(in CommandBuffer commandBuffer)
    {
        unsafe
        {
            var offsets = 0ul;
            fixed (Silk.NET.Vulkan.Buffer* pVertexBuffer = &this.buffer)
                this.Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, pVertexBuffer, &offsets);
        }
    }
}