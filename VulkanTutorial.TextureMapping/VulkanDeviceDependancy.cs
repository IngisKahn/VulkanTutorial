using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public abstract class VulkanDeviceDependancy : VulkanDependancy
{
    protected VulkanVirtualDevice Device { get; }
    protected VulkanDeviceDependancy(Vk vk, VulkanVirtualDevice device) : base(vk) => this.Device = device;

    protected uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        this.Vk.GetPhysicalDeviceMemoryProperties(this.physicalDevice.PhysicalDevice, out var memoryProperties);
        for (var i = 0; i < memoryProperties.MemoryTypeCount; i++)
            if ((typeFilter & (1u << i)) != 0 && (memoryProperties.MemoryTypes[i].PropertyFlags & properties) != 0)
                return (uint)i;
        throw new VulkanException("failed to find suitable memory type!");
    }
}
