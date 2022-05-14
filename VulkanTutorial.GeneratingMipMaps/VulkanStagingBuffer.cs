using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace VulkanTutorial.DepthBuffering;

public sealed class VulkanStagingBuffer<T> : VulkanBuffer where T : unmanaged
{
    internal VulkanStagingBuffer(Vk vk, VulkanVirtualDevice device, ulong size, T[] sourceData)
        : base(vk, device, size, BufferUsageFlags.BufferUsageTransferSrcBit, MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit)
    {
        unsafe
        {
            fixed (T* pSourceData = sourceData)
                this.CopyData(pSourceData);
        }
    }
    internal unsafe VulkanStagingBuffer(Vk vk, VulkanVirtualDevice device, ulong size, T* pSourceData)
        : base(vk, device, size, BufferUsageFlags.BufferUsageTransferSrcBit, MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit) => this.CopyData(pSourceData);

    private unsafe void CopyData(T* pSourceData)
    {
        void* data;
        this.Vk.MapMemory(this.Device.Device, this.Memory, 0, this.Size, 0, &data);
        System.Buffer.MemoryCopy(pSourceData, data, (long)this.Size, (long)this.Size);
        this.Vk.UnmapMemory(this.Device.Device, this.Memory);
    }

    public VulkanStagingBuffer(Vk vk, VulkanVirtualDevice device, T[] sourceData) : this(vk, device, (ulong)(Marshal.SizeOf<T>() * sourceData.Length), sourceData) { }
}
