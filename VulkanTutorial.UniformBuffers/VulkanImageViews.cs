namespace VulkanTutorial.UniformBuffers;
using Silk.NET.Vulkan;

public sealed class VulkanImageViews : VulkanDeviceDependancy, IDisposable
{
    private readonly ImageView[] swapchainImageViews;
    public VulkanImageViews(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain) : base(vk, device)
    {
        var images = swapChain.SwapchainImages;
        var format = swapChain.SwapchainImageFormat;
        this.swapchainImageViews = new ImageView[images.Length];

        for (var i = 0; i < images.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = images[i],
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

            ImageView imageView = default;
            unsafe
            {
                if (vk.CreateImageView(device.Device, &createInfo, null, &imageView) != Result.Success)
                    throw new("failed to create image views!");
            }

            this.swapchainImageViews[i] = imageView;
        }
    }

    public int Length => this.swapchainImageViews.Length;
    public ImageView this[int i] => this.swapchainImageViews[i];

    public void Dispose()
    {
        unsafe
        {
            foreach (var imageView in this.swapchainImageViews)
                this.Vk.DestroyImageView(this.Device.Device, imageView, null);
        }
    }
}