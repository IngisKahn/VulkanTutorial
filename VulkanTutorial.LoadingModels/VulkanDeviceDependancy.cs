using Silk.NET.Vulkan;

namespace VulkanTutorial.DepthBuffering;

public abstract class VulkanDeviceDependancy : VulkanDependancy
{
    protected VulkanVirtualDevice Device { get; }
    protected VulkanDeviceDependancy(Vk vk, VulkanVirtualDevice device) : base(vk) => this.Device = device;

}
