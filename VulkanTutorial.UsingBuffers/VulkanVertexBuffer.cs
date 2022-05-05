using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace VulkanTutorial.UsingBuffers;

public abstract class VulkanDeviceBuffer<T> : VulkanBuffer where T : unmanaged
{
    protected VulkanDeviceBuffer(Vk vk,
        VulkanPhysicalDevice physicalDevice,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        T[] sourceData, BufferUsageFlags bufferUsageFlags) : base(vk, physicalDevice, device, (ulong)(Marshal.SizeOf<T>() * sourceData.Length),
            bufferUsageFlags | BufferUsageFlags.BufferUsageTransferDstBit,
            MemoryPropertyFlags.MemoryPropertyDeviceLocalBit)
    {
        unsafe
        {
            using VulkanBuffer staging = new(vk, physicalDevice, device, this.Size, BufferUsageFlags.BufferUsageTransferSrcBit, MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);

            void* data;
            vk.MapMemory(device.Device, staging.Memory, 0, this.Size, 0, &data);
            fixed (T* pSourceData = sourceData)
                System.Buffer.MemoryCopy(pSourceData, data, (long)this.Size, (long)this.Size);
            vk.UnmapMemory(device.Device, staging.Memory);
            staging.CopyTo(this, commandPool);
        }
    }

    public abstract void Bind(in CommandBuffer commandBuffer);
}
public class VulkanIndexBuffer : VulkanDeviceBuffer<ushort>
{
    public VulkanIndexBuffer(Vk vk,
        VulkanPhysicalDevice physicalDevice,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        ushort[] indices) : base(vk, physicalDevice, device, commandPool, indices,
            BufferUsageFlags.BufferUsageIndexBufferBit) { }

    public override void Bind(in CommandBuffer commandBuffer) => this.Vk.CmdBindIndexBuffer(commandBuffer, this.Buffer, 0, IndexType.Uint16);
}

public sealed class VulkanVertexBuffer : VulkanDeviceBuffer<Vertex>
{
    public VulkanVertexBuffer(
        Vk vk,
        VulkanPhysicalDevice physicalDevice,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        Vertex[] vertices) :
        base(
            vk,
            physicalDevice,
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