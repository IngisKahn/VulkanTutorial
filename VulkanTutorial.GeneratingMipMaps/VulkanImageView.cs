namespace VulkanTutorial.DepthBuffering;
using Silk.NET.Vulkan;

public sealed class VulkanImageView : VulkanDeviceDependancy, IDisposable
{
    public ImageView ImageView;
    public VulkanImageView(Vk vk, VulkanVirtualDevice device, VulkanImage image, Format format, uint mipLevels, ImageAspectFlags aspectFlags = ImageAspectFlags.ImageAspectColorBit) : this(vk, device, image.Image, format, mipLevels, aspectFlags) { }
    internal VulkanImageView(Vk vk, VulkanVirtualDevice device, Image image, Format format, uint mipLevels, ImageAspectFlags aspectFlags = ImageAspectFlags.ImageAspectColorBit) : base(vk, device)
    {
        var createInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.ImageViewType2D,
            Format = format,
            Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
            SubresourceRange =
                {
                    AspectMask = aspectFlags,
                    BaseMipLevel = 0,
                    LevelCount = mipLevels,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
        };

        unsafe
        {
            fixed (ImageView* pImageView = &this.ImageView)
                if (vk.CreateImageView(device.Device, &createInfo, null, pImageView) != Result.Success)
                    throw new("failed to create image views!");
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyImageView(this.Device.Device, this.ImageView, null);
        }
    }
}