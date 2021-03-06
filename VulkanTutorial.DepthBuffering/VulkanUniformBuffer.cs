using Silk.NET.Vulkan;
using System.Runtime.InteropServices;

namespace VulkanTutorial.DepthBuffering;

public sealed class VulkanUniformBuffer<T> : VulkanBuffer where T : unmanaged
{
    public VulkanUniformBuffer(Vk vk, VulkanVirtualDevice device) 
        : base(vk, device, (ulong)Marshal.SizeOf<T>(), BufferUsageFlags.BufferUsageUniformBufferBit, MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit) { }

    public void CopyData(in T sourceData)
    {
        unsafe
        {
            void* data;
            this.Vk.MapMemory(this.Device.Device, this.Memory, 0, this.Size, 0, &data);
            fixed (T* pSourceData = &sourceData)
                System.Buffer.MemoryCopy(pSourceData, data, (long)this.Size, (long)this.Size);
            this.Vk.UnmapMemory(this.Device.Device, this.Memory);
        }
    }
}
