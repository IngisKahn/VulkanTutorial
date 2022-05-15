using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public class VulkanDepthBuffer : VulkanDeviceDependancy, IDisposable
{
    public VulkanImage DepthImage { get; }
    public VulkanImageView DepthView { get; }

    public VulkanDepthBuffer(Vk vk, VulkanVirtualDevice device, VulkanCommandPool commandPool, uint width, uint height) 
        : base(vk, device)
    {
        var format = device.PhysicalDevice.DepthFormat;
        this.DepthImage = new(vk, device, width, height, 1, format, ImageTiling.Optimal, ImageUsageFlags.ImageUsageDepthStencilAttachmentBit, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit);
        this.DepthView = new(vk, device, this.DepthImage.Image, format, 1, ImageAspectFlags.ImageAspectDepthBit);

        this.DepthImage.TransitionImageLayout(commandPool, format, ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal, 1);
    }

    public void Dispose()
    {
        this.DepthImage.Dispose();
        this.DepthView.Dispose();
    }
}
