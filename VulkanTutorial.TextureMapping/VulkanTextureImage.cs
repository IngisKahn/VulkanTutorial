using Silk.NET.Vulkan;
using SixLabors.ImageSharp.PixelFormats;
using RawImage = SixLabors.ImageSharp.Image;
using Image = Silk.NET.Vulkan.Image;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanTextureImage : VulkanDeviceDependancy
{
    public Image Image;
    public DeviceMemory Memory;

    public VulkanTextureImage(Vk vk, VulkanVirtualDevice device) : base(vk, device) { }

    private async Task Initialize(Stream imageStream)
    {
        using var image = await RawImage.LoadAsync<Rgb24>(imageStream);
        if (image == null)
            throw new VulkanException("failed to load texture image!");
        var imageSize = image.Width * image.Height * 4;
        unsafe
        {
            ImageCreateInfo imageInfo = new(
                imageType: ImageType.ImageType2D, 
                extent: new((uint)image.Width, (uint)image.Height, 1u), 
                mipLevels: 1, 
                arrayLayers: 1, 
                format: Format.R8G8B8A8Srgb, 
                tiling: ImageTiling.Optimal, 
                initialLayout: ImageLayout.Undefined,
                usage: ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageSampledBit,
                sharingMode: SharingMode.Exclusive,
                samples: SampleCountFlags.SampleCount1Bit);

            fixed (Image* pImage = &this.Image)
                if (this.Vk.CreateImage(this.Device.Device, in imageInfo, null, pImage) != Result.Success)
                    throw new VulkanException("failed to create image!");
            this.Vk.GetImageMemoryRequirements(this.Device.Device, this.Image, out var memoryRequirements);

            MemoryAllocateInfo allocateInfo = new(allocationSize: memoryRequirements.Size, memoryTypeIndex: this.FindMemoryType(memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit));
            fixed (DeviceMemory* pDeviceMemory = &this.Memory)
                if (this.Vk.AllocateMemory(this.Device.Device, in allocateInfo, null, pDeviceMemory) != Result.Success)
                    throw new VulkanException("failed to allocate image memory!");
            this.Vk.BindImageMemory(this.Device.Device, this.Image, this.Memory, 0);
        }
    }

    public static async Task<VulkanTextureImage> Load(Vk vk, VulkanVirtualDevice device, Stream imageStream)
    {
        VulkanTextureImage textureImage = new(vk, device);
        await textureImage.Initialize(imageStream);
        return textureImage;
    }
}
