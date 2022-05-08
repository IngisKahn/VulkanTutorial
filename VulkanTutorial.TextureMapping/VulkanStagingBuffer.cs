using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanStagingBuffer<T> : VulkanBuffer where T : unmanaged
{
    internal VulkanStagingBuffer(Vk vk, VulkanPhysicalDevice physicalDevice, VulkanVirtualDevice device, ulong size, T[] sourceData)
        : base(vk, physicalDevice, device, size, BufferUsageFlags.BufferUsageTransferSrcBit, MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit)
    {
        unsafe
        {
            void* data;
            vk.MapMemory(device.Device, this.Memory, 0, this.Size, 0, &data);
            fixed (T* pSourceData = sourceData)
                System.Buffer.MemoryCopy(pSourceData, data, (long)this.Size, (long)this.Size);
            vk.UnmapMemory(device.Device, this.Memory);
        }
    }
    public VulkanStagingBuffer(Vk vk, VulkanPhysicalDevice physicalDevice, VulkanVirtualDevice device, T[] sourceData) : this(vk, physicalDevice, device, (ulong)(Marshal.SizeOf<T>() * sourceData.Length), sourceData) { }
}
