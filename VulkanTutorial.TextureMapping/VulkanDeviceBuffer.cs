using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace VulkanTutorial.TextureMapping;

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
            using VulkanStagingBuffer<T> staging = new(vk, physicalDevice, device, this.Size, sourceData);
            staging.CopyTo(this, commandPool);
        }
    }

    public abstract void Bind(in CommandBuffer commandBuffer);
}

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

public sealed class VulkanTextureImage
{
    private VulkanTextureImage() { }
    private async Task Initialize(Stream imageStream)
    {
        var image = await Image.LoadAsync<Rgb24>(imageStream);
        if (image == null)
            throw new VulkanException("failed to load texture image!");
        var imageSize = image.Width * image.Height * 4;
    }

    public static async Task<VulkanTextureImage> Load(Stream imageStream)
    {
        VulkanTextureImage textureImage = new();
        await textureImage.Initialize(imageStream);
        return textureImage;
    }
}
