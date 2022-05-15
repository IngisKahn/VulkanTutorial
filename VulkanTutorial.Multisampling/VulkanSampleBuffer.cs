using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public class VulkanSampleBuffer : VulkanDeviceDependancy, IDisposable
{
    public VulkanImage SampleImage { get; }
    public VulkanImageView SampleView { get; }

    public VulkanSampleBuffer(Vk vk, VulkanVirtualDevice device, Format format, uint width, uint height, SampleCountFlags sampleCount)
        : base(vk, device)
    {
        this.SampleImage = new(vk, device, width, height, 1, format, ImageTiling.Optimal, ImageUsageFlags.ImageUsageTransientAttachmentBit | ImageUsageFlags.ImageUsageColorAttachmentBit, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, sampleCount);
        this.SampleView = new(vk, device, this.SampleImage.Image, format, 1, ImageAspectFlags.ImageAspectColorBit);
    }

    public void Dispose()
    {
        this.SampleImage.Dispose();
        this.SampleView.Dispose();
    }
}
