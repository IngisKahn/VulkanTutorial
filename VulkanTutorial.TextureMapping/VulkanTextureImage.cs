using Silk.NET.Vulkan;
using SixLabors.ImageSharp.PixelFormats;
using RawImage = SixLabors.ImageSharp.Image;
using Image = Silk.NET.Vulkan.Image;
using System.Runtime.InteropServices;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanTextureImage : VulkanDeviceDependancy
{
    public VulkanImage? Image { get; private set; }

    public VulkanTextureImage(Vk vk, VulkanVirtualDevice device) : base(vk, device) { }

    private async Task Initialize(Stream imageStream)
    {
        using var image = await RawImage.LoadAsync<Rgba32>(imageStream);
        if (image == null)
            throw new VulkanException("failed to load texture image!");
        var imageSize = image.Width * image.Height * 4;
        unsafe
        {
            VulkanStagingBuffer<Rgba32> staging;
            image.ProcessPixelRows(accessor =>
            {
                fixed (Rgba32* data = &MemoryMarshal.GetReference(accessor.GetRowSpan(0)))

                    staging = new(this.Vk, this.Device, (ulong)imageSize, data);
            });
            this.Image = new(this.Vk, this.Device, (uint)image.Width, (uint)image.Height, Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageSampledBit, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit);
        }
    }

    public static async Task<VulkanTextureImage> Load(Vk vk, VulkanVirtualDevice device, Stream imageStream)
    {
        VulkanTextureImage textureImage = new(vk, device);
        await textureImage.Initialize(imageStream);
        return textureImage;
    }
}

public class VulkanImage : VulkanDeviceDependancy
{
    public Image Image;
    public DeviceMemory Memory;

    public VulkanImage(Vk vk, VulkanVirtualDevice device, uint width, uint height, Format format, ImageTiling imageTiling, ImageUsageFlags imageUsage, MemoryPropertyFlags memoryProperty) : base(vk, device)
    {
        unsafe
        {
            ImageCreateInfo imageInfo = new(
                imageType: ImageType.ImageType2D,
                extent: new(width, height, 1u),
                mipLevels: 1,
                arrayLayers: 1,
                format: format,
                tiling: imageTiling,
                initialLayout: ImageLayout.Undefined,
                usage: imageUsage,
                sharingMode: SharingMode.Exclusive,
                samples: SampleCountFlags.SampleCount1Bit);

            fixed (Image* pImage = &this.Image)
                if (this.Vk.CreateImage(this.Device.Device, in imageInfo, null, pImage) != Result.Success)
                    throw new VulkanException("failed to create image!");
            this.Vk.GetImageMemoryRequirements(this.Device.Device, this.Image, out var memoryRequirements);

            MemoryAllocateInfo allocateInfo = new(allocationSize: memoryRequirements.Size, memoryTypeIndex: device.PhysicalDevice.FindMemoryType(memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit));
            fixed (DeviceMemory* pDeviceMemory = &this.Memory)
                if (this.Vk.AllocateMemory(this.Device.Device, in allocateInfo, null, pDeviceMemory) != Result.Success)
                    throw new VulkanException("failed to allocate image memory!");
            this.Vk.BindImageMemory(this.Device.Device, this.Image, this.Memory, 0);
        }
    }
}
