using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public abstract class VulkanDependancy
{
    protected Vk Vk { get; }
    protected VulkanDependancy(Vk vk) => this.Vk = vk;
}
