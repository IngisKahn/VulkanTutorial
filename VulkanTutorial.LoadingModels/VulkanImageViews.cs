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
