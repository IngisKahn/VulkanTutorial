namespace VulkanTutorial.DepthBuffering;
using Silk.NET.Vulkan;

public sealed class VulkanImageViews : VulkanDeviceDependancy, IDisposable
{
    private readonly VulkanImageView[] swapchainImageViews;
    public VulkanImageViews(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain) : base(vk, device)
    {
        var images = swapChain.SwapchainImages;
        var format = swapChain.SwapchainImageFormat;
        this.swapchainImageViews = new VulkanImageView[images.Length];

        for (var i = 0; i < images.Length; i++)
        {
            this.swapchainImageViews[i] = new(vk, device, images[i], format);
        }
    }

    public int Length => this.swapchainImageViews.Length;
    public VulkanImageView this[int i] => this.swapchainImageViews[i];

    public void Dispose()
    {
        foreach (var imageView in this.swapchainImageViews)
            imageView.Dispose();
    }
}

public sealed class VulkanImageView : VulkanDeviceDependancy, IDisposable
{
    public ImageView ImageView;
    public VulkanImageView(Vk vk, VulkanVirtualDevice device, VulkanImage image, Format format) : this(vk, device, image.Image, format) { }
    internal VulkanImageView(Vk vk, VulkanVirtualDevice device, Image image, Format format) : base(vk, device)
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
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
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